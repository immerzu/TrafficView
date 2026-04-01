$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $root "src"
$outputDir = Join-Path $root "dist"
$outputFile = Join-Path $outputDir "TrafficView.exe"
$manifestFile = Join-Path $root "TrafficView.manifest"
$iconFile = Join-Path $root "TrafficView.ico"
$configSourceFile = Join-Path $root "TrafficView.exe.config"
$configOutputFile = Join-Path $outputDir "TrafficView.exe.config"
$settingsOutputFile = Join-Path $outputDir "TrafficView.settings.ini"
$languageSourceFile = Join-Path $root "TrafficView.languages.ini"
$languageOutputFile = Join-Path $outputDir "TrafficView.languages.ini"
$panelAssetFiles = @(
    "TrafficView.panel.png",
    "TrafficView.panel.90.png",
    "TrafficView.panel.110.png",
    "TrafficView.panel.125.png",
    "TrafficView.panel.150.png"
)
$menuAssetFiles = @(
    "LOLO-SOFT_00_SW.png"
)
$sourceFiles = Get-ChildItem -Path $sourceDir -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$compilerCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)

$compiler = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $compiler) {
    throw "Kein passender C#-Compiler gefunden. Erwartet wurde csc.exe aus dem .NET Framework 4.x."
}

if (-not (Test-Path $manifestFile)) {
    throw "Manifest fuer DPI-Unterstuetzung nicht gefunden: $manifestFile"
}

if (-not (Test-Path $iconFile)) {
    throw "Programm-Icon nicht gefunden: $iconFile"
}

if (-not (Test-Path $configSourceFile)) {
    throw "EXE-Konfigurationsdatei nicht gefunden: $configSourceFile"
}

if (-not (Test-Path $languageSourceFile)) {
    throw "Sprachdatei nicht gefunden: $languageSourceFile"
}

if (-not (Test-Path (Join-Path $root "TrafficView.panel.png"))) {
    Write-Warning "Panel-Basis-Asset nicht gefunden: $(Join-Path $root 'TrafficView.panel.png'). Die Anwendung nutzt dann den prozeduralen Fallback."
}

if (-not $sourceFiles -or $sourceFiles.Count -eq 0) {
    throw "Keine C#-Quelldateien im Verzeichnis '$sourceDir' gefunden."
}

& $compiler `
    /nologo `
    /target:winexe `
    /optimize+ `
    /platform:anycpu `
    /win32icon:$iconFile `
    /win32manifest:$manifestFile `
    /out:$outputFile `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $sourceFiles

if ($LASTEXITCODE -ne 0) {
    throw "Build fehlgeschlagen."
}

Copy-Item $configSourceFile $configOutputFile -Force
Copy-Item $languageSourceFile $languageOutputFile -Force

foreach ($panelAssetFile in $panelAssetFiles) {
    $panelAssetSourceFile = Join-Path $root $panelAssetFile
    $panelAssetOutputFile = Join-Path $outputDir $panelAssetFile
    if (Test-Path $panelAssetSourceFile) {
        Copy-Item $panelAssetSourceFile $panelAssetOutputFile -Force
    }
}

foreach ($menuAssetFile in $menuAssetFiles) {
    $menuAssetSourceFile = Join-Path $root $menuAssetFile
    $menuAssetOutputFile = Join-Path $outputDir $menuAssetFile
    if (Test-Path $menuAssetSourceFile) {
        Copy-Item $menuAssetSourceFile $menuAssetOutputFile -Force
    }
}

$defaultSettings = @(
    "AdapterId=",
    "AdapterName=",
    "CalibrationPeakBytesPerSecond=0",
    "CalibrationDownloadPeakBytesPerSecond=0",
    "CalibrationUploadPeakBytesPerSecond=0",
    "InitialCalibrationPromptHandled=0",
    "InitialLanguagePromptHandled=0",
    "TransparencyPercent=0",
    "LanguageCode=de",
    "PopupScalePercent=100"
)

Set-Content -Path $settingsOutputFile -Value $defaultSettings -Encoding ASCII

Write-Host ""
Write-Host "Fertig:" $outputFile
