# TrafficView 1.4.39

TrafficView allows only one active instance during normal operation. A second launch shows an informational message and keeps the already running instance active.

## Portable -> Portable: keep data-usage history

When unpacking a new portable version and keeping the existing data-usage history, copy these files from the old portable folder into the new portable folder next to `TrafficView.exe`:

- `Verbrauch.txt`
- `Verbrauch.archiv.txt` if present
- all `Verbrauch.archiv.*.txt.gz` files if present
- `TrafficView.settings.ini`
- `TrafficView.settings.ini_` if present

This preserves current usage data, archived usage data, compressed monthly archives, and the saved adapter, calibration, and display settings. The `TrafficView.settings.ini_` file acts as a backup copy of the saved settings.

## Taskbar Integration: Toggle Visible Area

When TrafficView is placed on the taskbar, it starts there with only the right side of the display visible by default. If the overlay is dragged left on the taskbar and enough room is available, the full `both sections` view can appear again. Another light drag to the left on the taskbar can switch the taskbar view back and forth between `both sections` and `right only`.

## Taskbar Integration: Local Window Order

In taskbar mode, TrafficView keeps the display locally in front of the current taskbar without using global TopMost or forcing focus. Normal windows, including Windows Explorer and StartAllBack, are not pulled into foreground conflicts.

## Simple Display: Build Display Images

The source images for the Simple display live in the repository under `DisplayModeAssetSources\Simple`.

The runtime images for the app are generated with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-DisplayModeAssets.ps1
```

The generated files are written to `DisplayModeAssets\Simple` and copied to `dist\DisplayModeAssets\Simple` during the build.

The `Simple blue` display uses the same logic as `Simple` but with its own blue image set under `DisplayModeAssets\SimpleBlue`.

## Create a Clean Portable Output

A distributable portable version without local settings, usage data, and logs is created with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Create-PortableRelease.ps1
```

By default the output is written to the `01_Ausgabe` folder next to the repository (standing rule). The script rebuilds the display images and `TrafficView.exe` from scratch, copies only the released program files, and aborts if private runtime data such as `TrafficView.settings.ini`, `Verbrauch.txt`, or logs is found in the portable output. The ZIP also contains `release-manifest.json` with version, commit, file sizes, and SHA-256 checksums.

## Run All Checks

Before a commit or release, the complete local verification path can be started with one command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Run-AllTests.ps1
```

This builds the app, runs the smoke tests, and verifies both portable release scripts including ZIP contents.

## Back Up User Data

Local settings and usage data can be backed up before a test or update:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Backup-UserData.ps1
```

Restore a backup with `-Mode Restore -BackupPath <BackupFolder>` when needed.

## Verify ZIP Integrity After Download

Each portable ZIP is accompanied by a `.sha256` file. After downloading, verify the ZIP integrity with:

```powershell
Get-FileHash .\TrafficView_Portable_1.4.39.zip -Algorithm SHA256
```

The output hash must match the content of `TrafficView_Portable_1.4.39.zip.sha256`.

## Prepare a Release

Version numbers are updated consistently in README files and AssemblyInfo with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Bump-Version.ps1 -Version 1.4.39
```

Before publishing a release, also walk through `docs\ui-release-checklist.md` and note the result in `docs\manual-test-log.md`.
