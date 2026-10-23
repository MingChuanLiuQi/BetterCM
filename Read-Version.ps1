param([string]$ProjectPath = (Join-Path $PSScriptRoot 'BetterCM.csproj'))
$ErrorActionPreference = 'Stop'
$value = & dotnet msbuild $ProjectPath -getProperty:Version -nologo
if ($LASTEXITCODE -ne 0) { throw 'Cannot evaluate project version.' }
$version = ($value -join "`n").Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid project version: $version" }
return $version
