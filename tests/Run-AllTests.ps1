$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

function Invoke-CheckedScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$ScriptPath
    )

    if (-not (Test-Path -LiteralPath $ScriptPath)) {
        throw "Test-Schritt '$Name' wurde nicht gefunden: $ScriptPath"
    }

    Write-Host ""
    Write-Host "== $Name =="
    & powershell -NoProfile -ExecutionPolicy Bypass -File $ScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw "Test-Schritt '$Name' ist fehlgeschlagen."
    }
}

Invoke-CheckedScript -Name "Build" -ScriptPath (Join-Path $repoRoot "build.ps1")
Invoke-CheckedScript -Name "Smoke tests" -ScriptPath (Join-Path $PSScriptRoot "Run-SmokeTests.ps1")
Invoke-CheckedScript -Name "Tooling script tests" -ScriptPath (Join-Path $PSScriptRoot "Run-ToolingScriptTests.ps1")
Invoke-CheckedScript -Name "Release script tests" -ScriptPath (Join-Path $PSScriptRoot "Run-ReleaseScriptTests.ps1")

Write-Host ""
Write-Host "All tests passed."
