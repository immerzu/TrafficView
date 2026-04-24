$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $root "src"
$projectFile = Join-Path $root "TrafficView.csproj"
$outputDir = Join-Path $root "dist"
$outputFile = Join-Path $outputDir "TrafficView.exe"
$buildStagingDirectory = Join-Path $root "_build_staging"
$stagedOutputFile = Join-Path $buildStagingDirectory "TrafficView.exe"
$fallbackOutputFile = Join-Path $outputDir "TrafficView.new.exe"
$settingsOutputFile = Join-Path $outputDir "TrafficView.settings.ini"
$settingsBackupOutputFile = Join-Path $outputDir "TrafficView.settings.ini_"
$usageOutputFile = Join-Path $outputDir "Verbrauch.txt"
$usageBackupOutputFile = Join-Path $outputDir "Verbrauch.txt_"
$usageArchiveOutputFile = Join-Path $outputDir "Verbrauch.archiv.txt"
$usageArchiveBackupOutputFile = Join-Path $outputDir "Verbrauch.archiv.txt_"
$runtimeLogOutputDirectory = Join-Path $outputDir "TrafficView"
$legacyLogOutputDirectory = Join-Path $outputDir "Logs"
$manifestFile = Join-Path $root "TrafficView.manifest"
$iconFile = Join-Path $root "TrafficView.ico"
$configSourceFile = Join-Path $root "TrafficView.exe.config"
$configOutputFile = Join-Path $outputDir "TrafficView.exe.config"
$languageSourceFile = Join-Path $root "TrafficView.languages.ini"
$languageOutputFile = Join-Path $outputDir "TrafficView.languages.ini"
$manualSourceFile = Join-Path $root "Manual.txt"
$manualOutputFile = Join-Path $outputDir "Manual.txt"
$readmeSourceFile = Join-Path $root "README.md"
$readmeOutputFile = Join-Path $outputDir "README.md"
$skinsSourceDirectory = Join-Path $root "Skins"
$skinsOutputDirectory = Join-Path $outputDir "Skins"
$displayModeAssetsSourceDirectory = Join-Path $root "DisplayModeAssets"
$displayModeAssetsOutputDirectory = Join-Path $outputDir "DisplayModeAssets"
$legacyPanelAssetFiles = @(
    "TrafficView.panel.png",
    "TrafficView.panel.90.png",
    "TrafficView.panel.110.png",
    "TrafficView.panel.125.png",
    "TrafficView.panel.150.png"
)
$menuAssetFiles = @(
    "LOLO-SOFT_00_SW.png"
)
$sourceFiles = @()
$requiredSkinFiles = @(
    "skin.ini",
    "TrafficView.panel.90.png",
    "TrafficView.panel.png",
    "TrafficView.panel.110.png",
    "TrafficView.panel.125.png",
    "TrafficView.panel.150.png"
)
$requiredDisplayModeAssets = [ordered]@{
    "Simple" = [ordered]@{
        "TrafficView.panel.90.png" = @{
            Width = 92
            Height = 50
        }
        "TrafficView.panel.png" = @{
            Width = 102
            Height = 56
        }
        "TrafficView.panel.110.png" = @{
            Width = 112
            Height = 62
        }
        "TrafficView.panel.125.png" = @{
            Width = 128
            Height = 70
        }
        "TrafficView.panel.150.png" = @{
            Width = 153
            Height = 84
        }
        "TrafficView.center_core.png" = @{
            Width = 512
            Height = 512
        }
    }
    "SimpleBlue" = [ordered]@{
        "TrafficView.panel.90.png" = @{
            Width = 92
            Height = 50
        }
        "TrafficView.panel.png" = @{
            Width = 102
            Height = 56
        }
        "TrafficView.panel.110.png" = @{
            Width = 112
            Height = 62
        }
        "TrafficView.panel.125.png" = @{
            Width = 128
            Height = 70
        }
        "TrafficView.panel.150.png" = @{
            Width = 153
            Height = 84
        }
        "TrafficView.center_core.png" = @{
            Width = 512
            Height = 512
        }
    }
}
$deleteStagingDirectoryName = ".delete"
$supportedSurfaceEffects = @(
    "none",
    "glass",
    "glass-readable"
)
$invalidSkinFolderCharacters = [System.IO.Path]::GetInvalidFileNameChars()

Add-Type -AssemblyName System.Drawing

function Read-ExistingTextFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return $null
    }

    return Get-Content -LiteralPath $Path
}

function Restore-TextFileIfAvailable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [string[]]$Lines
    )

    if ($null -eq $Lines) {
        return
    }

    Set-Content -LiteralPath $Path -Value $Lines -Encoding UTF8
}

function Get-ProjectCompileSourceFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectFilePath,

        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    if (-not (Test-Path -LiteralPath $ProjectFilePath)) {
        throw "Projektdatei nicht gefunden: $ProjectFilePath"
    }

    [xml]$projectXml = Get-Content -LiteralPath $ProjectFilePath
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($projectXml.NameTable)
    $namespaceManager.AddNamespace("msb", $projectXml.Project.NamespaceURI)
    $compileNodes = $projectXml.SelectNodes("//msb:Compile[@Include]", $namespaceManager)
    $files = @()

    foreach ($compileNode in $compileNodes) {
        $includePath = $compileNode.Include
        if ([string]::IsNullOrWhiteSpace($includePath)) {
            continue
        }

        $sourcePath = Join-Path $ProjectRoot $includePath
        if (-not (Test-Path -LiteralPath $sourcePath)) {
            throw "Projekt-Quelldatei fehlt: $sourcePath"
        }

        $files += $sourcePath
    }

    return $files
}

function Remove-DirectoryIfAvailable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    try {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    catch {
        Write-Warning "$Description konnte nicht bereinigt werden: $Path"
    }
}

function Test-FileLocked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $stream = $null
    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
        return $false
    }
    catch [System.IO.IOException] {
        return $true
    }
    finally {
        if ($stream -ne $null) {
            $stream.Dispose()
        }
    }
}

function Get-ProcessesUsingPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return @()
    }

    $normalizedPath = [System.IO.Path]::GetFullPath($Path)

    return @(
        Get-Process |
            Where-Object {
                -not [string]::IsNullOrWhiteSpace($_.Path) -and
                ([System.IO.Path]::GetFullPath($_.Path)) -ieq $normalizedPath
            } |
            Sort-Object Id
    )
}

function Publish-BuiltExecutable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StagedPath,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [string]$FallbackPath
    )

    if (-not (Test-Path -LiteralPath $StagedPath)) {
        throw "Die neu kompilierte EXE wurde im Staging nicht gefunden: $StagedPath"
    }

    if (Test-FileLocked -Path $OutputPath) {
        Copy-Item -LiteralPath $StagedPath -Destination $FallbackPath -Force

        $processes = Get-ProcessesUsingPath -Path $OutputPath
        $processHint = ""
        if ($processes.Count -gt 0) {
            $processHint = " Aktive Prozesse: " + (($processes | ForEach-Object { "$($_.ProcessName)#$($_.Id)" }) -join ", ")
        }

        throw "Die fertige EXE konnte nicht nach '$OutputPath' kopiert werden, weil die Datei noch verwendet wird. Eine aktuelle Ersatzdatei liegt unter '$FallbackPath'.$processHint"
    }

    Copy-Item -LiteralPath $StagedPath -Destination $OutputPath -Force
}

function Remove-DeleteStagingDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkinsDirectoryPath
    )

    $deleteStagingDirectoryPath = Join-Path $SkinsDirectoryPath $deleteStagingDirectoryName
    if (-not (Test-Path $deleteStagingDirectoryPath)) {
        return
    }

    try {
        Remove-Item -LiteralPath $deleteStagingDirectoryPath -Recurse -Force
    }
    catch {
        throw "Temporäres Skin-Löschverzeichnis konnte nicht bereinigt werden: $deleteStagingDirectoryPath"
    }
}

function Get-SkinMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkinDirectoryPath
    )

    $skinSettingsPath = Join-Path $SkinDirectoryPath "skin.ini"
    $skinDirectoryName = Split-Path -Leaf $SkinDirectoryPath
    $skinId = $skinDirectoryName
    $displayNameFallback = $skinDirectoryName
    $surfaceEffect = "none"
    $clientWidth = 102
    $clientHeight = 56

    foreach ($line in Get-Content $skinSettingsPath) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $trimmedLine = $line.Trim()
        if ($trimmedLine.StartsWith("#") -or $trimmedLine.StartsWith(";")) {
            continue
        }

        $equalsIndex = $trimmedLine.IndexOf("=")
        if ($equalsIndex -le 0) {
            continue
        }

        $key = $trimmedLine.Substring(0, $equalsIndex).Trim()
        $value = $trimmedLine.Substring($equalsIndex + 1).Trim()

        if ($key -ieq "Id" -and -not [string]::IsNullOrWhiteSpace($value)) {
            $skinId = $value
            continue
        }

        if ($key -ieq "DisplayNameFallback" -and -not [string]::IsNullOrWhiteSpace($value)) {
            $displayNameFallback = $value
            continue
        }

        if ($key -ieq "SurfaceEffect") {
            $surfaceEffect = $value
            continue
        }

        if ($key -ieq "ClientSize") {
            $parts = $value.Split(',')
            if ($parts.Length -ne 2) {
                throw "ClientSize ist ungueltig in: $skinSettingsPath"
            }

            $parsedWidth = 0
            $parsedHeight = 0
            if (-not [int]::TryParse($parts[0].Trim(), [ref]$parsedWidth) -or
                -not [int]::TryParse($parts[1].Trim(), [ref]$parsedHeight) -or
                $parsedWidth -le 0 -or
                $parsedHeight -le 0) {
                throw "ClientSize ist ungueltig in: $skinSettingsPath"
            }

            $clientWidth = $parsedWidth
            $clientHeight = $parsedHeight
        }
    }

    if ([string]::IsNullOrWhiteSpace($surfaceEffect)) {
        $surfaceEffect = "none"
    }

    if ([string]::IsNullOrWhiteSpace($displayNameFallback)) {
        $displayNameFallback = $skinId
    }

    $expectedDirectoryName = Get-SkinFolderNameFromFallback -Value $displayNameFallback
    if ($expectedDirectoryName -ine $skinDirectoryName) {
        throw "Skin-Ordnername stimmt nicht mit DisplayNameFallback überein: $skinSettingsPath ($expectedDirectoryName statt $skinDirectoryName)"
    }

    [PSCustomObject]@{
        Id = $skinId
        DisplayNameFallback = $displayNameFallback
        SurfaceEffect = $surfaceEffect
        ClientWidth = $clientWidth
        ClientHeight = $clientHeight
        DirectoryName = $skinDirectoryName
        DirectoryPath = $SkinDirectoryPath
    }
}

function Get-SkinFolderNameFromFallback {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $folderName = $Value.Trim()
    if ([string]::IsNullOrWhiteSpace($folderName)) {
        throw "DisplayNameFallback ist leer und kann nicht als Skin-Ordnername verwendet werden."
    }

    foreach ($invalidCharacter in $invalidSkinFolderCharacters) {
        if ($folderName.Contains([string]$invalidCharacter)) {
            throw "DisplayNameFallback enthaelt ungueltige Zeichen fuer einen Skin-Ordner: '$Value'"
        }
    }

    if ($folderName.EndsWith(".", [System.StringComparison]::Ordinal)) {
        throw "DisplayNameFallback darf nicht mit einem Punkt enden: '$Value'"
    }

    return $folderName
}

function Test-SkinDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkinDirectoryPath
    )

    if (-not (Test-Path $SkinDirectoryPath)) {
        throw "Skin-Ordner nicht gefunden: $SkinDirectoryPath"
    }

    $skinSettingsPath = Join-Path $SkinDirectoryPath "skin.ini"

    foreach ($fileName in $requiredSkinFiles) {
        $filePath = Join-Path $SkinDirectoryPath $fileName

        if (-not (Test-Path $filePath)) {
            throw "Skin-Datei fehlt: $filePath"
        }
    }

    $skinMetadata = Get-SkinMetadata -SkinDirectoryPath $SkinDirectoryPath
    $skinId = $skinMetadata.Id
    $surfaceEffect = $skinMetadata.SurfaceEffect

    if ([string]::IsNullOrWhiteSpace($skinId)) {
        throw "Skin-ID leer in: $skinSettingsPath"
    }

    if ([string]::IsNullOrWhiteSpace($surfaceEffect)) {
        $surfaceEffect = "none"
    }

    if ($supportedSurfaceEffects -inotcontains $surfaceEffect) {
        throw "Nicht unterstützter SurfaceEffect in $skinSettingsPath : $surfaceEffect"
    }

    $expectedAssetSizes = [ordered]@{
        "TrafficView.panel.90.png" = @{
            Width = [int][Math]::Floor((($skinMetadata.ClientWidth * 90) / 100.0) + 0.5)
            Height = [int][Math]::Floor((($skinMetadata.ClientHeight * 90) / 100.0) + 0.5)
        }
        "TrafficView.panel.png" = @{
            Width = $skinMetadata.ClientWidth
            Height = $skinMetadata.ClientHeight
        }
        "TrafficView.panel.110.png" = @{
            Width = [int][Math]::Floor((($skinMetadata.ClientWidth * 110) / 100.0) + 0.5)
            Height = [int][Math]::Floor((($skinMetadata.ClientHeight * 110) / 100.0) + 0.5)
        }
        "TrafficView.panel.125.png" = @{
            Width = [int][Math]::Floor((($skinMetadata.ClientWidth * 125) / 100.0) + 0.5)
            Height = [int][Math]::Floor((($skinMetadata.ClientHeight * 125) / 100.0) + 0.5)
        }
        "TrafficView.panel.150.png" = @{
            Width = [int][Math]::Floor((($skinMetadata.ClientWidth * 150) / 100.0) + 0.5)
            Height = [int][Math]::Floor((($skinMetadata.ClientHeight * 150) / 100.0) + 0.5)
        }
    }

    foreach ($entry in $expectedAssetSizes.GetEnumerator()) {
        $filePath = Join-Path $SkinDirectoryPath $entry.Key
        $bitmap = $null
        try {
            $bitmap = New-Object System.Drawing.Bitmap($filePath)
            if ($bitmap.Width -ne $entry.Value.Width -or $bitmap.Height -ne $entry.Value.Height) {
                throw "Skin-Datei hat falsche Groesse: $filePath ($($bitmap.Width)x$($bitmap.Height) statt $($entry.Value.Width)x$($entry.Value.Height))"
            }
        }
        finally {
            if ($bitmap -ne $null) {
                $bitmap.Dispose()
            }
        }
    }
}

function Test-AllSkinDirectories {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkinsDirectoryPath
    )

    Remove-DeleteStagingDirectory -SkinsDirectoryPath $SkinsDirectoryPath

    $skinDirectories = Get-ChildItem -Path $SkinsDirectoryPath -Directory |
        Where-Object { $_.Name -ne $deleteStagingDirectoryName } |
        Sort-Object Name
    if (-not $skinDirectories -or $skinDirectories.Count -eq 0) {
        return @()
    }

    $seenSkinIds = @{}
    $seenOutputDirectoryNames = @{}
    $skinMetadataList = @()
    foreach ($skinDirectory in $skinDirectories) {
        Test-SkinDirectory -SkinDirectoryPath $skinDirectory.FullName
        $skinMetadata = Get-SkinMetadata -SkinDirectoryPath $skinDirectory.FullName
        $skinOutputDirectoryName = Get-SkinFolderNameFromFallback -Value $skinMetadata.DisplayNameFallback

        if ($seenSkinIds.ContainsKey($skinMetadata.Id)) {
            throw "Doppelte Skin-ID erkannt: $($skinMetadata.Id)"
        }

        if ($seenOutputDirectoryNames.ContainsKey($skinOutputDirectoryName)) {
            throw "Doppelter Ausgabeordnername erkannt: $skinOutputDirectoryName"
        }

        $seenSkinIds[$skinMetadata.Id] = $true
        $seenOutputDirectoryNames[$skinOutputDirectoryName] = $true
        $skinMetadataList += $skinMetadata
    }

    return $skinMetadataList
}

function Test-DisplayModeAssets {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DisplayModeAssetsDirectoryPath
    )

    if (-not (Test-Path $DisplayModeAssetsDirectoryPath)) {
        throw "DisplayModeAssets-Ordner nicht gefunden: $DisplayModeAssetsDirectoryPath"
    }

    foreach ($displayModeEntry in $requiredDisplayModeAssets.GetEnumerator()) {
        $displayModeDirectoryPath = Join-Path $DisplayModeAssetsDirectoryPath $displayModeEntry.Key
        if (-not (Test-Path $displayModeDirectoryPath)) {
            throw "DisplayModeAssets-Modusordner nicht gefunden: $displayModeDirectoryPath"
        }

        foreach ($assetEntry in $displayModeEntry.Value.GetEnumerator()) {
            $assetPath = Join-Path $displayModeDirectoryPath $assetEntry.Key
            if (-not (Test-Path $assetPath)) {
                throw "DisplayModeAsset fehlt: $assetPath"
            }

            $bitmap = $null
            try {
                $bitmap = New-Object System.Drawing.Bitmap($assetPath)
                if ($bitmap.Width -ne $assetEntry.Value.Width -or $bitmap.Height -ne $assetEntry.Value.Height) {
                    throw "DisplayModeAsset hat falsche Groesse: $assetPath ($($bitmap.Width)x$($bitmap.Height) statt $($assetEntry.Value.Width)x$($assetEntry.Value.Height))"
                }
            }
            finally {
                if ($bitmap -ne $null) {
                    $bitmap.Dispose()
                }
            }
        }
    }
}

function Copy-SkinDirectoriesToOutput {
    param(
        [Parameter(Mandatory = $true)]
        [Object[]]$SkinMetadataList,

        [Parameter(Mandatory = $true)]
        [string]$OutputDirectoryPath
    )

    New-Item -ItemType Directory -Force -Path $OutputDirectoryPath | Out-Null

    foreach ($skinMetadata in $SkinMetadataList) {
        $outputDirectoryName = Get-SkinFolderNameFromFallback -Value $skinMetadata.DisplayNameFallback
        $skinOutputPath = Join-Path $OutputDirectoryPath $outputDirectoryName
        Copy-Item -Path $skinMetadata.DirectoryPath -Destination $skinOutputPath -Recurse -Force
    }
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
New-Item -ItemType Directory -Force -Path $buildStagingDirectory | Out-Null

$preservedSettingsLines = Read-ExistingTextFile -Path $settingsOutputFile
$preservedSettingsBackupLines = Read-ExistingTextFile -Path $settingsBackupOutputFile
$preservedUsageLines = Read-ExistingTextFile -Path $usageOutputFile
$preservedUsageBackupLines = Read-ExistingTextFile -Path $usageBackupOutputFile
$preservedUsageArchiveLines = Read-ExistingTextFile -Path $usageArchiveOutputFile
$preservedUsageArchiveBackupLines = Read-ExistingTextFile -Path $usageArchiveBackupOutputFile

if (Test-Path $settingsOutputFile) {
    Remove-Item -LiteralPath $settingsOutputFile -Force
}

if (Test-Path $settingsBackupOutputFile) {
    Remove-Item -LiteralPath $settingsBackupOutputFile -Force
}

if (Test-Path $usageOutputFile) {
    Remove-Item -LiteralPath $usageOutputFile -Force
}

if (Test-Path $usageBackupOutputFile) {
    Remove-Item -LiteralPath $usageBackupOutputFile -Force
}

if (Test-Path $usageArchiveOutputFile) {
    Remove-Item -LiteralPath $usageArchiveOutputFile -Force
}

if (Test-Path $usageArchiveBackupOutputFile) {
    Remove-Item -LiteralPath $usageArchiveBackupOutputFile -Force
}

Remove-DirectoryIfAvailable -Path $runtimeLogOutputDirectory -Description "Runtime-Logordner"
Remove-DirectoryIfAvailable -Path $legacyLogOutputDirectory -Description "Legacy-Logordner"

if (Test-Path $skinsOutputDirectory) {
    Remove-Item -LiteralPath $skinsOutputDirectory -Recurse -Force
}

foreach ($legacyPanelAssetFile in $legacyPanelAssetFiles) {
    $legacyPanelAssetOutputFile = Join-Path $outputDir $legacyPanelAssetFile
    if (Test-Path $legacyPanelAssetOutputFile) {
        Remove-Item -LiteralPath $legacyPanelAssetOutputFile -Force
    }
}

if (Test-Path -LiteralPath $stagedOutputFile) {
    Remove-Item -LiteralPath $stagedOutputFile -Force
}

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

if (-not (Test-Path $manualSourceFile)) {
    throw "Manual-Datei nicht gefunden: $manualSourceFile"
}

if (-not (Test-Path $readmeSourceFile)) {
    throw "README-Datei nicht gefunden: $readmeSourceFile"
}

$sourceFiles = Get-ProjectCompileSourceFiles -ProjectFilePath $projectFile -ProjectRoot $root
if (-not $sourceFiles -or $sourceFiles.Count -eq 0) {
    throw "Keine C#-Quelldateien in '$projectFile' gefunden."
}

Test-DisplayModeAssets -DisplayModeAssetsDirectoryPath $displayModeAssetsSourceDirectory

$skinMetadataList = @()
if (Test-Path $skinsSourceDirectory) {
    $skinMetadataList = Test-AllSkinDirectories -SkinsDirectoryPath $skinsSourceDirectory
}

& $compiler `
    /nologo `
    /target:winexe `
    /optimize+ `
    /platform:anycpu `
    /win32icon:$iconFile `
    /win32manifest:$manifestFile `
    /out:$stagedOutputFile `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.IO.Compression.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $sourceFiles

if ($LASTEXITCODE -ne 0) {
    throw "Build fehlgeschlagen."
}

Publish-BuiltExecutable -StagedPath $stagedOutputFile -OutputPath $outputFile -FallbackPath $fallbackOutputFile

Copy-Item $configSourceFile $configOutputFile -Force
Copy-Item $languageSourceFile $languageOutputFile -Force
Copy-Item $manualSourceFile $manualOutputFile -Force
Copy-Item $readmeSourceFile $readmeOutputFile -Force
if ($skinMetadataList -and $skinMetadataList.Count -gt 0) {
    Copy-SkinDirectoriesToOutput -SkinMetadataList $skinMetadataList -OutputDirectoryPath $skinsOutputDirectory
}
if (Test-Path $displayModeAssetsSourceDirectory) {
    if (Test-Path $displayModeAssetsOutputDirectory) {
        Remove-Item $displayModeAssetsOutputDirectory -Recurse -Force
    }

    Copy-Item $displayModeAssetsSourceDirectory $displayModeAssetsOutputDirectory -Recurse -Force
}

foreach ($menuAssetFile in $menuAssetFiles) {
    $menuAssetSourceFile = Join-Path $root $menuAssetFile
    $menuAssetOutputFile = Join-Path $outputDir $menuAssetFile
    if (Test-Path $menuAssetSourceFile) {
        Copy-Item $menuAssetSourceFile $menuAssetOutputFile -Force
    }
}

Restore-TextFileIfAvailable -Path $settingsOutputFile -Lines $preservedSettingsLines
Restore-TextFileIfAvailable -Path $settingsBackupOutputFile -Lines $preservedSettingsBackupLines
Restore-TextFileIfAvailable -Path $usageOutputFile -Lines $preservedUsageLines
Restore-TextFileIfAvailable -Path $usageBackupOutputFile -Lines $preservedUsageBackupLines
Restore-TextFileIfAvailable -Path $usageArchiveOutputFile -Lines $preservedUsageArchiveLines
Restore-TextFileIfAvailable -Path $usageArchiveBackupOutputFile -Lines $preservedUsageArchiveBackupLines

Write-Host ""
Write-Host "Fertig:" $outputFile
