# Draws the app icon (a speed gauge) and saves it as app.ico (PNG-in-ICO, several sizes).
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot 'app.ico'

function New-IconPng([int]$s) {
    $bmp = New-Object Drawing.Bitmap($s, $s)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([Drawing.Color]::Transparent)
    $pad = [float]($s * 0.04)
    $rect = New-Object Drawing.RectangleF($pad, $pad, ($s - 2 * $pad), ($s - 2 * $pad))
    $bg = New-Object Drawing.Drawing2D.LinearGradientBrush($rect, [Drawing.Color]::FromArgb(37, 99, 235), [Drawing.Color]::FromArgb(14, 165, 233), 45)
    $g.FillEllipse($bg, $rect)

    $w = [float]($s * 0.11)
    $arcPad = [float]($s * 0.2)
    $arc = New-Object Drawing.RectangleF($arcPad, $arcPad, ($s - 2 * $arcPad), ($s - 2 * $arcPad))
    $pen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(110, 255, 255, 255), $w)
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawArc($pen, $arc, 150, 240)
    $pen2 = New-Object Drawing.Pen([Drawing.Color]::White, $w)
    $pen2.StartCap = 'Round'; $pen2.EndCap = 'Round'
    $g.DrawArc($pen2, $arc, 150, 170)

    $c = [float]($s / 2)
    $ang = (150 + 170) * [Math]::PI / 180
    $len = [float]($s * 0.27)
    $needle = New-Object Drawing.Pen([Drawing.Color]::White, [float]($s * 0.07))
    $needle.StartCap = 'Round'; $needle.EndCap = 'Round'
    $g.DrawLine($needle, $c, $c, [float]($c + $len * [Math]::Cos($ang)), [float]($c + $len * [Math]::Sin($ang)))
    $hub = [float]($s * 0.09)
    $g.FillEllipse([Drawing.Brushes]::White, ($c - $hub), ($c - $hub), (2 * $hub), (2 * $hub))
    $g.Dispose()

    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) { ,(New-IconPng $s) }

$fs = [IO.File]::Create($out)
$bw = New-Object IO.BinaryWriter($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $d = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()
Write-Host "Wrote $out"
