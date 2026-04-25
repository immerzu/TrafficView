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

Test-UserDataBackupRestore
Test-UserDataRestoreRejectsTamperedBackup
Test-UserDataRestoreRejectsIncompleteManifestBackup
Test-UserDataRestoreRejectsEmptyBackup

Write-Host "Tooling script tests passed."
