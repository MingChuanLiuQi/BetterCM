param([string]$SetupPath, [switch]$Isolated, [ValidateSet('Release', 'Internal')][string]$ReleaseKind = 'Release')
$ErrorActionPreference = 'Stop'
if ($Isolated -and -not $PSBoundParameters.ContainsKey('ReleaseKind')) { $ReleaseKind = 'Internal' }
$projectRoot = Split-Path $PSScriptRoot
$version = & (Join-Path $projectRoot 'Read-Version.ps1')
. (Join-Path $PSScriptRoot 'ReleaseReports.ps1')
$releaseContext = Get-InstallerReportContext $projectRoot $version $ReleaseKind
if ($ReleaseKind -eq 'Release') {
    $payload = Get-Content -LiteralPath (Join-Path $projectRoot ('artifacts\qa\payload-' + $version + '.json')) -Raw | ConvertFrom-Json
    $head = & git -C $projectRoot rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $payload.SourceCommit -ne $head -or $payload.Version -ne $version -or $payload.InstallerSHA256 -ne $releaseContext.FinalSetupSHA256) {
        throw 'Final installer, payload manifest and current source commit must agree before a release smoke test.'
    }
}
$fileVersion = $version + '.0'
if (-not $SetupPath) { $SetupPath = if ($Isolated) { Join-Path $projectRoot ('artifacts\qa\BetterCM-' + $version + '-QA-Setup-x64.exe') } elseif ($ReleaseKind -eq 'Internal') { Join-Path $projectRoot ('artifacts\internal\' + $version + '\BetterCM-' + $version + '-Setup-x64.exe') } else { $releaseContext.FinalSetupPath } }
if (-not (Test-Path -LiteralPath $SetupPath -PathType Leaf)) { throw 'Test installer does not exist.' }
$qaFolder = Join-Path $projectRoot 'artifacts\qa'
$installPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot ('artifacts\install-smoke-' + $version)))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [System.IO.Path]::DirectorySeparatorChar
if (-not $installPath.StartsWith($artifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Test installation must stay inside project artifacts.' }
$uninstallRegistry = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{3497A457-D1C1-4D4C-A10A-64D7B32EC101}_is1'
if ($Isolated) { $uninstallRegistry = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{7E8D5579-A731-4323-8AD8-61AA89407AC2}_is1' }
$startupName = if ($Isolated) { 'BetterCM.QA' } else { 'BetterCM' }
$existingInstall = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($uninstallRegistry)
if ($existingInstall) { $existingInstall.Dispose(); throw 'An existing BetterCM installation is registered; smoke test will not replace it.' }
if ($Isolated -and [System.IO.Path]::GetFileName($SetupPath) -notlike '*-QA-Setup-x64.exe') { throw 'Isolated checks require the TestBuild installer.' }
if (-not $Isolated -and (Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'Programs\BetterCM\BetterCM.exe'))) { throw 'Existing installed files detected. Use a TestBuild installer and -Isolated.' }
if (Test-Path -LiteralPath $installPath) { throw 'Smoke directory already exists; inspect it before reusing.' }
New-Item -ItemType Directory -Path $qaFolder -Force | Out-Null
$startupRegistry = 'Software\Microsoft\Windows\CurrentVersion\Run'
$startupKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($startupRegistry)
$previousStartup = $startupKey.GetValue($startupName)
$previousKind = if ($null -ne $previousStartup) { $startupKey.GetValueKind($startupName) } else { $null }
$startupKey.Dispose()
$realStartupKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($startupRegistry)
$realStartupBefore = $realStartupKey.GetValue('BetterCM')
$realStartupKey.Dispose()
$userConfig = Join-Path $env:LOCALAPPDATA 'BetterCM\config.json'
$configHashBefore = if (Test-Path -LiteralPath $userConfig) { (Get-FileHash -LiteralPath $userConfig).Hash } else { $null }
$results = [System.Collections.Generic.List[object]]::new()
$testAppData = Join-Path $qaFolder 'installed-user-data'
function Check([string]$Name, [bool]$Passed) {
    $results.Add([pscustomobject]@{Name=$Name;Passed=$Passed})
    if (-not $Passed) { throw ('Check failed: ' + $Name) }
}
function Run-Checked([string]$Executable, [string[]]$Arguments, [int]$Timeout = 60000, [bool]$Isolate = $false) {
    $processInfo = [System.Diagnostics.ProcessStartInfo]::new($Executable)
    $processInfo.UseShellExecute = $false
    $processInfo.CreateNoWindow = $true
    foreach ($argument in $Arguments) { $processInfo.ArgumentList.Add($argument) }
    if ($Isolate) { $processInfo.Environment['BETTERCM_DATA_DIR'] = $testAppData; $processInfo.Environment['BETTERCM_STARTUP_NAME'] = $startupName; $processInfo.Environment['DOTNET_ROOT'] = (Join-Path $qaFolder 'no-global-dotnet'); $processInfo.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0' }
    $process = [System.Diagnostics.Process]::Start($processInfo)
    if (-not $process.WaitForExit($Timeout)) { $process.Kill(); throw ('Process timed out: ' + [System.IO.Path]::GetFileName($Executable)) }
    if ($process.ExitCode -ne 0) { throw ('Process failed: ' + [System.IO.Path]::GetFileName($Executable) + ', exit ' + $process.ExitCode) }
}
$failure = $null
try {
    # Production payloads ignore QA startup overrides. Do not replace the user's startup value.
    $tasks = if ($Isolated) { '/TASKS=autostart' } else { '/TASKS=' }
    Run-Checked $SetupPath @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR=' + $installPath),$tasks,('/LOG=' + (Join-Path $qaFolder 'install-smoke.log')))
    $installedExe = Join-Path $installPath 'BetterCM.exe'
    if (-not $Isolated -and $payload -and $payload.Signed -and (Get-FileHash -LiteralPath $SetupPath).Hash -eq $payload.InstallerSHA256) {
        . (Join-Path $PSScriptRoot 'Signing.ps1')
        $installedUninstaller = Join-Path $installPath 'unins000.exe'
        Assert-ArtifactSignature $installedUninstaller $payload.SignerThumbprint
        Check '安装后的卸载器与签名回调捕获的架构证据一致' ((Get-FileHash -LiteralPath $installedUninstaller).Hash -in @($payload.SignedUninstallers.SHA256))
    }
    Check ('安装可执行文件和 ' + $version + ' 版本') ((Test-Path -LiteralPath $installedExe) -and (Get-Item -LiteralPath $installedExe).VersionInfo.FileVersion -eq $fileVersion)
    Check '安装包含 .NET、WPF 与系统材质运行组件' ((Test-Path -LiteralPath (Join-Path $installPath 'coreclr.dll')) -and (Test-Path -LiteralPath (Join-Path $installPath 'PresentationFramework.dll')) -and (Test-Path -LiteralPath (Join-Path $installPath 'dwmcorei.dll')) -and (Test-Path -LiteralPath (Join-Path $installPath 'Microsoft.UI.Composition.OSSupport.dll')) -and (Test-Path -LiteralPath (Join-Path $installPath 'Microsoft.InteractiveExperiences.Projection.dll')))
    Check '运行时和安装语言许可说明已随包安装' ((Test-Path -LiteralPath (Join-Path $installPath 'DotNet-LICENSE.TXT')) -and (Test-Path -LiteralPath (Join-Path $installPath 'WindowsDesktop-LICENSE')) -and (Test-Path -LiteralPath (Join-Path $installPath 'THIRD-PARTY-NOTICES.md')))
    $publishPath = Join-Path $projectRoot 'artifacts\publish\win-x64'
    if ($Isolated) { $publishPath = Join-Path $projectRoot 'artifacts\diagnostics\win-x64' }
    $payloadMatches = $true
    foreach ($file in @(Get-ChildItem -LiteralPath $publishPath -File -Recurse)) {
        $relative = [System.IO.Path]::GetRelativePath($publishPath, $file.FullName)
        $destination = Join-Path $installPath $relative
        if (-not (Test-Path -LiteralPath $destination) -or (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { $payloadMatches = $false; break }
    }
    Check '安装的全部发布文件 SHA256 一致' $payloadMatches
    $startupKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($startupRegistry)
    if ($Isolated) { Check '启动项为带引号的安装路径及后台参数' ($startupKey.GetValue($startupName) -eq ('"' + $installedExe + '" --background')) }
    else { Check '正式载荷测试保留原启动项' ($startupKey.GetValue($startupName) -eq $previousStartup) }
    $startupKey.Dispose()
    $installedReport = Join-Path $qaFolder ('installed-tests-' + [Guid]::NewGuid().ToString('N') + '.json')
    if ($Isolated) {
    Run-Checked $installedExe @('--self-test', $installedReport) 45000 $true
    $installedResults = Get-Content -LiteralPath $installedReport -Raw | ConvertFrom-Json
    $publishedResults = Get-Content -LiteralPath (Join-Path $qaFolder 'published-tests.json') -Raw | ConvertFrom-Json
    $expectedChecks = Get-Content -LiteralPath (Join-Path $projectRoot 'Installer\Expected-Checks.json') -Raw | ConvertFrom-Json
    $installedNames = $installedResults.Results.Id | Sort-Object -CaseSensitive | ConvertTo-Json -Compress
    $expectedNames = $expectedChecks.UiShell | Sort-Object -CaseSensitive | ConvertTo-Json -Compress
    Check ('安装目录可独立运行且完整 ' + $expectedChecks.UiShell.Count + ' 项自检通过') ($installedResults.Passed -and $installedResults.Version -eq $version -and $publishedResults.Passed -and $publishedResults.Version -eq $version -and $installedResults.Build -eq $publishedResults.Build -and $installedNames -ceq $expectedNames -and $installedResults.Results.Count -eq $publishedResults.Results.Count)
    } else {
        $probe = [Diagnostics.ProcessStartInfo]::new($installedExe)
        $probe.UseShellExecute = $false; $probe.CreateNoWindow = $true
        $probe.ArgumentList.Add('--self-test'); $probe.ArgumentList.Add($installedReport)
        $rejected = [Diagnostics.Process]::Start($probe)
        if (-not $rejected.WaitForExit(10000)) { $rejected.Kill(); throw 'Production argument rejection timed out.' }
        Check '正式载荷拒绝 QA 自检入口' ($rejected.ExitCode -eq 2 -and -not (Test-Path -LiteralPath $installedReport))
    }
    $backgroundInfo = [System.Diagnostics.ProcessStartInfo]::new($installedExe)
    $backgroundInfo.UseShellExecute = $false
    $backgroundInfo.CreateNoWindow = $true
    $backgroundInfo.ArgumentList.Add('--background')
    $backgroundInfo.Environment['BETTERCM_DATA_DIR'] = $testAppData
    $backgroundInfo.Environment['BETTERCM_STARTUP_NAME'] = $startupName
    $background = [System.Diagnostics.Process]::Start($backgroundInfo)
    Start-Sleep -Seconds 2
    Check '后台启动进程持续运行并初始化用户配置' (-not $background.HasExited -and (Test-Path -LiteralPath (Join-Path $testAppData 'config.json')))
    Run-Checked $installedExe @('--background') 10000 $true
    # The UI process now starts isolated helpers at startup. Count only UI instances.
    $instances = @(Get-CimInstance Win32_Process -Filter "Name='BetterCM.exe'" | Where-Object { $_.ExecutablePath -eq $installedExe -and $_.CommandLine -notmatch '--(?:shell|context)(?:-test)?-host' })
    Check '重复启动保持单个 UI 实例，允许独立辅助宿主' ($instances.Count -eq 1 -and $instances[0].ProcessId -eq $background.Id)
    Run-Checked $installedExe @('--shutdown') 10000 $true
    Check '退出信号结束后台进程' ($background.WaitForExit(5000))
} catch { $failure = $_.Exception.Message }
finally {
    if (Test-Path -LiteralPath (Join-Path $installPath 'BetterCM.exe')) {
        try { Run-Checked (Join-Path $installPath 'BetterCM.exe') @('--shutdown') 10000 $true } catch { $failure = ($failure + '; shutdown: ' + $_.Exception.Message) }
    }
    $uninstaller = Join-Path $installPath 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        try { Run-Checked $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG=' + (Join-Path $qaFolder 'uninstall-smoke.log'))) } catch { $failure = ($failure + '; uninstall: ' + $_.Exception.Message) }
    }
    $startupKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($startupRegistry)
    if ($Isolated) { $results.Add([pscustomobject]@{Name='卸载清除 BetterCM.QA 测试启动项';Passed=($null -eq $startupKey.GetValue($startupName))}) }
    if ($null -eq $previousStartup) { $startupKey.DeleteValue($startupName, $false) } else { $startupKey.SetValue($startupName, $previousStartup, $previousKind) }
    if (-not $Isolated) { $results.Add([pscustomobject]@{Name='非隔离测试恢复后保留原启动项及值类型';Passed=($previousStartup -eq $startupKey.GetValue($startupName) -and ($null -eq $previousKind -or $previousKind -eq $startupKey.GetValueKind($startupName)))}) }
    $startupKey.Dispose()
    $realStartupKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($startupRegistry)
    $results.Add([pscustomobject]@{Name='原有 BetterCM 启动项保持不变';Passed=($realStartupBefore -eq $realStartupKey.GetValue('BetterCM'))})
    $realStartupKey.Dispose()
    $uninstallKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($uninstallRegistry)
    $results.Add([pscustomobject]@{Name='卸载移除程序和卸载注册项';Passed=(-not (Test-Path -LiteralPath (Join-Path $installPath 'BetterCM.exe')) -and $null -eq $uninstallKey)})
    if ($uninstallKey) { $uninstallKey.Dispose() }
    $configHashAfter = if (Test-Path -LiteralPath $userConfig) { (Get-FileHash -LiteralPath $userConfig).Hash } else { $null }
    $results.Add([pscustomobject]@{Name='原有用户配置保持不变';Passed=($configHashBefore -eq $configHashAfter)})
    $finalSetupPath = $releaseContext.FinalSetupPath
    $finalSetupHash = if (Test-Path -LiteralPath $finalSetupPath) { (Get-FileHash -LiteralPath $finalSetupPath -Algorithm SHA256).Hash } else { $null }
    if ($ReleaseKind -eq 'Release' -and (-not $finalSetupHash -or $finalSetupHash -ne $releaseContext.FinalSetupSHA256)) { $failure = ($failure + '; Final release installer missing or changed during smoke test.') }
    $setupHash = (Get-FileHash -LiteralPath $SetupPath -Algorithm SHA256).Hash
    $scope = Get-InstallerValidationScope ([bool]$Isolated) $setupHash $finalSetupHash ($null -eq $failure -and @($results | Where-Object { -not $_.Passed }).Count -eq 0)
    $report = [pscustomobject]@{Version=$version;Time=[DateTimeOffset]::Now;ReleaseKind=$ReleaseKind;ValidationScope=$scope.Scope;ExactFinalSetupInstalled=$scope.ExactFinalSetupInstalled;FinalSetupValidation=$scope.FinalSetupValidation;CleanEnvironmentVerification=$scope.CleanEnvironmentVerification;SourceCommit=(& git -C $projectRoot rev-parse HEAD);IsolatedInstallerIdentity=[bool]$Isolated;SetupSHA256=$setupHash;FinalSetupPath=[IO.Path]::GetRelativePath($projectRoot,$finalSetupPath).Replace('\','/');FinalSetupSHA256=$finalSetupHash;Passed=($null -eq $failure -and @($results | Where-Object { -not $_.Passed }).Count -eq 0);Failure=$failure;Results=$results}
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $qaFolder 'installer-smoke.json') -Encoding utf8
    $report | ConvertTo-Json -Depth 5
}
if (-not $report.Passed) { throw 'Installer smoke test failed; see artifacts/qa/installer-smoke.json.' }
