function Assert-ArtifactSignature {
    param([Parameter(Mandatory)][string]$Path, [string]$CertificateThumbprint)
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate) { throw "A trusted, timestamped Authenticode signature is required: $Path ($($signature.Status))" }
    if ($CertificateThumbprint -and $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint) { throw "Unexpected signer: $Path" }
    $chain = [Security.Cryptography.X509Certificates.X509Chain]::new()
    try {
        $chain.ChainPolicy.RevocationMode = [Security.Cryptography.X509Certificates.X509RevocationMode]::Online
        # Authenticode validates signing time and timestamp validity; explicitly check chain too.
        if (-not $chain.Build($signature.SignerCertificate)) { throw "Certificate chain validation failed: $Path" }
    } finally { $chain.Dispose() }
}
function Set-ArtifactSignature {
    param([string]$Path, [string]$SignToolPath, [string]$CertificateThumbprint, [string]$TimestampServer)
    if (-not [IO.Path]::IsPathFullyQualified($SignToolPath) -or -not (Test-Path -LiteralPath $SignToolPath -PathType Leaf)) { throw 'Supply an absolute Windows SDK signtool.exe path.' }
    if ($CertificateThumbprint -notmatch '^[0-9A-Fa-f]{40}$') { throw 'CertificateThumbprint must be 40 hex characters (CurrentUser certificate store).' }
    if ($TimestampServer -notmatch '^https://[a-zA-Z0-9.-]+(?::[0-9]+)?(?:/[a-zA-Z0-9/_-]*)?$') { throw 'Supply an HTTPS timestamp service URL.' }
    & $SignToolPath sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampServer /td SHA256 $Path
    if ($LASTEXITCODE -ne 0) { throw "Signing failed: $Path" }
    Assert-ArtifactSignature $Path $CertificateThumbprint
}

function Save-SignedUninstallerEvidence {
    param([string]$Path, [string]$EvidenceDirectory, [string]$CertificateThumbprint)
    # With SignTool enabled, Inno signs uninst.e32.tmp (or e64), then deletes it.
    # Installed unins000.exe does not exist at compiler time.
    if ([IO.Path]::GetFileName($Path) -notmatch '^uninst\.e(32|64)\.tmp$') { return }
    Assert-ArtifactSignature $Path $CertificateThumbprint
    $destination = Join-Path $EvidenceDirectory 'signed-uninstaller.exe'
    Copy-Item -LiteralPath $Path -Destination $destination -Force
    Assert-ArtifactSignature $destination $CertificateThumbprint
    if ((Get-FileHash -LiteralPath $Path).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw 'Signed uninstaller evidence differs from the embedded binary.' }
}
