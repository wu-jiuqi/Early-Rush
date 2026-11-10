#requires -Version 7
[CmdletBinding()]
param([string]$Root = (Join-Path (Split-Path -Parent $PSScriptRoot) 'EarlyRush_Art_v002'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$rootPath = [IO.Path]::GetFullPath($Root)
$projectPath = Split-Path -Parent $rootPath
$previewPath = Join-Path $rootPath 'previews'
[IO.Directory]::CreateDirectory($previewPath) | Out-Null
$config = Get-Content (Join-Path $rootPath 'metadata/export-config.json') -Raw | ConvertFrom-Json
$script:images = @{}
$script:assets = @{}
foreach($asset in $config.assets) {
    $key = $asset.output -replace '^(runtime-static/V8Art|environment-source)/','' -replace '\.png$',''
    $script:images[$key] = [Drawing.Bitmap]::new((Join-Path $rootPath $asset.output))
    $script:assets[$key] = $asset
}
$script:heroImage = [Drawing.Bitmap]::new((Join-Path $projectPath 'EarlyRush_Art_v001/runtime-static/V8Art/people/boy-down.png'))
$font = [Drawing.Font]::new('Segoe UI',16,[Drawing.FontStyle]::Bold)
$smallFont = [Drawing.Font]::new('Segoe UI',10)
$ink = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#244D4A'))
$mutedInk = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#647A70'))
$cream = [Drawing.ColorTranslator]::FromHtml('#F4F0E6')
$script:floorCache = @{}

function New-Board([int]$Width,[int]$Height) {
    $bitmap=[Drawing.Bitmap]::new($Width,$Height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear($cream)
    $graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    return ,@($bitmap,$graphics)
}
function Save-Board($Board,[string]$Name) {
    $Board[1].Dispose()
    $Board[0].Save((Join-Path $previewPath $Name),[Drawing.Imaging.ImageFormat]::Png)
    $Board[0].Dispose()
    Write-Host "Preview: $Name"
}
function In-Rect($Point,$Rect) {
    return ($Point.x -ge $Rect.x -and $Point.x -lt ($Rect.x+$Rect.width) -and $Point.y -ge $Rect.y -and $Point.y -lt ($Rect.y+$Rect.height))
}
function Cell-Key([int]$X,[int]$Y) { return "$X,$Y" }
function Build-Scene($Map) {
    $floor=@('floors-v3/asphalt','floors-v3/ceramic','floors-v3/carpet')[$Map.stage]
    $cells=[Collections.Generic.HashSet[string]]::new()
    $tiles=[Collections.Generic.List[object]]::new()
    foreach($cell in $Map.cells) {
        [void]$cells.Add((Cell-Key $cell.x $cell.y))
        $key=$floor
        foreach($zone in $Map.zones) {if(In-Rect $cell $zone.bounds){$key=$zone.floorKey}}
        $tiles.Add([pscustomobject]@{x=$cell.x;y=$cell.y;key=$key})
    }
    $objects=[Collections.Generic.List[object]]::new()
    foreach($structure in $Map.structures) {
        if(-not $script:images.ContainsKey($structure.artKey)){throw "Missing structure art: $($structure.artKey)"}
        $objects.Add([pscustomobject]@{key=$structure.artKey;x=$structure.position.x;y=$structure.position.y;width=$structure.width;layer=1})
    }
    # Reproduce border dressing against the existing cells and candidate full-canvas bounds.
    $keys = switch($Map.stage) {
        0 {@('street/storefront','street/bush-planter','street/bench','street/long-planter','street/bicycle-rack')}
        1 {@('metro/vending','metro/platform-bench','metro/kiosk','metro/pillar','metro/escalator')}
        2 {@('office/desk-chair','office/plant','office/sofa','office/filing-cabinet','office/reception')}
    }
    $candidateSet=[Collections.Generic.HashSet[string]]::new()
    $candidates=[Collections.Generic.List[object]]::new()
    $directions=@(@(0,1),@(0,-1),@(-1,0),@(1,0))
    foreach($cell in $Map.cells){foreach($d in $directions){
        if(-not $cells.Contains((Cell-Key ($cell.x+$d[0]) ($cell.y+$d[1])))){
            foreach($distance in 2..6){
                $x=$cell.x+$d[0]*$distance;$y=$cell.y+$d[1]*$distance
                if($candidateSet.Add((Cell-Key $x $y))){$candidates.Add([pscustomobject]@{x=$x;y=$y})}
            }
        }
    }}
    $placed=[Collections.Generic.List[object]]::new()
    $rng=[Random]::new([int]$Map.seed+[int]$Map.stage*359)
    foreach($c in ($candidates | Sort-Object y,x)){
        $near=$false;foreach($p in $placed){if((($p.x-$c.x)*($p.x-$c.x)+($p.y-$c.y)*($p.y-$c.y)) -lt 25){$near=$true;break}}
        if($near){continue}
        $key=$keys[$rng.Next($keys.Count)];$image=$script:images[$key];$width=3.2
        $height=$width*$image.Height/$image.Width
        # Current renderer's general (0.5,0.1) pivot, not visible-alpha bounds.
        $loX=$c.x-$width*.5-.5;$hiX=$c.x+$width*.5+.5
        $loY=$c.y-$height*.1-.5;$hiY=$c.y+$height*.9+.5
        $overlap=$false
        for($y=[Math]::Floor($loY);$y -le [Math]::Ceiling($hiY) -and -not $overlap;$y++){
            for($x=[Math]::Floor($loX);$x -le [Math]::Ceiling($hiX);$x++){
                if($cells.Contains((Cell-Key $x $y))){$overlap=$true;break}
            }
        }
        if(-not $overlap){foreach($s in $Map.structures){$b=$s.bounds;if($loX -lt $b.x+$b.width -and $hiX -gt $b.x -and $loY -lt $b.y+$b.height -and $hiY -gt $b.y){$overlap=$true;break}}}
        if(-not $overlap){$objects.Add([pscustomobject]@{key=$key;x=$c.x;y=$c.y;width=$width;layer=1});$placed.Add($c)}
    }
    if($Map.stage -eq 0){
        $objects.Add([pscustomobject]@{key='street/station-doorway';x=$Map.exit.x;y=$Map.exit.y+2;width=4;layer=1})
        $objects.Add([pscustomobject]@{key='stalls/breakfast-stall';x=$Map.tasks[0].center.x;y=$Map.tasks[0].center.y;width=2.6;layer=1})
        $objects.Add([pscustomobject]@{key='corrections/crosswalk-flat';x=$Map.signal.x;y=$Map.signal.y;width=7.65;layer=0})
    } elseif($Map.stage -eq 1){
        $objects.Add([pscustomobject]@{key='metro/train-open';x=$Map.trainDoor.x;y=$Map.trainDoor.y+1.5;width=7;layer=1})
        $objects.Add([pscustomobject]@{key='metro/exit-marker';x=$Map.exit.x;y=$Map.exit.y+2;width=4;layer=1})
        $objects.Add([pscustomobject]@{key='metro/vending';x=$Map.tasks[1].center.x;y=$Map.tasks[1].center.y;width=2.6;layer=1})
        for($x=-6;$x -le 6;$x+=3){$objects.Add([pscustomobject]@{key='metro/tactile-strip';x=$Map.trainDoor.x+$x;y=$Map.trainDoor.y-2.3;width=3;layer=0})}
    } else {
        $objects.Add([pscustomobject]@{key='office/meeting-table';x=$Map.exit.x;y=$Map.exit.y+3.5;width=5;layer=1})
    }
    return [pscustomobject]@{map=$Map;tiles=$tiles;objects=$objects;cells=$cells;decorCount=$placed.Count}
}
function Draw-Sprite($Graphics,[string]$Key,[double]$X,[double]$Y,[double]$WorldWidth,[double]$MinX,[double]$MaxY,[double]$PixelsPerUnit,[int]$W,[int]$H) {
    if(-not $script:images.ContainsKey($Key)){return}
    $image=$script:images[$Key];$asset=$script:assets[$Key]
    $width=$WorldWidth*$PixelsPerUnit;$height=$width*$image.Height/$image.Width
    $left=($X-$MinX)*$PixelsPerUnit-$width*$asset.pivotNormalizedBottomLeft[0]
    $top=($MaxY-$Y)*$PixelsPerUnit-$height*(1-$asset.pivotNormalizedBottomLeft[1])
    if($left -ge $W -or $top -ge $H -or $left+$width -le 0 -or $top+$height -le 0){return}
    $Graphics.DrawImage($image,[Drawing.RectangleF]::new($left,$top,$width,$height))
}
function Render-View($Scene,[string]$Name,[double]$CenterX,[double]$CenterY,[double]$PixelsPerUnit,[int]$Width,[int]$Height,[bool]$Overview) {
    $board=New-Board $Width $Height;$g=$board[1]
    $minX=$CenterX-$Width/$PixelsPerUnit/2;$maxY=$CenterY+$Height/$PixelsPerUnit/2
    $tileAttributes=[Drawing.Imaging.ImageAttributes]::new()
    $tileAttributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
    foreach($tile in $Scene.tiles){
        $left=($tile.x-.5-$minX)*$PixelsPerUnit;$top=($maxY-$tile.y-.5)*$PixelsPerUnit
        if($left -gt $Width -or $top -gt $Height -or $left+$PixelsPerUnit -lt 0 -or $top+$PixelsPerUnit -lt 0){continue}
        $texture=$script:images[$tile.key]
        $sx=(($tile.x%4+4)%4)*128;$sy=(3-(($tile.y%4+4)%4))*128
        # Shared rounded edges prevent subpixel alpha gaps in small overviews.
        $px0=[Math]::Round($left);$py0=[Math]::Round($top)
        $px1=[Math]::Round($left+$PixelsPerUnit);$py1=[Math]::Round($top+$PixelsPerUnit)
        $points=[Drawing.PointF[]]@([Drawing.PointF]::new($px0,$py0),[Drawing.PointF]::new($px1,$py0),[Drawing.PointF]::new($px0,$py1))
        $g.DrawImage($texture,$points,[Drawing.RectangleF]::new($sx,$sy,128,128),[Drawing.GraphicsUnit]::Pixel,$tileAttributes)
    }
    $tileAttributes.Dispose()
    foreach($object in ($Scene.objects | Where-Object layer -eq 0)){Draw-Sprite $g $object.key $object.x $object.y $object.width $minX $maxY $PixelsPerUnit $Width $Height}
    foreach($object in ($Scene.objects | Where-Object layer -eq 1 | Sort-Object y -Descending)){Draw-Sprite $g $object.key $object.x $object.y $object.width $minX $maxY $PixelsPerUnit $Width $Height}
    if($Overview){
        $routePen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(150,255,177,70),2)
        $points=[Drawing.PointF[]]@($Scene.map.route | ForEach-Object {[Drawing.PointF]::new(($_.x-$minX)*$PixelsPerUnit,($maxY-$_.y)*$PixelsPerUnit)})
        $g.DrawLines($routePen,$points);$routePen.Dispose()
        foreach($task in $Scene.map.tasks){$px=($task.center.x-$minX)*$PixelsPerUnit;$py=($maxY-$task.center.y)*$PixelsPerUnit;$g.FillEllipse($ink,[single]($px-5),[single]($py-5),10,10);$g.DrawString($task.kind,$smallFont,$ink,[single]($px+8),[single]$py)}
    } else {
        # A separate legend avoids implying a real actor position or hiding a fixture.
        $heroWidth=1.05*$PixelsPerUnit;$heroHeight=$heroWidth*1.5
        $legendBrush=[Drawing.SolidBrush]::new($cream)
        $g.FillRectangle($legendBrush,($Width-142),($Height-150),122,110);$legendBrush.Dispose()
        $heroX=$Width-81;$heroY=$Height-72
        $g.DrawImage($script:heroImage,[Drawing.RectangleF]::new($heroX-$heroWidth*.5,$heroY-$heroHeight*.9,$heroWidth,$heroHeight))
        $g.DrawString('PLAYER / 1.05 u',$smallFont,$ink,[single]($Width-133),[single]($Height-61))
    }
    $band=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(242,$cream));$g.FillRectangle($band,0,0,$Width,70);$g.FillRectangle($band,0,$Height-34,$Width,34);$band.Dispose()
    $stageName=@('STREET','METRO','OFFICE')[$Scene.map.stage]
    $g.DrawString("EARLY RUSH / $stageName / v002",$font,$ink,24,13)
    $g.DrawString("Seed $($Scene.map.seed) | existing cells, zones and structure positions | art assembly preview",$smallFont,$mutedInk,24,42)
    $footer=if($Overview){'Overview: route and task labels are review overlays, not baked into game art.'}else{'Review: centre-pivot decals preview the intended mapping; player is a scale reference. Not a Unity capture.'}
    $g.DrawString($footer,$smallFont,$mutedInk,20,($Height-26))
    Save-Board $board $Name
}
function Contact-Sheet([object[]]$List,[string]$Name,[string]$Title){
    $cols=4;$rows=[Math]::Ceiling($List.Count/$cols);$board=New-Board 1440 ([int](100+$rows*290));$g=$board[1]
    $g.DrawString($Title,$font,$ink,28,22);$g.DrawString('Complete exported canvases | A: existing pivot; B: mapping/pivot update needed',$smallFont,$mutedInk,28,52)
    for($i=0;$i -lt $List.Count;$i++){
        $asset=$List[$i];$key=$asset.output -replace '^(runtime-static/V8Art|environment-source)/','' -replace '\.png$',''
        $im=$script:images[$key];$x=20+($i%$cols)*355;$y=90+[Math]::Floor($i/$cols)*290
        $card=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#FFFAF1'));$g.FillRectangle($card,[single]$x,[single]$y,335,270);$card.Dispose()
        $scale=[Math]::Min(300/$im.Width,205/$im.Height);$w=$im.Width*$scale;$h=$im.Height*$scale
        $g.DrawImage($im,[Drawing.RectangleF]::new($x+(335-$w)/2,$y+10+(205-$h)/2,$w,$h))
        $g.DrawString($key,$smallFont,$ink,[single]($x+12),[single]($y+225))
        $g.DrawString(("{0} x {1} | {2}" -f $im.Width,$im.Height,$asset.integration),$smallFont,$mutedInk,[single]($x+12),[single]($y+245))
    }
    Save-Board $board $Name
}
try {
    $records=@()
    foreach($stage in 0..2){
        $map=Get-Content (Join-Path $rootPath "metadata/layouts/layout-stage-$stage.json") -Raw | ConvertFrom-Json
        $scene=Build-Scene $map
        $xs=@($map.cells | ForEach-Object x);$ys=@($map.cells | ForEach-Object y)
        $cx=(($xs|Measure-Object -Minimum).Minimum+($xs|Measure-Object -Maximum).Maximum)/2
        $cy=(($ys|Measure-Object -Minimum).Minimum+($ys|Measure-Object -Maximum).Maximum)/2
        Render-View $scene "map-stage-$stage-overview.png" $cx $cy 4.6 900 1700 $true
        $focus=if($stage -eq 0){$map.tasks[0].center}elseif($stage -eq 1){$map.platform}else{$map.tasks[1].center}
        Render-View $scene "map-stage-$stage-detail.png" $focus.x ($focus.y+2) 32 1280 720 $false
        Render-View $scene "map-stage-$stage-exit.png" $map.exit.x ($map.exit.y+2) 40 1280 720 $false
        $records += [ordered]@{stage=$stage;seed=$map.seed;cells=$map.cells.Count;structures=$map.structures.Count;derivedBorderDecor=$scene.decorCount;previewOnly=$true}
    }
    foreach($group in @('street','metro','office','common')){
        $list=@($config.assets | Where-Object {$_.source -like "source/generated/$group/*"})
        Contact-Sheet $list "assets-$group.png" "EARLY RUSH / $($group.ToUpper()) ASSETS / v002"
    }
    $floorBoard=New-Board 2560 600;$g=$floorBoard[1];$index=0
    $repeatAttributes=[Drawing.Imaging.ImageAttributes]::new()
    $repeatAttributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
    foreach($key in @('floors-v3/asphalt','floors-v3/ceramic','legacy-floors/floor-tile','floors-v3/carpet','floors-v3/sidewalk')){
        for($y=0;$y -lt 2;$y++){for($x=0;$x -lt 2;$x++){
            $left=$index*512+$x*256;$top=60+$y*256
            $pts=[Drawing.PointF[]]@([Drawing.PointF]::new($left,$top),[Drawing.PointF]::new($left+256,$top),[Drawing.PointF]::new($left,$top+256))
            $g.DrawImage($script:images[$key],$pts,[Drawing.RectangleF]::new(0,0,512,512),[Drawing.GraphicsUnit]::Pixel,$repeatAttributes)
        }}
        $g.DrawString($key,$font,$ink,[single]($index*512+12),18);$index++
    }
    $repeatAttributes.Dispose()
    Save-Board $floorBoard 'floors-repeat.png'
    [IO.File]::WriteAllText((Join-Path $rootPath 'metadata/preview-render-report.json'),($records|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
} finally {
    foreach($im in $script:images.Values){$im.Dispose()}
    $script:heroImage.Dispose();$font.Dispose();$smallFont.Dispose();$ink.Dispose();$mutedInk.Dispose()
}
