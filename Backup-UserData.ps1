param(
    [ValidateSet("Backup", "Restore")]
    [string]$Mode = "Backup",

    [string]$AppDirectory,
    [string]$BackupRoot,
    [string]$BackupPath
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestFileName = "TrafficView.UserDataBackup.manifest.txt"

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

function New-UniqueBackupDirectoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BackupRootPath,

        [Parameter(Mandatory = $true)]
        [string]$Timestamp
    )

    $baseName = "TrafficView_UserDataBackup_" + $Timestamp
    $candidate = Join-Path $BackupRootPath $baseName
    $index = 1

    while (Test-Path -LiteralPath $candidate) {
        $candidate = Join-Path $BackupRootPath ("{0}_{1:00}" -f $baseName, $index)
        $index++
    }

    return $candidate
}

function Write-BackupManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetDirectory,

        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Files
    )

    $manifestLines = @(
        "TrafficView user data backup",
        "CreatedUtc=$((Get-Date).ToUniversalTime().ToString('o'))",
        "SourceDirectory=$SourceDirectory",
        "FileCount=$($Files.Count)",
        "Files=Name|Length|Sha256"
    )

    foreach ($file in @($Files | Sort-Object Name)) {
        $hash = Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256
        $manifestLines += "{0}|{1}|{2}" -f $file.Name, $file.Length, $hash.Hash
    }

    Set-Content -LiteralPath (Join-Path $TargetDirectory $manifestFileName) -Value $manifestLines -Encoding ASCII
}

function Assert-BackupManifestMatchesFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BackupDirectory,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Files
    )

    $manifestPath = Join-Path $BackupDirectory $manifestFileName
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        return
    }

    $expectedByName = @{}
    $manifestFileCount = $null
    $manifestLines = Get-Content -LiteralPath $manifestPath

    foreach ($line in $manifestLines) {
        if ($line.StartsWith("FileCount=", [System.StringComparison]::OrdinalIgnoreCase)) {
            $parsedFileCount = 0
            $fileCountText = $line.Substring("FileCount=".Length)
            if (-not [int]::TryParse($fileCountText, [ref]$parsedFileCount)) {
                throw "Backup-Manifest enthaelt eine ungueltige Dateianzahl."
            }

            $manifestFileCount = $parsedFileCount
            continue
        }

        if ([string]::IsNullOrWhiteSpace($line) `
            -or $line.StartsWith("Files=", [System.StringComparison]::OrdinalIgnoreCase) `
            -or $line.IndexOf("|", [System.StringComparison]::Ordinal) -lt 0) {
            continue
        }

        $parts = $line.Split("|")
        if ($parts.Count -ne 3 -or $parts[0] -eq "Name") {
            continue
        }

        $expectedByName[$parts[0]] = @{
            Length = $parts[1]
            Sha256 = $parts[2]
        }
    }

    if ($null -eq $manifestFileCount) {
        throw "Backup-Manifest enthaelt keine Dateianzahl."
    }

    if ($manifestFileCount -ne $expectedByName.Count -or $Files.Count -ne $expectedByName.Count) {
        throw "Backup-Dateiliste stimmt nicht mit dem Manifest ueberein."
    }

    foreach ($file in @($Files | Sort-Object Name)) {
        if (-not $expectedByName.ContainsKey($file.Name)) {
            throw "Backup-Manifest enthaelt keinen Eintrag fuer: $($file.Name)"
        }

        $expected = $expectedByName[$file.Name]
        if ([string]$file.Length -ne [string]$expected.Length) {
            throw "Backup-Dateigroesse stimmt nicht mit dem Manifest ueberein: $($file.Name)"
        }

        $actualHash = Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256
        if (-not [string]::Equals($actualHash.Hash, [string]$expected.Sha256, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Backup-Hash stimmt nicht mit dem Manifest ueberein: $($file.Name)"
        }
    }
}

function Copy-UserDataFileForRestore {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$SourceFile,

        [Parameter(Mandatory = $true)]
        [string]$TargetDirectory
    )

    $targetPath = Join-Path $TargetDirectory $SourceFile.Name
    Assert-PathIsInside -ParentPath $TargetDirectory -ChildPath $targetPath -Description "Restore-Zieldatei"

    $tempPath = "{0}.{1}.restore.tmp" -f $targetPath, [Guid]::NewGuid().ToString("N")
    $replaceBackupPath = "{0}.{1}.restore.bak" -f $targetPath, [Guid]::NewGuid().ToString("N")
    $targetExisted = Test-Path -LiteralPath $targetPath
    $targetMoved = $false
    $restoreSucceeded = $false

    try {
        Copy-Item -LiteralPath $SourceFile.FullName -Destination $tempPath -Force

        if ($targetExisted) {
            Move-Item -LiteralPath $targetPath -Destination $replaceBackupPath -Force
            $targetMoved = $true
        }

        Move-Item -LiteralPath $tempPath -Destination $targetPath -Force
        $restoreSucceeded = $true

        if ($targetMoved -and (Test-Path -LiteralPath $replaceBackupPath)) {
            Remove-Item -LiteralPath $replaceBackupPath -Force
        }
    }
    catch {
        if (Test-Path -LiteralPath $tempPath) {
            Remove-Item -LiteralPath $tempPath -Force
        }

        if ($targetMoved -and -not (Test-Path -LiteralPath $targetPath) -and (Test-Path -LiteralPath $replaceBackupPath)) {
            Move-Item -LiteralPath $replaceBackupPath -Destination $targetPath -Force
        }

        throw
    }
    finally {
        if ($restoreSucceeded -and (Test-Path -LiteralPath $replaceBackupPath)) {
            Remove-Item -LiteralPath $replaceBackupPath -Force
        }
    }
}

$fullAppDirectory = Get-FullPath -Path $AppDirectory

if ($Mode -eq "Backup") {
    Assert-DirectoryExists -Path $fullAppDirectory -Description "App-Ordner"
    $fullBackupRoot = Get-FullPath -Path $BackupRoot
    New-Item -ItemType Directory -Force -Path $fullBackupRoot | Out-Null

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $targetDirectory = New-UniqueBackupDirectoryPath -BackupRootPath $fullBackupRoot -Timestamp $timestamp
    Assert-PathIsInside -ParentPath $fullBackupRoot -ChildPath $targetDirectory -Description "Backup-Zielordner"
    New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null

    $files = Get-UserDataFiles -Directory $fullAppDirectory
    foreach ($file in $files) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $targetDirectory $file.Name) -Force
    }

    Write-BackupManifest -TargetDirectory $targetDirectory -SourceDirectory $fullAppDirectory -Files $files

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
if ($filesToRestore.Count -eq 0) {
    throw "Backup-Ordner enthaelt keine wiederherstellbaren TrafficView-Nutzerdaten: $fullBackupPath"
}

Assert-BackupManifestMatchesFiles -BackupDirectory $fullBackupPath -Files $filesToRestore

foreach ($file in $filesToRestore) {
    Copy-UserDataFileForRestore -SourceFile $file -TargetDirectory $fullAppDirectory
}

Write-Host "Nutzerdaten wiederhergestellt nach:" $fullAppDirectory
Write-Host "Wiederhergestellte Dateien:" $filesToRestore.Count
