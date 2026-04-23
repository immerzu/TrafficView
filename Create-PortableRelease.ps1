param(
    [string]$OutputRoot,
    [string]$ReleaseName,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$distDirectory = Join-Path $root "dist"
$readmeFile = Join-Path $root "README.md"
$buildAssetsScript = Join-Path $root "Build-DisplayModeAssets.ps1"
$buildScript = Join-Path $root "build.ps1"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path (Split-Path -Parent $root) "Ausgabe"
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
        [string]$ChildPath
    )

    $fullParentPath = Get-FullPath -Path $ParentPath
    $fullChildPath = Get-FullPath -Path $ChildPath

    if (-not $fullParentPath.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
        $fullParentPath += [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $fullChildPath.StartsWith($fullParentPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Pfad liegt ausserhalb des erwarteten Ausgabeordners: $fullChildPath"
    }
}

function Get-TrafficViewVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReadmePath
    )

    if (-not (Test-Path -LiteralPath $ReadmePath)) {
        return "dev"
    }

    $firstLine = Get-Content -LiteralPath $ReadmePath -TotalCount 1
    if ($firstLine -match "^#\s+TrafficView\s+(.+?)\s*$") {
        return $Matches[1].Trim()
    }

    return "dev"
}

function Test-RequiredReleaseFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectory
    )

    $requiredPaths = @(
        "TrafficView.exe",
        "TrafficView.exe.config",
        "TrafficView.languages.ini",
        "TrafficView.portable",
        "Manual.txt",
        "README.md",
        "LOLO-SOFT_00_SW.png",
        "DisplayModeAssets\Simple\TrafficView.panel.png",
        "DisplayModeAssets\Simple\TrafficView.panel.90.png",
        "DisplayModeAssets\Simple\TrafficView.panel.110.png",
        "DisplayModeAssets\Simple\TrafficView.panel.125.png",
        "DisplayModeAssets\Simple\TrafficView.panel.150.png",
        "DisplayModeAssets\Simple\TrafficView.center_core.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.panel.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.panel.90.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.panel.110.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.panel.125.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.panel.150.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.center_core.png"
    )

    foreach ($relativePath in $requiredPaths) {
        $requiredPath = Join-Path $ReleaseDirectory $relativePath
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Pflichtdatei fehlt in der Portable-Ausgabe: $requiredPath"
        }
    }
}

function Test-NoPrivateRuntimeFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectory
    )

    $forbiddenFiles = Get-ChildItem -LiteralPath $ReleaseDirectory -Recurse -File -Force | Where-Object {
        $_.Name -eq "TrafficView.settings.ini" -or
        $_.Name -eq "TrafficView.settings.ini_" -or
        $_.Name -eq "TrafficView.log" -or
        $_.Name -like "Verbrauch*.txt" -or
        $_.Name -like "Verbrauch*.txt_" -or
        $_.Name -like "Verbrauch*.txt.gz"
    }

    if ($forbiddenFiles) {
        $forbiddenList = ($forbiddenFiles | ForEach-Object { $_.FullName }) -join [Environment]::NewLine
        throw "Private Laufzeitdaten duerfen nicht in die Portable-Ausgabe:$([Environment]::NewLine)$forbiddenList"
    }
}

function Copy-ReleaseItem {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$TargetDirectory,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $sourcePath = Join-Path $SourceDirectory $RelativePath
    $targetPath = Join-Path $TargetDirectory $RelativePath

    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Build-Ausgabedatei fehlt: $sourcePath"
    }

    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Recurse -Force
}

if (-not $SkipBuild) {
    if (-not (Test-Path -LiteralPath $buildAssetsScript)) {
        throw "Asset-Build-Skript nicht gefunden: $buildAssetsScript"
    }

    if (-not (Test-Path -LiteralPath $buildScript)) {
        throw "Build-Skript nicht gefunden: $buildScript"
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File $buildAssetsScript
    & powershell -NoProfile -ExecutionPolicy Bypass -File $buildScript
}

if (-not (Test-Path -LiteralPath $distDirectory)) {
    throw "Build-Ausgabeordner nicht gefunden: $distDirectory"
}

$version = Get-TrafficViewVersion -ReadmePath $readmeFile
if ([string]::IsNullOrWhiteSpace($ReleaseName)) {
    $ReleaseName = "TrafficView_Portable_$version"
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$fullOutputRoot = Get-FullPath -Path $OutputRoot
$releaseDirectory = Join-Path $fullOutputRoot $ReleaseName
$zipPath = Join-Path $fullOutputRoot ($ReleaseName + ".zip")

Assert-PathIsInside -ParentPath $fullOutputRoot -ChildPath $releaseDirectory
Assert-PathIsInside -ParentPath $fullOutputRoot -ChildPath $zipPath

if (Test-Path -LiteralPath $releaseDirectory) {
    Remove-Item -LiteralPath $releaseDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null

$releaseItems = @(
    "TrafficView.exe",
    "TrafficView.exe.config",
    "TrafficView.languages.ini",
    "TrafficView.portable",
    "Manual.txt",
    "README.md",
    "LOLO-SOFT_00_SW.png",
    "DisplayModeAssets"
)

foreach ($releaseItem in $releaseItems) {
    Copy-ReleaseItem `
        -SourceDirectory $distDirectory `
        -TargetDirectory $releaseDirectory `
        -RelativePath $releaseItem
}

Test-RequiredReleaseFiles -ReleaseDirectory $releaseDirectory
Test-NoPrivateRuntimeFiles -ReleaseDirectory $releaseDirectory

Compress-Archive -LiteralPath $releaseDirectory -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host ""
Write-Host "Portable-Ausgabe erstellt:" $releaseDirectory
Write-Host "Portable-ZIP erstellt:" $zipPath
