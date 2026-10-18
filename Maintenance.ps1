param([switch]$Apply, [int]$KeepRecent = 3, [string[]]$AdditionalSource = @(), [switch]$IncludeVerificationFixtures)
$ErrorActionPreference = 'Stop'
if ($KeepRecent -lt 0) { throw 'KeepRecent must be nonnegative.' }
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
$archiveRoot = Join-Path $root 'artifacts\archives'
$backupRoot = Join-Path $root 'Backups'
$stamp = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$plans = @()
$artifactPath = Join-Path $root 'artifacts'
if (Test-Path -LiteralPath $artifactPath) {
    $patterns = @('dev-*', 'installer-build-*')
    foreach ($pattern in $patterns) {
        $items = @(Get-ChildItem -LiteralPath $artifactPath -Directory -Filter $pattern | Sort-Object LastWriteTime -Descending | Select-Object -Skip $KeepRecent)
        foreach ($item in $items) {
            $plans += [pscustomobject]@{ Action = 'Archive then remove build directory'; Path = (Assert-WorkspacePath $item.FullName) }
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
if (Test-Path -LiteralPath $qa) { $plans += [pscustomobject]@{ Action = 'Archive QA evidence (keep directory)'; Path = (Assert-WorkspacePath $qa) } }
$plans | ForEach-Object { Write-Output ($_.Action + ": " + $_.Path) }
Write-Output 'Source backup: git tracked files + untracked source/document types + explicit AdditionalSource; excludes generated output at every depth.'
Write-Output 'History backup: git bundle --all. Release payloads and existing backups are always retained.'
if (-not $Apply) { Write-Output 'Dry run only. Use -Apply to write verified archives and remove archived build directories.'; return }
Assert-WorkspacePath $archiveRoot | Out-Null
Assert-WorkspacePath $backupRoot | Out-Null
New-Item -ItemType Directory -Path $archiveRoot, $backupRoot -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression
function Write-VerifiedZip([string]$Destination, [object[]]$Entries) {
    $temporary = (Assert-WorkspacePath $Destination) + '.tmp'
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
    if (Test-Path -LiteralPath $full -PathType Leaf) { [pscustomobject]@{ FullName = $full; Name = [IO.Path]::GetRelativePath($root, $full).Replace('\','/') } }
})
Write-VerifiedZip (Join-Path $backupRoot ("source-$stamp.zip")) $entries
$bundle = Join-Path $backupRoot ("history-$stamp.bundle")
& git -C $root bundle create $bundle --all
if ($LASTEXITCODE -ne 0) { throw 'Git history backup failed.' }
& git -C $root bundle verify $bundle
if ($LASTEXITCODE -ne 0) { throw 'Git history verification failed.' }
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
    $completed.Add([pscustomobject]@{ Path = [IO.Path]::GetRelativePath($root, $path); Archive = [IO.Path]::GetRelativePath($root, $destination); OriginalBytes = $sourceBytes; ArchiveBytes = (Get-Item -LiteralPath $destination).Length; Removed = -not (Test-Path -LiteralPath $path) })
}
[pscustomobject]@{ Time = [DateTimeOffset]::Now; KeepRecent = $KeepRecent; ExistingBackupsRetained = $true; Results = $completed } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $artifactPath 'maintenance-report.json') -Encoding utf8
Write-Output 'Maintenance complete. Release payloads, QA directory and all historical backups retained.'
