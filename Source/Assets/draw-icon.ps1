# Draws the Editor's bottom bar icon (a form with sliders and a pencil) in the game's icon style.
# Usage: powershell -File draw-icon.ps1 <output.png> [size]
param([string]$Out = "$PSScriptRoot\icon.png", [int]$Size = 128)

Add-Type -AssemblyName System.Drawing

$S = 512
$bmp = New-Object System.Drawing.Bitmap $S, $S, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

function C([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
$ink = C "#1B2433"
$outline = 34

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

# --- The form card ---
$card = Rounded 44 40 330 400 40
Outline $card $outline
Fill $card "#F5F7FA"
$g.SetClip($card)
$header = New-Object System.Drawing.Drawing2D.GraphicsPath
$header.AddRectangle((New-Object System.Drawing.RectangleF 30, 30, 360, 100))
Fill $header "#3D8BE0"
$shine = New-Object System.Drawing.Drawing2D.GraphicsPath
$shine.AddRectangle((New-Object System.Drawing.RectangleF 30, 30, 360, 38))
Fill $shine "#6BAAF0"
$g.ResetClip()
$sep = New-Object System.Drawing.Pen $ink, 14
$g.DrawLine($sep, 44, 130, 374, 130)
$sep.Dispose()

# --- Three sliders ---
$rows = @(
    @{ Y = 200; Knob = 268; Color = "#3DBA4E" },
    @{ Y = 280; Knob = 160; Color = "#F39C1F" },
    @{ Y = 360; Knob = 222; Color = "#E2483D" }
)
foreach ($r in $rows) {
    $track = Rounded 88 ($r.Y - 11) 240 22 11
    Outline $track 14
    Fill $track "#CDD5DE"
    $filled = Rounded 88 ($r.Y - 11) ($r.Knob - 88) 22 11
    Fill $filled $r.Color
    $knob = New-Object System.Drawing.Drawing2D.GraphicsPath
    $knob.AddEllipse([float]($r.Knob - 24), [float]($r.Y - 24), 48, 48)
    Outline $knob 16
    Fill $knob "#FFFFFF"
}

# --- The pencil, drawn along +x then turned 45 degrees up-right from its tip ---
$state = $g.Save()
$g.TranslateTransform(196, 486)
$g.RotateTransform(-45)
$hw = 40
$body = New-Object System.Drawing.Drawing2D.GraphicsPath
$body.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF 0, 0),
    (New-Object System.Drawing.PointF 78, (-$hw)),
    (New-Object System.Drawing.PointF 360, (-$hw)),
    (New-Object System.Drawing.PointF 360, $hw),
    (New-Object System.Drawing.PointF 78, $hw)))
$eraser = Rounded 340 (-$hw) 64 ($hw * 2) 16
$whole = New-Object System.Drawing.Drawing2D.GraphicsPath
$whole.AddPath($body, $false)
$whole.AddPath($eraser, $false)
Outline $whole ($outline + 6)
Fill $eraser "#F27BA5"
$eraserShine = Rounded 352 (-$hw + 8) 40 14 7
Fill $eraserShine "#F9AECB"
$ferrule = New-Object System.Drawing.Drawing2D.GraphicsPath
$ferrule.AddRectangle((New-Object System.Drawing.RectangleF 312, (-$hw), 40, ($hw * 2)))
Fill $ferrule "#C5CCD4"
$fl = New-Object System.Drawing.Pen (C "#8E98A4"), 6
$g.DrawLine($fl, 325, (-$hw + 4), 325, ($hw - 4))
$g.DrawLine($fl, 339, (-$hw + 4), 339, ($hw - 4))
$fl.Dispose()
# Three facets of the yellow body
$facets = @(@{ A = -$hw; B = -13; Color = "#FFE066" }, @{ A = -13; B = 13; Color = "#FFC21A" }, @{ A = 13; B = $hw; Color = "#E99A0C" })
foreach ($f in $facets) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddRectangle((New-Object System.Drawing.RectangleF 78, $f.A, 234, ($f.B - $f.A)))
    Fill $p $f.Color
}
# Sharpened wood and graphite tip
$wood = New-Object System.Drawing.Drawing2D.GraphicsPath
$wood.AddPolygon([System.Drawing.PointF[]]@((New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 78, (-$hw)), (New-Object System.Drawing.PointF 78, $hw)))
Fill $wood "#F3CF9E"
$lead = New-Object System.Drawing.Drawing2D.GraphicsPath
$lead.AddPolygon([System.Drawing.PointF[]]@((New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 28, -14), (New-Object System.Drawing.PointF 28, 14)))
Fill $lead "#2A2F38"
$edge = New-Object System.Drawing.Pen $ink, 8
$g.DrawLine($edge, 78, (-$hw), 78, $hw)
$g.DrawLine($edge, 312, (-$hw), 312, $hw)
$g.DrawLine($edge, 352, (-$hw), 352, $hw)
$edge.Dispose()
$g.Restore($state)

$g.Dispose()

# Downscale with high quality filtering
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
