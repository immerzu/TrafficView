param(
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$distDir = Join-Path $projectRoot 'dist'
$stageRoot = Join-Path $projectRoot '_portable_release'
$stageDir = Join-Path $stageRoot 'TrafficView'
$stageWithDefaultsRoot = Join-Path $stageRoot 'standard'
$stageWithDefaultsDir = Join-Path $stageWithDefaultsRoot 'TrafficView'
$sourceDirectoryPath = Join-Path $projectRoot 'src'
$settingsFileName = 'TrafficView.settings.ini'
$settingsBackupFileName = 'TrafficView.settings.ini_'

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path (Split-Path -Parent $projectRoot) 'Ausgabe'
}

function Get-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-PathIsInside {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ParentPath,

        [Parameter(Mandatory = $true)]
        [string]$ChildPath,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $fullParentPath = Get-FullPath -Path $ParentPath
    $fullChildPath = Get-FullPath -Path $ChildPath

    if (-not $fullParentPath.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
        $fullParentPath += [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $fullChildPath.StartsWith($fullParentPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description liegt ausserhalb des erwarteten Basisordners: $fullChildPath"
    }
}

$projectRoot = Get-FullPath -Path $projectRoot
$distDir = Get-FullPath -Path $distDir
$stageRoot = Get-FullPath -Path $stageRoot
$stageDir = Get-FullPath -Path $stageDir
$stageWithDefaultsRoot = Get-FullPath -Path $stageWithDefaultsRoot
$stageWithDefaultsDir = Get-FullPath -Path $stageWithDefaultsDir
$sourceDirectoryPath = Get-FullPath -Path $sourceDirectoryPath
$OutputRoot = Get-FullPath -Path $OutputRoot

Assert-PathIsInside -ParentPath $projectRoot -ChildPath $stageRoot -Description 'Portable-Staging-Ordner'
Assert-PathIsInside -ParentPath $stageRoot -ChildPath $stageDir -Description 'Portable-Stufe'
Assert-PathIsInside -ParentPath $stageRoot -ChildPath $stageWithDefaultsRoot -Description 'Portable-Standard-Staging-Ordner'
Assert-PathIsInside -ParentPath $stageWithDefaultsRoot -ChildPath $stageWithDefaultsDir -Description 'Portable-Standard-Stufe'

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

function ConvertTo-ZipPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return ($Path -replace '\\', '/').Trim([char]'/')
}

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,

        [Parameter(Mandatory = $true)]
        [string]$ChildPath
    )

    $baseFullPath = Get-FullPath -Path $BasePath
    if (-not $baseFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
        $baseFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = New-Object System.Uri($baseFullPath)
    $childUri = New-Object System.Uri((Get-FullPath -Path $ChildPath))
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($childUri).ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Get-CurrentGitCommit {
    try {
        $commit = & git -C $projectRoot rev-parse HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commit)) {
            return ($commit | Select-Object -First 1).Trim()
        }
    }
    catch {
    }

    return 'unknown'
}

function New-ReleaseManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectory,

        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $manifestPath = Join-Path $ReleaseDirectory 'release-manifest.json'
    $fileEntries = @(
        Get-ChildItem -LiteralPath $ReleaseDirectory -Recurse -File -Force |
            Where-Object {
                -not ([string]::Equals(
                    (Get-FullPath -Path $_.FullName),
                    (Get-FullPath -Path $manifestPath),
                    [System.StringComparison]::OrdinalIgnoreCase))
            } |
            Sort-Object FullName |
            ForEach-Object {
                $relativePath = Get-RelativePath -BasePath $ReleaseDirectory -ChildPath $_.FullName
                [PSCustomObject]@{
                    path = ConvertTo-ZipPath -Path $relativePath
                    bytes = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
    )

    $manifest = [PSCustomObject]@{
        app = 'TrafficView'
        version = $Version
        commit = Get-CurrentGitCommit
        createdUtc = (Get-Date).ToUniversalTime().ToString('o')
        files = $fileEntries
    }

    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
}

$version = Get-TrafficViewVersion -SourceDirectory $sourceDirectoryPath
$zipPath = Join-Path $OutputRoot ("TrafficView_Portable_{0}.zip" -f $version)
$defaultsZipPath = Join-Path $OutputRoot ("TrafficView_Portable_{0}_Standard.zip" -f $version)

Assert-PathIsInside -ParentPath $OutputRoot -ChildPath $zipPath -Description 'Portable-ZIP'
Assert-PathIsInside -ParentPath $OutputRoot -ChildPath $defaultsZipPath -Description 'Portable-Standard-ZIP'

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
        '*.new.exe',
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
        'release-manifest.json',
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
        $_.Name -eq 'TrafficView.new.exe' -or
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

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

if (Test-Path $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

try {
    New-PortableStageFromDist -TargetDirectory $stageDir
    New-ReleaseManifest -ReleaseDirectory $stageDir -Version $version
    Test-PortableStage -TargetDirectory $stageDir
    Copy-Item -LiteralPath $stageDir -Destination $stageWithDefaultsDir -Recurse -Force
    Set-Content -LiteralPath (Join-Path $stageWithDefaultsDir $settingsFileName) -Value (Get-DefaultSettingsLines) -Encoding ASCII
    New-ReleaseManifest -ReleaseDirectory $stageWithDefaultsDir -Version $version
    Test-PortableStage -TargetDirectory $stageWithDefaultsDir -AllowDefaultSettings

    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    if (Test-Path $defaultsZipPath) {
        Remove-Item -LiteralPath $defaultsZipPath -Force
    }

    Compress-Archive -LiteralPath $stageDir -DestinationPath $zipPath -CompressionLevel Optimal
    Compress-Archive -LiteralPath $stageWithDefaultsDir -DestinationPath $defaultsZipPath -CompressionLevel Optimal
}
finally {
    if (Test-Path $stageRoot) {
        Remove-Item -LiteralPath $stageRoot -Recurse -Force
    }
}

Write-Host ''
Write-Host "Portable-Paket fertig: $zipPath"
Write-Host "Portable-Paket mit Standard-Einstellungen fertig: $defaultsZipPath"
