# Generates the LenovoController tray icons.
#
# The application icon is the letter "L" set in MI Lan Pro VF SemBd. Tray icons mirror the
# active power mode with Segoe Fluent Icons glyphs (Leaf, PC, LightningBolt) in white and black.
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\source\Resources')
)

Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64
$superSampling = 8

function New-GlyphBitmap {
    param(
        [int] $Size,
        [System.Drawing.Color] $Color,
        [string] $FontName,
        [string] $Glyph,
        [double] $TargetRatio
    )

    $scale = $superSampling
    $canvas = $Size * $scale
    $bmp = New-Object System.Drawing.Bitmap($canvas, $canvas, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $family = New-Object System.Drawing.FontFamily($FontName)
    $format = [System.Drawing.StringFormat]::GenericTypographic.Clone()
    $format.Alignment = [System.Drawing.StringAlignment]::Near
    $format.LineAlignment = [System.Drawing.StringAlignment]::Near

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddString($Glyph, $family, [int][System.Drawing.FontStyle]::Regular, [single]($canvas * 0.8), (New-Object System.Drawing.PointF(0, 0)), $format)

    $bounds = $path.GetBounds()
    if ($bounds.Width -le 0 -or $bounds.Height -le 0) {
        throw "Could not measure '$Glyph'. Is '$FontName' installed?"
    }

    # Normalise the glyph's visible bounds, then centre it on the canvas.
    $target = $canvas * $TargetRatio
    $glyphScale = $target / [Math]::Max($bounds.Width, $bounds.Height)

    $matrix = New-Object System.Drawing.Drawing2D.Matrix
    $matrix.Scale($glyphScale, $glyphScale)
    $path.Transform($matrix)

    $bounds = $path.GetBounds()
    $offsetX = ($canvas - $bounds.Width) / 2 - $bounds.X
    $offsetY = ($canvas - $bounds.Height) / 2 - $bounds.Y
    $matrix.Reset()
    $matrix.Translate([single]$offsetX, [single]$offsetY)
    $path.Transform($matrix)

    $brush = New-Object System.Drawing.SolidBrush($Color)
    $g.FillPath($brush, $path)
    $g.Dispose()
    $brush.Dispose()
    $path.Dispose()
    $family.Dispose()

    $small = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $smallGraphics = [System.Drawing.Graphics]::FromImage($small)
    $smallGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $smallGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $smallGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $smallGraphics.DrawImage($bmp, (New-Object System.Drawing.Rectangle(0, 0, $Size, $Size)))
    $smallGraphics.Dispose()
    $bmp.Dispose()

    return $small
}

function Get-IconFrame {
    param([System.Drawing.Bitmap] $Bitmap, [int] $Size)

    # 32bpp DIB frame (compatible with every Windows icon consumer).
    $rect = New-Object System.Drawing.Rectangle(0, 0, $Size, $Size)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($data.Stride * $Size)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $Bitmap.UnlockBits($data)

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)
    $writer.Write([int]40)                    # biSize
    $writer.Write([int]$Size)                 # biWidth
    $writer.Write([int]($Size * 2))           # biHeight (XOR + AND mask)
    $writer.Write([int16]1)                   # biPlanes
    $writer.Write([int16]32)                  # biBitCount
    $writer.Write([int]0)                     # biCompression
    $writer.Write([int]0)                     # biSizeImage
    $writer.Write([int]0)                     # biXPelsPerMeter
    $writer.Write([int]0)                     # biYPelsPerMeter
    $writer.Write([int]0)                     # biClrUsed
    $writer.Write([int]0)                     # biClrImportant

    # DIBs are stored bottom-up.
    for ($y = $Size - 1; $y -ge 0; $y--) {
        $writer.Write($pixels, $y * $data.Stride, $Size * 4)
    }

    # AND mask, padded to 32-bit rows.
    $maskStride = [int]([Math]::Ceiling($Size / 32.0) * 4)
    $mask = New-Object byte[] ($maskStride * $Size)
    $writer.Write($mask)

    $writer.Flush()
    $bytes = $stream.ToArray()
    $writer.Dispose()
    return ,$bytes
}

function Write-Icon {
    param(
        [string] $Path,
        [System.Drawing.Color] $Color,
        [string] $FontName,
        [string] $Glyph,
        [double] $TargetRatio
    )

    $frames = @()
    foreach ($size in $sizes) {
        $bitmap = New-GlyphBitmap -Size $size -Color $Color -FontName $FontName -Glyph $Glyph -TargetRatio $TargetRatio
        $frames += ,(Get-IconFrame -Bitmap $bitmap -Size $size)
        $bitmap.Dispose()
    }

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)
    $writer.Write([int16]0)                   # reserved
    $writer.Write([int16]1)                   # type: icon
    $writer.Write([int16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $size = $sizes[$i]
        $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
        $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
        $writer.Write([byte]0)                # colours
        $writer.Write([byte]0)                # reserved
        $writer.Write([int16]1)               # planes
        $writer.Write([int16]32)              # bpp
        $writer.Write([int]$frames[$i].Length)
        $writer.Write([int]$offset)
        $offset += $frames[$i].Length
    }

    foreach ($frame in $frames) {
        $writer.Write($frame)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($Path, $stream.ToArray())
    $writer.Dispose()

    Write-Host "Wrote $Path"
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

$white = [System.Drawing.Color]::White
$black = [System.Drawing.Color]::Black
$appFont = 'MI Lan Pro VF SemBd'
$iconFont = 'Segoe Fluent Icons'

# Application icon and generic window icons.
Write-Icon -Path (Join-Path $OutputDirectory 'app.ico') -Color $black -FontName $appFont -Glyph 'L' -TargetRatio 0.58
Write-Icon -Path (Join-Path $OutputDirectory 'tray-white.ico') -Color $white -FontName $appFont -Glyph 'L' -TargetRatio 0.58
Write-Icon -Path (Join-Path $OutputDirectory 'tray-black.ico') -Color $black -FontName $appFont -Glyph 'L' -TargetRatio 0.58

# Power mode icons: E8BE Leaf, E7F8 PC1, E945 LightningBolt.
$modes = @(
    @{ Name = 'quiet'; Glyph = [string][char]0xE8BE; Ratio = 1 },
    @{ Name = 'balanced'; Glyph = [string][char]0xE7F8; Ratio = 1 },
    @{ Name = 'performance'; Glyph = [string][char]0xE945; Ratio = 1 }
)

foreach ($mode in $modes) {
    foreach ($color in @(@{ Name = 'white'; Value = $white }, @{ Name = 'black'; Value = $black })) {
        Write-Icon -Path (Join-Path $OutputDirectory "tray-$($mode.Name)-$($color.Name).ico") `
                   -Color $color.Value -FontName $iconFont -Glyph $mode.Glyph -TargetRatio $mode.Ratio
    }
}
