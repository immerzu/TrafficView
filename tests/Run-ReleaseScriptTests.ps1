$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot "_build_staging\release-script-tests"
$modernReleaseName = "TrafficView_Portable_ScriptTest"
$modernZipPath = Join-Path $outputRoot ($modernReleaseName + ".zip")
$legacyOutputRoot = Join-Path $outputRoot "legacy-portable-release"

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
        throw "$Description liegt ausserhalb des erwarteten Test-Ausgabeordners: $fullChildPath"
    }
}

Assert-PathIsInside -ParentPath $repoRoot -ChildPath $outputRoot -Description "Release-Test-Ausgabeordner"
Assert-PathIsInside -ParentPath $outputRoot -ChildPath $legacyOutputRoot -Description "Legacy-Release-Test-Ausgabeordner"

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
        [string[]]$EntryNames,

        [switch]$AllowSettingsFile
    )

    $forbiddenEntries = @($EntryNames | Where-Object {
        $leafName = ($_ -split "/")[-1]
        $_ -match "(^|/)Skins(/|$)" -or
        $leafName -eq "TrafficView_Code.txt" -or
        $leafName -eq "TrafficView.new.exe" -or
        $leafName -eq "TrafficView.log" -or
        ((-not $AllowSettingsFile) -and $leafName -eq "TrafficView.settings.ini") -or
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

function Assert-ZipExeVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ZipPath,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedVersion
    )

    $tempRoot = Join-Path $env:TEMP ("TrafficViewZipVersion_" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

    try {
        Expand-Archive -LiteralPath $ZipPath -DestinationPath $tempRoot -Force
        $exe = Get-ChildItem -LiteralPath $tempRoot -Recurse -File -Filter "TrafficView.exe" | Select-Object -First 1
        if (-not $exe) {
            throw "TrafficView.exe wurde im getesteten ZIP nicht gefunden: $ZipPath"
        }

        $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe.FullName).FileVersion
        if ([string]::IsNullOrWhiteSpace($fileVersion) -or
            -not $fileVersion.StartsWith($ExpectedVersion + ".", [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "EXE-Dateiversion im ZIP ($fileVersion) passt nicht zur erwarteten Version ($ExpectedVersion)."
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
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
Assert-ZipContains -EntryNames $modernEntries -RelativePath "release-manifest.json"
foreach ($requiredPath in $requiredReleasePaths) {
    Assert-ZipContains -EntryNames $modernEntries -RelativePath $requiredPath
}

Assert-ZipOmitsPrivateAndLegacyFiles -EntryNames $modernEntries
$version = Get-TrafficViewVersion
Assert-ZipExeVersion -ZipPath $modernZipPath -ExpectedVersion $version
$manifestText = Get-ZipEntryText -ZipPath $modernZipPath -RelativePath "release-manifest.json"
if ($manifestText.IndexOf('"version":  "' + $version + '"', [System.StringComparison]::OrdinalIgnoreCase) -lt 0 -and
    $manifestText.IndexOf('"version":"' + $version + '"', [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
    throw "Release-Manifest enthaelt die erwartete Version nicht: $version"
}

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "portable-release.ps1") -OutputRoot $legacyOutputRoot
if ($LASTEXITCODE -ne 0) {
    throw "portable-release.ps1 ist im Release-Skript-Test fehlgeschlagen."
}

$legacyZipPath = Join-Path $legacyOutputRoot ("TrafficView_Portable_{0}.zip" -f $version)
$legacyDefaultsZipPath = Join-Path $legacyOutputRoot ("TrafficView_Portable_{0}_Standard.zip" -f $version)

$legacyEntries = Get-ZipEntryNames -ZipPath $legacyZipPath
Assert-ZipContains -EntryNames $legacyEntries -RelativePath "release-manifest.json"
foreach ($requiredPath in $requiredReleasePaths) {
    Assert-ZipContains -EntryNames $legacyEntries -RelativePath $requiredPath
}

Assert-ZipOmitsPrivateAndLegacyFiles -EntryNames $legacyEntries
Assert-ZipExeVersion -ZipPath $legacyZipPath -ExpectedVersion $version
$legacyManifestText = Get-ZipEntryText -ZipPath $legacyZipPath -RelativePath "release-manifest.json"
if ($legacyManifestText.IndexOf('"version":  "' + $version + '"', [System.StringComparison]::OrdinalIgnoreCase) -lt 0 -and
    $legacyManifestText.IndexOf('"version":"' + $version + '"', [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
    throw "Legacy-Release-Manifest enthaelt die erwartete Version nicht: $version"
}

$legacyDefaultsEntries = Get-ZipEntryNames -ZipPath $legacyDefaultsZipPath
Assert-ZipContains -EntryNames $legacyDefaultsEntries -RelativePath "release-manifest.json"
Assert-ZipContains -EntryNames $legacyDefaultsEntries -RelativePath "TrafficView.settings.ini"
Assert-ZipOmitsPrivateAndLegacyFiles -EntryNames $legacyDefaultsEntries -AllowSettingsFile
Assert-ZipExeVersion -ZipPath $legacyDefaultsZipPath -ExpectedVersion $version

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
