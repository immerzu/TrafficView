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

$root = 'D:\Codex\TrafficView_Moi\TrafficView 1.4.12\Skin_Work\Templates'
$outputDir = Join-Path $root 'output'
$sourcePath = 'D:\Codex\TrafficView_Moi\Futuristisches Widget mit Geschwindigkeitsanzeige.png'
$finalPath = Join-Path $outputDir 'Tech_Base_aligned_1530x840.png'

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

# Ruhige linke Textbereiche direkt in der Referenzgeometrie
$darkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 4, 10, 18))
$g.FillRectangle($darkBrush, [System.Drawing.Rectangle]::new(92, 118, 610, 132))
$g.FillRectangle($darkBrush, [System.Drawing.Rectangle]::new(92, 342, 610, 132))

# Leichte Verbindung zur vorhandenen linken Glasplatte, ohne neue Verschiebung
$leftSoftenBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180, 6, 15, 24))
$leftPath = New-RoundedRectPath -X 92 -Y 94 -Width 585 -Height 505 -Radius 48
$g.FillPath($leftSoftenBrush, $leftPath)

# Kreiszentrum neutralisieren, Ring erhalten
$circleFill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 9, 17, 29))
$g.FillEllipse($circleFill, [System.Drawing.Rectangle]::new(972, 222, 286, 286))

# Untere Graph-Zone leicht abdunkeln, damit die echten Werte später dominieren
$graphBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(110, 8, 18, 27))
$graphPath = New-RoundedRectPath -X 110 -Y 640 -Width 510 -Height 72 -Radius 22
$g.FillPath($graphBrush, $graphPath)

# Subtile Material-Linie oben, keine Geometrieverschiebung
$highlightPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(48, 255, 255, 255), 7)
$g.DrawArc($highlightPen, 60, 20, 1410, 90, 180, 180)

$g.Dispose()
Save-Png -Image $canvas -Path $finalPath

# Als Skin 24 übernehmen
$skinDir = 'D:\Codex\TrafficView_Moi\TrafficView 1.4.12\Skins\24'
$distDir = 'D:\Codex\TrafficView_Moi\TrafficView 1.4.12\dist\Skins\24'
$sizes = @(
    @{ Name = 'TrafficView.panel.90.png'; Width = 92; Height = 50 },
    @{ Name = 'TrafficView.panel.png'; Width = 102; Height = 56 },
    @{ Name = 'TrafficView.panel.110.png'; Width = 112; Height = 62 },
    @{ Name = 'TrafficView.panel.125.png'; Width = 128; Height = 70 },
    @{ Name = 'TrafficView.panel.150.png'; Width = 153; Height = 84 }
)

foreach ($entry in $sizes) {
    $resized = Resize-Image -Source $canvas -Width $entry.Width -Height $entry.Height
    $target = Join-Path $skinDir $entry.Name
    Save-Png -Image $resized -Path $target
    Copy-Item $target (Join-Path $distDir $entry.Name) -Force
    $resized.Dispose()
}

$src.Dispose()
$cropped.Dispose()
$scaled.Dispose()
$base.Dispose()
$canvas.Dispose()

Write-Output $finalPath
