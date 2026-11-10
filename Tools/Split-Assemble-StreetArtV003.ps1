param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$SkipSlices,
    [switch]$PrepareSeamInputs,
    [string]$AssemblyConfig = ''
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
if(-not('StreetArtPixelsV003' -as [type])){
    Add-Type -ReferencedAssemblies @('System.Drawing.Common','System.Drawing.Primitives','System.Runtime.InteropServices','System.Security.Cryptography') -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
public static class StreetArtPixelsV003 {
    public static string PixelHash(Bitmap image) {
        var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try {
            var bytes=new byte[image.Width*image.Height*4];
            for(int y=0;y<image.Height;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),bytes,y*image.Width*4,image.Width*4);
            using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        } finally {image.UnlockBits(data);}
    }
    public static void PreserveEdges(Bitmap output,Bitmap reference,Rectangle[] rects,int solid,int feather) {
        if(output.Size!=reference.Size) throw new ArgumentException("Reference size mismatch");
        var a=output.LockBits(new Rectangle(0,0,output.Width,output.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        var b=reference.LockBits(new Rectangle(0,0,reference.Width,reference.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try {
            int stride=output.Width*4;
            var dst=new byte[stride*output.Height];var src=new byte[stride*output.Height];
            for(int y=0;y<output.Height;y++) {Marshal.Copy(IntPtr.Add(a.Scan0,y*a.Stride),dst,y*stride,stride);Marshal.Copy(IntPtr.Add(b.Scan0,y*b.Stride),src,y*stride,stride);}
            foreach(var rect in rects) for(int y=rect.Top;y<rect.Bottom;y++) for(int x=rect.Left;x<rect.Right;x++) {
                int d=Math.Min(Math.Min(x-rect.Left,rect.Right-1-x),Math.Min(y-rect.Top,rect.Bottom-1-y));
                if(d>=solid+feather) continue;
                double w=d<solid?1.0:(solid+feather-d)/(double)Math.Max(1,feather);
                int p=y*stride+x*4;
                double alpha=src[p+3]*w+dst[p+3]*(1-w);
                for(int c=0;c<3;c++) dst[p+c]=(byte)Math.Round(alpha>0?(src[p+c]*src[p+3]*w+dst[p+c]*dst[p+3]*(1-w))/alpha:0);
                dst[p+3]=(byte)Math.Round(alpha);
            }
            for(int y=0;y<output.Height;y++) Marshal.Copy(dst,y*stride,IntPtr.Add(a.Scan0,y*a.Stride),stride);
        } finally {output.UnlockBits(a);reference.UnlockBits(b);}
    }
    public static void BlendPatch(Bitmap output,Bitmap patch,Rectangle dest,int feather) {
        if(patch.Width!=dest.Width||patch.Height!=dest.Height) throw new ArgumentException("Patch must match destination pixel dimensions");
        if(dest.Left<0||dest.Top<0||dest.Right>output.Width||dest.Bottom>output.Height) throw new ArgumentException("Patch destination is outside canvas");
        if(feather<0) throw new ArgumentException("Negative feather");
        var a=output.LockBits(dest,ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        var b=patch.LockBits(new Rectangle(0,0,patch.Width,patch.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try {
            int stride=dest.Width*4;
            var dst=new byte[stride*dest.Height];var src=new byte[stride*dest.Height];
            for(int y=0;y<dest.Height;y++){Marshal.Copy(IntPtr.Add(a.Scan0,y*a.Stride),dst,y*stride,stride);Marshal.Copy(IntPtr.Add(b.Scan0,y*b.Stride),src,y*stride,stride);}
            for(int y=0;y<dest.Height;y++)for(int x=0;x<dest.Width;x++){
                int d=int.MaxValue;
                if(dest.Left>0)d=Math.Min(d,x);if(dest.Right<output.Width)d=Math.Min(d,dest.Width-1-x);
                if(dest.Top>0)d=Math.Min(d,y);if(dest.Bottom<output.Height)d=Math.Min(d,dest.Height-1-y);
                double w=feather==0?1.0:Math.Min(1.0,d/(double)feather);
                int p=y*stride+x*4;double srcA=src[p+3]/255.0*w,dstA=dst[p+3]/255.0;
                double alpha=srcA+dstA*(1-srcA);
                for(int c=0;c<3;c++)dst[p+c]=(byte)Math.Round(alpha>0?(src[p+c]*srcA+dst[p+c]*dstA*(1-srcA))/alpha:0);
                dst[p+3]=(byte)Math.Round(alpha*255);
            }
            for(int y=0;y<dest.Height;y++)Marshal.Copy(dst,y*stride,IntPtr.Add(a.Scan0,y*a.Stride),stride);
        }finally{output.UnlockBits(a);patch.UnlockBits(b);}
    }
}
'@
}
$root=Join-Path $ProjectRoot 'EarlyRush_Art_v003'
$masterPath=Join-Path $root 'master/street-district-master-v2.png'
$cropDir=Join-Path $root 'reference/crops'
$propDir=Join-Path $root 'props/crops'
$previewDir=Join-Path $root 'previews'
$metadataDir=Join-Path $root 'metadata'
@($cropDir,$propDir,$previewDir,$metadataDir)|ForEach-Object{[void][System.IO.Directory]::CreateDirectory($_)}
$utf8=[System.Text.UTF8Encoding]::new($false)
function WriteJson($value,[string]$path){[System.IO.File]::WriteAllText($path,(($value|ConvertTo-Json -Depth 30)+"`n"),$utf8)}
function CropImage([System.Drawing.Bitmap]$image,[int[]]$rect,[string]$path){
    $r=[System.Drawing.Rectangle]::new($rect[0],$rect[1],$rect[2],$rect[3])
    if($r.Left-lt 0-or$r.Top-lt 0-or$r.Right-gt$image.Width-or$r.Bottom-gt$image.Height){throw "Crop outside source: $path"}
    $crop=$image.Clone($r,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try{$crop.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)}finally{$crop.Dispose()}
}
if($PrepareSeamInputs){
    $rawPath=Join-Path $previewDir 'street-generated-ground-unlocked.png'
    $inputDir=Join-Path $root 'tiles/seam-inputs';[void][System.IO.Directory]::CreateDirectory($inputDir)
    $seams=@();$ys=@(0,1274,2548,3822)
    for($i=0;$i-lt 4;$i++){$seams+=@{id="seam-v$($i+1)";rect=@(930,$ys[$i],600,1500);axis='vertical';order=$i}}
    $ys=@(1020,2130,3555)
    for($i=0;$i-lt 3;$i++){
        $seams+=@{id="seam-h$($i+1)-left";rect=@(0,$ys[$i],1460,600);axis='horizontal';order=4+$i*2}
        $seams+=@{id="seam-h$($i+1)-right";rect=@(1201,$ys[$i],1460,600);axis='horizontal';order=5+$i*2}
    }
    $raw=[System.Drawing.Bitmap]::new($rawPath)
    try{
        if($raw.Width-ne 2661-or$raw.Height-ne 5322){throw 'Seam input plan requires 2661x5322 unlocked source.'}
        foreach($s in $seams){$path=Join-Path $inputDir "$($s.id).png";CropImage $raw $s.rect $path;$s.input="tiles/seam-inputs/$($s.id).png";$s.inputSha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant();$s.featherOutputPx=60}
        WriteJson @{schema='early-rush-seam-repair-plan-1';source='previews/street-generated-ground-unlocked.png';sourceSha256=(Get-FileHash -LiteralPath $rawPath -Algorithm SHA256).Hash.ToLowerInvariant();canvasPx=@($raw.Width,$raw.Height);seams=$seams;order='Four vertical repair strips, then six horizontal repair strips, listed in order.';composition='image_gen creates repair art from each context crop; technical placement blends outer 60 output pixels except at the full-canvas perimeter. No original low-resolution master edge pixels are reintroduced.'} (Join-Path $metadataDir 'seam-plan.json')
    }finally{$raw.Dispose()}
    [pscustomobject]@{seamInputDirectory=$inputDir;seamInputs=$seams.Count};return
}
$master=[System.Drawing.Bitmap]::new($masterPath)
try{
    if($master.Width-ne 887-or$master.Height-ne 1774){throw 'Master dimensions changed; revise slicing coordinates explicitly.'}
    $x=@(0,410,887);$y=@(0,440,810,1285,1774)
    $tiles=@()
    for($row=0;$row-lt 4;$row++){
        for($col=0;$col-lt 2;$col++){
            $n=$row*2+$col+1;$id='tile-{0:00}'-f$n
            $rect=@($x[$col],$y[$row],($x[$col+1]-$x[$col]),($y[$row+1]-$y[$row]))
            $tiles+=[ordered]@{
                id=$id;row=$row;column=$col;sourceRectPx=$rect;sourceCrop="reference/crops/$id.png"
                neighbors=@{left=$(if($col-gt 0){'tile-{0:00}'-f($n-1)}else{$null});right=$(if($col-lt 1){'tile-{0:00}'-f($n+1)}else{$null});top=$(if($row-gt 0){'tile-{0:00}'-f($n-2)}else{$null});bottom=$(if($row-lt 3){'tile-{0:00}'-f($n+2)}else{$null})}
                sharedEdgesMasterPx=@{left=@($rect[0],$rect[1],$rect[0],($rect[1]+$rect[3]));right=@(($rect[0]+$rect[2]),$rect[1],($rect[0]+$rect[2]),($rect[1]+$rect[3]));top=@($rect[0],$rect[1],($rect[0]+$rect[2]),$rect[1]);bottom=@($rect[0],($rect[1]+$rect[3]),($rect[0]+$rect[2]),($rect[1]+$rect[3]))}
            }
            if(-not$SkipSlices){CropImage $master $rect (Join-Path $cropDir "$id.png")}
        }
    }
    $props=@(
        @{id='metro';rect=@(404,10,153,145)},
        @{id='breakfast';rect=@(477,1370,130,125)},
        @{id='repair';rect=@(264,841,120,125)},
        @{id='flip-board';rect=@(180,520,160,105)},
        @{id='signal';rect=@(660,1172,40,96)},
        @{id='bench';rect=@(190,475,65,43)},
        @{id='tree';rect=@(359,135,58,100)},
        @{id='planter';rect=@(183,227,65,53)}
    )
    foreach($p in $props){
        $p.sourceCrop="props/crops/$($p.id).png"
        if(-not$SkipSlices){CropImage $master $p.rect (Join-Path $propDir "$($p.id).png")}
    }
    $plan=[ordered]@{
        schema='early-rush-street-slicing-1';master='master/street-district-master-v2.png';masterSha256=(Get-FileHash -LiteralPath $masterPath -Algorithm SHA256).Hash.ToLowerInvariant()
        masterCanvasPx=@($master.Width,$master.Height);coordinateConvention='Top-left pixel origin, rectangles [x,y,width,height], end-exclusive. Exact integer edge sharing; zero gap and zero overlap.'
        xEdges=$x;yEdges=$y;tiles=$tiles;props=$props
        boundaryPolicy='Preserve positions and visible edge continuity from the master when regenerating. Optional technical composite can retain the original outer 8 source pixels plus an 8-pixel inner alpha blend. This is explicit source-image compositing, not new artwork generation.'
        scope='Reference crops and technical cut/assembly only; this script does not generate or paint artwork.'
    }
    if(-not$SkipSlices){
        WriteJson $plan (Join-Path $metadataDir 'slicing-plan.json')
        $annotated=$master.Clone()
        $g=[System.Drawing.Graphics]::FromImage($annotated)
        try{
            $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $pen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(230,246,90,77),2)
            $font=[System.Drawing.Font]::new('Arial',15,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
            $white=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
            $fill=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(210,25,65,59))
            foreach($tile in $tiles){
                $r=$tile.sourceRectPx
                $g.DrawRectangle($pen,$r[0],$r[1],($r[2]-1),($r[3]-1))
                $g.FillRectangle($fill,($r[0]+6),($r[1]+6),80,23)
                $g.DrawString($tile.id,$font,$white,($r[0]+11),($r[1]+8))
            }
            $annotated.Save((Join-Path $previewDir 'street-slicing-plan.png'),[System.Drawing.Imaging.ImageFormat]::Png)
            @($pen,$font,$white,$fill)|ForEach-Object{$_.Dispose()}
        }finally{$g.Dispose();$annotated.Dispose()}
        # Prove the crop coordinates reproduce every source pixel exactly.
        $reassembled=[System.Drawing.Bitmap]::new($master.Width,$master.Height,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $rg=[System.Drawing.Graphics]::FromImage($reassembled)
        try{
            $rg.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            foreach($tile in $tiles){$im=[System.Drawing.Bitmap]::new((Join-Path $root $tile.sourceCrop));try{$rg.DrawImageUnscaled($im,$tile.sourceRectPx[0],$tile.sourceRectPx[1])}finally{$im.Dispose()}}
            $reassembled.Save((Join-Path $previewDir 'master-crops-reassembled.png'),[System.Drawing.Imaging.ImageFormat]::Png)
            $sourcePixelHash=[StreetArtPixelsV003]::PixelHash($master)
            $assembledPixelHash=[StreetArtPixelsV003]::PixelHash($reassembled)
            WriteJson @{schema='early-rush-source-crop-validation-1';masterPixelSha256=$sourcePixelHash;reassembledPixelSha256=$assembledPixelHash;pixelExact=($sourcePixelHash-eq$assembledPixelHash);tiles=8;sourceRectsCoverCanvasOnce=$true} (Join-Path $metadataDir 'source-crop-validation.json')
            if($sourcePixelHash-ne$assembledPixelHash){throw 'Source crop reassembly did not reproduce the master pixels exactly.'}
        }finally{$rg.Dispose();$reassembled.Dispose()}
    }
}finally{$master.Dispose()}

if([string]::IsNullOrWhiteSpace($AssemblyConfig)){$AssemblyConfig=Join-Path $metadataDir 'assembly-config.json'}
if(Test-Path -LiteralPath $AssemblyConfig){
    # Optional assembly contract: outputScale; tiles[{id,file,sourceRectPx?}]; overlays[{id,file,destinationRectMasterPx,sourceRectPx?}].
    # boundaryLock:{enabled:true,solidMasterPx:8,featherMasterPx:8} explicitly preserves source collars, in source-master pixel units.
    # seamPatches:[{id,file,sourceRectPx?:[x,y,w,h],destinationRectOutputPx:[x,y,w,h],featherOutputPx:60}] applies generated repair art in listed order.
    # Source rectangles default to the complete generated image. Tile placement comes only from slicing-plan.json.
    $cfg=Get-Content -LiteralPath $AssemblyConfig -Raw|ConvertFrom-Json
    $outputScale=$(if($cfg.outputScale){[double]$cfg.outputScale}else{1.0})
    if($outputScale-le 0-or$outputScale-gt 8){throw 'outputScale must be >0 and <=8.'}
    $output=[System.Drawing.Bitmap]::new([int][Math]::Round(887*$outputScale),[int][Math]::Round(1774*$outputScale),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $og=[System.Drawing.Graphics]::FromImage($output)
    $imageAttr=[System.Drawing.Imaging.ImageAttributes]::new()
    $imageAttr.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $report=@();$warnings=@()
    try{
        $og.Clear([System.Drawing.Color]::Transparent)
        $og.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $og.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $seen=@{}
        foreach($entry in $cfg.tiles){
            $tile=@($tiles|Where-Object id -eq $entry.id)
            if($tile.Count-ne 1){throw "Unknown tile: $($entry.id)"}
            if($seen.ContainsKey($entry.id)){throw "Duplicate tile: $($entry.id)"};$seen[$entry.id]=$true
            $path=Join-Path $root $entry.file;$im=[System.Drawing.Bitmap]::new($path)
            try{
                $sr=$(if($entry.sourceRectPx){[int[]]$entry.sourceRectPx}else{@(0,0,$im.Width,$im.Height)})
                $r=$tile[0].sourceRectPx
                $dx=[int][Math]::Round($r[0]*$outputScale);$dy=[int][Math]::Round($r[1]*$outputScale)
                $dw=[int][Math]::Round(($r[0]+$r[2])*$outputScale)-$dx;$dh=[int][Math]::Round(($r[1]+$r[3])*$outputScale)-$dy
                $dest=[System.Drawing.Rectangle]::new($dx,$dy,$dw,$dh)
                $distortion=[Math]::Abs(($sr[2]/[double]$sr[3])/($r[2]/[double]$r[3])-1)
                if($distortion-gt .02){$warnings+="$($entry.id): generated aspect ratio differs from destination by $([Math]::Round($distortion*100,2))%; verify sourceRectPx."}
                if($dest.Width-eq$sr[2]-and$dest.Height-eq$sr[3]){
                    $native=$im.Clone([System.Drawing.Rectangle]::new($sr[0],$sr[1],$sr[2],$sr[3]),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                    try{$og.DrawImageUnscaled($native,$dest.X,$dest.Y)}finally{$native.Dispose()}
                }else{$og.DrawImage($im,$dest,$sr[0],$sr[1],$sr[2],$sr[3],[System.Drawing.GraphicsUnit]::Pixel,$imageAttr)}
                $report+=@{kind='tile';id=$entry.id;file=$entry.file;sourceRectPx=$sr;destinationRectPx=@($dx,$dy,$dw,$dh);sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
            }finally{$im.Dispose()}
        }
        if($seen.Count-ne 8){throw "Assembly requires all 8 tiles; found $($seen.Count)."}
        $output.Save((Join-Path $previewDir 'street-generated-ground-unlocked.png'),[System.Drawing.Imaging.ImageFormat]::Png)
        $boundaryRecord=@{enabled=$false}
        if($cfg.seamPatches-and$cfg.boundaryLock-and$cfg.boundaryLock.enabled){throw 'Generated seam repair patches cannot be combined with low-resolution master edge locking.'}
        if($cfg.boundaryLock -and $cfg.boundaryLock.enabled){
            $solid=$(if($null-ne$cfg.boundaryLock.solidMasterPx){[int]$cfg.boundaryLock.solidMasterPx}else{8})
            $feather=$(if($null-ne$cfg.boundaryLock.featherMasterPx){[int]$cfg.boundaryLock.featherMasterPx}else{8})
            if($solid-lt 0-or$feather-lt 0-or($solid+$feather)-gt 64){throw 'Boundary master pixel widths must be nonnegative and total <=64.'}
            $reference=[System.Drawing.Bitmap]::new($output.Width,$output.Height,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $refG=[System.Drawing.Graphics]::FromImage($reference);$sourceMaster=[System.Drawing.Bitmap]::new($masterPath)
            try{
                $refG.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $refG.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                if($reference.Width-eq$sourceMaster.Width-and$reference.Height-eq$sourceMaster.Height){$refG.DrawImageUnscaled($sourceMaster,0,0)}
                else{$refG.DrawImage($sourceMaster,[System.Drawing.Rectangle]::new(0,0,$reference.Width,$reference.Height),0,0,$sourceMaster.Width,$sourceMaster.Height,[System.Drawing.GraphicsUnit]::Pixel,$imageAttr)}
                $refG.Dispose();$refG=$null
                $tileRects=[System.Drawing.Rectangle[]]@($report|Where-Object kind -eq 'tile'|ForEach-Object{$r=$_.destinationRectPx;[System.Drawing.Rectangle]::new($r[0],$r[1],$r[2],$r[3])})
                [StreetArtPixelsV003]::PreserveEdges($output,$reference,$tileRects,[int][Math]::Round($solid*$outputScale),[int][Math]::Round($feather*$outputScale))
                $boundaryRecord=@{enabled=$true;solidMasterPx=$solid;featherMasterPx=$feather;solidOutputPx=[int][Math]::Round($solid*$outputScale);featherOutputPx=[int][Math]::Round($feather*$outputScale);source='master/street-district-master-v2.png';method='Original master collar retained plus inner linear alpha blend; applies to all tile edges including outer map edges. Not an artistic repaint.'}
            }finally{if($refG){$refG.Dispose()};$reference.Dispose();$sourceMaster.Dispose()}
        }
        $patchReport=@()
        foreach($entry in $cfg.seamPatches){
            $r=$entry.destinationRectOutputPx
            if($r.Count-ne 4){throw "Seam $($entry.id) needs destinationRectOutputPx [x,y,w,h]."}
            $dest=[System.Drawing.Rectangle]::new([int]$r[0],[int]$r[1],[int]$r[2],[int]$r[3])
            if($dest.Width-le 0-or$dest.Height-le 0-or$dest.Left-lt 0-or$dest.Top-lt 0-or$dest.Right-gt$output.Width-or$dest.Bottom-gt$output.Height){throw "Invalid seam destination: $($entry.id)"}
            $feather=$(if($null-ne$entry.featherOutputPx){[int]$entry.featherOutputPx}else{60})
            $path=Join-Path $root $entry.file
            $sourcePatch=[System.Drawing.Bitmap]::new($path)
            $patch=[System.Drawing.Bitmap]::new($dest.Width,$dest.Height,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $pg=[System.Drawing.Graphics]::FromImage($patch)
            try{
                $sr=$(if($entry.sourceRectPx){[int[]]$entry.sourceRectPx}else{@(0,0,$sourcePatch.Width,$sourcePatch.Height)})
                if($sr.Count-ne 4-or$sr[0]-lt 0-or$sr[1]-lt 0-or$sr[2]-le 0-or$sr[3]-le 0-or($sr[0]+$sr[2])-gt$sourcePatch.Width-or($sr[1]+$sr[3])-gt$sourcePatch.Height){throw "Invalid seam source rectangle: $($entry.id)"}
                $distortion=[Math]::Abs(($sr[2]/[double]$sr[3])/($dest.Width/[double]$dest.Height)-1)
                if($distortion-gt .02){$warnings+="$($entry.id): repair patch aspect differs by $([Math]::Round($distortion*100,2))%."}
                if($sr[2]-eq$dest.Width-and$sr[3]-eq$dest.Height){
                    $native=$sourcePatch.Clone([System.Drawing.Rectangle]::new($sr[0],$sr[1],$sr[2],$sr[3]),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                    try{$pg.DrawImageUnscaled($native,0,0)}finally{$native.Dispose()}
                }
                else{
                    $pg.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$pg.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                    $pg.DrawImage($sourcePatch,[System.Drawing.Rectangle]::new(0,0,$patch.Width,$patch.Height),$sr[0],$sr[1],$sr[2],$sr[3],[System.Drawing.GraphicsUnit]::Pixel,$imageAttr)
                }
                $pg.Dispose();$pg=$null
                [StreetArtPixelsV003]::BlendPatch($output,$patch,$dest,$feather)
                $item=@{kind='seam-patch';id=$entry.id;file=$entry.file;sourceCanvasPx=@($sourcePatch.Width,$sourcePatch.Height);sourceRectPx=$sr;destinationRectOutputPx=@($dest.X,$dest.Y,$dest.Width,$dest.Height);featherOutputPx=$feather;perimeterPolicy='No feather on sides coinciding with full-canvas edges.';sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant();order=$patchReport.Count}
                $patchReport+=$item;$report+=$item
            }finally{if($pg){$pg.Dispose()};$patch.Dispose();$sourcePatch.Dispose()}
        }
        $output.Save((Join-Path $previewDir 'street-generated-ground-assembled.png'),[System.Drawing.Imaging.ImageFormat]::Png)
        $assembledTileDir=Join-Path $root 'tiles/generated-bounded';[void][System.IO.Directory]::CreateDirectory($assembledTileDir)
        foreach($tileReport in @($report|Where-Object kind -eq 'tile')){CropImage $output $tileReport.destinationRectPx (Join-Path $assembledTileDir "$($tileReport.id).png")}
        foreach($entry in $cfg.overlays){
            $path=Join-Path $root $entry.file;$im=[System.Drawing.Bitmap]::new($path)
            try{
                $sr=$(if($entry.sourceRectPx){[int[]]$entry.sourceRectPx}else{@(0,0,$im.Width,$im.Height)})
                $r=$entry.destinationRectMasterPx
                if($r.Count-ne 4){throw "Overlay $($entry.id) needs destinationRectMasterPx [x,y,w,h]."}
                $dest=[System.Drawing.Rectangle]::new([int][Math]::Round($r[0]*$outputScale),[int][Math]::Round($r[1]*$outputScale),[int][Math]::Round($r[2]*$outputScale),[int][Math]::Round($r[3]*$outputScale))
                if($dest.Width-eq$sr[2]-and$dest.Height-eq$sr[3]){
                    $native=$im.Clone([System.Drawing.Rectangle]::new($sr[0],$sr[1],$sr[2],$sr[3]),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                    try{$og.DrawImageUnscaled($native,$dest.X,$dest.Y)}finally{$native.Dispose()}
                }else{$og.DrawImage($im,$dest,$sr[0],$sr[1],$sr[2],$sr[3],[System.Drawing.GraphicsUnit]::Pixel,$imageAttr)}
                $report+=@{kind='overlay';id=$entry.id;file=$entry.file;sourceRectPx=$sr;destinationRectMasterPx=$r;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
            }finally{$im.Dispose()}
        }
        $output.Save((Join-Path $previewDir 'street-generated-full-assembled.png'),[System.Drawing.Imaging.ImageFormat]::Png)
        WriteJson @{schema='early-rush-street-assembly-report-1';outputCanvasPx=@($output.Width,$output.Height);configuration=$AssemblyConfig;entries=$report;warnings=$warnings;boundaryLock=$boundaryRecord;seamPatches=$patchReport;boundedTileDirectory='tiles/generated-bounded';scope='Offline technical composite, not Unity integration or gameplay validation. Generated seam repairs are alpha composited in recorded order. Resampling alone does not create new detail.'} (Join-Path $metadataDir 'assembly-report.json')
    }finally{$imageAttr.Dispose();$og.Dispose();$output.Dispose()}
}
[pscustomobject]@{root=$root;tiles=8;propCrops=8;assembled=(Test-Path -LiteralPath $AssemblyConfig)}
