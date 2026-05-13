param(
    [ValidateSet("Backup", "Restore")]
    [string]$Mode = "Backup",

    [string]$BackupRoot,
    [string]$BackupPath
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $repoRoot
$manifestFileName = "TrafficView_CodeBackup.manifest.txt"
$bundleFileName = "TrafficView.git.bundle"
$snapshotDirectoryName = "TrafficView_Snapshot"

function Get-FullPath {
    param([Parameter(Mandatory=$true)][string]$Path)
    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-PathIsInside {
    param(
        [Parameter(Mandatory=$true)][string]$ParentPath,
        [Parameter(Mandatory=$true)][string]$ChildPath,
        [Parameter(Mandatory=$true)][string]$Description
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

function New-UniqueBackupDirectoryPath {
    param(
        [Parameter(Mandatory=$true)][string]$BackupRootPath,
        [Parameter(Mandatory=$true)][string]$Timestamp
    )
    $baseName = "TrafficView_CodeBackup_" + $Timestamp
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
        [Parameter(Mandatory=$true)][string]$TargetDirectory,
        [Parameter(Mandatory=$true)][hashtable]$Metadata
    )
    $now = Get-Date
    $lines = @(
        "# TrafficView Code Backup Manifest",
        "# =================================",
        "CreatedUtc=$($now.ToUniversalTime().ToString('o'))",
        "CreatedLocal=$($now.ToString('yyyy-MM-dd HH:mm:ss zzz'))",
        "Machine=$env:COMPUTERNAME",
        "User=$env:USERNAME",
        "RepoRemote=$($Metadata.RemoteUrl)",
        "DefaultBranch=$($Metadata.DefaultBranch)",
        "HeadCommit=$($Metadata.HeadCommit)",
        "CommitCount=$($Metadata.CommitCount)",
        "TagList=$($Metadata.TagList)",
        "BranchList=$($Metadata.BranchList)",
        "BundleFile=$bundleFileName",
        "SnapshotDirectory=$snapshotDirectoryName",
        "",
        "# Wiederherstellung:",
        "# 1. Backup-ZIP entpacken",
        "# 2. git clone --mirror TrafficView.git.bundle Zielordner/.git",
        "# 3. cd Zielordner && git config --bool core.bare false && git checkout main",
        "# 4. Den Snapshot-Ordnerinhalt (Logik/, Skins/, AGENTS.md) manuell einspielen",
        "# 5. git remote add origin <URL> (optional)",
        "",
        "# Datei-Hashes:"
    )

    $files = @(Get-ChildItem -LiteralPath $TargetDirectory -Recurse -File -Force |
        Where-Object { $_.Name -ne $manifestFileName } |
        Sort-Object FullName)
    foreach ($file in $files) {
        $relPath = $file.FullName.Substring($TargetDirectory.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
        $hash = Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256
        $lines += "$relPath|$($file.Length)|$($hash.Hash)"
    }

    Set-Content -LiteralPath (Join-Path $TargetDirectory $manifestFileName) -Value $lines -Encoding UTF8
}

function Assert-ManifestMatchesFiles {
    param(
        [Parameter(Mandatory=$true)][string]$BackupDirectory,
        [Parameter(Mandatory=$true)][object[]]$Files
    )
    $manifestPath = Join-Path $BackupDirectory $manifestFileName
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        Write-Warning "Kein Backup-Manifest gefunden, Hash-Pruefung wird uebersprungen."
        return
    }
    $expectedByName = @{}
    foreach ($line in (Get-Content -LiteralPath $manifestPath)) {
        if ($line.StartsWith("#") -or [string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line.Split("|")
        if ($parts.Count -ne 3) { continue }
        $expectedByName[$parts[0]] = @{ Length = $parts[1]; Sha256 = $parts[2] }
    }
    if ($expectedByName.Count -eq 0) {
        Write-Warning "Backup-Manifest enthaelt keine Hash-Eintraege."
        return
    }
    foreach ($file in @($Files | Sort-Object FullName)) {
        $relPath = $file.FullName.Substring($BackupDirectory.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
        if (-not $expectedByName.ContainsKey($relPath)) {
            throw "Manifest enthaelt keinen Eintrag fuer: $relPath"
        }
        $expected = $expectedByName[$relPath]
        if ([string]$file.Length -ne [string]$expected.Length) {
            throw "Dateigroesse stimmt nicht mit Manifest ueberein: $relPath"
        }
        $actualHash = Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256
        if (-not [string]::Equals($actualHash.Hash, $expected.Sha256, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Hash stimmt nicht mit Manifest ueberein: $relPath"
        }
    }
}

if ([string]::IsNullOrWhiteSpace($BackupRoot)) {
    $BackupRoot = Join-Path $projectRoot "!Backups"
}

$fullBackupRoot = Get-FullPath -Path $BackupRoot

# ============================================================================
# BACKUP-MODUS
# ============================================================================

if ($Mode -eq "Backup") {
    Write-Host "TrafficView Code-Backup wird erstellt..."
    Write-Host "  Projektordner: $projectRoot"
    Write-Host "  Repository    : $repoRoot"

    Push-Location $repoRoot
    try {
        $status = git status --porcelain
        if ($status) {
            Write-Warning "ACHTUNG: Es gibt uncommittete Aenderungen im Repository:"
            Write-Host $status
            Write-Warning "Diese werden NICHT im Bundle enthalten sein."
        }

        $remoteUrl = (git remote get-url origin 2>$null)
        if (-not $remoteUrl) { $remoteUrl = "kein Remote" }
        $headCommit = git rev-parse HEAD
        $commitCount = [int](git rev-list --count HEAD 2>$null)
        $tagList = (git tag -l | Sort-Object) -join ", "
        $branchList = (git branch -a | ForEach-Object { $_.Trim().TrimStart('*').Trim() } | Where-Object { $_ -notlike "*HEAD*" } | Sort-Object) -join ", "
        $defaultBranch = (git symbolic-ref refs/remotes/origin/HEAD 2>$null)
        if (-not $defaultBranch) { $defaultBranch = "origin/main" }
        $defaultBranch = ($defaultBranch -split "/")[-1]
    }
    finally {
        Pop-Location
    }

    New-Item -ItemType Directory -Force -Path $fullBackupRoot | Out-Null
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $targetDirectory = New-UniqueBackupDirectoryPath -BackupRootPath $fullBackupRoot -Timestamp $timestamp
    Assert-PathIsInside -ParentPath $fullBackupRoot -ChildPath $targetDirectory -Description "Backup-Zielordner"
    New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null

    # 1. Git-Bundle erstellen
    Write-Host "Erstelle Git-Bundle (alle Branches + Tags)..."
    $bundlePath = Join-Path $targetDirectory $bundleFileName
    Push-Location $repoRoot
    try {
        & git bundle create $bundlePath --branches --tags 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Git-Bundle-Erstellung fehlgeschlagen."
        }
    }
    finally {
        Pop-Location
    }

    # 2. Snapshot-Ordner mit projektwichtigen Dateien
    Write-Host "Erstelle Datei-Snapshot..."
    $snapshotPath = Join-Path $targetDirectory $snapshotDirectoryName
    $filesToCopy = @(
        "AGENTS.md",
        "Logik\Standard_Uebersicht.md",
        "Logik\Hintergrund\Standard.md",
        "Logik\Kreislogik\Standard.md",
        "Logik\PulsPfeile\Standard.md",
        "Logik\Rundumbeleuchtung\Standard.md",
        "Logik\Verlaufsgraph\Standard.md",
        "Logik\Zahlenlogik\Standard.md",
        "TrafficView\README.md",
        "TrafficView\README_EN.md",
        "TrafficView\Manual.txt",
        "TrafficView\docs\tooling-inventory.md",
        "TrafficView\docs\ui-release-checklist.md",
        "TrafficView\docs\manual-test-log.md",
        "TrafficView\docs\regressionstest-dialoge.md",
        "TrafficView\docs\anzeigentafel-produktionsvorlage.md",
        "TrafficView\TrafficView.csproj",
        "TrafficView\TrafficView.sln",
        "TrafficView\TrafficView.exe.config",
        "TrafficView\TrafficView.manifest",
        "TrafficView\TrafficView.languages.ini",
        "TrafficView\.gitignore",
        "TrafficView\.github\workflows\ci.yml",
        "Skins\Digi_01.txt",
        "Skins\Digi_01.png",
        "Skins\43f8342c-8d48-4410-93df-5e0949bba6b6.png",
        "Skins\ChatGPT Image 22. Apr. 2026, 13_11_18.png",
        "Skins\ChatGPT Image 22. Apr. 2026, 13_18_29.png",
        "Skins\TrafficView_Digi_01_clean_1530x840.png",
        "Skins\TrafficView_empty_panel_1530x840.png",
        "Skins\TrafficView_simple_blue_panel_1530x840.png",
        "Skins\futuristic_marquee_1530x840.png",
        "TrafficView\DisplayModeAssetSources\Simple\TrafficView.center_core.png",
        "TrafficView\DisplayModeAssetSources\SimpleBlue\TrafficView.center_core.png"
    )

    foreach ($relPath in $filesToCopy) {
        $sourcePath = Join-Path $projectRoot $relPath
        if (-not (Test-Path -LiteralPath $sourcePath)) {
            Write-Warning "Ueberspringe fehlende Datei: $relPath"
            continue
        }
        $targetPath = Join-Path $snapshotPath $relPath
        $targetDir = Split-Path -Parent $targetPath
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
    }

    # 3. Manifest schreiben
    Write-Host "Erstelle Backup-Manifest..."
    Write-BackupManifest -TargetDirectory $targetDirectory -Metadata @{
        RemoteUrl = $remoteUrl
        DefaultBranch = $defaultBranch
        HeadCommit = $headCommit
        CommitCount = $commitCount
        TagList = $tagList
        BranchList = $branchList
    }

    # 4. Hash-Pruefung
    $allBackupFiles = @(Get-ChildItem -LiteralPath $targetDirectory -Recurse -File -Force |
        Where-Object { $_.Name -ne $manifestFileName } |
        Sort-Object FullName)
    Assert-ManifestMatchesFiles -BackupDirectory $targetDirectory -Files $allBackupFiles

    # 5. ZIP erstellen
    $zipName = (Split-Path -Leaf $targetDirectory) + ".zip"
    $zipPath = Join-Path $fullBackupRoot $zipName
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -LiteralPath $targetDirectory -DestinationPath $zipPath -CompressionLevel Optimal

    Write-Host ""
    Write-Host "======================================================================"
    Write-Host "  CODE-BACKUP ERFOLGREICH ERSTELLT"
    Write-Host "======================================================================"
    Write-Host ""
    Write-Host "  Backup-Ordner : $targetDirectory"
    Write-Host "  Backup-ZIP    : $zipPath"
    Write-Host "  Git-Bundle    : $bundleFileName"
    Write-Host "  Branches      : $branchList"
    Write-Host "  Tags          : $tagList"
    Write-Host "  Commits       : $commitCount"
    Write-Host "  Head          : $headCommit"
    Write-Host "  Remote        : $remoteUrl"
    Write-Host ""
    Write-Host "  WIEDERHERSTELLUNG:"
    Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File Backup-CodeRepository.ps1 -Mode Restore -BackupPath <Pfad>"
    return
}

# ============================================================================
# RESTORE-MODUS
# ============================================================================

if (-not (Test-Path -LiteralPath $BackupPath)) {
    throw "Backup-Pfad nicht gefunden: $BackupPath"
}

$fullBackupPath = Get-FullPath -Path $BackupPath
$isZip = $fullBackupPath.EndsWith(".zip", [System.StringComparison]::OrdinalIgnoreCase)
$extractPath = $fullBackupPath

if ($isZip) {
    $extractPath = Join-Path ([System.IO.Path]::GetTempPath()) ("TrafficView_CodeRestore_" + [Guid]::NewGuid().ToString("N"))
    Write-Host "Entpacke ZIP nach: $extractPath"
    if (Test-Path -LiteralPath $extractPath) {
        Remove-Item -LiteralPath $extractPath -Recurse -Force
    }
    Expand-Archive -LiteralPath $fullBackupPath -DestinationPath $extractPath -Force
}

try {
    $bundlePath = Join-Path $extractPath $bundleFileName
    $snapshotPath = Join-Path $extractPath $snapshotDirectoryName
    $manifestPath = Join-Path $extractPath $manifestFileName

    if (-not (Test-Path -LiteralPath $bundlePath)) {
        throw "Git-Bundle nicht im Backup gefunden: $bundleFileName"
    }

    if (Test-Path -LiteralPath $manifestPath) {
        Write-Host ""
        Write-Host "Backup-Manifest:"
        Get-Content -LiteralPath $manifestPath | Where-Object { $_ -match "^(CreatedUtc|HeadCommit|CommitCount|DefaultBranch|TagList|RepoRemote)=" } | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
    }

    $targetRepo = Read-Host "Zielpfad fuer das wiederhergestellte Repository (leer = aktuelles Verzeichnis)"
    if ([string]::IsNullOrWhiteSpace($targetRepo)) {
        $targetRepo = Get-Location
    }
    $targetRepo = Get-FullPath -Path $targetRepo
    $repoName = "TrafficView"
    $targetRepoPath = Join-Path $targetRepo $repoName

    if (Test-Path -LiteralPath $targetRepoPath) {
        $confirm = Read-Host "'$targetRepoPath' existiert bereits. Ueberschreiben? (j/N)"
        if ($confirm -ne "j" -and $confirm -ne "J") {
            throw "Wiederherstellung abgebrochen."
        }
        Remove-Item -LiteralPath $targetRepoPath -Recurse -Force
    }

    Write-Host "Stelle Repository wieder her aus: $bundlePath"
    Write-Host "Ziel: $targetRepoPath"

    New-Item -ItemType Directory -Force -Path $targetRepoPath | Out-Null
    Push-Location $targetRepoPath
    try {
        & git init
        if ($LASTEXITCODE -ne 0) { throw "git init fehlgeschlagen." }

        & git remote add bundle-source $bundlePath
        if ($LASTEXITCODE -ne 0) { throw "git remote add fehlgeschlagen." }

        & git fetch bundle-source --tags
        if ($LASTEXITCODE -ne 0) { throw "git fetch fehlgeschlagen." }

        $branches = git branch -r | Where-Object { $_ -match "bundle-source/(.+)" } | ForEach-Object { $Matches[1] } | Where-Object { $_ -ne "HEAD" }
        if (-not $branches) {
            $branches = @("main")
        }

        foreach ($branch in $branches) {
            Write-Host "  Stelle Branch wieder her: $branch"
            & git checkout -b $branch "bundle-source/$branch" 2>$null
        }

        $defaultBranch = $branches | Where-Object { $_ -eq "main" -or $_ -eq "master" } | Select-Object -First 1
        if (-not $defaultBranch) { $defaultBranch = $branches[0] }
        & git checkout $defaultBranch

        & git remote remove bundle-source

        Write-Host ""
        Write-Host "Repository wiederhergestellt. Branches:"
        & git branch -a
        Write-Host "Tags:"
        & git tag -l
    }
    finally {
        Pop-Location
    }

    if (Test-Path -LiteralPath $snapshotPath) {
        Write-Host ""
        $copySnapshot = Read-Host "Snapshot-Dateien (Logik/, Skins/, Konfiguration) aus dem Backup kopieren? (j/N)"
        if ($copySnapshot -eq "j" -or $copySnapshot -eq "J") {
            $projectRootTarget = Split-Path -Parent $targetRepoPath
            Copy-Item -LiteralPath "$snapshotPath\*" -Destination $projectRootTarget -Recurse -Force
            Write-Host "Snapshot-Dateien kopiert nach: $projectRootTarget"
        }
    }

    Write-Host ""
    Write-Host "======================================================================"
    Write-Host "  WIEDERHERSTELLUNG ABGESCHLOSSEN"
    Write-Host "======================================================================"
    Write-Host ""
    Write-Host "  Repository: $targetRepoPath"
    Write-Host ""
    Write-Host "  Naechste Schritte (optional):"
    Write-Host "    cd $targetRepoPath"
    Write-Host "    git remote add origin https://github.com/immerzu/TrafficView.git"
    Write-Host "    git fetch origin"
    Write-Host "    git branch --set-upstream-to=origin/main main"
}
finally {
    if ($isZip -and (Test-Path -LiteralPath $extractPath)) {
        Remove-Item -LiteralPath $extractPath -Recurse -Force
    }
}
