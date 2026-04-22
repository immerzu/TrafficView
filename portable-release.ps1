$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$distDir = Join-Path $projectRoot 'dist'
$stageRoot = Join-Path $projectRoot '_portable_release'
$stageDir = Join-Path $stageRoot 'TrafficView'
$stageWithDefaultsRoot = Join-Path $stageRoot 'standard'
$stageWithDefaultsDir = Join-Path $stageWithDefaultsRoot 'TrafficView'
$outputRoot = Join-Path (Split-Path -Parent $projectRoot) 'Ausgabe'
$programCsPath = Join-Path $projectRoot 'src\Program.cs'
$settingsFileName = 'TrafficView.settings.ini'
$settingsBackupFileName = 'TrafficView.settings.ini_'

if (-not (Test-Path $distDir)) {
    throw "Dist-Ordner nicht gefunden: $distDir"
}

if (-not (Test-Path $programCsPath)) {
    throw "Programmdatei nicht gefunden: $programCsPath"
}

$programCsContent = Get-Content -LiteralPath $programCsPath -Raw
$versionMatch = [regex]::Match($programCsContent, 'AssemblyVersion\("(?<version>\d+\.\d+\.\d+)\.\d+"\)')
if (-not $versionMatch.Success) {
    throw "Versionsnummer konnte nicht aus $programCsPath gelesen werden."
}

$version = $versionMatch.Groups['version'].Value
$zipPath = Join-Path $outputRoot ("TrafficView.Portable.{0}.zip" -f $version)
$defaultsZipPath = Join-Path $outputRoot ("TrafficView.Portable.{0}.Standard.zip" -f $version)

function Get-DefaultSettingsLines {
    return @(
        'AdapterId=',
        'AdapterName=',
        'CalibrationPeakBytesPerSecond=0',
        'CalibrationDownloadPeakBytesPerSecond=0',
        'CalibrationUploadPeakBytesPerSecond=0',
        'InitialCalibrationPromptHandled=0',
        'InitialLanguagePromptHandled=0',
        'TransparencyPercent=0',
        'LanguageCode=de',
        'HasSavedPopupLocation=0',
        'PopupLocationX=0',
        'PopupLocationY=0',
        'PopupScalePercent=100',
        'PanelSkinId=08',
        'PopupDisplayMode=Standard',
        'PopupSectionMode=Both',
        'RotatingMeterGlossEnabled=1'
    )
}

function Remove-PortableNoise {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetDirectory
    )

    $excludedFiles = @(
        'Verbrauch.txt',
        'Verbrauch.txt_',
        'Verbrauch.archiv.txt',
        'Verbrauch.archiv.txt_',
        $settingsBackupFileName,
        $settingsFileName
    )

    foreach ($name in $excludedFiles) {
        Get-ChildItem -LiteralPath $TargetDirectory -Recurse -Force -File -Filter $name -ErrorAction SilentlyContinue | ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Force
        }
    }

    $excludedFilePatterns = @(
        '*.bak',
        '*.bak_*',
        '*.bak-*',
        '*.backup',
        '*.old',
        '*.tmp',
        '*~'
    )

    foreach ($pattern in $excludedFilePatterns) {
        Get-ChildItem -LiteralPath $TargetDirectory -Recurse -Force -File -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Force
        }
    }

    Get-ChildItem -LiteralPath $TargetDirectory -Recurse -Force -File -Filter 'Verbrauch.archiv.*.txt.gz' -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force
    }

    Get-ChildItem -LiteralPath $TargetDirectory -Recurse -Force -File -Filter '*.lnk' -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force
    }

    $excludedDirectories = @(
        (Join-Path $TargetDirectory 'TrafficView'),
        (Join-Path $TargetDirectory 'Logs')
    )

    foreach ($path in $excludedDirectories) {
        if (Test-Path $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }

    Get-ChildItem -LiteralPath $TargetDirectory -Recurse -Force -Directory -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -eq '__pycache__' -or $_.Name -eq '.delete'
    } | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Recurse -Force
    }

    Get-ChildItem -LiteralPath $TargetDirectory -Recurse -Force -File -ErrorAction SilentlyContinue | Where-Object {
        $_.Extension -in '.py', '.pyc'
    } | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force
    }
}

function New-PortableStageFromDist {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetDirectory
    )

    New-Item -ItemType Directory -Force -Path $TargetDirectory | Out-Null

    Get-ChildItem -LiteralPath $distDir -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $TargetDirectory -Recurse -Force
    }

    Remove-PortableNoise -TargetDirectory $TargetDirectory
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

if (Test-Path $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

New-PortableStageFromDist -TargetDirectory $stageDir
Copy-Item -LiteralPath $stageDir -Destination $stageWithDefaultsDir -Recurse -Force
Set-Content -LiteralPath (Join-Path $stageWithDefaultsDir $settingsFileName) -Value (Get-DefaultSettingsLines) -Encoding ASCII

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path $defaultsZipPath) {
    Remove-Item -LiteralPath $defaultsZipPath -Force
}

Compress-Archive -LiteralPath $stageDir -DestinationPath $zipPath -CompressionLevel Optimal
Compress-Archive -LiteralPath $stageWithDefaultsDir -DestinationPath $defaultsZipPath -CompressionLevel Optimal

Remove-Item -LiteralPath $stageRoot -Recurse -Force

Write-Host ''
Write-Host "Portable-Paket fertig: $zipPath"
Write-Host "Portable-Paket mit Standard-Einstellungen fertig: $defaultsZipPath"
