Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

function New-Bitmap {
    param([int]$Width, [int]$Height)
    return [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function Save-Png {
    param([System.Drawing.Image]$Image, [string]$Path)
    $dir = Split-Path -Parent $Path
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $Image.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Resize-Image {
    param([System.Drawing.Image]$Source, [int]$Width, [int]$Height)
    $bitmap = New-Bitmap -Width $Width -Height $Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.DrawImage($Source, 0, 0, $Width, $Height)
    $graphics.Dispose()
    return $bitmap
}

function Crop-NonWhiteBounds {
    param([System.Drawing.Bitmap]$Source)

    $minX = $Source.Width
    $minY = $Source.Height
    $maxX = 0
    $maxY = 0

    for ($y = 0; $y -lt $Source.Height; $y++) {
        for ($x = 0; $x -lt $Source.Width; $x++) {
            $pixel = $Source.GetPixel($x, $y)
            if ($pixel.A -gt 0 -and -not ($pixel.R -ge 245 -and $pixel.G -ge 245 -and $pixel.B -ge 245)) {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    if ($maxX -le $minX -or $maxY -le $minY) {
        throw 'Kein nicht-weisser Bereich im Referenzbild gefunden.'
    }

    return @{
        X = $minX
        Y = $minY
        Width = ($maxX - $minX + 1)
        Height = ($maxY - $minY + 1)
    }
}

function Crop-Image {
    param([System.Drawing.Bitmap]$Source, [int]$X, [int]$Y, [int]$Width, [int]$Height)
    $target = New-Bitmap -Width $Width -Height $Height
    $graphics = [System.Drawing.Graphics]::FromImage($target)
    $graphics.DrawImage($Source, [System.Drawing.Rectangle]::new(0,0,$Width,$Height), [System.Drawing.Rectangle]::new($X,$Y,$Width,$Height), [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    return $target
}

function New-RoundedRectPath {
    param([float]$X,[float]$Y,[float]$Width,[float]$Height,[float]$Radius)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Blend-Color {
    param([System.Drawing.Color]$BaseColor, [System.Drawing.Color]$OverlayColor, [double]$Strength)
    $alpha = ($OverlayColor.A / 255.0) * $Strength
    $inv = 1.0 - $alpha
    return [System.Drawing.Color]::FromArgb(
        [int][Math]::Min(255, [Math]::Round($BaseColor.A * $inv + 255 * $alpha)),
        [int][Math]::Min(255, [Math]::Round($BaseColor.R * $inv + $OverlayColor.R * $alpha)),
        [int][Math]::Min(255, [Math]::Round($BaseColor.G * $inv + $OverlayColor.G * $alpha)),
        [int][Math]::Min(255, [Math]::Round($BaseColor.B * $inv + $OverlayColor.B * $alpha))
    )
}

$root = 'D:\Codex\TrafficView_Moi\TrafficView 1.4.12\Skin_Work\Templates'
$outputDir = Join-Path $root 'output'
$workflowPath = Join-Path $root 'ComfyUI_Anzeigentafel_Grundtafel_51x28_v3_controlled.json'
$sourcePath = 'D:\Codex\TrafficView_Moi\Futuristisches Widget mit Geschwindigkeitsanzeige.png'
$comfyInputDir = 'D:\Codex\!Grafikhilfen\ComfyUI\ComfyUI\input'
$comfyOutputDir = 'D:\Codex\!Grafikhilfen\ComfyUI\ComfyUI\output'
$controlledBasePath = Join-Path $outputDir 'Anzeigentafel_Grundtafel_51x28_v3_controlled_base.png'
$finalPath = Join-Path $outputDir 'Anzeigentafel_Grundtafel_51x28_v3_controlled.png'
$previewPath = Join-Path $outputDir 'Anzeigentafel_Grundtafel_51x28_v3_controlled_preview.png'
$rawRenderPath = Join-Path $outputDir 'Anzeigentafel_Grundtafel_51x28_v3_controlled_raw.png'
$paddedInputPath = Join-Path $comfyInputDir 'Anzeigentafel_Grundtafel_51x28_v3_controlled_input.png'

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$src = [System.Drawing.Bitmap]::FromFile($sourcePath)
$bounds = Crop-NonWhiteBounds -Source $src
$cropped = Crop-Image -Source $src -X $bounds.X -Y $bounds.Y -Width $bounds.Width -Height $bounds.Height
$scale = [Math]::Max(1530 / [double]$cropped.Width, 840 / [double]$cropped.Height)
$scaled = Resize-Image -Source $cropped -Width ([int][Math]::Round($cropped.Width * $scale)) -Height ([int][Math]::Round($cropped.Height * $scale))
$base = Crop-Image -Source $scaled -X ([int][Math]::Max(0, [Math]::Round(($scaled.Width - 1530) / 2))) -Y ([int][Math]::Max(0, [Math]::Round(($scaled.Height - 840) / 2))) -Width 1530 -Height 840

$canvas = Resize-Image -Source $base -Width 1530 -Height 840
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

# Linke ruhige Textplatte
$leftPath = New-RoundedRectPath -X 18 -Y 92 -Width 725 -Height 525 -Radius 56
$leftBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    [System.Drawing.Point]::new(18,92),
    [System.Drawing.Point]::new(743,617),
    [System.Drawing.Color]::FromArgb(255, 9, 20, 31),
    [System.Drawing.Color]::FromArgb(255, 6, 14, 23)
)
$g.FillPath($leftBrush, $leftPath)
$leftPenOuter = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160, 152, 244, 255), 5)
$leftPenInner = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90, 255, 255, 255), 2)
$g.DrawPath($leftPenOuter, $leftPath)
$innerLeftPath = New-RoundedRectPath -X 38 -Y 112 -Width 685 -Height 485 -Radius 42
$g.DrawPath($leftPenInner, $innerLeftPath)

# DL- und UL-Zone zusätzlich abdunkeln
$dlRect = [System.Drawing.Rectangle]::new(82, 132, 615, 145)
$ulRect = [System.Drawing.Rectangle]::new(82, 352, 615, 145)
$metricBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(235, 3, 9, 17))
$g.FillRectangle($metricBrush, $dlRect)
$g.FillRectangle($metricBrush, $ulRect)

# Untere Graph-Trägerfläche
$graphPath = New-RoundedRectPath -X 46 -Y 628 -Width 690 -Height 102 -Radius 28
$graphBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(245, 8, 18, 27))
$graphPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(95, 87, 234, 255), 2)
$g.FillPath($graphBrush, $graphPath)
$g.DrawPath($graphPen, $graphPath)

# Rechte Kreis-Trägerfläche - neutral, ohne Pfeile
$circleOuterRect = [System.Drawing.Rectangle]::new(860, 135, 520, 520)
$circleInnerRect = [System.Drawing.Rectangle]::new(940, 215, 360, 360)
$ringFillBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70, 10, 24, 38))
$g.FillEllipse($ringFillBrush, $circleOuterRect)
$ringPenOuter = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(185, 94, 142, 255), 10)
$ringPenMid = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(115, 36, 230, 255), 24)
$ringPenInner = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(150, 255, 255, 255), 3)
$g.DrawEllipse($ringPenMid, [System.Drawing.Rectangle]::new(915, 190, 410, 410))
$g.DrawEllipse($ringPenOuter, [System.Drawing.Rectangle]::new(900, 175, 440, 440))
$g.FillEllipse((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 9, 17, 29))), $circleInnerRect)
$g.DrawEllipse($ringPenInner, $circleInnerRect)

# Subtile Segmentmarken
for ($i = 0; $i -lt 16; $i++) {
    $angle = ($i / 16.0) * [Math]::PI * 2.0
    $cx = 1120
    $cy = 395
    $innerR = 200
    $outerR = 235
    $x1 = $cx + [Math]::Cos($angle) * $innerR
    $y1 = $cy + [Math]::Sin($angle) * $innerR
    $x2 = $cx + [Math]::Cos($angle) * $outerR
    $y2 = $cy + [Math]::Sin($angle) * $outerR
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(165, 168, 227, 255), 4)
    $g.DrawLine($pen, [float]$x1, [float]$y1, [float]$x2, [float]$y2)
    $pen.Dispose()
}

# Zusätzliche obere Lichtkante
$topHighlight = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(75, 255, 255, 255), 8)
$g.DrawArc($topHighlight, 60, 22, 1410, 92, 180, 180)
$g.Dispose()

Save-Png -Image $canvas -Path $controlledBasePath

$padded = New-Bitmap -Width 1536 -Height 840
$pg = [System.Drawing.Graphics]::FromImage($padded)
$pg.Clear([System.Drawing.Color]::Transparent)
$pg.DrawImage($canvas, 3, 0, 1530, 840)
$pg.Dispose()
Save-Png -Image $padded -Path $paddedInputPath

$workflow = @{
    prompt = @{
        '1' = @{
            class_type = 'CheckpointLoaderSimple'
            inputs = @{ ckpt_name = 'sd_xl_base_1.0.safetensors' }
        }
        '2' = @{
            class_type = 'LoadImage'
            inputs = @{ image = 'Anzeigentafel_Grundtafel_51x28_v3_controlled_input.png'; upload = 'image' }
        }
        '3' = @{
            class_type = 'CLIPTextEncode'
            inputs = @{
                clip = @('1',1)
                text = 'premium desktop overlay baseplate, same layout, polished dark blue cyan glass panel, controlled reflective material, subtle glossy highlights only on background and border, clean left metric carrier plate, empty right circular HUD carrier, no text, no numbers, no arrows, no pseudo widgets, realistic software surface, crisp edges, restrained reflections, professional traffic overlay background'
            }
        }
        '4' = @{
            class_type = 'CLIPTextEncode'
            inputs = @{
                clip = @('1',1)
                text = 'text, letters, numbers, arrows, icons, fake UI widgets, clutter, fantasy interface, anime, cartoon, blur, edge noise, extra panels, overexposed highlights, unreadable overlay zones, pseudo dashboard'
            }
        }
        '5' = @{
            class_type = 'VAEEncode'
            inputs = @{ pixels = @('2',0); vae = @('1',2) }
        }
        '6' = @{
            class_type = 'KSampler'
            inputs = @{
                model = @('1',0)
                seed = 320044120
                steps = 24
                cfg = 5.6
                sampler_name = 'dpmpp_2m'
                scheduler = 'karras'
                denoise = 0.18
                positive = @('3',0)
                negative = @('4',0)
                latent_image = @('5',0)
            }
        }
        '7' = @{
            class_type = 'VAEDecode'
            inputs = @{ samples = @('6',0); vae = @('1',2) }
        }
        '8' = @{
            class_type = 'SaveImage'
            inputs = @{ filename_prefix = 'Anzeigentafel_Grundtafel_51x28_v3_controlled'; images = @('7',0) }
        }
    }
} | ConvertTo-Json -Depth 10

Set-Content -Path $workflowPath -Value $workflow -Encoding UTF8

$start = Get-Date
Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:8188/prompt' -ContentType 'application/json' -Body $workflow | Out-Null
$render = $null
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 2
    $candidate = Get-ChildItem $comfyOutputDir -Filter 'Anzeigentafel_Grundtafel_51x28_v3_controlled*.png' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -ge $start } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($candidate) {
        $render = $candidate.FullName
        break
    }
}

if (-not $render) {
    $candidate = Get-ChildItem $comfyOutputDir -Filter 'Anzeigentafel_Grundtafel_51x28_v3_controlled*.png' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($candidate) { $render = $candidate.FullName }
}

if (-not $render) {
    throw 'Kein Render fuer Anzeigentafel_Grundtafel_51x28_v3_controlled gefunden.'
}

Copy-Item $render $rawRenderPath -Force
$raw = [System.Drawing.Bitmap]::FromFile($rawRenderPath)
$rawCropped = Crop-Image -Source $raw -X 3 -Y 0 -Width 1530 -Height 840

$final = Resize-Image -Source $canvas -Width 1530 -Height 840
for ($y = 0; $y -lt 840; $y++) {
    for ($x = 0; $x -lt 1530; $x++) {
        $basePx = $canvas.GetPixel($x, $y)
        $rawPx = $rawCropped.GetPixel($x, $y)
        if ($basePx.A -eq 0) { continue }

        $strength = 0.0
        $brightness = ($rawPx.R + $rawPx.G + $rawPx.B) / 3.0

        $inMetricCore = ($x -ge 140 -and $x -le 700 -and $y -ge 120 -and $y -le 535)
        $inGraphCore = ($x -ge 120 -and $x -le 720 -and $y -ge 640 -and $y -le 730)
        $inCircleCore = ($x -ge 920 -and $x -le 1320 -and $y -ge 195 -and $y -le 595)

        if ($inMetricCore -or $inGraphCore -or $inCircleCore) {
            $strength = 0.0
        } elseif ($brightness -gt 150) {
            $strength = 0.18
        } elseif ($brightness -gt 115) {
            $strength = 0.10
        }

        if ($strength -gt 0) {
            $final.SetPixel($x, $y, (Blend-Color -BaseColor $basePx -OverlayColor $rawPx -Strength $strength))
        }
    }
}

Save-Png -Image $final -Path $finalPath
Save-Png -Image $final -Path $previewPath

$src.Dispose()
$cropped.Dispose()
$scaled.Dispose()
$base.Dispose()
$canvas.Dispose()
$padded.Dispose()
$raw.Dispose()
$rawCropped.Dispose()
$final.Dispose()

Write-Output "BASE=$controlledBasePath"
Write-Output "RAW=$rawRenderPath"
Write-Output "FINAL=$finalPath"
