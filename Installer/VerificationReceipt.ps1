function Assert-VerifiedPublish([string]$PublishPath, [object]$Receipt, [string]$Version, [string]$SourceCommit, [bool]$RequireDesktop) {
    if (-not $Receipt.SourceClean -or $Receipt.SourceCommit -ne $SourceCommit -or $Receipt.Version -ne $Version -or ($RequireDesktop -and -not $Receipt.DesktopChecks)) { throw 'Verified publish receipt does not match this source/check scope.' }
    $root = [IO.Path]::GetFullPath($PublishPath)
    $actualFiles = @(Get-ChildItem -LiteralPath $root -Recurse -File)
    if ($actualFiles.Count -eq 0 -or $actualFiles.Count -ne $Receipt.Files.Count) { throw 'Verified payload file count changed.' }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $Receipt.Files) {
        $target = [IO.Path]::GetFullPath((Join-Path $root $file.Path))
        if (-not $target.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not $seen.Add($target) -or (Get-FileHash -LiteralPath $target).Hash -ne $file.SHA256) { throw 'Verified payload hash/path mismatch.' }
    }
}
