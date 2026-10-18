param([Parameter(Mandatory)][string]$SettingsPath, [Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Signing.ps1')
$settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
$stage = [IO.Path]::GetFullPath([IO.Path]::GetDirectoryName($SettingsPath))
$artifact = [IO.Path]::GetFullPath($Path)
if ([IO.Path]::GetDirectoryName($artifact) -ne $stage) { throw 'Inno signing callback must operate inside this build staging directory.' }
Set-ArtifactSignature $artifact $settings.SignToolPath $settings.CertificateThumbprint $settings.TimestampServer
Save-SignedUninstallerEvidence $artifact $stage $settings.CertificateThumbprint
