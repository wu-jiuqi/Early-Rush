param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = Join-Path $ProjectRoot 'EarlyRush_Art_v002/metadata/layouts/layout-stage-0.json'
$outDir = Join-Path $ProjectRoot 'EarlyRush_Art_v003/reference/layout-guide'
[void][System.IO.Directory]::CreateDirectory($outDir)
$map = Get-Content -LiteralPath $source -Raw | ConvertFrom-Json
$width = 1024; $height = 2048
$xMin = -90.0; $xMax = 90.0; $yMin = -10.0; $yMax = 335.0
$scale = [Math]::Min($width / ($xMax-$xMin), $height / ($yMax-$yMin))
$offsetX = ($width-($xMax-$xMin)*$scale)/2
$offsetY = ($height-($yMax-$yMin)*$scale)/2
function Point([double]$x,[double]$y) {
    [System.Drawing.PointF]::new([single]($offsetX+($x-$xMin)*$scale), [single]($offsetY+($yMax-$y)*$scale))
}
function WorldRect([double]$x,[double]$y,[double]$w,[double]$h) {
    $p = Point $x ($y+$h)
    [System.Drawing.RectangleF]::new($p.X,$p.Y,[single]($w*$scale),[single]($h*$scale))
}
function Brush([string]$hex) { [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($hex)) }
$background = Brush '#D9DFD5'; $paving = Brush '#F6ECD8'; $road = Brush '#929795'; $solid = Brush '#78968C'
$bmp = [System.Drawing.Bitmap]::new($width,$height,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear($background.Color)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
$throughZones = @($map.zones | Where-Object kind -eq 'through-lane')
foreach($cell in $map.cells) {
    $isRoad = $false
    foreach($zone in $throughZones) {
        $b=$zone.bounds
        if($cell.x -ge $b.x -and $cell.x -le ($b.x+$b.width) -and $cell.y -ge $b.y -and $cell.y -le ($b.y+$b.height)) { $isRoad=$true; break }
    }
    $r=WorldRect ($cell.x-.5) ($cell.y-.5) 1 1
    # Shared rounded edges avoid introducing seams into this technical diagram.
    $left=[int][Math]::Round($r.Left); $top=[int][Math]::Round($r.Top)
    $right=[int][Math]::Round($r.Right); $bottom=[int][Math]::Round($r.Bottom)
    $g.FillRectangle($(if($isRoad){$road}else{$paving}),$left,$top,($right-$left),($bottom-$top))
}
foreach($structure in $map.structures) {
    $b=$structure.bounds; $r=WorldRect $b.x $b.y $b.width $b.height
    $g.FillRectangle($solid,$r)
}
$cleanPath=Join-Path $outDir 'street-layout-guide-clean.png'
$bmp.Save($cleanPath,[System.Drawing.Imaging.ImageFormat]::Png)

$g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$routePen=[System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#FFE05C'),3)
$routePen.LineJoin=[System.Drawing.Drawing2D.LineJoin]::Round
$points=[System.Drawing.PointF[]]@($map.route | ForEach-Object { Point $_.x $_.y })
$g.DrawLines($routePen,$points)
$font=[System.Drawing.Font]::new('Arial',18,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$small=[System.Drawing.Font]::new('Arial',14,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
$ink=Brush '#173B35'; $white=Brush '#FFFFFF'; $badge=Brush '#207B6C'; $task=Brush '#D27545'
function Label([string]$text,[double]$x,[double]$y,[System.Drawing.Font]$textFont,[System.Drawing.SolidBrush]$fill) {
    $p=Point $x $y; $size=$g.MeasureString($text,$textFont)
    $r=[System.Drawing.RectangleF]::new($p.X-$size.Width/2-5,$p.Y-$size.Height/2-3,$size.Width+10,$size.Height+6)
    $g.FillRectangle($fill,$r); $g.DrawString($text,$textFont,$white,[single]($r.X+5),[single]($r.Y+3))
}
$routeLength=0.0; $segments=@()
foreach($module in $map.modules) {
    $a=$module.entrance; $b=$module.exit
    $length=[Math]::Sqrt([Math]::Pow($b.x-$a.x,2)+[Math]::Pow($b.y-$a.y,2))
    $routeLength+=$length
    $segments+=@{index=[int]$module.index;kind=$module.kind;start=$a;end=$b;length=$length}
    $labelX=($a.x+$b.x)/2; $labelY=($a.y+$b.y)/2
    if([Math]::Abs($a.x-$b.x)-lt .1) { $labelX+=$(if($module.index -eq 4){13}else{-13}) } else { $labelY+=8 }
    Label ('M{0:00}' -f ($module.index+1)) $labelX $labelY $font $badge
}
foreach($t in $map.tasks) { Label ('S{0} {1}' -f ($t.index+1),$t.kind) $t.center.x $t.center.y $font $task }
Label 'START' $map.spawn.x ($map.spawn.y-6) $font $badge
Label 'METRO EXIT' $map.exit.x ($map.exit.y+7) $font $badge
Label 'SIGNAL' ($map.signal.x+12) $map.signal.y $small $task
$view=@{center=@{x=-45.0;y=100.0};width=26.6666666666667;height=15.0;screenPx=@(1280,720);cameraOrthographicSize=7.5}
$viewRect=WorldRect ($view.center.x-$view.width/2) ($view.center.y-$view.height/2) $view.width $view.height
$viewPen=[System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#2758B0'),3)
$viewPen.DashStyle=[System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawRectangle($viewPen,$viewRect.X,$viewRect.Y,$viewRect.Width,$viewRect.Height)
$viewBrush=Brush '#2758B0'
Label 'ONE GAME VIEW' $view.center.x ($view.center.y+11) $small $viewBrush
$g.DrawString('TECHNICAL GUIDE - existing seed 91026 / 9 modules',$small,$ink,18,10)
$g.DrawString(('Route {0:0} world units | art-only fill outside lanes' -f $routeLength),$small,$ink,18,($height-27))
$labeledPath=Join-Path $outDir 'street-layout-guide-labeled.png'
$bmp.Save($labeledPath,[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()

$snapshotPath=Join-Path $outDir 'layout-stage-0.json'
Copy-Item -LiteralPath $source -Destination $snapshotPath -Force
$minCellX=($map.cells.x|Measure-Object -Minimum).Minimum; $maxCellX=($map.cells.x|Measure-Object -Maximum).Maximum
$minCellY=($map.cells.y|Measure-Object -Minimum).Minimum; $maxCellY=($map.cells.y|Measure-Object -Maximum).Maximum
$meta=[ordered]@{
    schema='early-rush-large-layout-guide-1'; seed=$map.seed; stage=0
    source='EarlyRush_Art_v002/metadata/layouts/layout-stage-0.json'
    sourceSha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    purpose='Non-artistic layout guide for image generation. This is not generated environment art or a gameplay screenshot.'
    canvasPx=@($width,$height)
    worldBounds=@{minX=$xMin;maxX=$xMax;minY=$yMin;maxY=$yMax}
    transform=@{pixelsPerWorldUnit=$scale;offsetPx=@($offsetX,$offsetY);worldToPixelX='offsetX+(worldX-minX)*pixelsPerWorldUnit';worldToPixelY='offsetY+(maxY-worldY)*pixelsPerWorldUnit'}
    walkableBounds=@{minX=$minCellX-.5;maxX=$maxCellX+.5;minY=$minCellY-.5;maxY=$maxCellY+.5;width=$maxCellX-$minCellX+1;height=$maxCellY-$minCellY+1;cells=$map.cells.Count}
    route=$map.route;routeLengthWorldUnits=$routeLength;segments=$segments;tasks=$map.tasks;spawn=$map.spawn;exit=$map.exit;signal=$map.signal;exampleView=$view
    colors=@{outsideNonWalkable='#D9DFD5';walkableSideAreas='#F6ECD8';walkableMainLane='#929795';fixedStructureFootprint='#78968C'}
    constraints=@('Preserve continuous main route and task branch/rejoin connections.','The outside color is nonwalkable context; ornamental buildings may fill it without creating new playable shortcuts.','Art may use softer corners but must not silently redraw collision or route data.','The rectangular view is a scale comparison, not a constraint on final map camera placement.','No art has been painted by this script.')
}
$utf8=[System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText((Join-Path $outDir 'world-to-pixel.json'),(($meta|ConvertTo-Json -Depth 20)+"`n"),$utf8)
@($background,$paving,$road,$solid,$routePen,$font,$small,$ink,$white,$badge,$task,$viewPen,$viewBrush)|ForEach-Object{$_.Dispose()}
[pscustomobject]@{clean=$cleanPath;labeled=$labeledPath;routeLength=$routeLength;walkableWidth=$meta.walkableBounds.width;walkableHeight=$meta.walkableBounds.height}
