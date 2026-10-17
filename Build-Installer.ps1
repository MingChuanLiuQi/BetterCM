param([string]$Compiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe")
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'BetterCM.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Install Inno Setup 6.5+ or supply -Compiler with ISCC.exe path.' }
$sourceCommit = & git -C $projectRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or (& git -C $projectRoot status --porcelain)) { throw 'Installer requires a committed, clean source tree.' }
& (Join-Path $projectRoot 'verify.ps1')
if ((& git -C $projectRoot rev-parse HEAD) -ne $sourceCommit -or (& git -C $projectRoot status --porcelain)) { throw 'Source tree changed during verification.' }
$published = Get-Content -LiteralPath (Join-Path $projectRoot 'artifacts\qa\published-tests.json') -Raw | ConvertFrom-Json
if ($published.Build -ne ($version + '+' + $sourceCommit)) { throw 'Published assembly does not identify the verified source commit.' }
$stagingPath = Join-Path $projectRoot ('artifacts\installer-build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
$buildCompleted = $false
try {
& $Compiler '/Qp' ('/O' + $stagingPath) (Join-Path $projectRoot 'Installer\BetterCM.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
$setupPath = Join-Path $projectRoot ("BetterCM-" + $version + "-Setup-x64.exe")
Copy-Item -LiteralPath (Join-Path $stagingPath ([IO.Path]::GetFileName($setupPath))) -Destination $setupPath -Force
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
        InstallerSHA256 = $hash.Hash
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
