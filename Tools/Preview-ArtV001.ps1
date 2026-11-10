#requires -Version 5.1
<#
.SYNOPSIS
Build review-only compositions from the exported v001 images.
.DESCRIPTION
Preserves source artwork and alpha. Only lays out full canvases, applies normal
source-over compositing, and draws labels/grids/pivots in the preview images.
#>
[CmdletBinding()]
param([string]$Root = (Join-Path (Split-Path -Parent $PSScriptRoot) 'EarlyRush_Art_v001'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$packageRoot = [System.IO.Path]::GetFullPath($Root)
$projectRoot = Split-Path -Parent $packageRoot
$config = Get-Content -LiteralPath (Join-Path $packageRoot 'metadata/export-config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$previewRoot = Join-Path $packageRoot 'previews'
[System.IO.Directory]::CreateDirectory($previewRoot) | Out-Null
$assets = @{}
$images = @{}
$fontTitle = [System.Drawing.Font]::new('Segoe UI', 30, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fontSubtitle = [System.Drawing.Font]::new('Segoe UI', 18, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fontLabel = [System.Drawing.Font]::new('Segoe UI', 20, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fontSmall = [System.Drawing.Font]::new('Segoe UI', 15, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$cream = [System.Drawing.ColorTranslator]::FromHtml('#f5efdf')
$paper = [System.Drawing.ColorTranslator]::FromHtml('#fffaf0')
$ink = [System.Drawing.ColorTranslator]::FromHtml('#244d4a')
$muted = [System.Drawing.ColorTranslator]::FromHtml('#647a70')
$accent = [System.Drawing.ColorTranslator]::FromHtml('#c45636')
$gridPen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#ded9c9'), 1)
$cardPen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#ded6c5'), 1)
$pivotPen = [System.Drawing.Pen]::new($accent, 2)
$whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)

function New-ReviewCanvas([int]$Width, [int]$Height, [System.Drawing.Color]$Background) {
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear($Background)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    return [pscustomobject]@{ Bitmap = $bitmap; Graphics = $graphics }
}
function Write-Label($G, [string]$Text, [single]$X, [single]$Y, $Font, [System.Drawing.Color]$Colour) {
    $brush = [System.Drawing.SolidBrush]::new($Colour)
    try { $G.DrawString($Text, $Font, $brush, $X, $Y) } finally { $brush.Dispose() }
}
function Fill-Rectangle($G, [System.Drawing.Color]$Colour, [single]$X, [single]$Y, [single]$W, [single]$H) {
    $brush = [System.Drawing.SolidBrush]::new($Colour)
    try { $G.FillRectangle($brush, $X, $Y, $W, $H) } finally { $brush.Dispose() }
}
function Draw-Fit($G, $Image, [single]$X, [single]$Y, [single]$W, [single]$H) {
    $factor = [Math]::Min($W / $Image.Width, $H / $Image.Height)
    $drawW = [single]($Image.Width * $factor)
    $drawH = [single]($Image.Height * $factor)
    $destination = [System.Drawing.RectangleF]::new(($X + ($W - $drawW) / 2), ($Y + ($H - $drawH) / 2), $drawW, $drawH)
    $G.DrawImage($Image, $destination, [System.Drawing.RectangleF]::new(0, 0, $Image.Width, $Image.Height), [System.Drawing.GraphicsUnit]::Pixel)
}
function Draw-Pivot($G, [single]$X, [single]$Y) {
    $G.DrawLine($pivotPen, ($X - 6), $Y, ($X + 6), $Y)
    $G.DrawLine($pivotPen, $X, ($Y - 6), $X, ($Y + 6))
}
function Draw-WorldAsset($G, [string]$Id, [single]$AnchorX, [single]$AnchorY) {
    $asset = $assets[$Id]
    $image = $images[$Id]
    $drawW = [single]([double]$asset.worldWidth * 128)
    $drawH = [single]($drawW * $image.Height / $image.Width)
    $drawX = [single]($AnchorX - [double]$asset.pivotNormalizedBottomLeft[0] * $drawW)
    $drawY = [single]($AnchorY - (1 - [double]$asset.pivotNormalizedBottomLeft[1]) * $drawH)
    $destination = [System.Drawing.RectangleF]::new($drawX, $drawY, $drawW, $drawH)
    $G.DrawImage($image, $destination, [System.Drawing.RectangleF]::new(0, 0, $image.Width, $image.Height), [System.Drawing.GraphicsUnit]::Pixel)
    Draw-Pivot $G $AnchorX $AnchorY
}
function Save-ReviewCanvas($Canvas, [string]$Name) {
    try {
        $Canvas.Graphics.Dispose()
        $Canvas.Bitmap.Save((Join-Path $previewRoot $Name), [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "Created previews/$Name"
    } finally { $Canvas.Bitmap.Dispose() }
}

try {
    foreach ($asset in $config.assets) {
        $path = Join-Path $packageRoot $asset.output
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Exported candidate missing: $path" }
        $assets[$asset.id] = $asset
        $images[$asset.id] = [System.Drawing.Bitmap]::new($path)
    }

    # 1. Complete-canvas contact sheet, four columns and sixteen candidates.
    $sheet = New-ReviewCanvas 1600 1840 $cream
    $g = $sheet.Graphics
    Write-Label $g 'EARLY RUSH / v001' 40 27 $fontTitle $ink
    Write-Label $g '16 generated candidates | full canvases | visual and in-game acceptance pending' 40 72 $fontSubtitle $muted
    $order = @('boy-down', 'boy-up', 'boy-left', 'boy-right', 'man-down', 'man-up', 'man-left', 'man-right', 'wheel', 'chair', 'cloth', 'leader-01', 'asphalt', 'windup-000', 'windup-001', 'windup-002')
    for ($index = 0; $index -lt $order.Count; $index++) {
        $id = $order[$index]
        $asset = $assets[$id]
        $x = [single](40 + ($index % 4) * 380)
        $y = [single](120 + [Math]::Floor($index / 4) * 420)
        Fill-Rectangle $g $paper $x $y 360 400
        $g.DrawRectangle($cardPen, $x, $y, 360, 400)
        Draw-Fit $g $images[$id] ($x + 20) ($y + 12) 320 320
        Write-Label $g $id ($x + 20) ($y + 338) $fontLabel $ink
        $details = '{0}  |  {1} x {2} px  |  {3} candidate' -f $asset.sourceId, $asset.canvasPx[0], $asset.canvasPx[1], $asset.integration
        Write-Label $g $details ($x + 20) ($y + 371) $fontSmall $muted
    }
    Write-Label $g 'A = existing resource key; B = new interface required. Labels belong to this review sheet only.' 40 1811 $fontSmall $muted
    Save-ReviewCanvas $sheet 'contact-sheet.png'

    # 2. Fixed pixel/world ratio with actual exported canvases and declared pivots.
    $scale = New-ReviewCanvas 1280 720 $cream
    $g = $scale.Graphics
    Write-Label $g 'EARLY RUSH / SCALE CALIBRATION' 30 21 $fontTitle $ink
    Write-Label $g '1 world unit = 128 px | full canvas width drives scale | candidate ground anchors' 30 65 $fontSubtitle $muted
    for ($x = 0; $x -le 1280; $x += 128) { $g.DrawLine($gridPen, $x, 112, $x, 650) }
    for ($y = 138; $y -le 650; $y += 128) { $g.DrawLine($gridPen, 0, $y, 1280, $y) }
    $topObjects = @(
        @{ Id = 'boy-down'; X = 150; Label = 'PLAYER / 1.05 u' },
        @{ Id = 'wheel'; X = 440; Label = 'WHEEL / 1.21 u' },
        @{ Id = 'chair'; X = 740; Label = 'CHAIR / 1.21 u' },
        @{ Id = 'cloth'; X = 1080; Label = 'CLOTH / 1.54 u' }
    )
    foreach ($entry in $topObjects) {
        Write-Label $g $entry.Label ($entry.X - 98) 112 $fontSmall $ink
        Draw-WorldAsset $g $entry.Id $entry.X 330
    }
    $legacyInfo = @(
        @{ File = 'props.png'; Index = 12; X = 360; Width = 2.25; Label = 'DESK / 2.25 u (provisional)' },
        @{ File = 'extras.png'; Index = 11; X = 930; Width = 2.4; Label = 'DOOR / 2.40 u (provisional)' }
    )
    foreach ($entry in $legacyInfo) {
        $legacyPath = Join-Path $projectRoot ('Assets/Resources/RushArt/' + $entry.File)
        $atlas = [System.Drawing.Bitmap]::new($legacyPath)
        try {
            $cellW = [single]($atlas.Width / 4.0)
            $cellH = [single]($atlas.Height / 4.0)
            $sourceRectangle = [System.Drawing.RectangleF]::new(($entry.Index % 4 * $cellW), ([Math]::Floor($entry.Index / 4) * $cellH), $cellW, $cellH)
            $drawW = [single]($entry.Width * 128)
            $drawH = [single]($drawW * $cellH / $cellW)
            $destination = [System.Drawing.RectangleF]::new(($entry.X - $drawW * 0.5), (600 - $drawH * 0.9), $drawW, $drawH)
            $g.DrawImage($atlas, $destination, $sourceRectangle, [System.Drawing.GraphicsUnit]::Pixel)
            Draw-Pivot $g $entry.X 600
            Write-Label $g 'LEGACY REFERENCE ONLY' ($entry.X - 137) 641 $fontSmall $accent
            Write-Label $g $entry.Label ($entry.X - 137) 662 $fontSmall $ink
        } finally { $atlas.Dispose() }
    }
    Draw-Pivot $g 34 704
    Write-Label $g 'Cross = configured pivot. Grid is a scale guide. Legacy widths are provisional references, not locked level dimensions.' 51 693 $fontSmall $muted
    Save-ReviewCanvas $scale 'scale-calibration-1280x720.png'

    # 3. Normal source-over compositing on three contrasting solid backgrounds.
    $alpha = New-ReviewCanvas 1280 1100 $cream
    $g = $alpha.Graphics
    Write-Label $g 'EARLY RUSH / ALPHA REVIEW' 30 22 $fontTitle $ink
    $alphaOrder = @('boy-down', 'wheel', 'chair', 'cloth', 'leader-01')
    for ($column = 0; $column -lt $alphaOrder.Count; $column++) {
        Write-Label $g $alphaOrder[$column] (125 + $column * 225) 79 $fontLabel $ink
    }
    $backgrounds = @(
        @{ Name = 'CREAM'; Colour = $cream; Text = $ink },
        @{ Name = 'TEAL'; Colour = [System.Drawing.ColorTranslator]::FromHtml('#123c3b'); Text = [System.Drawing.Color]::White },
        @{ Name = 'PURPLE'; Colour = [System.Drawing.ColorTranslator]::FromHtml('#b940dc'); Text = [System.Drawing.Color]::White }
    )
    for ($row = 0; $row -lt 3; $row++) {
        $y = [single](120 + $row * 305)
        $background = $backgrounds[$row]
        Fill-Rectangle $g $background.Colour 0 $y 1280 305
        Write-Label $g $background.Name 18 ($y + 137) $fontSmall $background.Text
        for ($column = 0; $column -lt $alphaOrder.Count; $column++) {
            Draw-Fit $g $images[$alphaOrder[$column]] (110 + $column * 225) ($y + 14) 215 275
        }
    }
    Write-Label $g 'Review-only source-over composite. No background removal, edge repainting, colour grading or alpha cleanup applied.' 30 1060 $fontSmall $muted
    Save-ReviewCanvas $alpha 'alpha-backgrounds.png'

    # 4. Exact, uninterrupted 2x2 floor repeat. No drawn lines conceal the seams.
    $floor = New-ReviewCanvas 1024 1024 $cream
    for ($row = 0; $row -lt 2; $row++) {
        for ($column = 0; $column -lt 2; $column++) {
            $floor.Graphics.DrawImageUnscaled($images['asphalt'], ($column * 512), ($row * 512))
        }
    }
    Save-ReviewCanvas $floor 'asphalt-2x2.png'

    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText((Join-Path $previewRoot 'README.md'), @'
# v001 review previews

- `contact-sheet.png`: all 16 candidates, complete exported canvases. A means an existing runtime key; B requires a new program interface. Neither means visual acceptance.
- `scale-calibration-1280x720.png`: 1 world unit = 128 px; exported PNGs use their complete canvas, configured world width and pivot. Crosses mark pivots, not collision boundaries. Approximate ground anchors still require in-engine review.
- The desk is `Assets/Resources/RushArt/props.png`, zero-based index 12 of a 4x4 sheet. The door is `extras.png`, index 11. Both use the original fractional source rectangles. Their 2.25 / 2.40 world-unit widths and `(0.5, 0.1)` reference pivots are provisional layout aids, not final map dimensions. These old images also use an older viewing angle and are explicitly labelled LEGACY REFERENCE ONLY.
- `alpha-backgrounds.png`: normal source-over compositing on cream, dark teal and bright purple. Artwork and alpha remain unchanged.
- `asphalt-2x2.png`: four exact copies of the 512px export, no padding or drawn seam lines. Inspect the centre axes for visible repetition; numeric border differences alone cannot establish seamlessness.
- `review.html`: local viewer with a background selector and a three-frame windup review. Its optional 6 fps loop is a review aid only, not game timing or an approved looping animation.

Regenerate with `pwsh -File Tools/Preview-ArtV001.ps1` after exporting the package. This script writes previews only.
'@, $utf8)
    [System.IO.File]::WriteAllText((Join-Path $previewRoot 'review.html'), @'
<!doctype html>
<html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Early Rush v001 review</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#f5efdf;color:#244d4a;font:16px/1.5 system-ui,sans-serif}main{max-width:1500px;margin:auto;padding:28px}h1{margin:0}header p{margin-top:6px}section{margin:28px 0}.board{display:block;width:100%;height:auto;border:1px solid #ded6c5;background:#fffaf0}.stage{display:flex;align-items:end;justify-content:center;min-height:420px;padding:20px;background:#f5efdf;border:1px solid #ded6c5}.stage img{width:256px;height:384px;object-fit:contain}.controls{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin:14px 0}button,select{font:inherit;padding:7px 12px;border:1px solid #8ba19a;border-radius:5px;background:#fffaf0;color:#244d4a}.small{font-size:14px;color:#647a70}figure{margin:0}figcaption{text-align:center}a{color:inherit}.two{display:grid;grid-template-columns:1fr 1fr;gap:22px}@media(max-width:800px){.two{grid-template-columns:1fr}}
</style><main>
<header><h1>Early Rush / v001</h1><p>16 candidate assets · full canvases · visual and in-game acceptance pending</p></header>
<section><h2>Contact sheet</h2><img class="board" src="contact-sheet.png" alt="All sixteen candidate assets"></section>
<section><h2>Windup pose review</h2><p class="small">One-direction, three-frame candidate. This optional 6 fps review loop does not define gameplay timing.</p>
<div class="controls"><label>Background <select id="background"><option value="#f5efdf">Cream</option><option value="#123c3b">Dark teal</option><option value="#b940dc">Bright purple</option></select></label><button id="play">Play review (6 fps)</button><button id="previous">Previous frame</button><button id="next">Next frame</button><span id="frame">Frame 000</span></div>
<div class="stage" id="stage"><img id="windup" src="../animation-source/crowd/rusher/windup/down/000.png" alt="Windup candidate frame"></div></section>
<section><h2>Scale calibration</h2><img class="board" src="scale-calibration-1280x720.png" alt="Configured pivots and world widths on a grid"></section>
<section class="two"><div><h2>Alpha backgrounds</h2><img class="board" src="alpha-backgrounds.png" alt="Candidates over three background colours"></div><div><h2>Asphalt repeat</h2><img class="board" src="asphalt-2x2.png" alt="Uninterrupted 2 by 2 floor texture repeat"><p class="small">No seam lines or padding have been added. Inspect the middle horizontal and vertical joins.</p></div></section>
<p class="small">Read <a href="README.md">preview notes</a> for legacy reference sources and review limitations.</p>
</main><script>
let index=0,timer=null;const image=document.querySelector('#windup'),label=document.querySelector('#frame'),play=document.querySelector('#play');function show(){const frame=String(index).padStart(3,'0');image.src='../animation-source/crowd/rusher/windup/down/'+frame+'.png';label.textContent='Frame '+frame}function pause(){clearInterval(timer);timer=null;play.textContent='Play review (6 fps)'}play.onclick=()=>{if(timer){pause();return}timer=setInterval(()=>{index=(index+1)%3;show()},1000/6);play.textContent='Pause review'};document.querySelector('#previous').onclick=()=>{pause();index=(index+2)%3;show()};document.querySelector('#next').onclick=()=>{pause();index=(index+1)%3;show()};document.querySelector('#background').onchange=e=>document.querySelector('#stage').style.background=e.target.value;document.addEventListener('visibilitychange',()=>{if(document.hidden)pause()});
</script></html>
'@, $utf8)
    Write-Host 'Created previews/README.md and previews/review.html'
} finally {
    foreach ($image in $images.Values) { $image.Dispose() }
    foreach ($resource in @($fontTitle, $fontSubtitle, $fontLabel, $fontSmall, $gridPen, $cardPen, $pivotPen, $whiteBrush)) { $resource.Dispose() }
}
