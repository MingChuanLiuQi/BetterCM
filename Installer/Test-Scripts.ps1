param([string]$ReportPath = (Join-Path (Split-Path $PSScriptRoot) 'artifacts\qa\script-tests.json'))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Signing.ps1')
. (Join-Path $PSScriptRoot 'ReleaseReports.ps1')
. (Join-Path $PSScriptRoot 'VerificationReceipt.ps1')
. (Join-Path $PSScriptRoot 'SourceIdentity.ps1')
$fixture = Join-Path (Split-Path $PSScriptRoot) ('artifacts\script-fixture-' + [Guid]::NewGuid().ToString('N'))
$testStorage = $fixture.Replace('script-fixture-', 'script-storage-')
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
$sourceFixture = Join-Path $fixture 'source-identity'
New-Item -ItemType Directory -Path $sourceFixture -Force | Out-Null
& git -C $sourceFixture init --quiet
if ($LASTEXITCODE -ne 0) { throw 'Source identity fixture initialization failed.' }
'artifacts/' | Set-Content -LiteralPath (Join-Path $sourceFixture '.gitignore') -Encoding utf8
'baseline' | Set-Content -LiteralPath (Join-Path $sourceFixture '源 码.txt') -Encoding utf8
& git -C $sourceFixture add --all
& git -C $sourceFixture -c user.name=BetterCM-Fixture -c user.email=fixture@localhost commit --quiet -m 'Source identity fixture'
if ($LASTEXITCODE -ne 0) { throw 'Source identity fixture commit failed.' }
$cleanSource = Get-SourceIdentity $sourceFixture
Assert-Check 'Source/CleanIncludesUnicodePath' ($cleanSource.SourceClean -and $cleanSource.Files.Count -eq 2 -and '源 码.txt' -in $cleanSource.Files.Path)
New-Item -ItemType Directory -Path (Join-Path $sourceFixture 'artifacts') -Force | Out-Null
'generated' | Set-Content -LiteralPath (Join-Path $sourceFixture 'artifacts\ignored.txt')
Assert-SourceIdentity $cleanSource (Get-SourceIdentity $sourceFixture)
Assert-Check 'Source/IgnoredBuildOutputsDoNotChangeIdentity' $true
'changed' | Set-Content -LiteralPath (Join-Path $sourceFixture '源 码.txt') -Encoding utf8
$editedSource = Get-SourceIdentity $sourceFixture
Assert-Check 'Source/DetectsUncommittedContents' (-not $editedSource.SourceClean -and $editedSource.SourceTreeSHA256 -ne $cleanSource.SourceTreeSHA256)
Assert-Rejected 'Source/RejectsChangedSource' { Assert-SourceIdentity $cleanSource $editedSource }
'new file' | Set-Content -LiteralPath (Join-Path $sourceFixture 'new.cs')
$newSource = Get-SourceIdentity $sourceFixture
Assert-Check 'Source/IncludesUntrackedSource' ('new.cs' -in $newSource.Files.Path -and $newSource.SourceTreeSHA256 -ne $editedSource.SourceTreeSHA256)
Assert-Rejected 'Source/RejectsLegacyReceiptWithoutTreeHash' { Assert-SourceIdentity ([pscustomobject]@{SourceCommit=$cleanSource.SourceCommit;SourceClean=$true}) $cleanSource }
$receiptPath = New-Item -ItemType Directory -Path (Join-Path $fixture 'verified-payload')
'one' | Set-Content -LiteralPath (Join-Path $receiptPath.FullName 'one.txt')
'two' | Set-Content -LiteralPath (Join-Path $receiptPath.FullName 'two.txt')
$receiptFiles = @(Get-ChildItem -LiteralPath $receiptPath.FullName -File | ForEach-Object { [pscustomobject]@{ Path = $_.Name; SHA256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
$receipt = [pscustomobject]@{ SourceClean=$true; SourceCommit='fixture-commit'; Version='1.9.0'; DesktopChecks=$false; Files=$receiptFiles }
Assert-VerifiedPublish $receiptPath.FullName $receipt '1.9.0' 'fixture-commit' $false
Assert-Check 'Receipt/AcceptsMatchingInternalPayload' $true
Assert-Rejected 'Receipt/RejectsWrongCommit' { Assert-VerifiedPublish $receiptPath.FullName $receipt '1.9.0' 'other-commit' $false }
Assert-Rejected 'Receipt/RejectsWrongVersion' { Assert-VerifiedPublish $receiptPath.FullName $receipt '2.0.0' 'fixture-commit' $false }
Assert-Rejected 'Receipt/RejectsMissingDesktopEvidence' { Assert-VerifiedPublish $receiptPath.FullName $receipt '1.9.0' 'fixture-commit' $true }
$receipt.SourceClean = $false
Assert-Rejected 'Receipt/RejectsDirtySource' { Assert-VerifiedPublish $receiptPath.FullName $receipt '1.9.0' 'fixture-commit' $false }
$receipt.SourceClean = $true
$receipt.Files = @($receiptFiles[0], $receiptFiles[0])
Assert-Rejected 'Receipt/RejectsDuplicatePaths' { Assert-VerifiedPublish $receiptPath.FullName $receipt '1.9.0' 'fixture-commit' $false }
$receipt.Files = @([pscustomobject]@{ Path='..\outside.txt'; SHA256='unused' }, $receiptFiles[1])
Assert-Rejected 'Receipt/RejectsEscapingPaths' { Assert-VerifiedPublish $receiptPath.FullName $receipt '1.9.0' 'fixture-commit' $false }
$receipt.Files = $receiptFiles
'tampered' | Set-Content -LiteralPath (Join-Path $receiptPath.FullName 'one.txt')
Assert-Rejected 'Receipt/RejectsModifiedPayload' { Assert-VerifiedPublish $receiptPath.FullName $receipt '1.9.0' 'fixture-commit' $false }
'extra' | Set-Content -LiteralPath (Join-Path $receiptPath.FullName 'extra.txt')
Assert-Rejected 'Receipt/RejectsAdditionalFiles' { Assert-VerifiedPublish $receiptPath.FullName $receipt '1.9.0' 'fixture-commit' $false }
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
    Baseline = @('baseline-old', 'baseline-latest')
    Maintenance = @('maintenance-old', 'maintenance-latest')
    ScriptFixture = @('script-fixture-00000000000000000000000000000001', 'script-storage-00000000000000000000000000000002', 'script-fixture-00000000000000000000000000000003', 'script-storage-00000000000000000000000000000004')
}
foreach ($names in $families.Values) {
    for ($i = 0; $i -lt $names.Count; $i++) {
        $directory = New-Item -ItemType Directory -Path (Join-Path $fixture ('artifacts\' + $names[$i])) -Force
        'build fixture' | Set-Content -LiteralPath (Join-Path $directory.FullName 'output.txt')
        $directory.LastWriteTime = [datetime]::new(2020, 1, 1).AddDays($i)
    }
}
foreach ($name in @('device-manual', 'qa-important')) { New-Item -ItemType Directory -Path (Join-Path $fixture ('artifacts\' + $name)) | Out-Null }
& (Join-Path $fixture 'Maintenance.ps1') -BackupRoot $testStorage -KeepRecent 1 -SkipQaArchive *> (Join-Path $fixture 'dry-run.log')
Assert-Check 'Maintenance/DryRunDoesNotCreateBackups' (-not (Test-Path -LiteralPath (Join-Path $fixture 'Backups')))
Assert-Check 'Maintenance/DryRunPreservesAllDirectories' (Test-Path -LiteralPath (Join-Path $fixture 'artifacts\dev2-old'))
Assert-Rejected 'Maintenance/RejectWorkspaceRootAsStorage' { & (Join-Path $fixture 'Maintenance.ps1') -BackupRoot $fixture }
Assert-Rejected 'Maintenance/RejectTrailingSeparatorWorkspaceRoot' { & (Join-Path $fixture 'Maintenance.ps1') -BackupRoot ($fixture + '\') }
Assert-Rejected 'Maintenance/RejectNestedWorkspaceStorage' { & (Join-Path $fixture 'Maintenance.ps1') -BackupRoot (Join-Path $fixture 'storage') }
Assert-Rejected 'Maintenance/RejectNonfiniteCapacity' { & (Join-Path $fixture 'Maintenance.ps1') -MaxArchiveGB ([double]::NaN) }
& (Join-Path $fixture 'Maintenance.ps1') -BackupRoot $testStorage -Apply -KeepRecent 1 -SkipQaArchive *> (Join-Path $fixture 'apply.log')
$maintenanceReport = Get-Content -LiteralPath (Join-Path $fixture 'artifacts\maintenance-report.json') -Raw | ConvertFrom-Json
foreach ($family in $families.GetEnumerator()) {
    $group = $maintenanceReport.Groups | Where-Object Name -EQ $family.Key
    $names = $family.Value
    Assert-Check ('Maintenance/CompleteCounts/' + $family.Key) ($group.MatchedBefore -eq $names.Count -and $group.Remaining -eq 1 -and $group.PlannedArchives -eq ($names.Count - 1))
    Assert-Check ('Maintenance/RetainsNewest/' + $family.Key) ((Test-Path -LiteralPath (Join-Path $fixture ('artifacts\' + $names[-1]))) -and -not (Test-Path -LiteralPath (Join-Path $fixture ('artifacts\' + $names[0]))))
}
Assert-Check 'Maintenance/UnrelatedDirectoriesPreserved' ((Test-Path -LiteralPath (Join-Path $fixture 'artifacts\device-manual')) -and (Test-Path -LiteralPath (Join-Path $fixture 'artifacts\qa-important')))
Assert-Check 'Maintenance/EveryRemovalHasVerifiedArchive' (@($maintenanceReport.Results | Where-Object { -not $_.Removed -or -not (Test-Path -LiteralPath $_.Archive) }).Count -eq 0)
$snapshot = Get-ChildItem -LiteralPath (Join-Path $testStorage 'snapshots') -Filter 'source-*.zip' | Select-Object -First 1
$zip = [IO.Compression.ZipFile]::OpenRead($snapshot.FullName)
try {
    Assert-Check 'Maintenance/UntrackedDotfilesIncluded' (@('.gitattributes', '.gitignore', '.editorconfig' | Where-Object { $_ -notin $zip.Entries.FullName }).Count -eq 0)
    Assert-Check 'Maintenance/TrackedBuildOutputExcluded' ('Tests/bin/generated.cs' -notin $zip.Entries.FullName -and 'source.cs' -in $zip.Entries.FullName)
} finally { $zip.Dispose() }
Assert-Check 'Maintenance/ReleasePayloadRetained' (Test-Path -LiteralPath $release.FinalSetupPath)
$failedStorageArchive = $maintenanceReport.Results | Where-Object Path -EQ 'artifacts\script-storage-00000000000000000000000000000002'
Assert-Check 'Maintenance/FailedStorageGetsVerifiedArchive' ($failedStorageArchive.Removed -and (Test-Path -LiteralPath $failedStorageArchive.Archive))
# Retention never touches unknown archives, snapshots, bundles or migrated legacy backups.
$ownedArchive = Join-Path $testStorage 'archives\dev-extra-20200101-000000-12345678.zip'
'old generated fixture' | Set-Content -LiteralPath $ownedArchive
$unknownArchive = Join-Path $testStorage 'archives\user-important.zip'
'keep me' | Set-Content -LiteralPath $unknownArchive
& (Join-Path $fixture 'Maintenance.ps1') -BackupRoot $testStorage -PruneArchives -KeepArchiveRecent 0 -SkipQaArchive *> (Join-Path $fixture 'prune-dry.log')
Assert-Check 'Maintenance/PruningDryRunPreservesArchive' (Test-Path -LiteralPath $ownedArchive)
& (Join-Path $fixture 'Maintenance.ps1') -BackupRoot $testStorage -Apply -PruneArchives -KeepArchiveRecent 0 -SkipQaArchive *> (Join-Path $fixture 'prune.log')
Assert-Check 'Maintenance/PrunesOnlyOwnedArchives' (-not (Test-Path -LiteralPath $ownedArchive) -and (Test-Path -LiteralPath $unknownArchive) -and (Test-Path -LiteralPath $snapshot.FullName))
Assert-Check 'Maintenance/PrunesOldFailedStorageArchive' (-not (Test-Path -LiteralPath $failedStorageArchive.Archive))
$capacityArchive = Join-Path $testStorage 'archives\dev-capacity-20200101-000000-12345678.zip'
('x' * 256) | Set-Content -LiteralPath $capacityArchive
& (Join-Path $fixture 'Maintenance.ps1') -BackupRoot $testStorage -Apply -PruneArchives -KeepArchiveRecent 100 -MaxArchiveGB 0.00000001 -SkipQaArchive *> (Join-Path $fixture 'capacity.log')
Assert-Check 'Maintenance/CapacityLimitOverridesRecentCount' (-not (Test-Path -LiteralPath $capacityArchive) -and (Test-Path -LiteralPath $unknownArchive))
# Reproduce deletion of the source and pruning of its sole new archive in the same invocation.
foreach ($budget in @(@{ Name='zero-count'; Count=0; GB=10 }, @{ Name='over-capacity'; Count=12; GB=0.00000001 })) {
    $newBuild = New-Item -ItemType Directory -Path (Join-Path $fixture ('artifacts\dev-' + $budget.Name))
    $payload = Join-Path $newBuild.FullName 'unique-output.txt'
    ('unique build output ' + $budget.Name) | Set-Content -LiteralPath $payload
    $payloadHash = (Get-FileHash -LiteralPath $payload).Hash
    & (Join-Path $fixture 'Maintenance.ps1') -BackupRoot $testStorage -Apply -KeepRecent 0 -PruneArchives -KeepArchiveRecent $budget.Count -MaxArchiveGB $budget.GB -SkipQaArchive *> (Join-Path $fixture ($budget.Name + '.log'))
    $newReport = Get-Content -LiteralPath (Join-Path $fixture 'artifacts\maintenance-report.json') -Raw | ConvertFrom-Json
    $archived = $newReport.Results | Where-Object Path -EQ ('artifacts\dev-' + $budget.Name)
    Assert-Check ('Maintenance/NewArchiveSurvives/' + $budget.Name) ($archived.Removed -and (Test-Path -LiteralPath $archived.Archive) -and $archived.Archive -notin $newReport.PrunedArchives)
    $zip = [IO.Compression.ZipFile]::OpenRead($archived.Archive)
    try {
        $inputStream = $zip.GetEntry('unique-output.txt').Open()
        try { $restoredHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($inputStream)) } finally { $inputStream.Dispose() }
        Assert-Check ('Maintenance/NewArchiveRestoresExactPayload/' + $budget.Name) ($restoredHash -eq $payloadHash)
    } finally { $zip.Dispose() }
    Assert-Check ('Maintenance/AllNewArchivesProtected/' + $budget.Name) (@($newReport.Results | Where-Object { -not (Test-Path -LiteralPath $_.Archive) }).Count -eq 0)
}
$legacy = New-Item -ItemType Directory -Path (Join-Path $fixture 'Backups')
'legacy backup' | Set-Content -LiteralPath (Join-Path $legacy.FullName 'old.zip')
$legacyHash = (Get-FileHash -LiteralPath (Join-Path $legacy.FullName 'old.zip')).Hash
& (Join-Path $fixture 'Maintenance.ps1') -BackupRoot $testStorage -MigrateLegacyStorage -MigrateOnly *> (Join-Path $fixture 'migration-dry.log')
Assert-Check 'Maintenance/MigrationDryRunPreservesSource' (Test-Path -LiteralPath $legacy.FullName)
& (Join-Path $fixture 'Maintenance.ps1') -BackupRoot $testStorage -MigrateLegacyStorage -MigrateOnly -Apply *> (Join-Path $fixture 'migration.log')
Assert-Check 'Maintenance/MigrationPreservesContents' (-not (Test-Path -LiteralPath $legacy.FullName) -and (Get-FileHash -LiteralPath (Join-Path $testStorage 'legacy-backups\old.zip')).Hash -eq $legacyHash)
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ReportPath))) -Force | Out-Null
[pscustomobject]@{ Passed = $true; SignatureSuccessPath = 'BlockedNoCertificate; callback tests use a fixture stub'; Results = $checks } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding utf8
Write-Output ('Script regression checks passed: ' + $checks.Count)
$fixtureSucceeded = $true
} finally {
    # Successful fixtures are disposable; retain a failed run for diagnosis.
    if ($fixtureSucceeded) {
      foreach ($cleanupPath in @($fixture, $testStorage)) {
        if (-not (Test-Path -LiteralPath $cleanupPath)) { continue }
        $resolved = [IO.Path]::GetFullPath($cleanupPath)
        $parent = [IO.Path]::GetFullPath((Join-Path (Split-Path $PSScriptRoot) 'artifacts')) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^script-(fixture|storage)-[0-9a-f]{32}$') { throw 'Unsafe fixture cleanup path.' }
        foreach ($directory in @((Get-Item -LiteralPath $resolved -Force)) + @(Get-ChildItem -LiteralPath $resolved -Directory -Recurse -Force)) {
            if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing fixture cleanup through a reparse point.' }
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
      }
    }
}
