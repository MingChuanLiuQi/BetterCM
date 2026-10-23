param([Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version)
$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot 'Version.props'
[xml]$document = Get-Content -LiteralPath $path -Raw
$document.Project.PropertyGroup.Version = $Version
$document.Save($path)
Write-Output ('Version.props updated to ' + $Version + '. Add the corresponding CHANGELOG and QA evidence before release.')
