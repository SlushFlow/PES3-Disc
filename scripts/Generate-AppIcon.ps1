# Regenerates assets/PES3-Disc.ico from assets/PES3-Disc.png (multi-size for Windows).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$pngPath = Join-Path $root 'assets\PES3-Disc.png'
$icoPath = Join-Path $root 'assets\PES3-Disc.ico'

if (-not (Test-Path -LiteralPath $pngPath)) {
    throw "Missing source PNG: $pngPath"
}

Add-Type -AssemblyName System.Drawing

function Save-IconFromPng {
    param(
        [string]$InputPng,
        [string]$OutputIco,
        [int[]]$Sizes = @(16, 32, 48, 256)
    )
    $source = [System.Drawing.Bitmap]::FromFile($InputPng)
    try {
        $stream = New-Object System.IO.MemoryStream
        $writer = New-Object System.IO.BinaryWriter $stream
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$Sizes.Count)
        $offset = 6 + (16 * $Sizes.Count)
        $pngChunks = @()
        foreach ($size in $Sizes) {
            $scaled = New-Object System.Drawing.Bitmap $size, $size
            try {
                $graphics = [System.Drawing.Graphics]::FromImage($scaled)
                try {
                    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                    $graphics.DrawImage($source, 0, 0, $size, $size)
                }
                finally { $graphics.Dispose() }
                $pngStream = New-Object System.IO.MemoryStream
                try {
                    $scaled.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
                    $pngChunks += ,@($size, $pngStream.ToArray())
                }
                finally { $pngStream.Dispose() }
            }
            finally { $scaled.Dispose() }
        }
        foreach ($chunk in $pngChunks) {
            $size = $chunk[0]
            $data = $chunk[1]
            $writer.Write([byte][Math]::Min($size, 255))
            $writer.Write([byte][Math]::Min($size, 255))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$data.Length)
            $writer.Write([uint32]$offset)
            $offset += $data.Length
        }
        $writer.Flush()
        foreach ($chunk in $pngChunks) {
            $stream.Write($chunk[1], 0, $chunk[1].Length)
        }
        [IO.File]::WriteAllBytes($OutputIco, $stream.ToArray())
    }
    finally {
        $source.Dispose()
    }
}

Save-IconFromPng -InputPng $pngPath -OutputIco $icoPath
Write-Host "Wrote $icoPath"
