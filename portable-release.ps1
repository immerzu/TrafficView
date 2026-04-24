$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$distDir = Join-Path $projectRoot 'dist'
$stageRoot = Join-Path $projectRoot '_portable_release'
$stageDir = Join-Path $stageRoot 'TrafficView'
$stageWithDefaultsRoot = Join-Path $stageRoot 'standard'
$stageWithDefaultsDir = Join-Path $stageWithDefaultsRoot 'TrafficView'
$outputRoot = Join-Path (Split-Path -Parent $projectRoot) 'Ausgabe'
$sourceDirectoryPath = Join-Path $projectRoot 'src'
$settingsFileName = 'TrafficView.settings.ini'
$settingsBackupFileName = 'TrafficView.settings.ini_'

if (-not (Test-Path $distDir)) {
    throw "Dist-Ordner nicht gefunden: $distDir"
}

if (-not (Test-Path $sourceDirectoryPath)) {
    throw "Quellcodeordner nicht gefunden: $sourceDirectoryPath"
}

function Get-TrafficViewVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory
    )

    $versionMatch = Get-ChildItem -LiteralPath $SourceDirectory -Filter *.cs |
        Sort-Object Name |
        Select-String -Pattern 'AssemblyVersion\("(?<version>\d+\.\d+\.\d+)\.\d+"\)' |
        Select-Object -First 1

    if ($null -eq $versionMatch -or -not $versionMatch.Matches[0].Groups['version'].Success) {
        throw "Versionsnummer konnte nicht aus $SourceDirectory gelesen werden."
    }

    return $versionMatch.Matches[0].Groups['version'].Value
}

$version = Get-TrafficViewVersion -SourceDirectory $sourceDirectoryPath
$zipPath = Join-Path $outputRoot ("TrafficView_Portable_{0}.zip" -f $version)
$defaultsZipPath = Join-Path $outputRoot ("TrafficView_Portable_{0}_Standard.zip" -f $version)

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
        'TaskbarPopupSectionMode=RightOnly',
        'RotatingMeterGlossEnabled=1',
        'ActivityBorderGlowEnabled=0',
        'TaskbarIntegrationEnabled=0'
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
        'TrafficView_Code.txt',
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

    $legacyRootFiles = @(
        'TrafficView.panel.png',
        'TrafficView.panel.90.png',
        'TrafficView.panel.110.png',
        'TrafficView.panel.125.png',
        'TrafficView.panel.150.png',
        'TrafficView.center_core.png'
    )

    foreach ($name in $legacyRootFiles) {
        $legacyRootFile = Join-Path $TargetDirectory $name
        if (Test-Path -LiteralPath $legacyRootFile) {
            Remove-Item -LiteralPath $legacyRootFile -Force
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
        (Join-Path $TargetDirectory 'Logs'),
        (Join-Path $TargetDirectory 'Skins')
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

function Test-PortableStage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetDirectory,

        [switch]$AllowDefaultSettings
    )

    $requiredPaths = @(
        'TrafficView.exe',
        'TrafficView.exe.config',
        'TrafficView.languages.ini',
        'TrafficView.portable',
        'Manual.txt',
        'README.md',
        'LOLO-SOFT_00_SW.png',
        'DisplayModeAssets\Simple\TrafficView.panel.png',
        'DisplayModeAssets\Simple\TrafficView.panel.90.png',
        'DisplayModeAssets\Simple\TrafficView.panel.110.png',
        'DisplayModeAssets\Simple\TrafficView.panel.125.png',
        'DisplayModeAssets\Simple\TrafficView.panel.150.png',
        'DisplayModeAssets\Simple\TrafficView.center_core.png',
        'DisplayModeAssets\SimpleBlue\TrafficView.panel.png',
        'DisplayModeAssets\SimpleBlue\TrafficView.panel.90.png',
        'DisplayModeAssets\SimpleBlue\TrafficView.panel.110.png',
        'DisplayModeAssets\SimpleBlue\TrafficView.panel.125.png',
        'DisplayModeAssets\SimpleBlue\TrafficView.panel.150.png',
        'DisplayModeAssets\SimpleBlue\TrafficView.center_core.png'
    )

    foreach ($relativePath in $requiredPaths) {
        $requiredPath = Join-Path $TargetDirectory $relativePath
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Pflichtdatei fehlt in der Portable-Stufe: $requiredPath"
        }
    }

    $skinDirectory = Join-Path $TargetDirectory 'Skins'
    if (Test-Path -LiteralPath $skinDirectory) {
        throw "Veralteter Skins-Ordner darf nicht in die Portable-Stufe: $skinDirectory"
    }

    $forbiddenFiles = Get-ChildItem -LiteralPath $TargetDirectory -Recurse -File -Force | Where-Object {
        $_.Name -eq $settingsBackupFileName -or
        $_.Name -eq 'TrafficView_Code.txt' -or
        $_.Name -eq 'TrafficView.log' -or
        $_.Name -like 'Verbrauch*.txt' -or
        $_.Name -like 'Verbrauch*.txt_' -or
        $_.Name -like 'Verbrauch*.txt.gz' -or
        ((-not $AllowDefaultSettings) -and $_.Name -eq $settingsFileName)
    }

    if ($forbiddenFiles) {
        $forbiddenList = ($forbiddenFiles | ForEach-Object { $_.FullName }) -join [Environment]::NewLine
        throw "Private oder veraltete Dateien duerfen nicht in die Portable-Stufe:$([Environment]::NewLine)$forbiddenList"
    }
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

if (Test-Path $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

New-PortableStageFromDist -TargetDirectory $stageDir
Test-PortableStage -TargetDirectory $stageDir
Copy-Item -LiteralPath $stageDir -Destination $stageWithDefaultsDir -Recurse -Force
Set-Content -LiteralPath (Join-Path $stageWithDefaultsDir $settingsFileName) -Value (Get-DefaultSettingsLines) -Encoding ASCII
Test-PortableStage -TargetDirectory $stageWithDefaultsDir -AllowDefaultSettings

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
