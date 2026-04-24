param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version muss im Format Major.Minor.Patch angegeben werden, z. B. 1.4.26."
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$readmePath = Join-Path $root "README.md"
$readmeEnPath = Join-Path $root "README_EN.md"
$assemblyInfoPath = Join-Path $root "src\AssemblyInfo.cs"
$assemblyVersion = $Version + ".0"

function Update-TextFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Update
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Datei fuer Versionsupdate nicht gefunden: $Path"
    }

    $content = Get-Content -LiteralPath $Path -Raw
    $updatedContent = & $Update $content

    if ($content -eq $updatedContent) {
        throw "Versionsupdate hat keine Aenderung erzeugt: $Path"
    }

    Set-Content -LiteralPath $Path -Value $updatedContent -NoNewline
}

Update-TextFile -Path $readmePath -Update {
    param($content)
    return ($content -replace '(?m)^# TrafficView \d+\.\d+\.\d+\s*$', "# TrafficView $Version")
}

Update-TextFile -Path $readmeEnPath -Update {
    param($content)
    return ($content -replace '(?m)^# TrafficView \d+\.\d+\.\d+\s*$', "# TrafficView $Version")
}

Update-TextFile -Path $assemblyInfoPath -Update {
    param($content)
    $content = $content -replace 'AssemblyVersion\("\d+\.\d+\.\d+\.\d+"\)', "AssemblyVersion(`"$assemblyVersion`")"
    return ($content -replace 'AssemblyFileVersion\("\d+\.\d+\.\d+\.\d+"\)', "AssemblyFileVersion(`"$assemblyVersion`")")
}

Write-Host "TrafficView-Version aktualisiert auf $assemblyVersion"
