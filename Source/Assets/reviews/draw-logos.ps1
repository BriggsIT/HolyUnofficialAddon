# Draws a masthead logo for every outlet in reviews.json, using each outlet's "logo" settings.
# Usage: powershell -File draw-logos.ps1 [outputFolder]
# Styles: box (filled panel), outline (framed panel), stripe (slanted band), plain (lettering only).
param([string]$OutDir = "$PSScriptRoot\logos")

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutDir | Out-Null
$json = Get-Content "$PSScriptRoot\reviews.json" -Raw -Encoding UTF8 | ConvertFrom-Json

$W = 400
$H = 125

function C([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }

function Rounded([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Lettering as a path, scaled to fill the box while keeping its proportions.
function Lettering($g, [string]$text, [string]$font, [bool]$bold, [bool]$italic, [System.Drawing.RectangleF]$box, $fill, $stroke, [float]$strokeWidth) {
    $style = [System.Drawing.FontStyle]::Regular
    if ($bold) { $style = $style -bor [System.Drawing.FontStyle]::Bold }
    if ($italic) { $style = $style -bor [System.Drawing.FontStyle]::Italic }
    $family = New-Object System.Drawing.FontFamily $font
    if (-not $family.IsStyleAvailable($style)) { $style = [System.Drawing.FontStyle]::Regular }
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddString($text, $family, [int]$style, 100, (New-Object System.Drawing.PointF 0, 0), $format)
    $b = $path.GetBounds()
    $scale = [Math]::Min($box.Width / $b.Width, $box.Height / $b.Height)
    $m = New-Object System.Drawing.Drawing2D.Matrix
    $m.Translate($box.X + $box.Width / 2, $box.Y + $box.Height / 2)
    $m.Scale($scale, $scale)
    $m.Translate(-($b.X + $b.Width / 2), -($b.Y + $b.Height / 2))
    $path.Transform($m)
    if ($stroke) {
        $pen = New-Object System.Drawing.Pen $stroke, $strokeWidth
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPath($pen, $path)
        $pen.Dispose()
    }
    $brush = New-Object System.Drawing.SolidBrush $fill
    $g.FillPath($brush, $path)
    $brush.Dispose()
    $path.Dispose()
}

foreach ($o in $json.outlets) {
    $l = $o.logo
    $bmp = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $fg = C $l.fg
    $bg = if ($l.bg) { C $l.bg } else { [System.Drawing.Color]::White }
    $hasSub = [bool]$l.sub
    $textTop = 14
    $textHeight = if ($hasSub) { $H - 52 } else { $H - 28 }
    $box = New-Object System.Drawing.RectangleF 24, $textTop, ($W - 48), $textHeight
    $stroke = $null
    switch ($l.style) {
        "box" {
            $panel = Rounded 3 3 ($W - 6) ($H - 6) 14
            $g.FillPath((New-Object System.Drawing.SolidBrush $bg), $panel)
        }
        "outline" {
            $panel = Rounded 5 5 ($W - 10) ($H - 10) 12
            $g.FillPath((New-Object System.Drawing.SolidBrush $bg), $panel)
            $pen = New-Object System.Drawing.Pen $fg, 8
            $g.DrawPath($pen, $panel)
            $pen.Dispose()
        }
        "stripe" {
            $band = New-Object System.Drawing.Drawing2D.GraphicsPath
            $band.AddPolygon([System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF 28, 4), (New-Object System.Drawing.PointF ($W - 2), 4),
                (New-Object System.Drawing.PointF ($W - 28), ($H - 4)), (New-Object System.Drawing.PointF 2, ($H - 4))))
            $g.FillPath((New-Object System.Drawing.SolidBrush $bg), $band)
            $box = New-Object System.Drawing.RectangleF 40, $textTop, ($W - 80), $textHeight
        }
        default {
            # plain lettering gets a white halo so it reads on any card colour
            $stroke = [System.Drawing.Color]::White
        }
    }
    Lettering $g ($l.text -replace '\\n', "`n") $l.font ([bool]$l.bold) ([bool]$l.italic) $box $fg $stroke 14
    if ($hasSub) {
        $subBox = New-Object System.Drawing.RectangleF 40, ($H - 38), ($W - 80), 24
        Lettering $g $l.sub "Verdana" $false $false $subBox $fg $stroke 6
    }
    $g.Dispose()
    $bmp.Save((Join-Path $OutDir ($o.id + ".png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
Write-Output ("Drew " + $json.outlets.Count + " logos into " + $OutDir)
