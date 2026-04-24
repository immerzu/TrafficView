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

        Remove-Item -LiteralPath (Join-Path $appDirectory "TrafficView.settings.ini") -Force
        Remove-Item -LiteralPath (Join-Path $appDirectory "Verbrauch.txt") -Force
        Remove-Item -LiteralPath (Join-Path $appDirectory "Verbrauch.archiv.202604.txt.gz") -Force

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

Write-Host "Tooling script tests passed."
