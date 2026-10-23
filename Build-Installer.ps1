param([string]$Compiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    [switch]$SkipDesktopChecks, [switch]$RequireSigning, [string]$CertificateThumbprint,
    [string]$SignToolPath, [string]$TimestampServer = 'https://timestamp.digicert.com', [switch]$ReuseVerifiedPublish)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$version = & (Join-Path $projectRoot 'Read-Version.ps1')
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Install Inno Setup 6.5+ or supply -Compiler with ISCC.exe path.' }
$sourceCommit = & git -C $projectRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or (& git -C $projectRoot status --porcelain)) { throw 'Installer requires a committed, clean source tree.' }
if ($RequireSigning -and -not $CertificateThumbprint) { throw 'Public signed releases require a code-signing certificate.' }
. (Join-Path $projectRoot 'Installer\Signing.ps1')
if ($ReuseVerifiedPublish) {
    . (Join-Path $projectRoot 'Installer\VerificationReceipt.ps1')
    $receipt = Get-Content -LiteralPath (Join-Path $projectRoot 'artifacts\qa\verified-publish.json') -Raw | ConvertFrom-Json
    $verifiedPath = Join-Path $projectRoot 'artifacts\publish\win-x64'
    Assert-VerifiedPublish $verifiedPath $receipt $version $sourceCommit (-not [bool]$SkipDesktopChecks)
    & (Join-Path $projectRoot 'Installer\Test-ProductionBinary.ps1') -Path (Join-Path $verifiedPath 'BetterCM.dll')
    if ($RequireSigning -or $CertificateThumbprint) {
        foreach ($name in @('BetterCM.exe', 'BetterCM.dll')) { Assert-ArtifactSignature (Join-Path $verifiedPath $name) $CertificateThumbprint }
    }
} else {
    & (Join-Path $projectRoot 'verify.ps1') -SkipDesktopChecks:$SkipDesktopChecks -CertificateThumbprint $CertificateThumbprint -SignToolPath $SignToolPath -TimestampServer $TimestampServer -RequireSignedPublish:$RequireSigning
}
if ((& git -C $projectRoot rev-parse HEAD) -ne $sourceCommit -or (& git -C $projectRoot status --porcelain)) { throw 'Source tree changed during verification.' }
if (-not $SkipDesktopChecks) {
    $published = Get-Content -LiteralPath (Join-Path $projectRoot 'artifacts\qa\published-tests.json') -Raw | ConvertFrom-Json
    if ($published.Build -ne ($version + '+' + $sourceCommit)) { throw 'Published assembly does not identify the verified source commit.' }
}
if ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $projectRoot 'artifacts\publish\win-x64\BetterCM.exe')).ProductVersion -ne ($version + '+' + $sourceCommit)) { throw 'Published source commit mismatch.' }
$stagingPath = Join-Path $projectRoot ('artifacts\installer-build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
$buildCompleted = $false
try {
$compilerArguments = @('/Qp', ('/O' + $stagingPath))
if ($CertificateThumbprint) {
    $settingsPath = Join-Path $stagingPath 'signing-settings.json'
    @{ SignToolPath = $SignToolPath; CertificateThumbprint = $CertificateThumbprint; TimestampServer = $TimestampServer } |
        ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding utf8
    $pwsh = (Get-Command pwsh -ErrorAction Stop).Source
    $signScript = Join-Path $projectRoot 'Installer\Sign-InnoArtifact.ps1'
    $signCommand = '$q' + $pwsh + '$q -NoProfile -NonInteractive -File $q' + $signScript + '$q -SettingsPath $q' + $settingsPath + '$q -Path $f'
    $compilerArguments += @('/DSignedBuild', ('/SBetterCMSign=' + $signCommand))
}
& $Compiler @compilerArguments (Join-Path $projectRoot 'Installer\BetterCM.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
$releaseDirectory = Join-Path $projectRoot ('artifacts\releases\' + $version)
if ($SkipDesktopChecks) { $releaseDirectory = Join-Path $projectRoot ('artifacts\internal\' + $version) }
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
$setupPath = Join-Path $releaseDirectory ("BetterCM-" + $version + "-Setup-x64.exe")
$stagedSetup = Join-Path $stagingPath ([IO.Path]::GetFileName($setupPath))
if ($CertificateThumbprint) {
    Assert-ArtifactSignature $stagedSetup $CertificateThumbprint
}
$signedUninstallers = @(Update-PublishedUninstallerEvidence $stagingPath $releaseDirectory ([bool]$CertificateThumbprint) $CertificateThumbprint)
Copy-Item -LiteralPath $stagedSetup -Destination $setupPath -Force
$hash = Get-FileHash -LiteralPath $setupPath -Algorithm SHA256
($hash.Hash + '  ' + [IO.Path]::GetFileName($setupPath)) | Set-Content -LiteralPath ($setupPath + '.sha256') -Encoding utf8
Write-Output ("Installer: " + $setupPath)
Write-Output ("SHA256: " + $hash.Hash)
    $payloadPath = Join-Path $projectRoot 'artifacts\publish\win-x64'
    $payloadFiles = @(Get-ChildItem -LiteralPath $payloadPath -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ Path = [IO.Path]::GetRelativePath($payloadPath, $_.FullName); Bytes = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    [pscustomobject]@{
        Version = $version
        Build = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $payloadPath 'BetterCM.exe')).ProductVersion
        SourceCommit = $sourceCommit
        Signed = [bool]$CertificateThumbprint
        SigningStatus = if ($CertificateThumbprint) { 'VerifiedTrustedTimestamped' } else { 'BlockedNoCertificate' }
        DesktopChecks = -not [bool]$SkipDesktopChecks
        InstallerSHA256 = $hash.Hash
        SignerThumbprint = $CertificateThumbprint
        SignedUninstallers = $signedUninstallers
        SignedUninstallerSHA256 = if ($signedUninstallers.Count -eq 1) { $signedUninstallers[0].SHA256 } else { $null }
        Files = $payloadFiles
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $projectRoot ('artifacts\qa\payload-' + $version + '.json')) -Encoding utf8
    $buildCompleted = $true
}
finally {
    # Keep a failed compiler's outputs for diagnosis. Remove only this run's
    # successful staging directory after both deliverables have been written.
    if ($buildCompleted) {
        $resolvedStage = [IO.Path]::GetFullPath($stagingPath)
        $expectedParent = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolvedStage.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolvedStage) -notmatch '^installer-build-[0-9a-f]{32}$') { throw 'Unsafe staging cleanup path.' }
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
