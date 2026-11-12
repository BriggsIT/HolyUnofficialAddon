# Renders one HTML page to a PNG of the given size with headless Edge (or Chrome).
# Used for the awards ceremony backdrop: .\render-page.ps1 -Page awards-art.html -Width 1800 -Height 1290 -Out ..\icons\png\awards-bg.png
param([Parameter(Mandatory)] [string]$Page, [int]$Width = 1800, [int]$Height = 1290, [Parameter(Mandatory)] [string]$Out, [string]$Browser, [string]$Hash)

$ErrorActionPreference = 'Stop'
if (-not $Browser) {
    $Browser = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
                 "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
                 "$env:ProgramFiles\Google\Chrome\Application\chrome.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Browser) { throw "No Edge or Chrome found" }
$pagePath = (Resolve-Path (Join-Path $PSScriptRoot $Page)).Path
$outPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Out))
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("render-page-" + [guid]::NewGuid())
New-Item -ItemType Directory -Force $tmp | Out-Null
try {
    $url = ([Uri]$pagePath).AbsoluteUri
    if ($Hash) { $url += "#" + $Hash }
    $shot = Join-Path $tmp "page.png"
    $args = @('--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--force-device-scale-factor=1',
              "--user-data-dir=$tmp\profile", "--window-size=$Width,$Height", "--screenshot=$shot", $url)
    Start-Process -FilePath $Browser -ArgumentList $args -Wait -WindowStyle Hidden
    if (-not (Test-Path $shot)) { throw "The browser didn't write a screenshot" }
    Copy-Item $shot $outPath -Force
    Write-Output ("Rendered " + $outPath + " (" + (Get-Item $outPath).Length + " bytes)")
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}
