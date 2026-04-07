$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$distDir = Join-Path $projectRoot 'dist'
$stageRoot = Join-Path $projectRoot '_portable_release'
$stageDir = Join-Path $stageRoot 'TrafficView'
$outputRoot = Join-Path (Split-Path -Parent $projectRoot) 'Ausgabe'
$programCsPath = Join-Path $projectRoot 'src\Program.cs'

if (-not (Test-Path $distDir)) {
    throw "Dist-Ordner nicht gefunden: $distDir"
}

if (-not (Test-Path $programCsPath)) {
    throw "Programmdatei nicht gefunden: $programCsPath"
}

$programCsContent = Get-Content -LiteralPath $programCsPath -Raw
$versionMatch = [regex]::Match($programCsContent, 'AssemblyVersion\("(?<version>\d+\.\d+\.\d+)\.\d+"\)')
if (-not $versionMatch.Success) {
    throw "Versionsnummer konnte nicht aus $programCsPath gelesen werden."
}

$version = $versionMatch.Groups['version'].Value
$zipPath = Join-Path $outputRoot ("TrafficView.Portable.{0}.zip" -f $version)

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

if (Test-Path $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stageDir | Out-Null

Get-ChildItem -LiteralPath $distDir -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $stageDir -Recurse -Force
}

$excludedFiles = @(
    'Verbrauch.txt',
    'Verbrauch.archiv.txt',
    'TrafficView.settings.ini'
)

foreach ($name in $excludedFiles) {
    Get-ChildItem -LiteralPath $stageDir -Recurse -Force -File -Filter $name -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force
    }
}

$excludedDirectories = @(
    (Join-Path $stageDir 'TrafficView'),
    (Join-Path $stageDir 'Logs')
)

foreach ($path in $excludedDirectories) {
    if (Test-Path $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

Get-ChildItem -LiteralPath $stageDir -Recurse -Force -Directory -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -eq '__pycache__' -or $_.Name -eq '.delete'
} | ForEach-Object {
    Remove-Item -LiteralPath $_.FullName -Recurse -Force
}

Get-ChildItem -LiteralPath $stageDir -Recurse -Force -File -ErrorAction SilentlyContinue | Where-Object {
    $_.Extension -in '.py', '.pyc'
} | ForEach-Object {
    Remove-Item -LiteralPath $_.FullName -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -LiteralPath $stageDir -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host ''
Write-Host "Portable-Paket fertig: $zipPath"
