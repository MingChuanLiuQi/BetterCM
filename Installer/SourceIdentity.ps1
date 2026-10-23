function Get-SourceIdentity([string]$ProjectRoot) {
    $root = [IO.Path]::GetFullPath($ProjectRoot)
    $commit = & git -C $root rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit.' }
    $status = & git -C $root status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source status.' }
    $listing = & git -c core.quotepath=false -C $root ls-files --cached --others --exclude-standard -z
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source files.' }
    $paths = [string[]](($listing -join "`n").Split([char]0, [StringSplitOptions]::RemoveEmptyEntries))
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $files = @(foreach ($path in $paths) {
        $target = [IO.Path]::GetFullPath((Join-Path $root $path))
        if (-not $target.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Source path escapes project.' }
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { continue } # Tracked deletions are represented by the omitted path and dirty status.
        $item = Get-Item -LiteralPath $target -Force
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Source identity does not follow reparse-point files.' }
        [pscustomobject]@{ Path=$path.Replace('\', '/'); Bytes=$item.Length; SHA256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash }
    })
    if ($files.Count -eq 0) { throw 'Source identity requires files.' }
    $canonical = ($files | ForEach-Object { $_.Path + [char]0 + $_.Bytes.ToString([Globalization.CultureInfo]::InvariantCulture) + [char]0 + $_.SHA256 }) -join "`n"
    [pscustomobject]@{
        SourceCommit=$commit; SourceClean=-not [bool]$status
        SourceTreeSHA256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical)))
        Files=$files
    }
}

function Assert-SourceIdentity([object]$Expected, [object]$Actual) {
    if (-not $Expected.SourceTreeSHA256 -or $Expected.SourceTreeSHA256 -ne $Actual.SourceTreeSHA256 -or
        $Expected.SourceCommit -ne $Actual.SourceCommit -or $Expected.SourceClean -ne $Actual.SourceClean) {
        throw 'Source commit, status or file contents changed during verification.'
    }
}
