param([switch]$Apply, [int]$KeepRecent = 3, [string[]]$AdditionalSource = @(), [switch]$IncludeVerificationFixtures, [switch]$SkipQaArchive,
    [string]$BackupRoot = $env:BETTERCM_BACKUP_ROOT, [switch]$MigrateLegacyStorage, [switch]$MigrateOnly,
    [switch]$PruneArchives, [int]$KeepArchiveRecent = 12, [double]$MaxArchiveGB = 10)
$ErrorActionPreference = 'Stop'
if ($KeepRecent -lt 0 -or $KeepArchiveRecent -lt 0 -or -not [double]::IsFinite($MaxArchiveGB) -or $MaxArchiveGB -le 0) { throw 'Invalid retention limits.' }
if ($MigrateOnly -and -not $MigrateLegacyStorage) { throw 'MigrateOnly requires MigrateLegacyStorage.' }
$root = [IO.Path]::GetFullPath($PSScriptRoot)
$prefix = $root + [IO.Path]::DirectorySeparatorChar
function Assert-WorkspacePath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Path outside workspace: $full" }
    $cursor = $full
    while ($cursor -ne $root) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse point is not allowed: $cursor" }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    return $full
}
if (-not $BackupRoot) { $BackupRoot = Join-Path (Split-Path $root) ((Split-Path $root -Leaf) + '.Storage') }
$storageRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($BackupRoot))
if ($storageRoot -eq $root -or $root.StartsWith($storageRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or $storageRoot.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'BackupRoot must be outside the workspace, and cannot contain it.' }
$archiveRoot = Join-Path $storageRoot 'archives'
$backupRoot = Join-Path $storageRoot 'snapshots'
function Assert-StoragePath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($storageRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Path outside storage: $full" }
    for ($cursor = $full; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse point is not allowed: $cursor" }
    }
    return $full
}
Assert-StoragePath $archiveRoot | Out-Null
Assert-StoragePath $backupRoot | Out-Null
Write-Output ('Storage root: ' + $storageRoot)
$migrations = @()
if ($MigrateLegacyStorage) {
    foreach ($pair in @(@('Backups', 'legacy-backups'), @('artifacts\archives', 'legacy-archives'))) {
        $sourcePath = Assert-WorkspacePath (Join-Path $root $pair[0])
        $destinationPath = Assert-StoragePath (Join-Path $storageRoot $pair[1])
        if (Test-Path -LiteralPath $sourcePath) {
            if (Test-Path -LiteralPath $destinationPath) { throw "Migration destination already exists: $destinationPath" }
            if ([IO.Path]::GetPathRoot($sourcePath) -ne [IO.Path]::GetPathRoot($destinationPath)) { throw 'Legacy migration requires a same-volume atomic directory move. Copy and hash-verify separately for another volume.' }
            Get-ChildItem -LiteralPath $sourcePath -Recurse -Force | ForEach-Object { Assert-WorkspacePath $_.FullName | Out-Null }
            $migrations += [pscustomobject]@{ Source = $sourcePath; Destination = $destinationPath }
            Write-Output ("Move legacy directory: $sourcePath -> $destinationPath (all contents retained)")
        }
    }
}
function Get-ArchivePrunePlan {
    $selected = @()
    if (-not (Test-Path -LiteralPath $archiveRoot)) { return $selected }
    $owned = @(Get-ChildItem -LiteralPath $archiveRoot -File | Where-Object Name -Match '^(dev[0-9]*-|installer-|qa-|script-(?:fixture|storage)-|baseline-|maintenance-|clean-build-fixture-)(?:.*-)?\d{8}-\d{6}-[0-9a-f]{8}\.zip$' | Sort-Object LastWriteTime, Name -Descending)
    $remainingBytes = ($owned | Measure-Object Length -Sum).Sum
    for ($i = $owned.Count - 1; $i -ge 0; $i--) {
        if ($i -lt $KeepArchiveRecent -and $remainingBytes -le $MaxArchiveGB * 1GB) { continue }
        $selected += Assert-StoragePath $owned[$i].FullName
        $remainingBytes -= $owned[$i].Length
    }
    return $selected
}
$prune = @(if ($PruneArchives) { Get-ArchivePrunePlan })
$prune | ForEach-Object { Write-Output ("Prune generated archive: $_") }
if ($PruneArchives) { Write-Output 'Retention applies only to archives present at planning time; new verified archives are retained even when they exceed the limits.' }
$stamp = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$plans = @()
$artifactPath = Join-Path $root 'artifacts'
$groups = @(
    @{ Name = 'Development'; Pattern = '^dev[0-9]*-' },
    @{ Name = 'InstallerBuild'; Pattern = '^installer-' },
    @{ Name = 'QaCompiler'; Pattern = '^qa-(?:.*-)?compiler-' },
    @{ Name = 'QaInstaller'; Pattern = '^qa-installer-' },
    @{ Name = 'Baseline'; Pattern = '^baseline-' },
    @{ Name = 'Maintenance'; Pattern = '^maintenance-' },
    @{ Name = 'ScriptFixture'; Pattern = '^script-(fixture|storage)-[0-9a-f]{32}$' }
)
$groupCounts = @()
if (-not $MigrateOnly -and (Test-Path -LiteralPath $artifactPath)) {
    $directories = @(Get-ChildItem -LiteralPath $artifactPath -Directory)
    foreach ($group in $groups) {
        # dev-, dev2-, dev9-, etc. share one retention budget, rather than N per prefix.
        $matchedDirectories = @($directories | Where-Object Name -Match $group.Pattern | Sort-Object -Property LastWriteTime, Name -Descending)
        $items = @($matchedDirectories | Select-Object -Skip $KeepRecent)
        $groupCounts += [pscustomobject]@{ Name = $group.Name; Pattern = $group.Pattern; MatchedBefore = $matchedDirectories.Count; PlannedArchives = $items.Count; Remaining = $matchedDirectories.Count - $items.Count }
        Write-Output ($group.Name + ': matched=' + $matchedDirectories.Count + ', keep=' + ($matchedDirectories.Count - $items.Count) + ', archive=' + $items.Count)
        foreach ($item in $items) {
            $plans += [pscustomobject]@{ Action = 'Archive then remove build directory'; Group = $group.Name; Path = (Assert-WorkspacePath $item.FullName) }
        }
    }
    if ($IncludeVerificationFixtures) {
        foreach ($item in @(Get-ChildItem -LiteralPath $artifactPath -Directory -Filter 'clean-build-fixture-*' |
            Where-Object Name -Match '^clean-build-fixture-[0-9a-f]{32}$')) {
            $plans += [pscustomobject]@{ Action = 'Archive then remove build directory'; Path = (Assert-WorkspacePath $item.FullName) }
        }
    }
}
$qa = Join-Path $root 'artifacts\qa'
if (-not $MigrateOnly -and -not $SkipQaArchive -and (Test-Path -LiteralPath $qa)) { $plans += [pscustomobject]@{ Action = 'Archive QA evidence (keep directory)'; Path = (Assert-WorkspacePath $qa) } }
$plans | ForEach-Object { Write-Output ($_.Action + ": " + $_.Path) }
Write-Output 'Source backup: git tracked files + untracked source/document types + explicit AdditionalSource; excludes generated output at every depth.'
Write-Output 'History backup: git bundle --all. Release payloads and existing backups are always retained.'
if (-not $Apply) { Write-Output 'Dry run only. Use -Apply to write verified archives and remove archived build directories.'; return }
Assert-StoragePath $archiveRoot | Out-Null
Assert-StoragePath $backupRoot | Out-Null
New-Item -ItemType Directory -Path $archiveRoot, $backupRoot -Force | Out-Null
foreach ($migration in $migrations) {
    $sourcePath = Assert-WorkspacePath $migration.Source
    $destinationPath = Assert-StoragePath $migration.Destination
    # Directory.Move is atomic on this volume; it neither merges nor overwrites contents.
    [IO.Directory]::Move($sourcePath, $destinationPath)
    if ((Test-Path -LiteralPath $sourcePath) -or -not (Test-Path -LiteralPath $destinationPath)) { throw 'Migration did not complete.' }
}
if ($MigrateOnly) { Write-Output 'Legacy storage migration complete; all historical contents retained.'; return }
Add-Type -AssemblyName System.IO.Compression
function Write-VerifiedZip([string]$Destination, [object[]]$Entries) {
    $temporary = (Assert-StoragePath $Destination) + '.tmp'
    try {
        $stream = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew)
        try {
            $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
            try {
                foreach ($entry in $Entries) { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $entry.FullName, $entry.Name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null }
            } finally { $zip.Dispose() }
        } finally { $stream.Dispose() }
        $zip = [IO.Compression.ZipFile]::OpenRead($temporary)
        try {
            if ($zip.Entries.Count -ne $Entries.Count) { throw 'Archive entry count mismatch.' }
            for ($i = 0; $i -lt $Entries.Count; $i++) {
                $inputStream = $zip.Entries[$i].Open()
                try { $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($inputStream)) } finally { $inputStream.Dispose() }
                if ($actual -ne (Get-FileHash -LiteralPath $Entries[$i].FullName -Algorithm SHA256).Hash) { throw 'Archive hash mismatch or source changed during archive.' }
            }
        } finally { $zip.Dispose() }
        Move-Item -LiteralPath $temporary -Destination $Destination
    } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
}
$tracked = (& git -C $root -c core.quotepath=false ls-files)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate tracked source.' }
$untracked = (& git -C $root -c core.quotepath=false ls-files --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate untracked source.' }
$source = @($tracked) + @($untracked | Where-Object { $_ -match '\.(cs|xaml|csproj|props|slnx|json|config|ps1|md|txt|yml|yaml|iss|isl|manifest|ico)$' -or $_ -match '(^|/)LICENSE$' -or $_ -match '(^|/)\.(gitattributes|gitignore|editorconfig)$' }) + $AdditionalSource
$entries = @($source | Sort-Object -Unique | ForEach-Object {
    if ($_ -match '(^|[/\\])(bin|obj|artifacts|Backups|\.git)([/\\]|$)' -or $_ -match '\.(exe|sha256|zip|log|trx)$') { return }
    $full = Assert-WorkspacePath (Join-Path $root $_)
    if ($full.StartsWith($storageRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { return }
    if (Test-Path -LiteralPath $full -PathType Leaf) { [pscustomobject]@{ FullName = $full; Name = [IO.Path]::GetRelativePath($root, $full).Replace('\','/') } }
})
Write-VerifiedZip (Join-Path $backupRoot ("source-$stamp.zip")) $entries
$bundle = Join-Path $backupRoot ("history-$stamp.bundle")
& git -C $root bundle create $bundle --all
if ($LASTEXITCODE -ne 0) { throw 'Git history backup failed.' }
& git -C $root bundle verify $bundle
if ($LASTEXITCODE -ne 0) { throw 'Git history verification failed.' }
# Prune only the pre-run inventory, before creating archives and removing source directories.
# Never recompute this plan after a new archive becomes the only remaining copy.
foreach ($oldArchive in $prune) { Remove-Item -LiteralPath (Assert-StoragePath $oldArchive) -Force }
$completed = [Collections.Generic.List[object]]::new()
foreach ($plan in $plans) {
    $path = Assert-WorkspacePath $plan.Path
    # Validate directories before enumeration as well, including empty junctions.
    Get-ChildItem -LiteralPath $path -Recurse -Force -Directory | ForEach-Object { Assert-WorkspacePath $_.FullName | Out-Null }
    $files = @(Get-ChildItem -LiteralPath $path -Recurse -Force -File | ForEach-Object {
        $full = Assert-WorkspacePath $_.FullName
        [pscustomobject]@{ FullName = $full; Name = [IO.Path]::GetRelativePath($path, $full).Replace('\','/') }
    })
    $destination = Join-Path $archiveRoot ((Split-Path $path -Leaf) + "-$stamp.zip")
    $sourceBytes = ($files | ForEach-Object { (Get-Item -LiteralPath $_.FullName).Length } | Measure-Object -Sum).Sum
    Write-Output ('Archiving: ' + [IO.Path]::GetRelativePath($root, $path))
    Write-VerifiedZip $destination $files
    if ($plan.Action -eq 'Archive then remove build directory') {
        $path = Assert-WorkspacePath $path
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    $completed.Add([pscustomobject]@{ Path = [IO.Path]::GetRelativePath($root, $path); Archive = $destination; OriginalBytes = $sourceBytes; ArchiveBytes = (Get-Item -LiteralPath $destination).Length; Removed = -not (Test-Path -LiteralPath $path) })
}
foreach ($group in $groupCounts) {
    $group.Remaining = @(Get-ChildItem -LiteralPath $artifactPath -Directory | Where-Object Name -Match $group.Pattern).Count
}
[pscustomobject]@{ Time = [DateTimeOffset]::Now; StorageRoot = $storageRoot; PrunedArchives = $prune; NewArchivesProtected = @($completed | ForEach-Object Archive); RetentionScope = 'PreRunArchivesOnly'; Migrations = $migrations; KeepRecent = $KeepRecent; ExistingBackupsRetained = $true; QaArchiveSkipped = [bool]$SkipQaArchive; Groups = $groupCounts; Results = $completed } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $artifactPath 'maintenance-report.json') -Encoding utf8
Write-Output 'Maintenance complete. Release payloads, QA directory and all historical backups retained.'
