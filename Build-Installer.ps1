param([string]$Compiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe")
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'BetterCM.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Install Inno Setup 6.5+ or supply -Compiler with ISCC.exe path.' }
& (Join-Path $projectRoot 'verify.ps1')
$stagingPath = Join-Path $projectRoot ('artifacts\installer-build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
& $Compiler '/Qp' ('/O' + $stagingPath) (Join-Path $projectRoot 'Installer\BetterCM.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
$setupPath = Join-Path $projectRoot ("BetterCM-" + $version + "-Setup-x64.exe")
Copy-Item -LiteralPath (Join-Path $stagingPath ([IO.Path]::GetFileName($setupPath))) -Destination $setupPath -Force
$hash = Get-FileHash -LiteralPath $setupPath -Algorithm SHA256
($hash.Hash + '  ' + [IO.Path]::GetFileName($setupPath)) | Set-Content -LiteralPath ($setupPath + '.sha256') -Encoding utf8
Write-Output ("Installer: " + $setupPath)
Write-Output ("SHA256: " + $hash.Hash)
