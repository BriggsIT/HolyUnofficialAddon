# Renders a title page (default unholy-title.html) with headless Edge or Chrome on a transparent background and saves
# it as a PNG trimmed to the lettering (plus -Pad pixels), next to the page: unholy-addon-title.png.
param([string]$Page = 'unholy-title.html', [string]$Out = 'unholy-addon-title.png', [int]$Width = 3400, [int]$Height = 900, [int]$Pad = 16, [string]$Browser)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$here = $PSScriptRoot
$html = if ([IO.Path]::IsPathRooted($Page)) { $Page } else { Join-Path $here $Page }
$dest = if ([IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $here $Out }
if (-not $Browser) {
	$Browser = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Google\Chrome\Application\chrome.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Browser) { throw 'Edge or Chrome is needed.' }
$tmp = Join-Path $env:TEMP ('title-' + [guid]::NewGuid())
New-Item -ItemType Directory $tmp | Out-Null
try {
	$shot = Join-Path $tmp 'shot.png'
	$url = 'file:///' + ($html -replace '\\', '/')
	$args = @('--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--force-device-scale-factor=1', "--user-data-dir=$tmp\profile", '--default-background-color=00000000', "--window-size=$Width,$Height", "--screenshot=$shot", $url)
	Start-Process -FilePath $Browser -ArgumentList $args -Wait -WindowStyle Hidden
	if (-not (Test-Path $shot)) { throw 'The browser did not save a screenshot.' }
	$bmp = [System.Drawing.Bitmap]::FromFile($shot)
	# The lettering's bounds: any pixel that isn't fully transparent.
	$minX = $bmp.Width; $minY = $bmp.Height; $maxX = -1; $maxY = -1
	$rect = New-Object System.Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height
	$data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$px = New-Object int[] ($bmp.Width * $bmp.Height)
	[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $px, 0, $px.Length)
	$bmp.UnlockBits($data)
	for ($y = 0; $y -lt $bmp.Height; $y++) {
		$row = $y * $bmp.Width
		for ($x = 0; $x -lt $bmp.Width; $x++) {
			if ((($px[$row + $x] -shr 24) -band 255) -gt 6) {
				if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
				if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
			}
		}
	}
	if ($maxX -lt 0) { throw 'Nothing was drawn (check the page).' }
	$x0 = [Math]::Max(0, $minX - $Pad); $y0 = [Math]::Max(0, $minY - $Pad)
	$x1 = [Math]::Min($bmp.Width - 1, $maxX + $Pad); $y1 = [Math]::Min($bmp.Height - 1, $maxY + $Pad)
	$crop = $bmp.Clone((New-Object System.Drawing.Rectangle $x0, $y0, ($x1 - $x0 + 1), ($y1 - $y0 + 1)), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$bmp.Dispose()
	$crop.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
	"Saved $dest ($($crop.Width)x$($crop.Height))"
	$crop.Dispose()
}
finally { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
