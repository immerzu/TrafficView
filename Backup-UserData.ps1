param(
    [ValidateSet("Backup", "Restore")]
    [string]$Mode = "Backup",

    [string]$AppDirectory,
    [string]$BackupRoot,
    [string]$BackupPath
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($AppDirectory)) {
    $AppDirectory = Join-Path $root "dist"
}

if ([string]::IsNullOrWhiteSpace($BackupRoot)) {
    $BackupRoot = Join-Path (Split-Path -Parent $root) "Ausgabe"
}

function Get-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return [System.IO.Path]::GetFullPath($Path)
}

function Get-UserDataFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Directory
    )

    $exactNames = @(
        "TrafficView.settings.ini",
        "TrafficView.settings.ini_",
        "Verbrauch.txt",
        "Verbrauch.archiv.txt"
    )

    $files = @()
    foreach ($name in $exactNames) {
        $path = Join-Path $Directory $name
        if (Test-Path -LiteralPath $path) {
            $files += Get-Item -LiteralPath $path
        }
    }

    $files += Get-ChildItem -LiteralPath $Directory -File -Force -Filter "Verbrauch.archiv.*.txt.gz" -ErrorAction SilentlyContinue
    return @($files | Sort-Object Name -Unique)
}

function Assert-DirectoryExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Description wurde nicht gefunden: $Path"
    }
}

$fullAppDirectory = Get-FullPath -Path $AppDirectory

if ($Mode -eq "Backup") {
    Assert-DirectoryExists -Path $fullAppDirectory -Description "App-Ordner"
    New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $targetDirectory = Join-Path (Get-FullPath -Path $BackupRoot) ("TrafficView_UserDataBackup_" + $timestamp)
    New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null

    $files = Get-UserDataFiles -Directory $fullAppDirectory
    foreach ($file in $files) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $targetDirectory $file.Name) -Force
    }

    Write-Host "Nutzerdaten-Sicherung erstellt:" $targetDirectory
    Write-Host "Gesicherte Dateien:" $files.Count
    return
}

if ([string]::IsNullOrWhiteSpace($BackupPath)) {
    throw "Fuer Restore muss -BackupPath angegeben werden."
}

$fullBackupPath = Get-FullPath -Path $BackupPath
Assert-DirectoryExists -Path $fullBackupPath -Description "Backup-Ordner"
New-Item -ItemType Directory -Force -Path $fullAppDirectory | Out-Null

$filesToRestore = Get-UserDataFiles -Directory $fullBackupPath
foreach ($file in $filesToRestore) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $fullAppDirectory $file.Name) -Force
}

Write-Host "Nutzerdaten wiederhergestellt nach:" $fullAppDirectory
Write-Host "Wiederhergestellte Dateien:" $filesToRestore.Count
