function ConvertTo-PublicDependencyAudit {
    param([Parameter(Mandatory)]$Audit, [Parameter(Mandatory)][string]$ProjectRoot)
    $prefix = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    foreach ($project in $Audit.projects) {
        $path = [IO.Path]::GetFullPath($project.path)
        $project.path = if ($path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            [IO.Path]::GetRelativePath($ProjectRoot, $path).Replace('\', '/')
        } else { [IO.Path]::GetFileName($path) }
    }
    return $Audit
}

function Get-InstallerReportContext {
    param([string]$ProjectRoot, [string]$Version, [ValidateSet('Release', 'Internal')][string]$ReleaseKind = 'Release')
    $path = Join-Path $ProjectRoot ('artifacts\releases\' + $Version + '\BetterCM-' + $Version + '-Setup-x64.exe')
    if ($ReleaseKind -eq 'Release' -and -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw 'Release smoke gate requires the final installer in artifacts/releases/<version>. Use -ReleaseKind Internal explicitly for an internal-only smoke test.'
    }
    [pscustomobject]@{
        Kind = $ReleaseKind
        FinalSetupPath = $path
        FinalSetupSHA256 = if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-FileHash -LiteralPath $path).Hash } else { $null }
    }
}
