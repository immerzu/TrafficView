$ErrorActionPreference = "Stop"

param(
    [Parameter(Mandatory = $true)]
    [string]$SourceSkinDirectory,

    [Parameter(Mandatory = $false)]
    [string]$TargetSkinId = "",

    [Parameter(Mandatory = $false)]
    [switch]$ReplaceExisting
)

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$skinsRoot = Join-Path $root "Skins"
$requiredSkinFiles = [ordered]@{
    "skin.ini" = $null
    "TrafficView.panel.90.png" = @{ Width = 92; Height = 50 }
    "TrafficView.panel.png" = @{ Width = 102; Height = 56 }
    "TrafficView.panel.110.png" = @{ Width = 112; Height = 62 }
    "TrafficView.panel.125.png" = @{ Width = 128; Height = 70 }
    "TrafficView.panel.150.png" = @{ Width = 153; Height = 84 }
}
$supportedSurfaceEffects = @(
    "none",
    "glass",
    "glass-readable"
)

function Test-SkinDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkinDirectoryPath
    )

    if (-not (Test-Path $SkinDirectoryPath)) {
        throw "Skin-Ordner nicht gefunden: $SkinDirectoryPath"
    }

    $skinSettingsPath = Join-Path $SkinDirectoryPath "skin.ini"
    $skinDirectoryName = Split-Path -Leaf $SkinDirectoryPath
    $skinId = $skinDirectoryName
    $surfaceEffect = "none"

    foreach ($entry in $requiredSkinFiles.GetEnumerator()) {
        $fileName = $entry.Key
        $filePath = Join-Path $SkinDirectoryPath $fileName

        if (-not (Test-Path $filePath)) {
            throw "Skin-Datei fehlt: $filePath"
        }

        if ($null -eq $entry.Value) {
            continue
        }

        $bitmap = $null
        try {
            $bitmap = New-Object System.Drawing.Bitmap($filePath)
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

        if ($key -ieq "SurfaceEffect") {
            $surfaceEffect = $value
        }
    }

    if ([string]::IsNullOrWhiteSpace($skinId)) {
        throw "Skin-ID leer in: $skinSettingsPath"
    }

    if ($skinId -ine $skinDirectoryName) {
        throw "Skin-ID stimmt nicht mit Ordnernamen überein: $skinSettingsPath ($skinId statt $skinDirectoryName)"
    }

    if ([string]::IsNullOrWhiteSpace($surfaceEffect)) {
        $surfaceEffect = "none"
    }

    if ($supportedSurfaceEffects -inotcontains $surfaceEffect) {
        throw "Nicht unterstützter SurfaceEffect in $skinSettingsPath : $surfaceEffect"
    }
}

if (-not (Test-Path $skinsRoot)) {
    throw "Zielordner fuer Skins nicht gefunden: $skinsRoot"
}

$resolvedSource = (Resolve-Path $SourceSkinDirectory).Path
Test-SkinDirectory -SkinDirectoryPath $resolvedSource

$sourceLeafName = Split-Path $resolvedSource -Leaf
$targetLeafName = if ([string]::IsNullOrWhiteSpace($TargetSkinId)) { $sourceLeafName } else { $TargetSkinId.Trim() }
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
