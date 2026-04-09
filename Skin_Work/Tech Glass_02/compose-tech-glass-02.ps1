Add-Type -AssemblyName System.Drawing

$workDir = 'D:\Codex\TrafficView_Moi\TrafficView 1.4.12\Skin_Work\Tech Glass_02'
$basePath = Join-Path $workDir 'Tech Glass_base_1530x840.png'
$rawPath = Join-Path $workDir 'Tech Glass_02_comfyui_raw.png'
$finalPath = Join-Path $workDir 'Tech Glass_02.png'
$previewPath = Join-Path $workDir 'Tech Glass_02_preview.png'
$specPath = Join-Path $workDir 'Tech Glass_02_spec.txt'
$skinDir = 'D:\Codex\TrafficView_Moi\TrafficView 1.4.12\Skins\24'
$distSkinDir = 'D:\Codex\TrafficView_Moi\TrafficView 1.4.12\dist\Skins\24'

function Get-Luminance($color) { return (0.2126 * $color.R) + (0.7152 * $color.G) + (0.0722 * $color.B) }
function Clamp01([double]$value) { if ($value -lt 0.0) { return 0.0 }; if ($value -gt 1.0) { return 1.0 }; return $value }
function Get-EdgeDistance {
    param([bool[,]]$opaqueMask,[int]$width,[int]$height,[int]$x,[int]$y,[double]$maxRadius)
    $limit = [int][Math]::Ceiling($maxRadius)
    $best = [double]::PositiveInfinity
    for ($dy = -$limit; $dy -le $limit; $dy++) {
        $yy = $y + $dy
        if ($yy -lt 0 -or $yy -ge $height) { continue }
        for ($dx = -$limit; $dx -le $limit; $dx++) {
            $xx = $x + $dx
            if ($xx -lt 0 -or $xx -ge $width) { continue }
            if ($opaqueMask[$xx,$yy]) { continue }
            $distance = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
            if ($distance -lt $best) { $best = $distance }
        }
    }
    return $best
}

$base = New-Object System.Drawing.Bitmap($basePath)
$rawPad = New-Object System.Drawing.Bitmap($rawPath)
$raw = New-Object System.Drawing.Bitmap(1530, 840, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$out = New-Object System.Drawing.Bitmap(1530, 840, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $gRaw = [System.Drawing.Graphics]::FromImage($raw)
    $gRaw.DrawImage($rawPad, (New-Object System.Drawing.Rectangle(0,0,1530,840)), 3, 0, 1530, 840, [System.Drawing.GraphicsUnit]::Pixel)
    $gRaw.Dispose()

    $width = $base.Width
    $height = $base.Height
    $opaqueMask = New-Object 'bool[,]' $width, $height
    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $opaqueMask[$x,$y] = $base.GetPixel($x,$y).A -ge 12
        }
    }

    $shadowBitmap = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y = 0; $y -lt $height; $y++) {
            for ($x = 0; $x -lt $width; $x++) {
                $shadowAlpha = 0.0
                $samples = @(
                    @{ X = $x;     Y = $y - 4; W = 0.10 },
                    @{ X = $x;     Y = $y - 3; W = 0.06 },
                    @{ X = $x - 1; Y = $y - 4; W = 0.03 },
                    @{ X = $x + 1; Y = $y - 4; W = 0.03 },
                    @{ X = $x;     Y = $y - 5; W = 0.02 }
                )
                foreach ($sample in $samples) {
                    if ($sample.X -lt 0 -or $sample.X -ge $width -or $sample.Y -lt 0 -or $sample.Y -ge $height) { continue }
                    $alpha = $base.GetPixel($sample.X, $sample.Y).A / 255.0
                    $shadowAlpha += $alpha * $sample.W
                }
                $shadowAlpha = Clamp01 $shadowAlpha
                if ($shadowAlpha -le 0.001) {
                    $shadowBitmap.SetPixel($x,$y,[System.Drawing.Color]::Transparent)
                } else {
                    $shadowByte = [int][Math]::Round(255 * $shadowAlpha)
                    $shadowBitmap.SetPixel($x,$y,[System.Drawing.Color]::FromArgb($shadowByte, 4, 10, 18))
                }
            }
        }
        $gOut = [System.Drawing.Graphics]::FromImage($out)
        $gOut.Clear([System.Drawing.Color]::Transparent)
        $gOut.DrawImageUnscaled($shadowBitmap,0,0)
        $gOut.Dispose()
    } finally { $shadowBitmap.Dispose() }

    $edgeProtectRadius = 3.4
    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $basePixel = $base.GetPixel($x,$y)
            if ($basePixel.A -le 0) { continue }
            $rawPixel = $raw.GetPixel($x,$y)
            $edgeDistance = Get-EdgeDistance -opaqueMask $opaqueMask -width $width -height $height -x $x -y $y -maxRadius $edgeProtectRadius
            if (-not [double]::IsPositiveInfinity($edgeDistance) -and $edgeDistance -le $edgeProtectRadius) {
                $out.SetPixel($x,$y,$basePixel)
                continue
            }
            $lum = Get-Luminance $rawPixel
            $highlight = Clamp01 (($lum - 138.0) / 92.0)
            $centerWeight = 1.0
            if ($x -lt ($width * 0.38)) { $centerWeight = 0.42 } elseif ($x -lt ($width * 0.55)) { $centerWeight = 0.75 }
            $highlight *= $centerWeight
            $diag = ($x / [double]$width) * 0.9 + ($y / [double]$height) * 0.3
            $band1 = [Math]::Exp(-[Math]::Pow(($diag - 0.34) / 0.055, 2.0)) * 0.22
            $band2 = [Math]::Exp(-[Math]::Pow(($diag - 0.79) / 0.06, 2.0)) * 0.14
            $manualHighlight = [Math]::Min(1.0, $band1 + $band2)
            if ($x -lt ($width * 0.36)) { $manualHighlight *= 0.45 }
            $combinedHighlight = Clamp01 ($highlight * 0.72 + $manualHighlight)
            $reflectionTintR = [Math]::Min(255.0, 220.0 + ($rawPixel.R * 0.15))
            $reflectionTintG = [Math]::Min(255.0, 232.0 + ($rawPixel.G * 0.10))
            $reflectionTintB = 250.0
            $screenOpacity = Clamp01 ($combinedHighlight * 0.72)
            $r = 255.0 - ((255.0 - $basePixel.R) * (255.0 - $reflectionTintR * $screenOpacity) / 255.0)
            $g = 255.0 - ((255.0 - $basePixel.G) * (255.0 - $reflectionTintG * $screenOpacity) / 255.0)
            $b = 255.0 - ((255.0 - $basePixel.B) * (255.0 - $reflectionTintB * $screenOpacity) / 255.0)
            $contrastBoost = 1.0 + ($combinedHighlight * 0.10)
            $r = [Math]::Max(0.0, [Math]::Min(255.0, (($r - 128.0) * $contrastBoost) + 128.0))
            $g = [Math]::Max(0.0, [Math]::Min(255.0, (($g - 128.0) * $contrastBoost) + 128.0))
            $b = [Math]::Max(0.0, [Math]::Min(255.0, (($b - 128.0) * $contrastBoost) + 128.0))
            $out.SetPixel($x,$y,[System.Drawing.Color]::FromArgb($basePixel.A, [int][Math]::Round($r), [int][Math]::Round($g), [int][Math]::Round($b)))
        }
    }

    if (Test-Path $finalPath) { Remove-Item -LiteralPath $finalPath -Force }
    if (Test-Path $previewPath) { Remove-Item -LiteralPath $previewPath -Force }
    $out.Save($finalPath, [System.Drawing.Imaging.ImageFormat]::Png)
    Copy-Item $finalPath $previewPath -Force

    $spec = @(
        'Name: Tech Glass_02',
        'Basis: Tech Glass',
        'Methode: ComfyUI Image-to-Image / Post-Processing',
        'Hauptänderung: stärkere Sonnen-/Glasreflexion',
        'Zusatz: 4 px Desktop-Schatten',
        'Rand: gestochen scharf'
    )
    Set-Content -Path $specPath -Value $spec -Encoding UTF8

    New-Item -ItemType Directory -Force -Path $skinDir | Out-Null
    New-Item -ItemType Directory -Force -Path $distSkinDir | Out-Null

    $sizes = @(
        @{ File='TrafficView.panel.90.png';  Width=92;  Height=50 },
        @{ File='TrafficView.panel.png';     Width=102; Height=56 },
        @{ File='TrafficView.panel.110.png'; Width=112; Height=62 },
        @{ File='TrafficView.panel.125.png'; Width=128; Height=70 },
        @{ File='TrafficView.panel.150.png'; Width=153; Height=84 }
    )
    foreach ($size in $sizes) {
        $scaled = New-Object System.Drawing.Bitmap($size.Width, $size.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $g = [System.Drawing.Graphics]::FromImage($scaled)
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $g.Clear([System.Drawing.Color]::Transparent)
            $g.DrawImage($out,0,0,$size.Width,$size.Height)
            $g.Dispose()
            $skinOut = Join-Path $skinDir $size.File
            if (Test-Path $skinOut) { Remove-Item -LiteralPath $skinOut -Force }
            $scaled.Save($skinOut,[System.Drawing.Imaging.ImageFormat]::Png)
            Copy-Item $skinOut (Join-Path $distSkinDir $size.File) -Force
        } finally { $scaled.Dispose() }
    }
} finally {
    $base.Dispose(); $rawPad.Dispose(); $raw.Dispose(); $out.Dispose()
}
Write-Host 'OK'
