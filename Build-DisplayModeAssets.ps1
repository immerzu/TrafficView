$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$displayModeAssetSets = @(
    @{
        Name = "Simple"
        SourceDirectory = Join-Path $root "DisplayModeAssetSources\Simple"
        OutputDirectory = Join-Path $root "DisplayModeAssets\Simple"
    },
    @{
        Name = "SimpleBlue"
        SourceDirectory = Join-Path $root "DisplayModeAssetSources\SimpleBlue"
        OutputDirectory = Join-Path $root "DisplayModeAssets\SimpleBlue"
    }
)

$panelTargets = @(
    @{
        FileName = "TrafficView.panel.90.png"
        Width = 92
        Height = 50
    },
    @{
        FileName = "TrafficView.panel.png"
        Width = 102
        Height = 56
    },
    @{
        FileName = "TrafficView.panel.110.png"
        Width = 112
        Height = 62
    },
    @{
        FileName = "TrafficView.panel.125.png"
        Width = 128
        Height = 70
    },
    @{
        FileName = "TrafficView.panel.150.png"
        Width = 153
        Height = 84
    }
)

Add-Type -AssemblyName System.Drawing

function Assert-FileExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        throw "Datei nicht gefunden: $Path"
    }
}

function Save-ResizedPng {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$TargetPath,

        [Parameter(Mandatory = $true)]
        [int]$Width,

        [Parameter(Mandatory = $true)]
        [int]$Height
    )

    $sourceImage = $null
    $targetBitmap = $null
    $graphics = $null

    try {
        $sourceImage = [System.Drawing.Image]::FromFile($SourcePath)

        if ($sourceImage.Width -eq $Width -and $sourceImage.Height -eq $Height) {
            Copy-Item -LiteralPath $SourcePath -Destination $TargetPath -Force
            return
        }

        $targetBitmap = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $targetBitmap.SetResolution($sourceImage.HorizontalResolution, $sourceImage.VerticalResolution)

        $graphics = [System.Drawing.Graphics]::FromImage($targetBitmap)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.DrawImage($sourceImage, 0, 0, $Width, $Height)

        $targetBitmap.Save($TargetPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        if ($graphics -ne $null) {
            $graphics.Dispose()
        }

        if ($targetBitmap -ne $null) {
            $targetBitmap.Dispose()
        }

        if ($sourceImage -ne $null) {
            $sourceImage.Dispose()
        }
    }
}

foreach ($assetSet in $displayModeAssetSets) {
    $sourceDirectory = $assetSet.SourceDirectory
    $outputDirectory = $assetSet.OutputDirectory
    $panelMasterFile = Join-Path $sourceDirectory "TrafficView.panel.master.png"
    $centerCoreMasterFile = Join-Path $sourceDirectory "TrafficView.center_core.master.png"

    Assert-FileExists -Path $panelMasterFile
    Assert-FileExists -Path $centerCoreMasterFile

    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

    foreach ($target in $panelTargets) {
        $targetFile = Join-Path $outputDirectory $target.FileName
        Save-ResizedPng `
            -SourcePath $panelMasterFile `
            -TargetPath $targetFile `
            -Width $target.Width `
            -Height $target.Height
    }

    Copy-Item `
        -LiteralPath $centerCoreMasterFile `
        -Destination (Join-Path $outputDirectory "TrafficView.center_core.png") `
        -Force

    Write-Host ""
    Write-Host "DisplayModeAssets erzeugt:" $outputDirectory
}
