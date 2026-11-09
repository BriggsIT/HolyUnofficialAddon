# Renders logos.html with headless Edge (or Chrome), cuts the sheet into one 256x256 PNG per logo (png\<id>.png)
# and writes logos.json: the file and company name of each logo, in the order the game numbers them.
# Run it again after adding a logo to logos.html. -Preview <file.png> also saves a sheet showing each logo at
# 128 and 64 pixels (roughly how big the game shows them) on alternating dark and light backgrounds.
# Other sheets in the same format: -Page ..\icons\update-icons.html -Size 128 -NoJson (PNGs go to png\ next to the page).
param([string]$Browser, [string]$Preview, [string]$Page = 'logos.html', [int]$Size = 256, [switch]$NoJson)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('LogoBleed' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Gives fully transparent pixels the colour of their visible neighbours (alpha stays 0), so the smaller copies the
// game makes of the logo (mipmaps) don't get dark fringes from averaging in transparent black.
public static class LogoBleed
{
    public static void Run(Bitmap bmp, int passes)
    {
        Rectangle r = new Rectangle(0, 0, bmp.Width, bmp.Height);
        BitmapData d = bmp.LockBits(r, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int w = bmp.Width, h = bmp.Height, n = w * h;
        int[] px = new int[n];
        Marshal.Copy(d.Scan0, px, 0, n);
        bool[] done = new bool[n];
        for (int i = 0; i < n; i++) done[i] = ((px[i] >> 24) & 255) > 0;
        for (int pass = 0; pass < passes; pass++)
        {
            bool[] next = (bool[])done.Clone();
            int[] outp = (int[])px.Clone();
            bool any = false;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (done[i]) continue;
                int rs = 0, gs = 0, bs = 0, c = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                    int j = yy * w + xx;
                    if (!done[j]) continue;
                    rs += (px[j] >> 16) & 255; gs += (px[j] >> 8) & 255; bs += px[j] & 255; c++;
                }
                if (c > 0) { outp[i] = ((rs / c) << 16) | ((gs / c) << 8) | (bs / c); next[i] = true; any = true; }
            }
            px = outp; done = next;
            if (!any) break;
        }
        Marshal.Copy(px, 0, d.Scan0, n);
        bmp.UnlockBits(d);
    }
}
'@
}

$html = if ([IO.Path]::IsPathRooted($Page)) { $Page } else { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Page)) }
$here = Split-Path $html
$pngDir = Join-Path $here 'png'
$size = $Size
if (-not $Browser) {
    $Browser = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
                 "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
                 "$env:ProgramFiles\Google\Chrome\Application\chrome.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Browser) { throw 'Edge or Chrome is needed to render the logos.' }

$tmp = Join-Path $env:TEMP ('logos-' + [guid]::NewGuid())
New-Item -ItemType Directory $tmp | Out-Null
$url = 'file:///' + ($html -replace '\\', '/')
$common = @('--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--force-device-scale-factor=1', "--user-data-dir=$tmp\profile")

try {
    # Names and order come from the page itself.
    $dom = & $Browser @common --dump-dom $url | Out-String
    $meta = [regex]::Match($dom, '<pre id="meta">(.*?)</pre>', 'Singleline').Groups[1].Value
    $logos = [System.Net.WebUtility]::HtmlDecode($meta) | ConvertFrom-Json
    if (-not $logos) { throw 'No logos found in logos.html (check it for script errors).' }

    $cols = 8
    $rows = [math]::Ceiling($logos.Count / $cols)
    $sheet = Join-Path $tmp 'sheet.png'
    $args = $common + @('--default-background-color=00000000', "--window-size=$($cols * $size),$($rows * $size + $size)", "--screenshot=$sheet", $url)
    Start-Process -FilePath $Browser -ArgumentList $args -Wait -WindowStyle Hidden
    if (-not (Test-Path $sheet)) { throw 'The browser did not save a screenshot.' }

    $bmp = [System.Drawing.Bitmap]::FromFile($sheet)
    if ($bmp.Width -lt $cols * $size -or $bmp.Height -lt $rows * $size) { throw "Screenshot is $($bmp.Width)x$($bmp.Height), too small." }
    New-Item -ItemType Directory -Force $pngDir | Out-Null
    $files = @()
    for ($k = 0; $k -lt $logos.Count; $k++) {
        $rect = New-Object System.Drawing.Rectangle (($k % $cols) * $size), ([math]::Floor($k / $cols) * $size), $size, $size
        $cell = $bmp.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        [LogoBleed]::Run($cell, 12)
        $file = "$($logos[$k].id).png"
        $cell.Save((Join-Path $pngDir $file), [System.Drawing.Imaging.ImageFormat]::Png)
        $cell.Dispose()
        $files += $file
    }
    $bmp.Dispose()

    # logos.json, written by hand so names keep their & and the file stays readable.
    $lines = for ($k = 0; $k -lt $logos.Count; $k++) {
        $name = $logos[$k].name.Replace('\', '\\').Replace('"', '\"')
        "    { ""file"": ""$($files[$k])"", ""name"": ""$name"" }"
    }
    if (-not $NoJson) {
        $json = "{`r`n  ""note"": ""Company logos from logos.html. The game numbers them from 1000 in this order: only add new ones at the end."",`r`n  ""logos"": [`r`n" + ($lines -join ",`r`n") + "`r`n  ]`r`n}`r`n"
        [IO.File]::WriteAllText((Join-Path $here 'logos.json'), $json, (New-Object Text.UTF8Encoding($false)))
    }
    Write-Host "Rendered $($logos.Count) images into $pngDir"

    if ($Preview) {
        $pc = 4; $cw = 128 + 64 + 30; $ch = 160
        $prow = [math]::Ceiling($logos.Count / $pc)
        $out = New-Object System.Drawing.Bitmap ($pc * $cw), ($prow * $ch)
        $g = [System.Drawing.Graphics]::FromImage($out)
        $g.InterpolationMode = 'HighQualityBicubic'
        $g.PixelOffsetMode = 'HighQuality'
        $g.TextRenderingHint = 'AntiAliasGridFit'
        $font = New-Object System.Drawing.Font 'Segoe UI', 9
        $fmt = New-Object System.Drawing.StringFormat
        $fmt.Alignment = 'Center'
        for ($k = 0; $k -lt $logos.Count; $k++) {
            $x = ($k % $pc) * $cw; $y = [math]::Floor($k / $pc) * $ch
            $dark = (($k % $pc) + [math]::Floor($k / $pc)) % 2 -eq 0
            $bg = if ($dark) { [System.Drawing.Color]::FromArgb(43, 52, 69) } else { [System.Drawing.Color]::FromArgb(233, 237, 242) }
            $fg = if ($dark) { [System.Drawing.Brushes]::WhiteSmoke } else { [System.Drawing.Brushes]::Black }
            $g.FillRectangle((New-Object System.Drawing.SolidBrush $bg), $x, $y, $cw, $ch)
            $img = [System.Drawing.Image]::FromFile((Join-Path $pngDir $files[$k]))
            $g.DrawImage($img, $x + 10, $y + 6, 128, 128)
            $g.DrawImage($img, $x + 148, $y + 38, 64, 64)
            $img.Dispose()
            $g.DrawString($logos[$k].name, $font, $fg, (New-Object System.Drawing.RectangleF $x, ($y + 137), $cw, 22), $fmt)
        }
        $g.Dispose()
        $out.Save($Preview, [System.Drawing.Imaging.ImageFormat]::Png)
        $out.Dispose()
        Write-Host "Preview saved to $Preview"
    }
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}
