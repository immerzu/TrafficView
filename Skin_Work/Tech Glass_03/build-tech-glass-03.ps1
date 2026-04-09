Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

function New-Bitmap {
    param(
        [int]$Width,
        [int]$Height
    )

    return [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function Save-Png {
    param(
        [System.Drawing.Image]$Image,
        [string]$Path
    )

    $dir = Split-Path -Parent $Path
    if ($dir) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }

    $Image.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Resize-Image {
    param(
        [System.Drawing.Image]$Source,
        [int]$Width,
        [int]$Height
    )

    $bitmap = New-Bitmap -Width $Width -Height $Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.DrawImage($Source, 0, 0, $Width, $Height)
    $graphics.Dispose()
    return $bitmap
}

function Crop-Image {
    param(
        [System.Drawing.Bitmap]$Source,
        [int]$X,
        [int]$Y,
        [int]$Width,
        [int]$Height
    )

    $target = New-Bitmap -Width $Width -Height $Height
    $graphics = [System.Drawing.Graphics]::FromImage($target)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.DrawImage($Source, [System.Drawing.Rectangle]::new(0, 0, $Width, $Height), [System.Drawing.Rectangle]::new($X, $Y, $Width, $Height), [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    return $target
}

function Blend-Color {
    param(
        [System.Drawing.Color]$BaseColor,
        [System.Drawing.Color]$OverlayColor,
        [double]$Strength
    )

    $alpha = ($OverlayColor.A / 255.0) * $Strength
    $inv = 1.0 - $alpha
    $a = [Math]::Min(255, [Math]::Round($BaseColor.A * $inv + 255 * $alpha))
    $r = [Math]::Min(255, [Math]::Round($BaseColor.R * $inv + $OverlayColor.R * $alpha))
    $g = [Math]::Min(255, [Math]::Round($BaseColor.G * $inv + $OverlayColor.G * $alpha))
    $b = [Math]::Min(255, [Math]::Round($BaseColor.B * $inv + $OverlayColor.B * $alpha))
    return [System.Drawing.Color]::FromArgb([int]$a, [int]$r, [int]$g, [int]$b)
}

function Add-Shadow {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [int]$OffsetX = 0,
        [int]$OffsetY = 4,
        [int]$BlurRadius = 4,
        [int]$ShadowAlpha = 48
    )

    $canvas = New-Bitmap -Width $Bitmap.Width -Height $Bitmap.Height

    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            $src = $Bitmap.GetPixel($x, $y)
            if ($src.A -eq 0) {
                continue
            }

            for ($dy = -$BlurRadius; $dy -le $BlurRadius; $dy++) {
                for ($dx = -$BlurRadius; $dx -le $BlurRadius; $dx++) {
                    $tx = $x + $OffsetX + $dx
                    $ty = $y + $OffsetY + $dy
                    if ($tx -lt 0 -or $ty -lt 0 -or $tx -ge $Bitmap.Width -or $ty -ge $Bitmap.Height) {
                        continue
                    }

                    $distance = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
                    if ($distance -gt $BlurRadius) {
                        continue
                    }

                    $weight = 1.0 - ($distance / ($BlurRadius + 0.01))
                    $alpha = [Math]::Min(255, [Math]::Round($ShadowAlpha * $weight * ($src.A / 255.0)))
                    if ($alpha -le 0) {
                        continue
                    }

                    $existing = $canvas.GetPixel($tx, $ty)
                    if ($alpha -gt $existing.A) {
                        $canvas.SetPixel($tx, $ty, [System.Drawing.Color]::FromArgb([int]$alpha, 6, 10, 18))
                    }
                }
            }
        }
    }

    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            $src = $Bitmap.GetPixel($x, $y)
            if ($src.A -gt 0) {
                $canvas.SetPixel($x, $y, $src)
            }
        }
    }

    return $canvas
}

$repoRoot = 'D:\Codex\TrafficView_Moi'
$projectRoot = Join-Path $repoRoot 'TrafficView 1.4.12'
$skinWork = Join-Path $projectRoot 'Skin_Work\Tech Glass_03'
$workflowDir = Join-Path $skinWork 'workflows'
$outputDir = Join-Path $skinWork 'output'
$comfyRoot = 'D:\Codex\!Grafikhilfen\ComfyUI\ComfyUI'
$comfyOutput = Join-Path $comfyRoot 'output'
$comfyInput = Join-Path $comfyRoot 'input'
$globalWorkflow = 'D:\Codex\!Grafikhilfen\Workflows\Tech_Glass_03.json'

$sourceSkin = Join-Path $projectRoot 'Skins\17\TrafficView.panel.150.png'
$referenceImage = 'D:\Codex\TrafficView_Moi\Futuristisches Widget mit Geschwindigkeitsanzeige.png'

$baseMasterPath = Join-Path $outputDir 'Tech_Glass_03_base_1530x840.png'
$paddedInputPath = Join-Path $comfyInput 'Tech_Glass_03_base_1536x840_pad.png'
$renderRawPath = Join-Path $outputDir 'Tech_Glass_03_render_raw_1536x840.png'
$generatedPath = Join-Path $outputDir 'Tech_Glass_03_generated_1530x840.png'
$finalPath = Join-Path $outputDir 'Tech_Glass_03.png'
$previewPath = Join-Path $outputDir 'Tech_Glass_03_preview.png'
$specPath = Join-Path $outputDir 'Tech_Glass_03_spec.txt'
$workflowPath = Join-Path $workflowDir 'Tech_Glass_03.json'

$sourceImage = [System.Drawing.Bitmap]::FromFile($referenceImage)

$minX = $sourceImage.Width
$minY = $sourceImage.Height
$maxX = 0
$maxY = 0
for ($y = 0; $y -lt $sourceImage.Height; $y++) {
    for ($x = 0; $x -lt $sourceImage.Width; $x++) {
        $pixel = $sourceImage.GetPixel($x, $y)
        if ($pixel.A -gt 0 -and -not ($pixel.R -ge 245 -and $pixel.G -ge 245 -and $pixel.B -ge 245)) {
            if ($x -lt $minX) { $minX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

if ($maxX -le $minX -or $maxY -le $minY) {
    throw "Reference image does not contain a detectable non-white skin area."
}

$croppedReference = Crop-Image -Source $sourceImage -X $minX -Y $minY -Width ($maxX - $minX + 1) -Height ($maxY - $minY + 1)
$refAspect = $croppedReference.Width / [double]$croppedReference.Height
$targetAspect = 1530 / [double]840
$scale = [Math]::Max(1530 / [double]$croppedReference.Width, 840 / [double]$croppedReference.Height)
$scaledWidth = [Math]::Round($croppedReference.Width * $scale)
$scaledHeight = [Math]::Round($croppedReference.Height * $scale)
$scaledReference = Resize-Image -Source $croppedReference -Width $scaledWidth -Height $scaledHeight
$baseMaster = Crop-Image -Source $scaledReference -X ([Math]::Max(0, [int](($scaledWidth - 1530) / 2))) -Y ([Math]::Max(0, [int](($scaledHeight - 840) / 2))) -Width 1530 -Height 840
Save-Png -Image $baseMaster -Path $baseMasterPath

$padded = New-Bitmap -Width 1536 -Height 840
$graphics = [System.Drawing.Graphics]::FromImage($padded)
$graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.DrawImage($baseMaster, 3, 0, 1530, 840)
$graphics.Dispose()
Save-Png -Image $padded -Path $paddedInputPath

$functionalSource = [System.Drawing.Image]::FromFile($sourceSkin)
$functionalBase = Resize-Image -Source $functionalSource -Width 1530 -Height 840

$positivePrompt = 'futuristic traffic overlay UI, dark blue cyan high-tech dashboard panel, translucent glass interface, semi-transparent information panel on the left, subtle gaussian blur background behind glass, layered interface depth, soft shadows, controlled highlights, crisp edges, high readability, modern traffic navigation dashboard UI, clean spacing and alignment, premium software product look, rounded rectangular glossy panel, dark tinted acrylic surface, precise circular HUD element on the right, segmented illuminated blue ring, highly readable metric area, polished glass reflections, restrained glow, professional desktop overlay skin, realistic software interface rendering'
$negativePrompt = 'cartoon, anime, illustration, concept art, fantasy UI, overdesigned interface, clutter, messy layout, low contrast UI, unreadable text areas, excessive glow, bloom overload, distorted geometry, warped UI, cheap gradients, noisy textures, fake reflections, unrealistic lighting, game HUD chaos'

$workflowObject = @{
    prompt = @{
        '1' = @{
            class_type = 'CheckpointLoaderSimple'
            inputs = @{
                ckpt_name = 'sd_xl_base_1.0.safetensors'
            }
        }
        '2' = @{
            class_type = 'LoadImage'
            inputs = @{
                image = 'Tech_Glass_03_base_1536x840_pad.png'
                upload = 'image'
            }
        }
        '3' = @{
            class_type = 'CLIPTextEncode'
            inputs = @{
                clip = @('1', 1)
                text = $positivePrompt
            }
        }
        '4' = @{
            class_type = 'CLIPTextEncode'
            inputs = @{
                clip = @('1', 1)
                text = $negativePrompt
            }
        }
        '5' = @{
            class_type = 'VAEEncode'
            inputs = @{
                pixels = @('2', 0)
                vae = @('1', 2)
            }
        }
        '6' = @{
            class_type = 'KSampler'
            inputs = @{
                model = @('1', 0)
                seed = 320044102
                steps = 28
                cfg = 6.0
                sampler_name = 'dpmpp_2m'
                scheduler = 'karras'
                denoise = 0.26
                positive = @('3', 0)
                negative = @('4', 0)
                latent_image = @('5', 0)
            }
        }
        '7' = @{
            class_type = 'VAEDecode'
            inputs = @{
                samples = @('6', 0)
                vae = @('1', 2)
            }
        }
        '8' = @{
            class_type = 'SaveImage'
            inputs = @{
                filename_prefix = 'Tech_Glass_03'
                images = @('7', 0)
            }
        }
    }
}

$workflowJson = $workflowObject | ConvertTo-Json -Depth 10
Set-Content -Path $workflowPath -Value $workflowJson -Encoding UTF8
Set-Content -Path $globalWorkflow -Value $workflowJson -Encoding UTF8

$startStamp = Get-Date
$response = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:8188/prompt' -ContentType 'application/json' -Body $workflowJson

$rawRender = $null
for ($i = 0; $i -lt 15; $i++) {
    Start-Sleep -Seconds 2
    $candidate = Get-ChildItem -Path $comfyOutput -Filter 'Tech_Glass_03*.png' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -ge $startStamp } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($candidate) {
        $rawRender = $candidate.FullName
        break
    }
}

if (-not $rawRender) {
    $candidate = Get-ChildItem -Path $comfyOutput -Filter 'Tech_Glass_03*.png' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($candidate) {
        $rawRender = $candidate.FullName
    }
}

if (-not $rawRender) {
    throw "ComfyUI render for Tech_Glass_03 was not produced."
}

Copy-Item -Path $rawRender -Destination $renderRawPath -Force

$rawBitmap = [System.Drawing.Bitmap]::FromFile($renderRawPath)
$generated = Crop-Image -Source $rawBitmap -X 3 -Y 0 -Width 1530 -Height 840
Save-Png -Image $generated -Path $generatedPath

$final = New-Bitmap -Width 1530 -Height 840
for ($y = 0; $y -lt 840; $y++) {
    for ($x = 0; $x -lt 1530; $x++) {
        $baseColor = $functionalBase.GetPixel($x, $y)
        $genColor = $generated.GetPixel($x, $y)

        if ($baseColor.A -eq 0) {
            $final.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
            continue
        }

        if ($x -lt 10 -or $x -ge 1520 -or $y -lt 10 -or $y -ge 830) {
            $final.SetPixel($x, $y, $baseColor)
            continue
        }

        $strength = 0.0
        $brightness = ($genColor.R + $genColor.G + $genColor.B) / 3.0
        $isOrangeText = ($genColor.R -gt 175 -and $genColor.G -gt 95 -and $genColor.G -lt 190 -and $genColor.B -lt 110)
        $isGreenText = ($genColor.G -gt 175 -and $genColor.R -lt 170 -and $genColor.B -lt 150)
        $isReflective = (($brightness -gt 168) -or ($genColor.B -gt 135 -and $genColor.G -gt 115)) -and -not ($isOrangeText -or $isGreenText)

        $isMetricZone = ($x -ge 35 -and $x -le 780 -and $y -ge 55 -and $y -le 635)
        $isRingZone = ($x -ge 845 -and $x -le 1495 -and $y -ge 35 -and $y -le 780)
        $isGraphZone = ($x -ge 25 -and $x -le 780 -and $y -ge 620)

        if ($isMetricZone -or $isRingZone -or $isGraphZone) {
            $strength = 0.0
        } elseif ($y -le 130) {
            $strength = if ($isReflective) { 0.40 } else { 0.02 }
        } elseif ($x -ge 560 -and $x -le 1020 -and $y -ge 120 -and $y -le 690) {
            $strength = if ($isReflective) { 0.24 } else { 0.04 }
        } elseif ($x -ge 1080 -and $y -ge 620) {
            $strength = if ($isReflective) { 0.28 } else { 0.03 }
        } else {
            $strength = if ($isReflective) { 0.10 } else { 0.0 }
        }

        $blended = Blend-Color -BaseColor $baseColor -OverlayColor $genColor -Strength $strength
        $final.SetPixel($x, $y, $blended)
    }
}

$withShadow = Add-Shadow -Bitmap $final -OffsetX 0 -OffsetY 4 -BlurRadius 4 -ShadowAlpha 44
Save-Png -Image $withShadow -Path $finalPath
Save-Png -Image $withShadow -Path $previewPath
Copy-Item -Path $finalPath -Destination (Join-Path $comfyOutput 'Tech_Glass_03.png') -Force

$sizes = @(
    @{ Name = 'TrafficView.panel.90.png'; Width = 92; Height = 50 },
    @{ Name = 'TrafficView.panel.png'; Width = 102; Height = 56 },
    @{ Name = 'TrafficView.panel.110.png'; Width = 112; Height = 62 },
    @{ Name = 'TrafficView.panel.125.png'; Width = 128; Height = 70 },
    @{ Name = 'TrafficView.panel.150.png'; Width = 153; Height = 84 }
)

$skinDir = Join-Path $projectRoot 'Skins\19'
$distSkinDir = Join-Path $projectRoot 'dist\Skins\19'
New-Item -ItemType Directory -Force -Path $skinDir | Out-Null
New-Item -ItemType Directory -Force -Path $distSkinDir | Out-Null

foreach ($entry in $sizes) {
    $resized = Resize-Image -Source $withShadow -Width $entry.Width -Height $entry.Height
    $targetPath = Join-Path $skinDir $entry.Name
    Save-Png -Image $resized -Path $targetPath
    Copy-Item -Path $targetPath -Destination (Join-Path $distSkinDir $entry.Name) -Force
    $resized.Dispose()
}

$skinIni = @"
Id=19
DisplayNameKey=Menu.SkinName19
DisplayNameFallback=Tech Glass_03
SurfaceEffect=glass-readable
"@

Set-Content -Path (Join-Path $skinDir 'skin.ini') -Value $skinIni -Encoding ASCII
Copy-Item -Path (Join-Path $skinDir 'skin.ini') -Destination (Join-Path $distSkinDir 'skin.ini') -Force

$spec = @"
Name: Tech Glass_03
Resolution: 1530x840
Ratio: 51:28
Method: ComfyUI img2img + protected edge composite
Model: sd_xl_base_1.0.safetensors
Input base: Tech Glass (Skin 17)
Reference image: $referenceImage
Output skin folder: $skinDir
"@
Set-Content -Path $specPath -Value $spec -Encoding UTF8

$sourceImage.Dispose()
$croppedReference.Dispose()
$scaledReference.Dispose()
$functionalSource.Dispose()
$functionalBase.Dispose()
$baseMaster.Dispose()
$padded.Dispose()
$rawBitmap.Dispose()
$generated.Dispose()
$final.Dispose()
$withShadow.Dispose()

Write-Output "RAW_RENDER=$rawRender"
Write-Output "FINAL=$finalPath"
