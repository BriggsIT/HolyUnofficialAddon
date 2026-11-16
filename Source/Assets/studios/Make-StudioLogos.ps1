# Makes the added studios' 256x256 logos (studios.json, Core/Studios/ExtraStudios.cs):
# - logos\<file>: a plain name tile for every studio (our own art; the public repo has these);
# - logos\real\<file>: the real logo downloaded from Wikipedia (logos-source\<slug>.png|jpg), fitted onto a transparent
#   square, where there is one. The game uses it when it's there. Real logos are the companies' own: like the landmark
#   screenshots they stay out of the public repo (Publish-Repo.ps1 without -Pictures).
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Add-Type -AssemblyName System.Drawing
$src = Join-Path $here "logos-source"
$out = Join-Path $here "logos"
$real = Join-Path $out "real"
New-Item -ItemType Directory -Force -Path $out, $real | Out-Null
$data = [IO.File]::ReadAllText((Join-Path $here "studios.json")) | ConvertFrom-Json
$size = 256
function New-Canvas { $b = New-Object System.Drawing.Bitmap $size, $size; $gr = [System.Drawing.Graphics]::FromImage($b); $gr.SmoothingMode = 'AntiAlias'; $gr.InterpolationMode = 'HighQualityBicubic'; $gr.PixelOffsetMode = 'HighQuality'; $gr.TextRenderingHint = 'AntiAliasGridFit'; $gr.Clear([System.Drawing.Color]::Transparent); return @($b, $gr) }
foreach ($s in $data.Studios) {
  $slug = [IO.Path]::GetFileNameWithoutExtension($s.Logo)
  $orig = Get-ChildItem $src -Filter "$slug.*" -File -ErrorAction SilentlyContinue | Select-Object -First 1
  $kind = "name tile"
  if ($orig) {
    $c = New-Canvas; $bmp = $c[0]; $g = $c[1]
    $img = [System.Drawing.Image]::FromFile($orig.FullName)
    $m = 10; $box = $size - 2 * $m
    $k = [Math]::Min($box / $img.Width, $box / $img.Height)
    $w = [int]($img.Width * $k); $h = [int]($img.Height * $k)
    $g.DrawImage($img, [int](($size - $w) / 2), [int](($size - $h) / 2), $w, $h)
    $img.Dispose(); $g.Dispose()
    $bmp.Save((Join-Path $real $s.Logo), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    $kind = "name tile + real logo"
  }
  $dest = Join-Path $out $s.Logo
  $c = New-Canvas; $bmp = $c[0]; $g = $c[1]
  if ($true) {
    # A name tile: rounded square in a colour from the name, the name in white.
    $hash = 0; foreach ($c in $s.Name.ToCharArray()) { $hash = ($hash * 31 + [int]$c) % 360 }
    $hsl = { param($hh, $ss, $ll) $c2 = (1 - [Math]::Abs(2 * $ll - 1)) * $ss; $x = $c2 * (1 - [Math]::Abs((($hh / 60) % 2) - 1)); $m2 = $ll - $c2 / 2
      $rgb = switch ([int][Math]::Floor($hh / 60)) { 0 { $c2, $x, 0 } 1 { $x, $c2, 0 } 2 { 0, $c2, $x } 3 { 0, $x, $c2 } 4 { $x, 0, $c2 } default { $c2, 0, $x } }
      [System.Drawing.Color]::FromArgb(255, [int](($rgb[0] + $m2) * 255), [int](($rgb[1] + $m2) * 255), [int](($rgb[2] + $m2) * 255)) }
    $top = & $hsl $hash 0.55 0.42; $bottom = & $hsl $hash 0.6 0.28
    $rect = New-Object System.Drawing.Rectangle 8, 8, 240, 240
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 36; $path.AddArc(8, 8, $r, $r, 180, 90); $path.AddArc(248 - $r, 8, $r, $r, 270, 90); $path.AddArc(248 - $r, 248 - $r, $r, $r, 0, 90); $path.AddArc(8, 248 - $r, $r, $r, 90, 90); $path.CloseFigure()
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $top, $bottom, 90
    $g.FillPath($brush, $path)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(90, 255, 255, 255)), 4
    $g.DrawPath($pen, $path)
    $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
    $text = $s.Name
    $fontSize = 44
    do {
      $font = New-Object System.Drawing.Font 'Segoe UI', $fontSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
      $measure = $g.MeasureString($text, $font, 210, $fmt)
      if ($measure.Height -le 200 -and $measure.Width -le 212) { break }
      $font.Dispose(); $fontSize -= 2
    } while ($fontSize -gt 18)
    $shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(110, 0, 0, 0))
    $g.DrawString($text, $font, $shadow, (New-Object System.Drawing.RectangleF 25, 27, 210, 210), $fmt)
    $g.DrawString($text, $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 23, 23, 210, 210), $fmt)
  }
  $g.Dispose()
  $bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  "{0,-24} {1}" -f $s.Logo, $kind
}
