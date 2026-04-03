$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $root "src"
$outputDir = Join-Path $root "dist"
$outputFile = Join-Path $outputDir "TrafficView.exe"
$settingsOutputFile = Join-Path $outputDir "TrafficView.settings.ini"
$usageOutputFile = Join-Path $outputDir "Verbrauch.txt"
$usageArchiveOutputFile = Join-Path $outputDir "Verbrauch.archiv.txt"
$manifestFile = Join-Path $root "TrafficView.manifest"
$iconFile = Join-Path $root "TrafficView.ico"
$configSourceFile = Join-Path $root "TrafficView.exe.config"
$configOutputFile = Join-Path $outputDir "TrafficView.exe.config"
$languageSourceFile = Join-Path $root "TrafficView.languages.ini"
$languageOutputFile = Join-Path $outputDir "TrafficView.languages.ini"
$skinsSourceDirectory = Join-Path $root "Skins"
$skinsOutputDirectory = Join-Path $outputDir "Skins"
$legacyPanelAssetFiles = @(
    "TrafficView.panel.png",
    "TrafficView.panel.90.png",
    "TrafficView.panel.110.png",
    "TrafficView.panel.125.png",
    "TrafficView.panel.150.png"
)
$menuAssetFiles = @(
    "LOLO-SOFT_00_SW.png"
)
$sourceFiles = Get-ChildItem -Path $sourceDir -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }
$requiredSkinFiles = [ordered]@{
    "skin.ini" = $null
    "TrafficView.panel.90.png" = @{ Width = 92; Height = 50 }
    "TrafficView.panel.png" = @{ Width = 102; Height = 56 }
    "TrafficView.panel.110.png" = @{ Width = 112; Height = 62 }
    "TrafficView.panel.125.png" = @{ Width = 128; Height = 70 }
    "TrafficView.panel.150.png" = @{ Width = 153; Height = 84 }
}
$deleteStagingDirectoryName = ".delete"
$supportedSurfaceEffects = @(
    "none",
    "glass",
    "glass-readable"
)

Add-Type -AssemblyName System.Drawing

function Remove-DeleteStagingDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkinsDirectoryPath
    )

    $deleteStagingDirectoryPath = Join-Path $SkinsDirectoryPath $deleteStagingDirectoryName
    if (-not (Test-Path $deleteStagingDirectoryPath)) {
        return
    }

    try {
        Remove-Item -LiteralPath $deleteStagingDirectoryPath -Recurse -Force
    }
    catch {
        throw "Temporäres Skin-Löschverzeichnis konnte nicht bereinigt werden: $deleteStagingDirectoryPath"
    }
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

function Test-AllSkinDirectories {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkinsDirectoryPath
    )

    Remove-DeleteStagingDirectory -SkinsDirectoryPath $SkinsDirectoryPath

    $skinDirectories = Get-ChildItem -Path $SkinsDirectoryPath -Directory |
        Where-Object { $_.Name -ne $deleteStagingDirectoryName } |
        Sort-Object Name
    if (-not $skinDirectories -or $skinDirectories.Count -eq 0) {
        throw "Keine Skin-Ordner gefunden: $SkinsDirectoryPath"
    }

    $seenSkinIds = @{}
    foreach ($skinDirectory in $skinDirectories) {
        Test-SkinDirectory -SkinDirectoryPath $skinDirectory.FullName

        $skinDirectoryName = $skinDirectory.Name
        if ($seenSkinIds.ContainsKey($skinDirectoryName)) {
            throw "Doppelte Skin-ID erkannt: $skinDirectoryName"
        }

        $seenSkinIds[$skinDirectoryName] = $true
    }
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (Test-Path $settingsOutputFile) {
    Remove-Item -LiteralPath $settingsOutputFile -Force
}

if (Test-Path $usageOutputFile) {
    Remove-Item -LiteralPath $usageOutputFile -Force
}

if (Test-Path $usageArchiveOutputFile) {
    Remove-Item -LiteralPath $usageArchiveOutputFile -Force
}

if (Test-Path $skinsOutputDirectory) {
    Remove-Item -LiteralPath $skinsOutputDirectory -Recurse -Force
}

foreach ($legacyPanelAssetFile in $legacyPanelAssetFiles) {
    $legacyPanelAssetOutputFile = Join-Path $outputDir $legacyPanelAssetFile
    if (Test-Path $legacyPanelAssetOutputFile) {
        Remove-Item -LiteralPath $legacyPanelAssetOutputFile -Force
    }
}

$compilerCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)

$compiler = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $compiler) {
    throw "Kein passender C#-Compiler gefunden. Erwartet wurde csc.exe aus dem .NET Framework 4.x."
}

if (-not (Test-Path $manifestFile)) {
    throw "Manifest fuer DPI-Unterstuetzung nicht gefunden: $manifestFile"
}

if (-not (Test-Path $iconFile)) {
    throw "Programm-Icon nicht gefunden: $iconFile"
}

if (-not (Test-Path $configSourceFile)) {
    throw "EXE-Konfigurationsdatei nicht gefunden: $configSourceFile"
}

if (-not (Test-Path $languageSourceFile)) {
    throw "Sprachdatei nicht gefunden: $languageSourceFile"
}

if (-not (Test-Path $skinsSourceDirectory)) {
    throw "Skin-Ordner nicht gefunden: $skinsSourceDirectory"
}

if (-not $sourceFiles -or $sourceFiles.Count -eq 0) {
    throw "Keine C#-Quelldateien im Verzeichnis '$sourceDir' gefunden."
}

Test-AllSkinDirectories -SkinsDirectoryPath $skinsSourceDirectory

& $compiler `
    /nologo `
    /target:winexe `
    /optimize+ `
    /platform:anycpu `
    /win32icon:$iconFile `
    /win32manifest:$manifestFile `
    /out:$outputFile `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $sourceFiles

if ($LASTEXITCODE -ne 0) {
    throw "Build fehlgeschlagen."
}

Copy-Item $configSourceFile $configOutputFile -Force
Copy-Item $languageSourceFile $languageOutputFile -Force
Copy-Item $skinsSourceDirectory $skinsOutputDirectory -Recurse -Force

foreach ($menuAssetFile in $menuAssetFiles) {
    $menuAssetSourceFile = Join-Path $root $menuAssetFile
    $menuAssetOutputFile = Join-Path $outputDir $menuAssetFile
    if (Test-Path $menuAssetSourceFile) {
        Copy-Item $menuAssetSourceFile $menuAssetOutputFile -Force
    }
}

Write-Host ""
Write-Host "Fertig:" $outputFile
