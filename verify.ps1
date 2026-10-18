param([string]$PublishDirectory = 'artifacts\publish\win-x64', [switch]$SkipDesktopChecks,
    [string]$CertificateThumbprint, [string]$SignToolPath,
    [string]$TimestampServer = 'https://timestamp.digicert.com', [switch]$RequireSignedPublish)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'BetterCM.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
$publishPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $PublishDirectory))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $publishPath.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Publish output must stay inside project artifacts.' }
$qaFolder = Join-Path $projectRoot 'artifacts\qa'
New-Item -ItemType Directory -Path $qaFolder -Force | Out-Null
& (Join-Path $projectRoot 'Installer\Test-Scripts.ps1') -ReportPath (Join-Path $qaFolder 'script-tests.json')

& dotnet restore (Join-Path $projectRoot 'BetterCM.slnx') --locked-mode --nologo
if ($LASTEXITCODE -ne 0) { throw 'Locked dependency restore failed.' }
$auditText = & dotnet list (Join-Path $projectRoot 'BetterCM.slnx') package --vulnerable --include-transitive --format json --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Dependency audit failed.' }
$audit = ($auditText -join "`n") | ConvertFrom-Json
. (Join-Path $projectRoot 'Installer\ReleaseReports.ps1')
ConvertTo-PublicDependencyAudit $audit $projectRoot | ConvertTo-Json -Depth 40 |
    Set-Content -LiteralPath (Join-Path $qaFolder 'dependency-audit.json') -Encoding utf8
if ($audit.sources.Count -eq 0) { throw 'Dependency audit did not use any advisory sources.' }
foreach ($auditProject in $audit.projects) {
    foreach ($framework in $auditProject.frameworks) {
        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
            if ($package.vulnerabilities.Count -gt 0) { throw ('Vulnerable dependency: ' + $package.id) }
        }
    }
}
& dotnet build (Join-Path $projectRoot 'BetterCM.csproj') -c Release --no-restore --nologo -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Build failed; warnings are errors.' }
& dotnet test (Join-Path $projectRoot 'Tests\BetterCM.Tests.csproj') -c Release --no-restore --nologo -warnaserror --logger 'trx;LogFileName=headless-tests.trx' --results-directory (Join-Path $qaFolder 'headless')
if ($LASTEXITCODE -ne 0) { throw 'Headless logic tests failed.' }
if (Test-Path -LiteralPath $publishPath) { Remove-Item -LiteralPath $publishPath -Recurse -Force }
& dotnet publish (Join-Path $projectRoot 'BetterCM.csproj') -c Release -r win-x64 --self-contained true -o $publishPath --nologo -warnaserror -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
if (Get-ChildItem -LiteralPath $publishPath -Recurse -Directory | Where-Object Name -eq 'Tests') { throw 'Publish output contains test directories.' }
if (Get-ChildItem -LiteralPath $publishPath -Recurse -File | Where-Object Name -match '(^BetterCM\.Tests|^xunit|testhost)') { throw 'Publish output contains test binaries.' }
. (Join-Path $projectRoot 'Installer\Signing.ps1')
foreach ($name in @('BetterCM.exe', 'BetterCM.dll')) {
    $ownedBinary = Join-Path $publishPath $name
    if ($CertificateThumbprint) { Set-ArtifactSignature $ownedBinary $SignToolPath $CertificateThumbprint $TimestampServer }
    if ($RequireSignedPublish -or $CertificateThumbprint) { Assert-ArtifactSignature $ownedBinary $CertificateThumbprint }
}

$assets = Get-Content -LiteralPath (Join-Path $projectRoot 'obj\project.assets.json') -Raw | ConvertFrom-Json
$frameworkAssets = $assets.project.frameworks.PSObject.Properties.Value | Select-Object -First 1
foreach ($runtimePackage in @('Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
    $dependency = $frameworkAssets.downloadDependencies | Where-Object { $_.name -eq $runtimePackage } | Select-Object -First 1
    if (-not $dependency) { throw ('Cannot locate runtime notices: ' + $runtimePackage) }
    $packageVersion = ($dependency.version.Trim('[', ']') -split ',')[0].Trim()
    $runtimeFolder = Join-Path $assets.project.restore.packagesPath ($runtimePackage.ToLowerInvariant() + '\' + $packageVersion)
    $noticePrefix = if ($runtimePackage -like '*NETCore*') { 'DotNet' } else { 'WindowsDesktop' }
    foreach ($notice in @(Get-ChildItem -LiteralPath $runtimeFolder -File | Where-Object { $_.Name -like 'LICENSE*' -or $_.Name -like 'THIRD-PARTY-NOTICES*' })) {
        Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $publishPath ($noticePrefix + '-' + $notice.Name))
    }
}
foreach ($package in $assets.libraries.PSObject.Properties | Where-Object { $_.Name -like 'Microsoft.WindowsAppSDK.*/*' }) {
    $packageFolder = Join-Path $assets.project.restore.packagesPath $package.Value.path
    foreach ($notice in @(Get-ChildItem -LiteralPath $packageFolder -File | Where-Object { $_.Name -like 'license*' })) {
        $prefix = ($package.Name -split '/')[0]
        Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $publishPath ($prefix + '-' + $notice.Name))
    }
}
if ($SkipDesktopChecks) {
    Write-Output ('Verified BetterCM ' + $version + '; build, headless tests and publish only; desktop checks explicitly skipped.')
    return
}
$qaReport = Join-Path $qaFolder 'published-tests.json'
if (Test-Path -LiteralPath $qaReport) { Remove-Item -LiteralPath $qaReport -Force }
$qaInfo = [Diagnostics.ProcessStartInfo]::new((Join-Path $publishPath 'BetterCM.exe'))
$qaInfo.UseShellExecute = $false
$qaInfo.CreateNoWindow = $true
$qaInfo.Environment['BETTERCM_DATA_DIR'] = Join-Path $qaFolder 'verify-user-data'
$qaInfo.ArgumentList.Add('--self-test')
$qaInfo.ArgumentList.Add($qaReport)
$qaProcess = [Diagnostics.Process]::Start($qaInfo)
if (-not $qaProcess.WaitForExit(60000)) { $qaProcess.Kill(); throw 'UI/Shell checks timed out.' }
if ($qaProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $qaReport)) { throw 'UI/Shell checks failed.' }
$qaResult = Get-Content -LiteralPath $qaReport -Raw | ConvertFrom-Json
if (-not $qaResult.Passed -or $qaResult.Version -ne $version) { throw 'UI/Shell report failed or version mismatched.' }
$expectedChecks = Get-Content -LiteralPath (Join-Path $projectRoot 'Installer\Expected-Checks.json') -Raw | ConvertFrom-Json
if (@($qaResult.Results.Id | Sort-Object -Unique).Count -ne $qaResult.Results.Count -or @($qaResult.Results | Where-Object { -not $_.Id }).Count -gt 0) { throw 'UI/Shell check IDs must be nonempty and unique.' }
if (($qaResult.Results.Id | Sort-Object -CaseSensitive | ConvertTo-Json -Compress) -cne ($expectedChecks.UiShell | Sort-Object -CaseSensitive | ConvertTo-Json -Compress)) { throw 'UI/Shell check IDs differ from reviewed expected suite.' }
$displayNames = Get-Content -LiteralPath (Join-Path $projectRoot 'Installer\Check-DisplayNames.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($result in $qaResult.Results) {
    if ($displayNames[$result.Id] -cne $result.Name) { Write-Warning ('Check display name changed; update Check-DisplayNames.json: ' + $result.Id) }
}
Write-Output ('Verified BetterCM ' + $version + '; UI/Shell checks: ' + $qaResult.Results.Count)
