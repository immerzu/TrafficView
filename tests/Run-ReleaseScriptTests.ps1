$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot "_build_staging\release-script-tests"
$modernReleaseName = "TrafficView_Portable_ScriptTest"
$modernZipPath = Join-Path $outputRoot ($modernReleaseName + ".zip")
$legacyOutputRoot = Join-Path (Split-Path -Parent $repoRoot) "Ausgabe"

function Get-TrafficViewVersion {
    $versionMatch = Get-ChildItem -LiteralPath (Join-Path $repoRoot "src") -Filter "*.cs" -File |
        Sort-Object Name |
        Select-String -Pattern 'AssemblyVersion\("(?<Version>\d+\.\d+\.\d+)\.\d+"\)' |
        Select-Object -First 1

    if (-not $versionMatch) {
        throw "AssemblyVersion wurde fuer den Release-Skript-Test nicht gefunden."
    }

    return $versionMatch.Matches[0].Groups["Version"].Value
}

function ConvertTo-ZipPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return ($Path -replace "\\", "/").Trim([char]'/')
}

function Get-ZipEntryNames {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ZipPath
    )

    $zipArchive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        return @($zipArchive.Entries | ForEach-Object { ConvertTo-ZipPath -Path $_.FullName })
    }
    finally {
        $zipArchive.Dispose()
    }
}

function Assert-ZipContains {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$EntryNames,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $requiredPath = ConvertTo-ZipPath -Path $RelativePath
    $found = @($EntryNames | Where-Object {
        $_ -eq $requiredPath -or $_.EndsWith("/$requiredPath", [System.StringComparison]::OrdinalIgnoreCase)
    }).Count -gt 0

    if (-not $found) {
        throw "Pflichtdatei fehlt im getesteten ZIP: $RelativePath"
    }
}

function Assert-ZipOmitsPrivateAndLegacyFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$EntryNames
    )

    $forbiddenEntries = @($EntryNames | Where-Object {
        $leafName = ($_ -split "/")[-1]
        $_ -match "(^|/)Skins(/|$)" -or
        $leafName -eq "TrafficView_Code.txt" -or
        $leafName -eq "TrafficView.log" -or
        $leafName -eq "TrafficView.settings.ini_" -or
        $leafName -like "Verbrauch*.txt" -or
        $leafName -like "Verbrauch*.txt_" -or
        $leafName -like "Verbrauch*.txt.gz"
    })

    if ($forbiddenEntries.Count -gt 0) {
        throw "Private oder veraltete Dateien im getesteten ZIP gefunden: $($forbiddenEntries -join ', ')"
    }
}

function Get-ZipEntryText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ZipPath,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $requiredPath = ConvertTo-ZipPath -Path $RelativePath
    $zipArchive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entry = $zipArchive.Entries | Where-Object {
            $entryPath = ConvertTo-ZipPath -Path $_.FullName
            $entryPath -eq $requiredPath -or $entryPath.EndsWith("/$requiredPath", [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1

        if (-not $entry) {
            throw "ZIP-Eintrag wurde nicht gefunden: $RelativePath"
        }

        $stream = $entry.Open()
        try {
            $reader = New-Object System.IO.StreamReader($stream)
            try {
                return $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $zipArchive.Dispose()
    }
}

$requiredReleasePaths = @(
    "TrafficView.exe",
    "TrafficView.exe.config",
    "TrafficView.languages.ini",
    "TrafficView.portable",
    "Manual.txt",
    "README.md",
    "LOLO-SOFT_00_SW.png",
    "DisplayModeAssets\Simple\TrafficView.panel.png",
    "DisplayModeAssets\Simple\TrafficView.center_core.png",
    "DisplayModeAssets\SimpleBlue\TrafficView.panel.png",
    "DisplayModeAssets\SimpleBlue\TrafficView.center_core.png"
)

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "Create-PortableRelease.ps1") -OutputRoot $outputRoot -ReleaseName $modernReleaseName
if ($LASTEXITCODE -ne 0) {
    throw "Create-PortableRelease.ps1 ist im Release-Skript-Test fehlgeschlagen."
}

$modernEntries = Get-ZipEntryNames -ZipPath $modernZipPath
foreach ($requiredPath in $requiredReleasePaths) {
    Assert-ZipContains -EntryNames $modernEntries -RelativePath $requiredPath
}

Assert-ZipOmitsPrivateAndLegacyFiles -EntryNames $modernEntries

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "portable-release.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "portable-release.ps1 ist im Release-Skript-Test fehlgeschlagen."
}

$version = Get-TrafficViewVersion
$legacyZipPath = Join-Path $legacyOutputRoot ("TrafficView.Portable.{0}.zip" -f $version)
$legacyDefaultsZipPath = Join-Path $legacyOutputRoot ("TrafficView.Portable.{0}.Standard.zip" -f $version)

$legacyEntries = Get-ZipEntryNames -ZipPath $legacyZipPath
foreach ($requiredPath in $requiredReleasePaths) {
    Assert-ZipContains -EntryNames $legacyEntries -RelativePath $requiredPath
}

Assert-ZipOmitsPrivateAndLegacyFiles -EntryNames $legacyEntries

$legacyDefaultsEntries = Get-ZipEntryNames -ZipPath $legacyDefaultsZipPath
Assert-ZipContains -EntryNames $legacyDefaultsEntries -RelativePath "TrafficView.settings.ini"
Assert-ZipOmitsPrivateAndLegacyFiles -EntryNames $legacyDefaultsEntries

$defaultSettingsText = Get-ZipEntryText -ZipPath $legacyDefaultsZipPath -RelativePath "TrafficView.settings.ini"
$requiredDefaultSettingsLines = @(
    "TaskbarPopupSectionMode=RightOnly",
    "ActivityBorderGlowEnabled=0",
    "TaskbarIntegrationEnabled=0"
)

foreach ($requiredLine in $requiredDefaultSettingsLines) {
    if ($defaultSettingsText.IndexOf($requiredLine, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Standard-Settings-ZIP enthaelt die erwartete Einstellung nicht: $requiredLine"
    }
}

Write-Host "Release script tests passed."
