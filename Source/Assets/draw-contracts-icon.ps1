# Draws the Contracts bottom bar icon (a signed contract and a gold coin) in the game's icon style.
# Usage: powershell -File draw-contracts-icon.ps1 <output.png> [size]
param([string]$Out = "$PSScriptRoot\contracts.png", [int]$Size = 128)

Add-Type -AssemblyName System.Drawing

$S = 512
$bmp = New-Object System.Drawing.Bitmap $S, $S, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

function C([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
$ink = C "#1B2433"

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

function Outline($path, [float]$width) {
    $pen = New-Object System.Drawing.Pen $ink, $width
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $path)
    $pen.Dispose()
}

function Fill($path, [string]$hex) {
    $b = New-Object System.Drawing.SolidBrush (C $hex)
    $g.FillPath($b, $path)
    $b.Dispose()
}

# --- The contract ---
$paper = Rounded 58 34 300 420 30
Outline $paper 34
Fill $paper "#F7F4EC"
$fold = New-Object System.Drawing.Drawing2D.GraphicsPath
$fold.AddRectangle((New-Object System.Drawing.RectangleF 58, 34, 300, 70))
$g.SetClip($paper)
Fill $fold "#3D8BE0"
$g.ResetClip()
$sep = New-Object System.Drawing.Pen $ink, 12
$g.DrawLine($sep, 58, 104, 358, 104)
$sep.Dispose()
$line = New-Object System.Drawing.Pen (C "#B9C2CC"), 18
$line.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$line.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
foreach ($y in 160, 215, 270) { $g.DrawLine($line, 108, $y, 308, $y) }
$g.DrawLine($line, 108, 325, 230, 325)
$line.Dispose()
# Signature squiggle
$sig = New-Object System.Drawing.Pen (C "#1F4FA8"), 12
$sig.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$sig.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$sig.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$g.DrawBezier($sig, 104, 400, 130, 350, 150, 430, 175, 385)
$g.DrawBezier($sig, 175, 385, 195, 350, 205, 420, 235, 395)
$sig.Dispose()

# --- The gold coin ---
$coin = New-Object System.Drawing.Drawing2D.GraphicsPath
$coin.AddEllipse(262, 262, 218, 218)
Outline $coin 34
Fill $coin "#F2B632"
$inner = New-Object System.Drawing.Drawing2D.GraphicsPath
$inner.AddEllipse(292, 292, 158, 158)
Fill $inner "#F8CF5A"
$ring = New-Object System.Drawing.Pen (C "#C98A12"), 8
$g.DrawEllipse($ring, 292, 292, 158, 158)
$ring.Dispose()
$dollar = New-Object System.Drawing.Drawing2D.GraphicsPath
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
$dollar.AddString('$', (New-Object System.Drawing.FontFamily "Arial Black"), 0, 150, (New-Object System.Drawing.PointF 371, 375), $fmt)
Fill $dollar "#8A5A00"

$g.Dispose()

$small = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g2 = [System.Drawing.Graphics]::FromImage($small)
$g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g2.Clear([System.Drawing.Color]::Transparent)
$g2.DrawImage($bmp, 0, 0, $Size, $Size)
$g2.Dispose()
$small.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$small.Dispose()
$bmp.Dispose()
Write-Output "Saved $Out ($Size x $Size)"
