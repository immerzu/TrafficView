$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

function Test-ScriptSyntax {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath
    )

    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($ScriptPath, [ref]$tokens, [ref]$errors) | Out-Null

    if ($errors -and $errors.Count -gt 0) {
        throw "PowerShell-Syntaxfehler in ${ScriptPath}: $($errors[0].Message)"
    }
}

function Invoke-UserDataRestoreProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppDirectory,

        [Parameter(Mandatory = $true)]
        [string]$BackupPath,

        [Parameter(Mandatory = $true)]
        [string]$OutputPrefix
    )

    $stdoutPath = "${OutputPrefix}.out"
    $stderrPath = "${OutputPrefix}.err"

    return Start-Process -FilePath "powershell" -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        (Join-Path $repoRoot "Backup-UserData.ps1"),
        "-Mode",
        "Restore",
        "-AppDirectory",
        $AppDirectory,
        "-BackupPath",
        $BackupPath
    ) -Wait -PassThru -NoNewWindow -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
}

function Test-UserDataBackupRestore {
    $tempRoot = Join-Path $env:TEMP ("TrafficViewTooling_" + [Guid]::NewGuid().ToString("N"))
    $appDirectory = Join-Path $tempRoot "app"
    $backupRoot = Join-Path $tempRoot "backups"

    New-Item -ItemType Directory -Force -Path $appDirectory | Out-Null
    Set-Content -LiteralPath (Join-Path $appDirectory "TrafficView.settings.ini") -Value "LanguageCode=de" -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $appDirectory "Verbrauch.txt") -Value "usage" -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $appDirectory "Verbrauch.archiv.202604.txt.gz") -Value "archive" -Encoding ASCII

    try {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "Backup-UserData.ps1") -AppDirectory $appDirectory -BackupRoot $backupRoot
        if ($LASTEXITCODE -ne 0) {
            throw "Backup-UserData.ps1 Backup ist fehlgeschlagen."
        }

        $backupDirectory = Get-ChildItem -LiteralPath $backupRoot -Directory | Select-Object -First 1
        if (-not $backupDirectory) {
            throw "Backup-UserData.ps1 hat keinen Backup-Ordner erstellt."
        }

        $manifestPath = Join-Path $backupDirectory.FullName "TrafficView.UserDataBackup.manifest.txt"
        if (-not (Test-Path -LiteralPath $manifestPath)) {
            throw "Backup-UserData.ps1 hat kein Manifest erstellt."
        }

        $manifestText = Get-Content -LiteralPath $manifestPath -Raw
        if ($manifestText.IndexOf("FileCount=3", [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Backup-Manifest enthaelt nicht die erwartete Dateianzahl."
        }

        Remove-Item -LiteralPath (Join-Path $appDirectory "TrafficView.settings.ini") -Force
        Remove-Item -LiteralPath (Join-Path $appDirectory "Verbrauch.txt") -Force
        Remove-Item -LiteralPath (Join-Path $appDirectory "Verbrauch.archiv.202604.txt.gz") -Force
        Set-Content -LiteralPath (Join-Path $appDirectory "TrafficView.settings.ini") -Value "LanguageCode=old" -Encoding ASCII

        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "Backup-UserData.ps1") -Mode Restore -AppDirectory $appDirectory -BackupPath $backupDirectory.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "Backup-UserData.ps1 Restore ist fehlgeschlagen."
        }

        $expectedFiles = @(
            "TrafficView.settings.ini",
            "Verbrauch.txt",
            "Verbrauch.archiv.202604.txt.gz"
        )

        foreach ($expectedFile in $expectedFiles) {
            if (-not (Test-Path -LiteralPath (Join-Path $appDirectory $expectedFile))) {
                throw "Restore-Datei fehlt: $expectedFile"
            }
        }

        $settingsText = Get-Content -LiteralPath (Join-Path $appDirectory "TrafficView.settings.ini") -Raw
        if ($settingsText.IndexOf("LanguageCode=de", [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Restore hat bestehende Settings nicht korrekt ersetzt."
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }
}

function Test-UserDataRestoreRejectsTamperedBackup {
    $tempRoot = Join-Path $env:TEMP ("TrafficViewTooling_" + [Guid]::NewGuid().ToString("N"))
    $appDirectory = Join-Path $tempRoot "app"
    $restoreDirectory = Join-Path $tempRoot "restore"
    $backupRoot = Join-Path $tempRoot "backups"

    try {
        New-Item -ItemType Directory -Force -Path $appDirectory | Out-Null
        Set-Content -LiteralPath (Join-Path $appDirectory "TrafficView.settings.ini") -Value "LanguageCode=de" -Encoding ASCII
        Set-Content -LiteralPath (Join-Path $appDirectory "Verbrauch.txt") -Value "usage" -Encoding ASCII

        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "Backup-UserData.ps1") -AppDirectory $appDirectory -BackupRoot $backupRoot
        if ($LASTEXITCODE -ne 0) {
            throw "Backup-UserData.ps1 Backup fuer Manipulationstest ist fehlgeschlagen."
        }

        $backupDirectory = Get-ChildItem -LiteralPath $backupRoot -Directory | Select-Object -First 1
        if (-not $backupDirectory) {
            throw "Backup-UserData.ps1 hat keinen Backup-Ordner fuer Manipulationstest erstellt."
        }

        Set-Content -LiteralPath (Join-Path $backupDirectory.FullName "Verbrauch.txt") -Value "abuse" -Encoding ASCII

        $process = Invoke-UserDataRestoreProcess `
            -AppDirectory $restoreDirectory `
            -BackupPath $backupDirectory.FullName `
            -OutputPrefix (Join-Path $tempRoot "restore-tampered")

        if ($process.ExitCode -eq 0) {
            throw "Backup-UserData.ps1 Restore darf manipulierte Backups nicht akzeptieren."
        }

        if (Test-Path -LiteralPath (Join-Path $restoreDirectory "Verbrauch.txt")) {
            throw "Manipuliertes Backup wurde trotz Manifestfehler wiederhergestellt."
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }
}

function Test-UserDataRestoreRejectsIncompleteManifestBackup {
    $tempRoot = Join-Path $env:TEMP ("TrafficViewTooling_" + [Guid]::NewGuid().ToString("N"))
    $appDirectory = Join-Path $tempRoot "app"
    $restoreDirectory = Join-Path $tempRoot "restore"
    $backupRoot = Join-Path $tempRoot "backups"

    try {
        New-Item -ItemType Directory -Force -Path $appDirectory | Out-Null
        Set-Content -LiteralPath (Join-Path $appDirectory "TrafficView.settings.ini") -Value "LanguageCode=de" -Encoding ASCII
        Set-Content -LiteralPath (Join-Path $appDirectory "Verbrauch.txt") -Value "usage" -Encoding ASCII

        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "Backup-UserData.ps1") -AppDirectory $appDirectory -BackupRoot $backupRoot
        if ($LASTEXITCODE -ne 0) {
            throw "Backup-UserData.ps1 Backup fuer unvollstaendigen Manifesttest ist fehlgeschlagen."
        }

        $backupDirectory = Get-ChildItem -LiteralPath $backupRoot -Directory | Select-Object -First 1
        if (-not $backupDirectory) {
            throw "Backup-UserData.ps1 hat keinen Backup-Ordner fuer unvollstaendigen Manifesttest erstellt."
        }

        Remove-Item -LiteralPath (Join-Path $backupDirectory.FullName "Verbrauch.txt") -Force

        $process = Invoke-UserDataRestoreProcess `
            -AppDirectory $restoreDirectory `
            -BackupPath $backupDirectory.FullName `
            -OutputPrefix (Join-Path $tempRoot "restore-incomplete")

        if ($process.ExitCode -eq 0) {
            throw "Backup-UserData.ps1 Restore darf unvollstaendige manifestierte Backups nicht akzeptieren."
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }
}

function Test-UserDataRestoreRejectsEmptyBackup {
    $tempRoot = Join-Path $env:TEMP ("TrafficViewTooling_" + [Guid]::NewGuid().ToString("N"))
    $appDirectory = Join-Path $tempRoot "app"
    $emptyBackupDirectory = Join-Path $tempRoot "empty-backup"

    try {
        New-Item -ItemType Directory -Force -Path $appDirectory | Out-Null
        New-Item -ItemType Directory -Force -Path $emptyBackupDirectory | Out-Null

        $process = Invoke-UserDataRestoreProcess `
            -AppDirectory $appDirectory `
            -BackupPath $emptyBackupDirectory `
            -OutputPrefix (Join-Path $tempRoot "restore-empty")

        if ($process.ExitCode -eq 0) {
            throw "Backup-UserData.ps1 Restore darf leere Backups nicht akzeptieren."
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }
}

function Test-BuildBackupRestore {
    $tempRoot = Join-Path $env:TEMP ("TrafficViewBuildBackup_" + [Guid]::NewGuid().ToString("N"))
    $distDir = Join-Path $tempRoot "dist"

    try {
        New-Item -ItemType Directory -Force -Path $distDir | Out-Null

        $targets = @(
            "TrafficView.settings.ini",
            "TrafficView.settings.ini_",
            "Verbrauch.txt",
            "Verbrauch.txt_",
            "Verbrauch.archiv.txt",
            "Verbrauch.archiv.txt_"
        )

        # Test 1: Backup existiert, Original fehlt -> Wiederherstellung
        $testOriginal = Join-Path $distDir $targets[0]
        $testBackup = $testOriginal + ".build_backup"
        Set-Content -LiteralPath $testBackup -Value "restored-content" -Encoding ASCII

        foreach ($targetName in $targets) {
            $target = Join-Path $distDir $targetName
            $backupPath = $target + ".build_backup"
            if (Test-Path $backupPath) {
                if (Test-Path $target) {
                    Write-Host "[Warnung] Backup und Original existieren: $target"
                }
                else {
                    Write-Host "[Wiederherstellung] $target aus .build_backup"
                    Copy-Item -LiteralPath $backupPath -Destination $target -Force
                }
            }
        }

        if (-not (Test-Path -LiteralPath $testOriginal)) {
            throw "Build-Backup: Original wurde nicht aus .build_backup wiederhergestellt."
        }

        $restoredContent = Get-Content -LiteralPath $testOriginal -Raw
        if ($restoredContent.IndexOf("restored-content", [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Build-Backup: Wiederhergestellter Inhalt stimmt nicht."
        }

        # Test 2: Backup und Original existieren -> Original nicht ueberschreiben
        Set-Content -LiteralPath $testOriginal -Value "original-content" -Encoding ASCII
        Set-Content -LiteralPath $testBackup -Value "backup-content" -Encoding ASCII

        foreach ($targetName in $targets) {
            $target = Join-Path $distDir $targetName
            $backupPath = $target + ".build_backup"
            if (Test-Path $backupPath) {
                if (Test-Path $target) {
                    Write-Host "[Warnung] Backup und Original existieren: $target"
                }
                else {
                    Write-Host "[Wiederherstellung] $target aus .build_backup"
                    Copy-Item -LiteralPath $backupPath -Destination $target -Force
                }
            }
        }

        $originalContent = Get-Content -LiteralPath $testOriginal -Raw
        if ($originalContent.IndexOf("original-content", [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Build-Backup: Original wurde ueberschrieben, obwohl beide existierten."
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }
}

$scriptsToParse = @(
    "Backup-UserData.ps1",
    "Bump-Version.ps1",
    "Build-DisplayModeAssets.ps1",
    "Create-PortableRelease.ps1",
    "portable-release.ps1",
    "build.ps1",
    "tests\Run-AllTests.ps1",
    "tests\Run-ReleaseScriptTests.ps1",
    "tests\Run-SmokeTests.ps1"
)

foreach ($relativeScriptPath in $scriptsToParse) {
    Test-ScriptSyntax -ScriptPath (Join-Path $repoRoot $relativeScriptPath)
}

function Test-ImportSkinRejectsOversizedIni {
    $tempRoot = Join-Path $env:TEMP ("TrafficViewSkinIni_" + [Guid]::NewGuid().ToString("N"))
    $skinDir = Join-Path $tempRoot "TestSkin"

    try {
        New-Item -ItemType Directory -Force -Path $skinDir | Out-Null

        Set-Content -LiteralPath (Join-Path $skinDir "skin.ini") -Value ([string]::new('X', 65537)) -Encoding ASCII -NoNewline

        @("TrafficView.panel.90.png", "TrafficView.panel.png", "TrafficView.panel.110.png", "TrafficView.panel.125.png", "TrafficView.panel.150.png") | ForEach-Object {
            Set-Content -LiteralPath (Join-Path $skinDir $_) -Value "" -Encoding ASCII
        }

        $stdoutPath = Join-Path $tempRoot "import-stdout.txt"
        $stderrPath = Join-Path $tempRoot "import-stderr.txt"
        $process = Start-Process -FilePath "powershell" -ArgumentList @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $repoRoot "Import-Skin.ps1"),
            "-SourceSkinDirectory",
            $skinDir,
            "-ReplaceExisting"
        ) -Wait -PassThru -NoNewWindow -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath

        if ($process.ExitCode -eq 0) {
            throw "Import-Skin.ps1 hat uebergrosse skin.ini akzeptiert."
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }
}

function Test-PortableReleaseSha256 {
    $tempRoot = Join-Path $env:TEMP ("TrafficViewSha256_" + [Guid]::NewGuid().ToString("N"))
    $releaseName = "TrafficView_Portable_ShaTest"
    $zipPath = Join-Path $tempRoot ($releaseName + ".zip")
    $sha256Path = $zipPath + ".sha256"

    try {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "Create-PortableRelease.ps1") `
            -OutputRoot $tempRoot `
            -ReleaseName $releaseName `
            -SkipBuild
        if ($LASTEXITCODE -ne 0) {
            throw "Create-PortableRelease.ps1 fuer SHA256-Test ist fehlgeschlagen."
        }

        if (-not (Test-Path -LiteralPath $zipPath)) {
            throw "ZIP fehlt: $zipPath"
        }

        if (-not (Test-Path -LiteralPath $sha256Path)) {
            throw "SHA256-Begleitdatei fehlt: $sha256Path"
        }

        $sha256Content = (Get-Content -LiteralPath $sha256Path -Raw).Trim()
        if ($sha256Content -notmatch '^[0-9a-f]{64}  .+\.zip$') {
            throw "SHA256-Begleitdatei hat unerwartetes Format: $sha256Content"
        }

        $parts = $sha256Content -split '  '
        $hashFromFile = $parts[0]
        $fileNameFromFile = $parts[1]

        $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($hashFromFile -ne $zipHash) {
            throw "SHA256-Hash stimmt nicht ueberein: Datei=$hashFromFile Get-FileHash=$zipHash"
        }

        $zipFileName = Split-Path -Leaf $zipPath
        if ($fileNameFromFile -ne $zipFileName) {
            throw "ZIP-Dateiname in SHA256-Datei stimmt nicht: '$fileNameFromFile' vs '$zipFileName'"
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }
}

Test-UserDataBackupRestore
Test-UserDataRestoreRejectsTamperedBackup
Test-UserDataRestoreRejectsIncompleteManifestBackup
Test-UserDataRestoreRejectsEmptyBackup
Test-BuildBackupRestore
Test-PortableReleaseSha256
Test-ImportSkinRejectsOversizedIni

Write-Host "Tooling script tests passed."
