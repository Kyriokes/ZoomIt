# Draws the Summit icon (a mountain peak) and writes it as a multi-size .ico.
# Usage: powershell -File tools\make-icon.ps1
param([string]$Out = (Join-Path $PSScriptRoot '..\src\Summit\Summit.ico'))

Add-Type -AssemblyName System.Drawing

function New-Points([double]$s, [double[]]$xy) {
    $pts = New-Object 'System.Drawing.PointF[]' ($xy.Length / 2)
    for ($i = 0; $i -lt $pts.Length; $i++) {
        $pts[$i] = New-Object System.Drawing.PointF ([single]($xy[2 * $i] * $s)), ([single]($xy[2 * $i + 1] * $s))
    }
    return ,$pts
}

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Rounded dark tile.
    $r = [single]($size * 0.22); $d = 2 * $r; $e = [single]$size
    $tile = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tile.AddArc(0, 0, $d, $d, 180, 90)
    $tile.AddArc($e - $d, 0, $d, $d, 270, 90)
    $tile.AddArc($e - $d, $e - $d, $d, $d, 0, 90)
    $tile.AddArc(0, $e - $d, $d, $d, 90, 90)
    $tile.CloseFigure()
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.RectangleF 0, 0, $e, $e),
        ([System.Drawing.Color]::FromArgb(255, 34, 44, 68)), ([System.Drawing.Color]::FromArgb(255, 13, 18, 31)), 90
    $g.FillPath($bg, $tile)

    # Far peak, near peak, snow cap.
    $far = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 52, 86, 138))
    $near = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 64, 150, 255))
    $snow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $g.FillPolygon($far, (New-Points $size @(0.50, 0.80, 0.72, 0.42, 0.92, 0.80)))
    $g.FillPolygon($near, (New-Points $size @(0.08, 0.80, 0.42, 0.18, 0.76, 0.80)))
    $g.FillPolygon($snow, (New-Points $size @(0.42, 0.18, 0.545, 0.41, 0.475, 0.355, 0.42, 0.44, 0.36, 0.36, 0.295, 0.41)))

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    if ($size -ge 256) {
        # Large entries are stored as PNG.
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        # Small entries are stored as classic 32-bit bitmaps, which every icon reader understands:
        # header, pixels bottom-up, then an (empty) 1-bit transparency mask.
        $w = New-Object System.IO.BinaryWriter $ms
        $w.Write([uint32]40); $w.Write([int32]$size); $w.Write([int32](2 * $size))
        $w.Write([uint16]1); $w.Write([uint16]32)
        $w.Write([uint32]0); $w.Write([uint32]0); $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
        $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
        $data = $bmp.LockBits($rect, 'ReadOnly', [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $row = New-Object byte[] (4 * $size)
        for ($y = $size - 1; $y -ge 0; $y--) {
            [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]::Add($data.Scan0, $y * $data.Stride), $row, 0, $row.Length)
            $w.Write($row)
        }
        $bmp.UnlockBits($data)
        $maskRow = [int]([math]::Ceiling($size / 32.0)) * 4
        $w.Write((New-Object byte[] ($maskRow * $size)))
        $w.Flush()
    }
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 48, 64, 256
$images = $sizes | ForEach-Object { ,(New-IconPng $_) }

$file = [System.IO.File]::Create([System.IO.Path]::GetFullPath($Out))
$w = New-Object System.IO.BinaryWriter $file
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Dispose()
"Wrote $([System.IO.Path]::GetFullPath($Out))"
