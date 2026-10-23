param([Parameter(Mandatory)][string]$Path, [string]$ProbeReport)
$ErrorActionPreference = 'Stop'
$resolved = [IO.Path]::GetFullPath($Path)
$stream = [IO.File]::OpenRead($resolved)
$pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
try {
    $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    foreach ($handle in $metadata.TypeDefinitions) {
        $definition = $metadata.GetTypeDefinition($handle)
        $name = $metadata.GetString($definition.Name)
        if ($name -match 'Checks$|^SelfTests$|^Regression(Fixtures|Check|Selection)$|^CheckIdentity$') { throw "QA type found in production: $name" }
    }
} finally { $pe.Dispose(); $stream.Dispose() }
$hex = [Convert]::ToHexString([IO.File]::ReadAllBytes($resolved))
foreach ($marker in @('--self-test', '--stress-test', '--input-check', '--cascade-input-check', '--performance-check', '--explorer-view-check', '--verb-probe', '--shell-test-host', '--context-test-host', '--inspect-menu', '--preview-', 'BETTERCM_QA_REPORT', 'BETTERCM_PERF', 'BETTERCM_STARTUP_NAME', 'test-log', 'slow-open')) {
    if ($hex.Contains([Convert]::ToHexString([Text.Encoding]::Unicode.GetBytes($marker)))) { throw "QA marker found in production: $marker" }
}
Write-Output 'Production binary contains no QA types, diagnostic switches or QA environment hooks.'
if ($ProbeReport) {
    $reportRoot = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ProbeReport))
    New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
    $sentinel = Join-Path $reportRoot ('qa-must-not-write-' + [Guid]::NewGuid().ToString('N') + '.json')
    $results = foreach ($arguments in @(@('--self-test', $sentinel), @('--verb-probe', $sentinel, 'probe'), @('--shell-test-host', 'unused-pipe'))) {
        $start = [Diagnostics.ProcessStartInfo]::new([IO.Path]::ChangeExtension($resolved, '.exe'))
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true
        $start.Environment['BETTERCM_DATA_DIR'] = Join-Path $reportRoot 'production-probe-data'
        $start.Environment['BETTERCM_QA_REPORT'] = $sentinel
        foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
        $process = [Diagnostics.Process]::Start($start)
        try {
            if (-not $process.WaitForExit(10000)) { $process.Kill(); throw 'Production QA rejection timed out.' }
            if ($process.ExitCode -ne 2 -or (Test-Path -LiteralPath $sentinel) -or (Test-Path -LiteralPath ($sentinel + '.tmp'))) { throw 'Production accepted a diagnostic entry point.' }
            [pscustomobject]@{ Switch = $arguments[0]; ExitCode = $process.ExitCode; ReportWritten = $false }
        } finally { $process.Dispose() }
    }
    [pscustomobject]@{ Passed = $true; Results = @($results) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ProbeReport -Encoding utf8
}
