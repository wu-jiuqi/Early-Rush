param([string]$Root = (Join-Path $PSScriptRoot '../../..'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$Root = [IO.Path]::GetFullPath($Root)
$metadataPath = Join-Path $Root 'metadata/facade-prompts.json'
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$sheet = [Drawing.Bitmap]::new(1536,1088,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sg = [Drawing.Graphics]::FromImage($sheet)
$sg.Clear([Drawing.Color]::FromArgb(255,248,240,219))
$font = [Drawing.Font]::new('Arial',15)
$brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,30,70,64))
$index = 0
foreach ($job in $metadata.jobs) {
  $sourcePath = Join-Path $Root ('assets/source/facades/' + $job.id + '.png')
  $outputPath = Join-Path $Root ('assets/exported/facades/' + $job.id + '.png')
  $source = [Drawing.Bitmap]::new($sourcePath)
  $output = [Drawing.Bitmap]::new(512,512,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [Drawing.Graphics]::FromImage($output)
  $g.Clear([Drawing.Color]::Transparent)
  $g.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
  $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $scale = [Math]::Min(512.0/$source.Width,512.0/$source.Height)
  $dest = [Drawing.Rectangle]::new([int][Math]::Round((512-$source.Width*$scale)/2),[int][Math]::Round((512-$source.Height*$scale)/2),[int][Math]::Round($source.Width*$scale),[int][Math]::Round($source.Height*$scale))
  $attrs = [Drawing.Imaging.ImageAttributes]::new()
  $attrs.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
  $g.DrawImage($source,$dest,0,0,$source.Width,$source.Height,[Drawing.GraphicsUnit]::Pixel,$attrs)
  $g.Dispose(); $attrs.Dispose()
  $output.Save($outputPath,[Drawing.Imaging.ImageFormat]::Png)
  $minX=512; $minY=512; $maxX=-1; $maxY=-1; $zero=0; $edgeMax=0
  for($y=0;$y -lt 512;$y++) { for($x=0;$x -lt 512;$x++) {
    $a=[int]$output.GetPixel($x,$y).A
    if($a -eq 0) {$zero++}
    if($a -ge 128) {$minX=[Math]::Min($minX,$x);$minY=[Math]::Min($minY,$y);$maxX=[Math]::Max($maxX,$x);$maxY=[Math]::Max($maxY,$y)}
    if($x -eq 0 -or $x -eq 511 -or $y -eq 0 -or $y -eq 511) {$edgeMax=[Math]::Max($edgeMax,$a)}
  }}
  $report = [ordered]@{source=('assets/source/facades/'+$job.id+'.png');output=('assets/exported/facades/'+$job.id+'.png');sourceCanvasPx=@($source.Width,$source.Height);canvasPx=@(512,512);scale=$scale;alpha128Bbox=@($minX,$minY,($maxX-$minX+1),($maxY-$minY+1));transparentFraction=($zero/(512.0*512));edgeAlphaMax=$edgeMax;sourceSha256=(Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash;outputSha256=(Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash;masterVisibleRect=$job.rect;placementNote='Visible-body guide rectangle in the district master pixels, not a world collision or runtime pivot; use alpha bbox for compositing, preserve aspect ratio.'}
  if ($job.id -eq 'metro-entry') {$report.openingAlphaAtOutputPx240_300=[int]$output.GetPixel(240,300).A}
  $job | Add-Member -NotePropertyName export -NotePropertyValue $report -Force
  $col=$index%3; $row=[Math]::Floor($index/3)
  $sg.DrawImage($output,[int]($col*512),[int]($row*544),512,512)
  $sg.DrawString($job.id,$font,$brush,[single]($col*512+12),[single]($row*544+516))
  $source.Dispose();$output.Dispose();$index++
}
$metadata.status='six generated and technically exported candidates; not imported or user-approved'
$metadata | Add-Member -NotePropertyName technicalExport -NotePropertyValue 'Uniform full-canvas scale to 512x512. No trim, background removal, recoloring, repainting or shape edit. Alpha preserved. Contact sheet is normal compositing on cream for inspection.' -Force
$json=$metadata | ConvertTo-Json -Depth 20
[IO.File]::WriteAllText($metadataPath,($json.Replace("`r`n","`n")+"`n"),[Text.UTF8Encoding]::new($false))
$sg.Dispose();$font.Dispose();$brush.Dispose()
$sheet.Save((Join-Path $Root 'assets/exported/facades/preview-on-cream.png'),[Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
$metadata.jobs | Select-Object id,@{n='size';e={$_.export.canvasPx -join 'x'}},@{n='edgeAlpha';e={$_.export.edgeAlphaMax}},@{n='bbox';e={$_.export.alpha128Bbox -join ','}}
