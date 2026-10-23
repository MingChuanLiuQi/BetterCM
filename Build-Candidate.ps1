param([switch]$SkipDesktopChecks, [switch]$InstallerSmoke, [string]$Compiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe')
$ErrorActionPreference = 'Stop'
if ($InstallerSmoke -and $SkipDesktopChecks) { throw 'Isolated installer smoke requires desktop verification and QA publish.' }
$projectRoot = $PSScriptRoot
. (Join-Path $projectRoot 'Installer\SourceIdentity.ps1')
$version = & (Join-Path $projectRoot 'Read-Version.ps1')
$origin = Get-SourceIdentity $projectRoot
$candidate = Join-Path $projectRoot ('artifacts\candidates\' + $version + '-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$snapshot = Join-Path $candidate 'source'
New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
$origin | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $candidate 'origin-source.json') -Encoding utf8
foreach ($file in $origin.Files) {
    $target = Join-Path $snapshot $file.Path
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot $file.Path) -Destination $target
    if ((Get-FileHash -LiteralPath $target).Hash -ne $file.SHA256) { throw 'Source changed while copying candidate snapshot.' }
}
& git -C $snapshot init --quiet
if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize candidate repository.' }
& git -C $snapshot -c core.autocrlf=false add --force --all 2> (Join-Path $candidate 'snapshot-git.log')
if ($LASTEXITCODE -ne 0) { throw 'Cannot stage candidate source.' }
& git -C $snapshot -c user.name=BetterCM-Candidate -c user.email=candidate@localhost commit --quiet --no-gpg-sign -m ('Prepare BetterCM ' + $version + ' candidate')
if ($LASTEXITCODE -ne 0) { throw 'Cannot commit isolated candidate snapshot.' }
$snapshotIdentity = Get-SourceIdentity $snapshot
if (-not $snapshotIdentity.SourceClean -or $snapshotIdentity.SourceTreeSHA256 -ne $origin.SourceTreeSHA256) { throw 'Candidate snapshot must exactly match source bytes.' }
Write-Output ('Verifying isolated source snapshot: ' + $snapshotIdentity.SourceCommit)
Write-Output ('Evidence directory: ' + $candidate)
& (Join-Path $snapshot 'verify.ps1') -SkipDesktopChecks:$SkipDesktopChecks *> (Join-Path $candidate 'verify.log')
& (Join-Path $snapshot 'Build-Installer.ps1') -Compiler $Compiler -SkipDesktopChecks:$SkipDesktopChecks -ReuseVerifiedPublish *> (Join-Path $candidate 'build-installer.log')
if ($InstallerSmoke) {
    & $Compiler /Qp /DTestBuild (Join-Path $snapshot 'Installer\BetterCM.iss') *> (Join-Path $candidate 'build-qa-installer.log')
    if ($LASTEXITCODE -ne 0) { throw 'QA installer compilation failed.' }
    & (Join-Path $snapshot 'Installer\Test-Installer.ps1') -Isolated -ReleaseKind Release *> (Join-Path $candidate 'installer-smoke.log')
    $installerEvidence = Join-Path $candidate 'installer-evidence'
    New-Item -ItemType Directory -Path $installerEvidence -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $snapshot 'artifacts\qa') -File |
        Where-Object { $_.Name -eq 'installer-smoke.json' -or $_.Name -like 'installed-tests-*.json' } |
        Copy-Item -Destination $installerEvidence
}
$kind = if ($SkipDesktopChecks) { 'internal' } else { 'releases' }
$package = Join-Path $candidate 'package'
Copy-Item -LiteralPath (Join-Path $snapshot ('artifacts\' + $kind + '\' + $version)) -Destination $package -Recurse
$sourceZip = Join-Path $candidate ('BetterCM-' + $version + '-Source.zip')
$archive = [IO.Compression.ZipFile]::Open($sourceZip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $origin.Files) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $snapshot $file.Path), $file.Path) | Out-Null
    }
} finally { $archive.Dispose() }
$bundle = Join-Path $candidate ('BetterCM-' + $version + '-Source.bundle')
& git -C $snapshot bundle create $bundle --all
if ($LASTEXITCODE -ne 0) { throw 'Cannot preserve candidate source history.' }
Assert-SourceIdentity $origin (Get-SourceIdentity $projectRoot)
Assert-SourceIdentity $snapshotIdentity (Get-SourceIdentity $snapshot)
$files = @(Get-ChildItem -LiteralPath $package -File -Recurse) + @(Get-Item -LiteralPath $sourceZip, $bundle, (Join-Path $candidate 'origin-source.json'))
if ($InstallerSmoke) { $files += @(Get-ChildItem -LiteralPath $installerEvidence -File) }
[pscustomobject]@{
    Version=$version; SnapshotCommit=$snapshotIdentity.SourceCommit; SourceTreeSHA256=$origin.SourceTreeSHA256
    OriginCommit=$origin.SourceCommit; OriginSourceClean=$origin.SourceClean
    DesktopChecks=-not [bool]$SkipDesktopChecks; Signed=$false
    IsolatedInstallerSmoke=[bool]$InstallerSmoke; ExactFinalSetupInstalled=$false; CleanVMVerified=$false
    SourceIdentity='Exact source bytes in Source.zip; isolated snapshot commit in binaries and Source.bundle; origin workspace is not committed by this script'
    Files=@($files | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ Path=[IO.Path]::GetRelativePath($candidate, $_.FullName).Replace('\', '/'); Bytes=$_.Length; SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash }
    })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $candidate 'candidate-manifest.json') -Encoding utf8
Write-Output ('Candidate complete: ' + $candidate)
