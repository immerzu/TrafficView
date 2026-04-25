$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceDir = Join-Path $repoRoot "src"
$testOutputDir = Join-Path $repoRoot "_build_staging\\smoke-tests"
$testExePath = Join-Path $testOutputDir "TrafficView.SmokeTests.exe"
$testSourcePath = Join-Path $PSScriptRoot "TrafficView.SmokeTests.cs"

$compilerCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\\Framework\\v4.0.30319\\csc.exe")
)

$compiler = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $compiler) {
    throw "Kein passender C#-Compiler für die Smoke-Tests gefunden."
}

New-Item -ItemType Directory -Force -Path $testOutputDir | Out-Null

$sourceFiles = @(
    (Join-Path $sourceDir "AppEnums.cs"),
    (Join-Path $sourceDir "AppStorage.cs"),
    (Join-Path $sourceDir "AppLog.cs"),
    (Join-Path $sourceDir "DiagnosticsExport.cs"),
    (Join-Path $sourceDir "UiLanguage.cs"),
    (Join-Path $sourceDir "MonitorSettings.cs"),
    (Join-Path $sourceDir "PanelSkinDefinition.cs"),
    (Join-Path $sourceDir "PanelSkinCatalog.cs"),
    (Join-Path $sourceDir "RuntimeDiagnostics.cs"),
    (Join-Path $sourceDir "SkinPathPolicy.cs"),
    (Join-Path $sourceDir "TrafficRateFormatter.cs"),
    (Join-Path $sourceDir "TrafficRateSmoothing.cs"),
    (Join-Path $sourceDir "TrafficUsageLog.cs"),
    (Join-Path $sourceDir "TrafficUsageFormatter.cs"),
    $testSourcePath
)

& $compiler `
    /nologo `
    /target:exe `
    /optimize+ `
    /platform:anycpu `
    /out:$testExePath `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.IO.Compression.dll `
    /reference:System.IO.Compression.FileSystem.dll `
    $sourceFiles

if ($LASTEXITCODE -ne 0) {
    throw "Smoke-Test-Kompilierung fehlgeschlagen."
}

& $testExePath
if ($LASTEXITCODE -ne 0) {
    throw "Smoke-Tests fehlgeschlagen."
}
