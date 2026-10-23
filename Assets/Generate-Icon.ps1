# Original BetterCM icon: a menu panel and pointer, drawn entirely from geometric primitives.
# Copyright (c) 2026 BetterCM contributors. SPDX-License-Identifier: MIT
param([string]$OutputPath = (Join-Path $PSScriptRoot 'BetterCM.ico'), [string]$PreviewPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$frames = foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $panel = [Drawing.Drawing2D.GraphicsPath]::new()
    $pointer = [Drawing.Drawing2D.GraphicsPath]::new()
    $dark = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#152538'))
    $mint = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#59DFBF'))
    $white = [Drawing.SolidBrush]::new([Drawing.Color]::White)
    $stream = [IO.MemoryStream]::new()
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.ScaleTransform($size / 256.0, $size / 256.0)
        foreach ($arc in @(@(12,12,48,48,180), @(184,12,48,48,270), @(184,184,48,48,0), @(12,184,48,48,90))) {
            $panel.AddArc($arc[0], $arc[1], $arc[2], $arc[3], $arc[4], 90)
        }
        $panel.CloseFigure()
        $graphics.FillPath($dark, $panel)
        $graphics.FillRectangle($mint, 48, 56, 140, 16)
        $graphics.FillRectangle($mint, 48, 104, 108, 16)
        $graphics.FillRectangle($mint, 48, 152, 70, 16)
        $pointer.AddPolygon([Drawing.PointF[]]@([Drawing.PointF]::new(154,137), [Drawing.PointF]::new(154,222), [Drawing.PointF]::new(175,202), [Drawing.PointF]::new(192,232), [Drawing.PointF]::new(210,222), [Drawing.PointF]::new(192,192), [Drawing.PointF]::new(220,184)))
        $graphics.FillPath($white, $pointer)
        $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        if ($PreviewPath -and $size -eq 256) { $bitmap.Save([IO.Path]::GetFullPath($PreviewPath), [Drawing.Imaging.ImageFormat]::Png) }
        [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
    } finally {
        $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
        $panel.Dispose(); $pointer.Dispose(); $dark.Dispose(); $mint.Dispose(); $white.Dispose()
    }
}
$output = [IO.File]::Create([IO.Path]::GetFullPath($OutputPath))
$writer = [IO.BinaryWriter]::new($output)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose(); $output.Dispose() }
