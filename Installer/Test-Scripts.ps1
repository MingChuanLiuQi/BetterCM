param([string]$ReportPath = (Join-Path (Split-Path $PSScriptRoot) 'artifacts\qa\script-tests.json'))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Signing.ps1')
. (Join-Path $PSScriptRoot 'ReleaseReports.ps1')
$fixture = Join-Path (Split-Path $PSScriptRoot) ('artifacts\script-fixture-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$checks = [Collections.Generic.List[object]]::new()
function Assert-Check([string]$Id, [bool]$Passed) {
    $checks.Add([pscustomobject]@{ Id = $Id; Passed = $Passed })
    if (-not $Passed) { throw "Script regression failed: $Id" }
}
function Assert-Rejected([string]$Id, [scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert-Check $Id $rejected
}
$fixtureSucceeded = $false
try {
$unsigned = Join-Path $fixture 'unsigned.exe'
'unsigned fixture' | Set-Content -LiteralPath $unsigned
Assert-Rejected 'Signing/RejectUnsigned' { Assert-ArtifactSignature $unsigned }
Assert-Rejected 'Reports/ReleaseRequiresFinalInstaller' { Get-InstallerReportContext $fixture '1.8.0' 'Release' }
$internal = Get-InstallerReportContext $fixture '1.8.0' 'Internal'
Assert-Check 'Reports/InternalExplicitlyAllowsNoFinalInstaller' ($internal.Kind -eq 'Internal' -and $null -eq $internal.FinalSetupSHA256)
$releasePath = Join-Path $fixture 'artifacts\releases\1.8.0'
New-Item -ItemType Directory -Path $releasePath -Force | Out-Null
'final installer fixture' | Set-Content -LiteralPath (Join-Path $releasePath 'BetterCM-1.8.0-Setup-x64.exe')
$release = Get-InstallerReportContext $fixture '1.8.0' 'Release'
Assert-Check 'Reports/ReleaseHashesActualFinalInstaller' ($release.FinalSetupSHA256 -eq (Get-FileHash -LiteralPath $release.FinalSetupPath).Hash)
$qaScope = Get-InstallerValidationScope $true 'QA-HASH' 'FINAL-HASH' $true
Assert-Check 'Reports/QaInstallDoesNotClaimFinalSetupInstalled' (-not $qaScope.ExactFinalSetupInstalled -and $qaScope.FinalSetupValidation -eq 'HashAndSourceOnly')
$exactScope = Get-InstallerValidationScope $false 'FINAL-HASH' 'FINAL-HASH' $true
Assert-Check 'Reports/ExactSetupDoesNotClaimCleanEnvironment' ($exactScope.ExactFinalSetupInstalled -and $exactScope.CleanEnvironmentVerification -eq 'NotPerformed')
$failedScope = Get-InstallerValidationScope $false 'FINAL-HASH' 'FINAL-HASH' $false
Assert-Check 'Reports/FailedInstallCannotClaimSuccess' (-not $failedScope.ExactFinalSetupInstalled -and $failedScope.FinalSetupValidation -eq 'AttemptFailed')
$audit = [pscustomobject]@{ projects = @(
    [pscustomobject]@{ path = (Join-Path $fixture 'BetterCM.csproj') },
    [pscustomobject]@{ path = (Join-Path $fixture 'Tests\BetterCM.Tests.csproj') },
    [pscustomobject]@{ path = (Join-Path ([IO.Path]::GetPathRoot($fixture)) 'outside\private\External.csproj') }
) }
$public = ConvertTo-PublicDependencyAudit $audit $fixture
Assert-Check 'Reports/RelativeProductProject' ($public.projects[0].path -eq 'BetterCM.csproj')
Assert-Check 'Reports/RelativeNestedProject' ($public.projects[1].path -eq 'Tests/BetterCM.Tests.csproj')
Assert-Check 'Reports/ExternalProjectBasenameOnly' ($public.projects[2].path -eq 'External.csproj')

# Test callback discovery/copy deterministically without installing certificates or changing trust stores.
# Signature acceptance below is a fixture stub, not evidence of a successful Authenticode signing run.
$validated = [Collections.Generic.List[string]]::new()
function Assert-ArtifactSignature { param([string]$Path, [string]$CertificateThumbprint)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw 'Missing fixture.' }
    $validated.Add([IO.Path]::GetFileName($Path))
}
foreach ($architecture in @('32', '64')) {
    $temporary = Join-Path $fixture ('uninst.e' + $architecture + '.tmp')
    ('signed content fixture ' + $architecture) | Set-Content -LiteralPath $temporary
    $validated.Clear()
    Save-SignedUninstallerEvidence $temporary $fixture 'fixture-thumbprint'
    $evidenceName = 'signed-uninstaller-e' + $architecture + '.exe'
    $evidence = Join-Path $fixture $evidenceName
    Assert-Check ('Signing/CaptureTemporaryE' + $architecture) ((Get-FileHash -LiteralPath $temporary).Hash -eq (Get-FileHash -LiteralPath $evidence).Hash -and $validated.Count -eq 2 -and $validated[1] -eq $evidenceName)
}
$validated.Clear()
Save-SignedUninstallerEvidence $unsigned $fixture 'fixture-thumbprint'
Assert-Check 'Signing/SetupCallbackDoesNotReplaceUninstallerEvidence' ($validated.Count -eq 0)
$publishedEvidence = @(Update-PublishedUninstallerEvidence $fixture $releasePath $true 'fixture-thumbprint')
Assert-Check 'Signing/PreservesBothArchitectures' ($publishedEvidence.Count -eq 2 -and ((@($publishedEvidence.Architecture | Sort-Object) -join ',') -eq 'e32,e64'))
foreach ($entry in $publishedEvidence) {
    Assert-Check ('Signing/PublishedHash/' + $entry.Architecture) ((Get-FileHash -LiteralPath (Join-Path $releasePath $entry.File)).Hash -eq $entry.SHA256)
}
$emptyStage = Join-Path $fixture 'empty-stage'
New-Item -ItemType Directory -Path $emptyStage | Out-Null
Assert-Rejected 'Signing/MissingEvidenceRejectsSignedBuild' { Update-PublishedUninstallerEvidence $emptyStage $releasePath $true 'fixture-thumbprint' }
Assert-Check 'Signing/RejectedBuildPreservesPreviousEvidence' (Test-Path -LiteralPath (Join-Path $releasePath 'signed-uninstaller-e32.exe'))
'legacy' | Set-Content -LiteralPath (Join-Path $releasePath 'signed-uninstaller.exe')
'unrelated' | Set-Content -LiteralPath (Join-Path $releasePath 'signed-uninstaller-user-notes.txt')
$unsignedEvidence = @(Update-PublishedUninstallerEvidence $emptyStage $releasePath $false '')
Assert-Check 'Signing/UnsignedRebuildRemovesOwnedEvidence' ($unsignedEvidence.Count -eq 0 -and @(Get-ChildItem -LiteralPath $releasePath -Filter 'signed-uninstaller*.exe').Count -eq 0)
Assert-Check 'Signing/EvidenceCleanupPreservesOtherFiles' ((Test-Path -LiteralPath (Join-Path $releasePath 'signed-uninstaller-user-notes.txt')) -and (Test-Path -LiteralPath $release.FinalSetupPath))

$maintenanceSource = Join-Path (Split-Path $PSScriptRoot) 'Maintenance.ps1'
Copy-Item -LiteralPath $maintenanceSource -Destination (Join-Path $fixture 'Maintenance.ps1')
'source fixture' | Set-Content -LiteralPath (Join-Path $fixture 'source.cs')
'* text=auto' | Set-Content -LiteralPath (Join-Path $fixture '.gitattributes')
'bin/' | Set-Content -LiteralPath (Join-Path $fixture '.gitignore')
'root = true' | Set-Content -LiteralPath (Join-Path $fixture '.editorconfig')
New-Item -ItemType Directory -Path (Join-Path $fixture 'Tests\bin') -Force | Out-Null
'generated' | Set-Content -LiteralPath (Join-Path $fixture 'Tests\bin\generated.cs')
& git -C $fixture init --quiet
if ($LASTEXITCODE -ne 0) { throw 'Fixture git init failed.' }
& git -C $fixture add -f source.cs Tests/bin/generated.cs
if ($LASTEXITCODE -ne 0) { throw 'Fixture git add failed.' }
& git -C $fixture -c user.name=ScriptFixture -c user.email=fixture@example.invalid commit --quiet -m 'Source snapshot regression fixture'
if ($LASTEXITCODE -ne 0) { throw 'Fixture git commit failed.' }
$families = [ordered]@{
    Development = @('dev-old', 'dev2-old', 'dev3-old', 'dev9-latest')
    InstallerBuild = @('installer-build-old', 'installer-0.6.0-old', 'installer-qa-latest')
    QaCompiler = @('qa-compiler-old', 'qa-final-compiler-old', 'qa-layout-compiler-latest')
    QaInstaller = @('qa-installer-old', 'qa-installer-latest')
    ScriptFixture = @('script-fixture-00000000000000000000000000000001', 'script-fixture-00000000000000000000000000000002')
}
foreach ($names in $families.Values) {
    for ($i = 0; $i -lt $names.Count; $i++) {
        $directory = New-Item -ItemType Directory -Path (Join-Path $fixture ('artifacts\' + $names[$i])) -Force
        'build fixture' | Set-Content -LiteralPath (Join-Path $directory.FullName 'output.txt')
        $directory.LastWriteTime = [datetime]::new(2020, 1, 1).AddDays($i)
    }
}
foreach ($name in @('device-manual', 'qa-important')) { New-Item -ItemType Directory -Path (Join-Path $fixture ('artifacts\' + $name)) | Out-Null }
& (Join-Path $fixture 'Maintenance.ps1') -KeepRecent 1 -SkipQaArchive *> (Join-Path $fixture 'dry-run.log')
Assert-Check 'Maintenance/DryRunDoesNotCreateBackups' (-not (Test-Path -LiteralPath (Join-Path $fixture 'Backups')))
Assert-Check 'Maintenance/DryRunPreservesAllDirectories' (Test-Path -LiteralPath (Join-Path $fixture 'artifacts\dev2-old'))
& (Join-Path $fixture 'Maintenance.ps1') -Apply -KeepRecent 1 -SkipQaArchive *> (Join-Path $fixture 'apply.log')
$maintenanceReport = Get-Content -LiteralPath (Join-Path $fixture 'artifacts\maintenance-report.json') -Raw | ConvertFrom-Json
foreach ($family in $families.GetEnumerator()) {
    $group = $maintenanceReport.Groups | Where-Object Name -EQ $family.Key
    $names = $family.Value
    Assert-Check ('Maintenance/CompleteCounts/' + $family.Key) ($group.MatchedBefore -eq $names.Count -and $group.Remaining -eq 1 -and $group.PlannedArchives -eq ($names.Count - 1))
    Assert-Check ('Maintenance/RetainsNewest/' + $family.Key) ((Test-Path -LiteralPath (Join-Path $fixture ('artifacts\' + $names[-1]))) -and -not (Test-Path -LiteralPath (Join-Path $fixture ('artifacts\' + $names[0]))))
}
Assert-Check 'Maintenance/UnrelatedDirectoriesPreserved' ((Test-Path -LiteralPath (Join-Path $fixture 'artifacts\device-manual')) -and (Test-Path -LiteralPath (Join-Path $fixture 'artifacts\qa-important')))
Assert-Check 'Maintenance/EveryRemovalHasVerifiedArchive' (@($maintenanceReport.Results | Where-Object { -not $_.Removed -or -not (Test-Path -LiteralPath (Join-Path $fixture $_.Archive)) }).Count -eq 0)
$snapshot = Get-ChildItem -LiteralPath (Join-Path $fixture 'Backups') -Filter 'source-*.zip' | Select-Object -First 1
$zip = [IO.Compression.ZipFile]::OpenRead($snapshot.FullName)
try {
    Assert-Check 'Maintenance/UntrackedDotfilesIncluded' (@('.gitattributes', '.gitignore', '.editorconfig' | Where-Object { $_ -notin $zip.Entries.FullName }).Count -eq 0)
    Assert-Check 'Maintenance/TrackedBuildOutputExcluded' ('Tests/bin/generated.cs' -notin $zip.Entries.FullName -and 'source.cs' -in $zip.Entries.FullName)
} finally { $zip.Dispose() }
Assert-Check 'Maintenance/ReleasePayloadRetained' (Test-Path -LiteralPath $release.FinalSetupPath)
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ReportPath))) -Force | Out-Null
[pscustomobject]@{ Passed = $true; SignatureSuccessPath = 'BlockedNoCertificate; callback tests use a fixture stub'; Results = $checks } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding utf8
Write-Output ('Script regression checks passed: ' + $checks.Count)
$fixtureSucceeded = $true
} finally {
    # Successful fixtures are disposable; retain a failed run for diagnosis.
    if ($fixtureSucceeded) {
        $resolved = [IO.Path]::GetFullPath($fixture)
        $parent = [IO.Path]::GetFullPath((Join-Path (Split-Path $PSScriptRoot) 'artifacts')) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^script-fixture-[0-9a-f]{32}$') { throw 'Unsafe fixture cleanup path.' }
        foreach ($directory in @((Get-Item -LiteralPath $resolved -Force)) + @(Get-ChildItem -LiteralPath $resolved -Directory -Recurse -Force)) {
            if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing fixture cleanup through a reparse point.' }
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
