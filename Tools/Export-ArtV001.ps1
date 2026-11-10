#requires -Version 5.1
<#
.SYNOPSIS
Export the v001 art candidates without altering their generated source images.
.DESCRIPTION
The configuration owns every scale and source anchor. This script never trims,
repaints, removes a background, changes colour, or adjusts animation timing.
Its report is a technical inspection, not visual or in-game acceptance.
#>
[CmdletBinding()]
param(
    [string]$Root = (Join-Path (Split-Path -Parent $PSScriptRoot) 'EarlyRush_Art_v001')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not ('EarlyRushArtV001.PixelInspector' -as [type])) {
    $drawingReferences = @(
        [System.Drawing.Bitmap].Assembly.Location,
        [System.Drawing.Color].Assembly.Location,
        [System.Object].Assembly.Location
    ) | Select-Object -Unique
    Add-Type -ReferencedAssemblies $drawingReferences -TypeDefinition @'
using System;
using System.Drawing;

namespace EarlyRushArtV001 {
    public sealed class PixelReport {
        public int Width;
        public int Height;
        public long PixelCount;
        public long AlphaZeroCount;
        public double AlphaZeroRatio;
        public long AlphaAtLeast128Count;
        public int[] AlphaAtLeast128Bounds;
        public int EdgeAlphaMax;
        public int[] CornerAlpha;
        public double LeftRightMeanAbsoluteRgbDifference;
        public double TopBottomMeanAbsoluteRgbDifference;
    }

    public static class PixelInspector {
        public static PixelReport Analyze(Bitmap image) {
            PixelReport report = new PixelReport();
            report.Width = image.Width;
            report.Height = image.Height;
            report.PixelCount = (long)image.Width * image.Height;
            int minX = image.Width, minY = image.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < image.Height; y++) {
                for (int x = 0; x < image.Width; x++) {
                    Color colour = image.GetPixel(x, y);
                    if (colour.A == 0) report.AlphaZeroCount++;
                    if (colour.A >= 128) {
                        report.AlphaAtLeast128Count++;
                        minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                    }
                    if (x == 0 || y == 0 || x == image.Width - 1 || y == image.Height - 1)
                        report.EdgeAlphaMax = Math.Max(report.EdgeAlphaMax, (int)colour.A);
                }
            }
            report.AlphaZeroRatio = (double)report.AlphaZeroCount / report.PixelCount;
            report.AlphaAtLeast128Bounds = maxX < 0 ? null : new int[] {
                minX, minY, maxX - minX + 1, maxY - minY + 1
            };
            report.CornerAlpha = new int[] {
                image.GetPixel(0, 0).A,
                image.GetPixel(image.Width - 1, 0).A,
                image.GetPixel(0, image.Height - 1).A,
                image.GetPixel(image.Width - 1, image.Height - 1).A
            };
            double horizontal = 0, vertical = 0;
            for (int y = 0; y < image.Height; y++) {
                Color a = image.GetPixel(0, y), b = image.GetPixel(image.Width - 1, y);
                horizontal += Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
            }
            for (int x = 0; x < image.Width; x++) {
                Color a = image.GetPixel(x, 0), b = image.GetPixel(x, image.Height - 1);
                vertical += Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
            }
            report.LeftRightMeanAbsoluteRgbDifference = horizontal / (image.Height * 3.0);
            report.TopBottomMeanAbsoluteRgbDifference = vertical / (image.Width * 3.0);
            return report;
        }
    }
}
'@
}

$packageRoot = [System.IO.Path]::GetFullPath($Root)
$rootPrefix = $packageRoot.TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
$configPath = Join-Path $packageRoot 'metadata/export-config.json'
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Export configuration is missing: $configPath"
}
$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $config.PSObject.Properties['assets'] -or @($config.assets).Count -eq 0) {
    throw "Configuration must contain a non-empty assets array: $configPath"
}

function Resolve-PackagePath {
    param([string]$RelativePath)
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "Expected a non-empty package-relative path, received: '$RelativePath'"
    }
    $resolved = [System.IO.Path]::GetFullPath((Join-Path $packageRoot $RelativePath))
    if (-not $resolved.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes the art package: '$RelativePath'"
    }
    return $resolved
}

function Get-PixelMetadata {
    param([System.Drawing.Bitmap]$Bitmap, [string]$Kind)
    $pixels = [EarlyRushArtV001.PixelInspector]::Analyze($Bitmap)
    $record = [ordered]@{
        canvasPx = @($pixels.Width, $pixels.Height)
        pixelCount = $pixels.PixelCount
        alphaZeroCount = $pixels.AlphaZeroCount
        alphaZeroRatio = [Math]::Round($pixels.AlphaZeroRatio, 6)
        alphaAtLeast128Count = $pixels.AlphaAtLeast128Count
        alphaAtLeast128BoundsTopLeftXYWH = $pixels.AlphaAtLeast128Bounds
        edgeAlphaMax = $pixels.EdgeAlphaMax
        cornerAlphaTopLeftTopRightBottomLeftBottomRight = $pixels.CornerAlpha
    }
    if ($Kind -eq 'floor') {
        $record['borderMeanAbsoluteRgbDifference0To255'] = [ordered]@{
            leftRight = [Math]::Round($pixels.LeftRightMeanAbsoluteRgbDifference, 4)
            topBottom = [Math]::Round($pixels.TopBottomMeanAbsoluteRgbDifference, 4)
            interpretation = 'Border statistics only; not proof of seamless tiling. Inspect a repeated preview and the game.'
        }
    }
    return $record
}

# Preflight the complete list before writing any output, especially source paths.
$prepared = @()
$seenIds = @{}
$seenOutputs = @{}
$sourcePaths = @{}
foreach ($asset in $config.assets) {
    foreach ($field in @('id', 'source', 'output', 'canvasPx', 'pivotNormalizedBottomLeft', 'sourceAnchorPx', 'scale', 'kind', 'worldWidth', 'sourceId', 'integration')) {
        if (-not $asset.PSObject.Properties[$field]) { throw "Asset is missing field '$field'." }
    }
    $id = [string]$asset.id
    if ([string]::IsNullOrWhiteSpace($id) -or $seenIds.ContainsKey($id)) { throw "Empty or duplicate asset ID: '$id'" }
    $seenIds[$id] = $true
    if ($asset.kind -notin @('sprite', 'floor')) { throw "Unsupported kind for '$id': $($asset.kind)" }
    if ($asset.integration -notin @('A', 'B')) { throw "Unsupported integration class for '$id': $($asset.integration)" }
    if (@($asset.canvasPx).Count -ne 2 -or @($asset.pivotNormalizedBottomLeft).Count -ne 2 -or @($asset.sourceAnchorPx).Count -ne 2) {
        throw "Canvas, pivot and source anchor must each contain two values: '$id'"
    }
    $width = [int]$asset.canvasPx[0]
    $height = [int]$asset.canvasPx[1]
    if ($width -lt 1 -or $height -lt 1 -or $width -gt 2048 -or $height -gt 2048 -or
        [double]$asset.canvasPx[0] -ne $width -or [double]$asset.canvasPx[1] -ne $height) {
        throw "Canvas must use integer dimensions from 1 through 2048 pixels: '$id'"
    }
    if ($asset.kind -eq 'floor' -and ($width -ne 512 -or $height -ne 512)) {
        throw "This delivery requires a 512x512 floor canvas: '$id'"
    }
    foreach ($coordinate in $asset.pivotNormalizedBottomLeft) {
        $value = [double]$coordinate
        if ([double]::IsNaN($value) -or [double]::IsInfinity($value) -or $value -lt 0 -or $value -gt 1) {
            throw "Pivot must be finite and normalized to [0,1]: '$id'"
        }
    }
    foreach ($coordinate in $asset.sourceAnchorPx) {
        if ([double]::IsNaN([double]$coordinate) -or [double]::IsInfinity([double]$coordinate)) {
            throw "Source anchor must be finite: '$id'"
        }
    }
    $scale = [double]$asset.scale
    if ([double]::IsNaN($scale) -or [double]::IsInfinity($scale) -or $scale -le 0) {
        throw "Scale must be positive and finite: '$id'"
    }
    $sourcePath = Resolve-PackagePath ([string]$asset.source)
    $outputPath = Resolve-PackagePath ([string]$asset.output)
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Source image is missing for '$id': $sourcePath" }
    if ([System.IO.Path]::GetExtension($outputPath) -ine '.png') { throw "Output must be PNG: '$id'" }
    if ($seenOutputs.ContainsKey($outputPath)) { throw "Duplicate output path: $outputPath" }
    $seenOutputs[$outputPath] = $true
    $sourcePaths[$sourcePath] = $true
    $prepared += [pscustomobject]@{ Asset = $asset; SourcePath = $sourcePath; OutputPath = $outputPath }
}
foreach ($item in $prepared) {
    if ($sourcePaths.ContainsKey($item.OutputPath)) { throw "Export would overwrite a source image: $($item.OutputPath)" }
}

$manifestAssets = @()
$validationAssets = @()
$totalWarnings = 0
foreach ($item in $prepared) {
    $asset = $item.Asset
    $source = $null
    $canvas = $null
    $graphics = $null
    $attributes = $null
    try {
        $source = [System.Drawing.Bitmap]::new($item.SourcePath)
        $sourceMetadata = Get-PixelMetadata -Bitmap $source -Kind $asset.kind
        $width = [int]$asset.canvasPx[0]
        $height = [int]$asset.canvasPx[1]
        $canvas = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($canvas)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
        $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)

        $targetPivot = @(
            ([double]$asset.pivotNormalizedBottomLeft[0] * $width),
            ((1.0 - [double]$asset.pivotNormalizedBottomLeft[1]) * $height)
        )
        if ($asset.kind -eq 'floor') {
            $destination = [System.Drawing.RectangleF]::new(0, 0, $width, $height)
            $transform = [ordered]@{ mode = 'full-image-resize'; scaleXY = @(($width / [double]$source.Width), ($height / [double]$source.Height)); offsetPx = @(0, 0) }
        } else {
            $scale = [double]$asset.scale
            $offsetX = $targetPivot[0] - [double]$asset.sourceAnchorPx[0] * $scale
            $offsetY = $targetPivot[1] - [double]$asset.sourceAnchorPx[1] * $scale
            $destination = [System.Drawing.RectangleF]::new([single]$offsetX, [single]$offsetY, [single]($source.Width * $scale), [single]($source.Height * $scale))
            $transform = [ordered]@{ mode = 'uniform-scale-and-anchor-translation'; scaleXY = @($scale, $scale); offsetPx = @($offsetX, $offsetY); sourceAnchorPx = @($asset.sourceAnchorPx); targetAnchorTopLeftPx = $targetPivot; automaticTrim = $false }
        }
        # The three destination points preserve subpixel placement and uniform sprite scale.
        $destinationPoints = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($destination.Left, $destination.Top),
            [System.Drawing.PointF]::new($destination.Right, $destination.Top),
            [System.Drawing.PointF]::new($destination.Left, $destination.Bottom)
        )
        $graphics.DrawImage($source, $destinationPoints, [System.Drawing.RectangleF]::new(0, 0, $source.Width, $source.Height), [System.Drawing.GraphicsUnit]::Pixel, $attributes)
        $graphics.Dispose()
        $graphics = $null
        $outputDirectory = Split-Path -Parent $item.OutputPath
        [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
        $canvas.Save($item.OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $outputMetadata = Get-PixelMetadata -Bitmap $canvas -Kind $asset.kind

        $warnings = @()
        if ($asset.kind -eq 'sprite') {
            if ($outputMetadata.edgeAlphaMax -gt 16) { $warnings += 'Sprite has alpha > 16 on its outer edge; review clipping and safety margins.' }
            if (@($outputMetadata.cornerAlphaTopLeftTopRightBottomLeftBottomRight | Where-Object { $_ -gt 16 }).Count -gt 0) {
                $warnings += 'One or more sprite corners have alpha > 16; review background transparency.'
            }
            if ($outputMetadata.alphaAtLeast128Count -eq 0) { $warnings += 'No sprite pixels have alpha >= 128; inspect the source and transform.' }
        }
        $totalWarnings += $warnings.Count
        $sourceSha = (Get-FileHash -LiteralPath $item.SourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $outputSha = (Get-FileHash -LiteralPath $item.OutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $manifestAssets += [ordered]@{
            id = $asset.id
            sourceId = $asset.sourceId
            source = $asset.source
            sourceSha256 = $sourceSha
            sourceCanvasPx = $sourceMetadata.canvasPx
            output = $asset.output
            outputSha256 = $outputSha
            canvasPx = @($width, $height)
            kind = $asset.kind
            integration = $asset.integration
            pivotNormalizedBottomLeft = @($asset.pivotNormalizedBottomLeft)
            worldWidth = $asset.worldWidth
            transform = $transform
            alphaEncoding = 'straight RGBA; PNG; no alpha cleanup or background removal'
            acceptance = 'candidate; technical export only; visual and in-game acceptance pending'
        }
        $validationAssets += [ordered]@{
            id = $asset.id
            source = $asset.source
            output = $asset.output
            sourceSha256 = $sourceSha
            outputSha256 = $outputSha
            sourceImage = $sourceMetadata
            exportedImage = $outputMetadata
            warnings = @($warnings)
        }
        Write-Host ("Exported {0}: {1}x{2}; warnings={3}" -f $asset.id, $width, $height, $warnings.Count)
    } finally {
        if ($graphics) { $graphics.Dispose() }
        if ($attributes) { $attributes.Dispose() }
        if ($canvas) { $canvas.Dispose() }
        if ($source) { $source.Dispose() }
    }
}

$generatedAt = [DateTimeOffset]::UtcNow.ToString('o')
$manifest = [ordered]@{
    schema = 'early-rush-art-manifest-1'
    generatedAtUtc = $generatedAt
    configuration = 'metadata/export-config.json'
    configurationSha256 = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash.ToLowerInvariant()
    assetCount = $manifestAssets.Count
    assets = $manifestAssets
}
$validation = [ordered]@{
    schema = 'early-rush-art-validation-1'
    generatedAtUtc = $generatedAt
    status = $(if ($totalWarnings -gt 0) { 'exported-with-review-warnings' } else { 'technical-checks-passed' })
    warningCount = $totalWarnings
    scope = 'Dimensions, alpha bounds/corners/edges and hashes only. No assertion of art quality, anchor correctness, animation coherence, seamlessness or in-game acceptance.'
    boundsConvention = 'Top-left origin, inclusive minimum, [x,y,width,height], threshold alpha >= 128.'
    assets = $validationAssets
}
$metadataDirectory = Join-Path $packageRoot 'metadata'
[System.IO.Directory]::CreateDirectory($metadataDirectory) | Out-Null
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText((Join-Path $metadataDirectory 'manifest.json'), ($manifest | ConvertTo-Json -Depth 20), $utf8NoBom)
[System.IO.File]::WriteAllText((Join-Path $metadataDirectory 'validation.json'), ($validation | ConvertTo-Json -Depth 20), $utf8NoBom)
Write-Host ("Export complete: {0} candidates, {1} technical warning(s). Visual and in-game acceptance remain pending." -f $manifestAssets.Count, $totalWarnings)
