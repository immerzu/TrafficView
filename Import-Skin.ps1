param(
    [Parameter(Mandatory = $true)]
    [string]$SourceSkinDirectory,

    [Parameter(Mandatory = $false)]
    [string]$TargetSkinId = "",

    [Parameter(Mandatory = $false)]
    [switch]$ReplaceExisting
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$skinsRoot = Join-Path $root "Skins"
$requiredSkinFiles = [ordered]@{
    "skin.ini" = $null
    "TrafficView.panel.90.png" = $true
    "TrafficView.panel.png" = $true
    "TrafficView.panel.110.png" = $true
    "TrafficView.panel.125.png" = $true
    "TrafficView.panel.150.png" = $true
}
$supportedSurfaceEffects = @(
    "none",
    "glass",
    "glass-readable"
)
$invalidSkinFolderCharacters = [System.IO.Path]::GetInvalidFileNameChars()

function Get-SkinMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkinDirectoryPath
    )

    $skinSettingsPath = Join-Path $SkinDirectoryPath "skin.ini"
    $skinDirectoryName = Split-Path -Leaf $SkinDirectoryPath
    $skinId = $skinDirectoryName
    $displayNameFallback = $skinDirectoryName
    $surfaceEffect = "none"
    $clientWidth = 102
    $clientHeight = 56

    foreach ($line in Get-Content $skinSettingsPath) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $trimmedLine = $line.Trim()
        if ($trimmedLine.StartsWith("#") -or $trimmedLine.StartsWith(";")) {
            continue
        }

        $equalsIndex = $trimmedLine.IndexOf("=")
        if ($equalsIndex -le 0) {
            continue
        }

        $key = $trimmedLine.Substring(0, $equalsIndex).Trim()
        $value = $trimmedLine.Substring($equalsIndex + 1).Trim()

        if ($key -ieq "Id" -and -not [string]::IsNullOrWhiteSpace($value)) {
            $skinId = $value
            continue
        }

        if ($key -ieq "DisplayNameFallback" -and -not [string]::IsNullOrWhiteSpace($value)) {
            $displayNameFallback = $value
            continue
        }

        if ($key -ieq "SurfaceEffect") {
            $surfaceEffect = $value
            continue
        }

        if ($key -ieq "ClientSize") {
            $parts = $value.Split(',')
            if ($parts.Length -ne 2) {
                throw "ClientSize ist ungueltig in: $skinSettingsPath"
            }

            $parsedWidth = 0
            $parsedHeight = 0
            if (-not [int]::TryParse($parts[0].Trim(), [ref]$parsedWidth) -or
                -not [int]::TryParse($parts[1].Trim(), [ref]$parsedHeight) -or
                $parsedWidth -le 0 -or
                $parsedHeight -le 0) {
                throw "ClientSize ist ungueltig in: $skinSettingsPath"
            }

            $clientWidth = $parsedWidth
            $clientHeight = $parsedHeight
        }
    }

    if ([string]::IsNullOrWhiteSpace($surfaceEffect)) {
        $surfaceEffect = "none"
    }

    if ([string]::IsNullOrWhiteSpace($displayNameFallback)) {
        $displayNameFallback = $skinId
    }

    [PSCustomObject]@{
        Id = $skinId
        DisplayNameFallback = $displayNameFallback
        SurfaceEffect = $surfaceEffect
        ClientWidth = $clientWidth
        ClientHeight = $clientHeight
        DirectoryName = $skinDirectoryName
        DirectoryPath = $SkinDirectoryPath
    }
}

function Get-SkinFolderNameFromFallback {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $folderName = $Value.Trim()
    if ([string]::IsNullOrWhiteSpace($folderName)) {
        throw "DisplayNameFallback ist leer und kann nicht als Skin-Ordnername verwendet werden."
    }

    foreach ($invalidCharacter in $invalidSkinFolderCharacters) {
        if ($folderName.Contains([string]$invalidCharacter)) {
            throw "DisplayNameFallback enthaelt ungueltige Zeichen fuer einen Skin-Ordner: '$Value'"
        }
    }

    if ($folderName.EndsWith(".", [System.StringComparison]::Ordinal)) {
        throw "DisplayNameFallback darf nicht mit einem Punkt enden: '$Value'"
    }

    return $folderName
}

function Test-SkinDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkinDirectoryPath
    )

    if (-not (Test-Path $SkinDirectoryPath)) {
        throw "Skin-Ordner nicht gefunden: $SkinDirectoryPath"
    }

    $skinSettingsPath = Join-Path $SkinDirectoryPath "skin.ini"

    foreach ($entry in $requiredSkinFiles.GetEnumerator()) {
        $fileName = $entry.Key
        $filePath = Join-Path $SkinDirectoryPath $fileName

        if (-not (Test-Path $filePath)) {
            throw "Skin-Datei fehlt: $filePath"
        }

        if ($null -eq $entry.Value) {
            continue
        }
    }

    $maxSkinIniBytes = 64 * 1024
    $maxSkinPngBytes = 2 * 1024 * 1024
    $maxSkinBitmapDimension = 4096
    $skinIniInfo = Get-Item -LiteralPath $skinSettingsPath
    if ($skinIniInfo.Length -gt $maxSkinIniBytes) {
        throw "skin.ini ist zu gross ($($skinIniInfo.Length) Bytes, Maximum $maxSkinIniBytes Bytes): $skinSettingsPath"
    }

    $skinMetadata = Get-SkinMetadata -SkinDirectoryPath $SkinDirectoryPath
    $skinId = $skinMetadata.Id
    $surfaceEffect = $skinMetadata.SurfaceEffect

    if ([string]::IsNullOrWhiteSpace($skinId)) {
        throw "Skin-ID leer in: $skinSettingsPath"
    }

    if ([string]::IsNullOrWhiteSpace($surfaceEffect)) {
        $surfaceEffect = "none"
    }

    if ($supportedSurfaceEffects -inotcontains $surfaceEffect) {
        throw "Nicht unterstützter SurfaceEffect in $skinSettingsPath : $surfaceEffect"
    }

    $expectedAssetSizes = [ordered]@{
        "TrafficView.panel.90.png" = @{
            Width = [int][Math]::Floor((($skinMetadata.ClientWidth * 90) / 100.0) + 0.5)
            Height = [int][Math]::Floor((($skinMetadata.ClientHeight * 90) / 100.0) + 0.5)
        }
        "TrafficView.panel.png" = @{
            Width = $skinMetadata.ClientWidth
            Height = $skinMetadata.ClientHeight
        }
        "TrafficView.panel.110.png" = @{
            Width = [int][Math]::Floor((($skinMetadata.ClientWidth * 110) / 100.0) + 0.5)
            Height = [int][Math]::Floor((($skinMetadata.ClientHeight * 110) / 100.0) + 0.5)
        }
        "TrafficView.panel.125.png" = @{
            Width = [int][Math]::Floor((($skinMetadata.ClientWidth * 125) / 100.0) + 0.5)
            Height = [int][Math]::Floor((($skinMetadata.ClientHeight * 125) / 100.0) + 0.5)
        }
        "TrafficView.panel.150.png" = @{
            Width = [int][Math]::Floor((($skinMetadata.ClientWidth * 150) / 100.0) + 0.5)
            Height = [int][Math]::Floor((($skinMetadata.ClientHeight * 150) / 100.0) + 0.5)
        }
    }

    foreach ($entry in $expectedAssetSizes.GetEnumerator()) {
        $filePath = Join-Path $SkinDirectoryPath $entry.Key

        $pngInfo = Get-Item -LiteralPath $filePath -ErrorAction Stop
        if ($pngInfo.Length -gt $maxSkinPngBytes) {
            throw "Skin PNG ist zu gross: $filePath ($($pngInfo.Length) Bytes, maximal $maxSkinPngBytes Bytes)"
        }

        $bitmap = $null
        try {
            $pngSignature = [byte[]]@(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
            $header = [byte[]]::new(8)
            $stream = $null
            try {
                $stream = [System.IO.File]::OpenRead($filePath)
                $bytesRead = $stream.Read($header, 0, 8)
                if ($bytesRead -lt 8) {
                    throw "Skin-Datei ist keine gueltige PNG-Datei (zu kurz): $filePath"
                }
            }
            finally {
                if ($stream -ne $null) { $stream.Dispose() }
            }

            for ($i = 0; $i -lt 8; $i++) {
                if ($header[$i] -ne $pngSignature[$i]) {
                    throw "Skin-Datei ist keine gueltige PNG-Datei: $filePath"
                }
            }

            $bitmap = New-Object System.Drawing.Bitmap($filePath)
            if ($bitmap.Width -gt $maxSkinBitmapDimension -or $bitmap.Height -gt $maxSkinBitmapDimension) {
                throw "Skin PNG hat zu grosse Abmessungen: $filePath ($($bitmap.Width)x$($bitmap.Height) px, maximal ${maxSkinBitmapDimension}x${maxSkinBitmapDimension} px)"
            }

            if ($bitmap.Width -ne $entry.Value.Width -or $bitmap.Height -ne $entry.Value.Height) {
                throw "Skin-Datei hat falsche Groesse: $filePath ($($bitmap.Width)x$($bitmap.Height) statt $($entry.Value.Width)x$($entry.Value.Height))"
            }
        }
        finally {
            if ($bitmap -ne $null) {
                $bitmap.Dispose()
            }
        }
    }
}

if (-not (Test-Path $skinsRoot)) {
    New-Item -ItemType Directory -Force -Path $skinsRoot | Out-Null
}

$resolvedSource = (Resolve-Path $SourceSkinDirectory).Path
Test-SkinDirectory -SkinDirectoryPath $resolvedSource

$skinMetadata = Get-SkinMetadata -SkinDirectoryPath $resolvedSource
$targetLeafName = Get-SkinFolderNameFromFallback -Value $skinMetadata.DisplayNameFallback
$targetDirectory = Join-Path $skinsRoot $targetLeafName

if ((Test-Path $targetDirectory) -and -not $ReplaceExisting.IsPresent) {
    throw "Ziel-Skin existiert bereits: $targetDirectory. Verwende -ReplaceExisting zum Ueberschreiben."
}

if (Test-Path $targetDirectory) {
    Remove-Item -LiteralPath $targetDirectory -Recurse -Force
}

$temporaryDirectory = Join-Path $skinsRoot ($targetLeafName + ".import-" + [Guid]::NewGuid().ToString("N"))
Copy-Item -Path $resolvedSource -Destination $temporaryDirectory -Recurse -Force

try {
    Test-SkinDirectory -SkinDirectoryPath $temporaryDirectory
    Move-Item -LiteralPath $temporaryDirectory -Destination $targetDirectory
}
catch {
    if (Test-Path $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
    throw
}

Write-Host "Skin importiert:" $targetDirectory
