param([string]$PublishDirectory = 'artifacts\publish\win-x64', [switch]$SkipDesktopChecks)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'BetterCM.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
$publishPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $PublishDirectory))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $publishPath.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Publish output must stay inside project artifacts.' }
$qaFolder = Join-Path $projectRoot 'artifacts\qa'
New-Item -ItemType Directory -Path $qaFolder -Force | Out-Null

& dotnet build (Join-Path $projectRoot 'BetterCM.csproj') -c Release --nologo -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Build failed; warnings are errors.' }
& dotnet test (Join-Path $projectRoot 'Tests\BetterCM.Tests.csproj') -c Release --nologo -warnaserror --logger 'trx;LogFileName=headless-tests.trx' --results-directory (Join-Path $qaFolder 'headless')
if ($LASTEXITCODE -ne 0) { throw 'Headless logic tests failed.' }
if (Test-Path -LiteralPath $publishPath) { Remove-Item -LiteralPath $publishPath -Recurse -Force }
& dotnet publish (Join-Path $projectRoot 'BetterCM.csproj') -c Release -r win-x64 --self-contained true -o $publishPath --nologo -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

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
Write-Output ('Verified BetterCM ' + $version + '; UI/Shell checks: ' + $qaResult.Results.Count)
