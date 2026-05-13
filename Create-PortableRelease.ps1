param(
    [string]$OutputRoot,
    [string]$ReleaseName,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDirectory = Join-Path $root "src"
$distDirectory = Join-Path $root "dist"
$readmeFile = Join-Path $root "README.md"
$buildAssetsScript = Join-Path $root "Build-DisplayModeAssets.ps1"
$buildScript = Join-Path $root "build.ps1"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path (Split-Path -Parent $root) "Ausgabe"
}

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
        [string]$ChildPath
    )

    $fullParentPath = Get-FullPath -Path $ParentPath
    $fullChildPath = Get-FullPath -Path $ChildPath

    if (-not $fullParentPath.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
        $fullParentPath += [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $fullChildPath.StartsWith($fullParentPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Pfad liegt ausserhalb des erwarteten Ausgabeordners: $fullChildPath"
    }
}

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,

        [Parameter(Mandatory = $true)]
        [string]$ChildPath
    )

    $baseFullPath = Get-FullPath -Path $BasePath
    if (-not $baseFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
        $baseFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = New-Object System.Uri($baseFullPath)
    $childUri = New-Object System.Uri((Get-FullPath -Path $ChildPath))
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($childUri).ToString()).Replace("/", [System.IO.Path]::DirectorySeparatorChar)
}

function Get-TrafficViewVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReadmePath
    )

    if (-not (Test-Path -LiteralPath $ReadmePath)) {
        return "dev"
    }

    $firstLine = Get-Content -LiteralPath $ReadmePath -TotalCount 1
    if ($firstLine -match "^#\s+TrafficView\s+(.+?)\s*$") {
        return $Matches[1].Trim()
    }

    return "dev"
}

function Get-TrafficViewAssemblyVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory
    )

    $versionMatch = Get-ChildItem -LiteralPath $SourceDirectory -Filter "*.cs" -File |
        Sort-Object Name |
        Select-String -Pattern 'AssemblyVersion\("(?<Version>\d+\.\d+\.\d+)\.\d+"\)' |
        Select-Object -First 1

    if (-not $versionMatch) {
        throw "AssemblyVersion wurde im Quellcode nicht gefunden."
    }

    return $versionMatch.Matches[0].Groups["Version"].Value
}

function Assert-ReleaseVersionMatchesAssembly {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReadmeVersion,

        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory
    )

    if ($ReadmeVersion -eq "dev") {
        return
    }

    $assemblyVersion = Get-TrafficViewAssemblyVersion -SourceDirectory $SourceDirectory
    if ($ReadmeVersion -ne $assemblyVersion) {
        throw "README-Version ($ReadmeVersion) passt nicht zur AssemblyVersion ($assemblyVersion)."
    }
}

function Get-RequiredReleaseRelativePaths {
    return @(
        "release-manifest.json",
        "TrafficView.exe",
        "TrafficView.exe.config",
        "TrafficView.languages.ini",
        "TrafficView.portable",
        "Manual.txt",
        "README.md",
        "LOLO-SOFT_00_SW.png",
        "DisplayModeAssets\Simple\TrafficView.panel.png",
        "DisplayModeAssets\Simple\TrafficView.panel.90.png",
        "DisplayModeAssets\Simple\TrafficView.panel.110.png",
        "DisplayModeAssets\Simple\TrafficView.panel.125.png",
        "DisplayModeAssets\Simple\TrafficView.panel.150.png",
        "DisplayModeAssets\Simple\TrafficView.center_core.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.panel.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.panel.90.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.panel.110.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.panel.125.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.panel.150.png",
        "DisplayModeAssets\SimpleBlue\TrafficView.center_core.png"
    )
}

function Get-CurrentGitCommit {
    try {
        $commit = & git -C $root rev-parse HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commit)) {
            return ($commit | Select-Object -First 1).Trim()
        }
    }
    catch {
    }

    return "unknown"
}

function New-ReleaseManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectory,

        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $manifestPath = Join-Path $ReleaseDirectory "release-manifest.json"
    $fileEntries = @(
        Get-ChildItem -LiteralPath $ReleaseDirectory -Recurse -File -Force |
            Where-Object {
                -not ([string]::Equals(
                    (Get-FullPath -Path $_.FullName),
                    (Get-FullPath -Path $manifestPath),
                    [System.StringComparison]::OrdinalIgnoreCase))
            } |
            Sort-Object FullName |
            ForEach-Object {
                $relativePath = Get-RelativePath -BasePath $ReleaseDirectory -ChildPath $_.FullName
                [PSCustomObject]@{
                    path = ConvertTo-ZipPath -Path $relativePath
                    bytes = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
    )

    $manifest = [PSCustomObject]@{
        app = "TrafficView"
        version = $Version
        commit = Get-CurrentGitCommit
        createdUtc = (Get-Date).ToUniversalTime().ToString("o")
        files = $fileEntries
    }

    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
}

function Test-RequiredReleaseFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectory
    )

    foreach ($relativePath in (Get-RequiredReleaseRelativePaths)) {
        $requiredPath = Join-Path $ReleaseDirectory $relativePath
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Pflichtdatei fehlt in der Portable-Ausgabe: $requiredPath"
        }
    }
}

function Test-NoPrivateRuntimeFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectory
    )

    $forbiddenFiles = Get-ChildItem -LiteralPath $ReleaseDirectory -Recurse -File -Force | Where-Object {
        $_.Name -eq "TrafficView.settings.ini" -or
        $_.Name -eq "TrafficView.settings.ini_" -or
        $_.Name -eq "TrafficView.new.exe" -or
        $_.Name -eq "TrafficView.log" -or
        $_.Name -like "Verbrauch*.txt" -or
        $_.Name -like "Verbrauch*.txt_" -or
        $_.Name -like "Verbrauch*.txt.gz"
    }

    if ($forbiddenFiles) {
        $forbiddenList = ($forbiddenFiles | ForEach-Object { $_.FullName }) -join [Environment]::NewLine
        throw "Private Laufzeitdaten duerfen nicht in die Portable-Ausgabe:$([Environment]::NewLine)$forbiddenList"
    }
}

function Test-NoLegacyReleaseFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectory
    )

    $legacySkinDirectory = Join-Path $ReleaseDirectory "Skins"
    if (Test-Path -LiteralPath $legacySkinDirectory) {
        throw "Veralteter Skins-Ordner darf nicht in die Portable-Ausgabe: $legacySkinDirectory"
    }

    $legacyRootFiles = @(
        "TrafficView_Code.txt",
        "TrafficView.panel.png",
        "TrafficView.panel.90.png",
        "TrafficView.panel.110.png",
        "TrafficView.panel.125.png",
        "TrafficView.panel.150.png",
        "TrafficView.center_core.png"
    )

    foreach ($legacyRootFile in $legacyRootFiles) {
        $legacyPath = Join-Path $ReleaseDirectory $legacyRootFile
        if (Test-Path -LiteralPath $legacyPath) {
            throw "Veraltete Root-Asset-Datei darf nicht in die Portable-Ausgabe: $legacyPath"
        }
    }
}

function Assert-ReleaseExeVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectory,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedVersion
    )

    if ($ExpectedVersion -eq "dev") {
        return
    }

    $exePath = Join-Path $ReleaseDirectory "TrafficView.exe"
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "TrafficView.exe fehlt fuer Versionspruefung: $exePath"
    }

    $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath).FileVersion
    if ([string]::IsNullOrWhiteSpace($fileVersion) -or
        -not $fileVersion.StartsWith($ExpectedVersion + ".", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "EXE-Dateiversion ($fileVersion) passt nicht zur Release-Version ($ExpectedVersion)."
    }
}

function ConvertTo-ZipPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return ($Path -replace "\\", "/").Trim([char]'/')
}

function Test-ZipEntryIsForbidden {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EntryName
    )

    $normalizedEntryName = ConvertTo-ZipPath -Path $EntryName
    $leafName = ($normalizedEntryName -split "/")[-1]

    return (
        $normalizedEntryName -match "(^|/)Skins(/|$)" -or
        $leafName -eq "TrafficView_Code.txt" -or
        $leafName -eq "TrafficView.new.exe" -or
        $leafName -eq "TrafficView.settings.ini" -or
        $leafName -eq "TrafficView.settings.ini_" -or
        $leafName -eq "TrafficView.log" -or
        $leafName -like "Verbrauch*.txt" -or
        $leafName -like "Verbrauch*.txt_" -or
        $leafName -like "Verbrauch*.txt.gz"
    )
}

function Test-PortableZipContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ZipPath
    )

    if (-not (Test-Path -LiteralPath $ZipPath)) {
        throw "Portable-ZIP wurde nicht erstellt: $ZipPath"
    }

    $zipArchive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entryNames = @($zipArchive.Entries | ForEach-Object { ConvertTo-ZipPath -Path $_.FullName })

        foreach ($requiredPath in (Get-RequiredReleaseRelativePaths)) {
            $requiredZipPath = ConvertTo-ZipPath -Path $requiredPath
            $hasRequiredEntry = @($entryNames | Where-Object {
                $_ -eq $requiredZipPath -or $_.EndsWith("/$requiredZipPath", [System.StringComparison]::OrdinalIgnoreCase)
            }).Count -gt 0

            if (-not $hasRequiredEntry) {
                throw "Pflichtdatei fehlt im Portable-ZIP: $requiredPath"
            }
        }

        $forbiddenEntries = @($entryNames | Where-Object { Test-ZipEntryIsForbidden -EntryName $_ })
        if ($forbiddenEntries.Count -gt 0) {
            throw "Private oder veraltete Dateien im Portable-ZIP gefunden: $($forbiddenEntries -join ', ')"
        }
    }
    finally {
        $zipArchive.Dispose()
    }
}

function Copy-ReleaseItem {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$TargetDirectory,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $sourcePath = Join-Path $SourceDirectory $RelativePath
    $targetPath = Join-Path $TargetDirectory $RelativePath

    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Build-Ausgabedatei fehlt: $sourcePath"
    }

    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Recurse -Force
}

if (-not $SkipBuild) {
    if (-not (Test-Path -LiteralPath $buildAssetsScript)) {
        throw "Asset-Build-Skript nicht gefunden: $buildAssetsScript"
    }

    if (-not (Test-Path -LiteralPath $buildScript)) {
        throw "Build-Skript nicht gefunden: $buildScript"
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File $buildAssetsScript
    & powershell -NoProfile -ExecutionPolicy Bypass -File $buildScript
}

if (-not (Test-Path -LiteralPath $distDirectory)) {
    throw "Build-Ausgabeordner nicht gefunden: $distDirectory"
}

$version = Get-TrafficViewVersion -ReadmePath $readmeFile
Assert-ReleaseVersionMatchesAssembly -ReadmeVersion $version -SourceDirectory $sourceDirectory
if ([string]::IsNullOrWhiteSpace($ReleaseName)) {
    $ReleaseName = "TrafficView_Portable_$version"
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$fullOutputRoot = Get-FullPath -Path $OutputRoot
$releaseDirectory = Join-Path $fullOutputRoot $ReleaseName
$zipPath = Join-Path $fullOutputRoot ($ReleaseName + ".zip")

Assert-PathIsInside -ParentPath $fullOutputRoot -ChildPath $releaseDirectory
Assert-PathIsInside -ParentPath $fullOutputRoot -ChildPath $zipPath

if (Test-Path -LiteralPath $releaseDirectory) {
    Remove-Item -LiteralPath $releaseDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null

$releaseItems = @(
    "TrafficView.exe",
    "TrafficView.exe.config",
    "TrafficView.languages.ini",
    "TrafficView.portable",
    "Manual.txt",
    "README.md",
    "LOLO-SOFT_00_SW.png",
    "DisplayModeAssets"
)

foreach ($releaseItem in $releaseItems) {
    Copy-ReleaseItem `
        -SourceDirectory $distDirectory `
        -TargetDirectory $releaseDirectory `
        -RelativePath $releaseItem
}

New-ReleaseManifest -ReleaseDirectory $releaseDirectory -Version $version
Test-RequiredReleaseFiles -ReleaseDirectory $releaseDirectory
Test-NoPrivateRuntimeFiles -ReleaseDirectory $releaseDirectory
Test-NoLegacyReleaseFiles -ReleaseDirectory $releaseDirectory
Assert-ReleaseExeVersion -ReleaseDirectory $releaseDirectory -ExpectedVersion $version

Compress-Archive -LiteralPath $releaseDirectory -DestinationPath $zipPath -CompressionLevel Optimal
Test-PortableZipContents -ZipPath $zipPath

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$zipFileName = Split-Path -Leaf $zipPath
$sha256Path = $zipPath + ".sha256"
"$zipHash  $zipFileName" | Set-Content -LiteralPath $sha256Path -Encoding ASCII

Write-Host ""
Write-Host "Portable-Ausgabe erstellt:" $releaseDirectory
Write-Host "Portable-ZIP erstellt:" $zipPath
