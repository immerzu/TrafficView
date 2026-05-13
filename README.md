# TrafficView 1.4.32

## Deutsch

TrafficView erlaubt im Normalbetrieb nur eine aktive Instanz. Ein zweiter Start zeigt nur einen Hinweis und laesst die bereits laufende Instanz weiterarbeiten.

### Portable -> Portable: Datenverbrauch uebernehmen

Wenn eine neue Portable-Version entpackt wird und die bisherigen Verbrauchsdaten erhalten bleiben sollen, muessen diese Dateien aus dem alten Portable-Ordner in den neuen Portable-Ordner neben `TrafficView.exe` kopiert werden:

- `Verbrauch.txt`
- `Verbrauch.archiv.txt` falls vorhanden
- alle `Verbrauch.archiv.*.txt.gz` falls vorhanden
- `TrafficView.settings.ini`
- `TrafficView.settings.ini_` falls vorhanden

Damit bleiben aktuelle Verbrauchsdaten, ausgelagerte Archivdaten, komprimierte Monatsarchive sowie Adapter-, Kalibrations- und Anzeigeeinstellungen erhalten. Die Datei `TrafficView.settings.ini_` dient zusaetzlich als Sicherungskopie der gespeicherten Einstellungen.

### Taskleistenintegration: Bereich umschalten

Befindet sich TrafficView auf der Taskleiste, wird dort standardmaessig zuerst nur die rechte Seite der Anzeige gezeigt. Wird die Anzeige auf der Taskleiste nach links gezogen und ist dort genug Platz vorhanden, kann wieder die Ansicht `beide Teile` erscheinen. Ein weiteres leichtes Ziehen nach links auf der Taskleiste erlaubt nun wieder den Wechsel zwischen `beide Teile` und `nur rechts`.

### Taskleistenintegration: lokale Fensterreihenfolge

Im Taskleistenmodus haelt TrafficView die Anzeige nun lokal vor der aktuellen Taskleiste, ohne globales TopMost und ohne Fokuswechsel. Normale Fenster wie Windows Explorer oder StartAllBack werden dadurch nicht in einen Vordergrundkonflikt gezogen.

### Simple-Anzeige: Anzeigebilder erzeugen

Die Quellbilder fuer die Simple-Anzeige liegen im Repository unter `DisplayModeAssetSources\Simple`.

Die fertigen Laufzeitbilder fuer die App werden mit folgendem Skript erzeugt:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-DisplayModeAssets.ps1
```

Die erzeugten Dateien liegen danach unter `DisplayModeAssets\Simple` und werden beim Build nach `dist\DisplayModeAssets\Simple` kopiert.

Die Anzeige `Simple blue` nutzt denselben Logikaufbau wie `Simple`, aber eigene blaue Bilddateien unter `DisplayModeAssets\SimpleBlue`.

### Saubere Portable-Ausgabe erstellen

Eine weitergabefaehige Portable-Version ohne lokale Einstellungen, Verbrauchsdaten und Logs wird mit folgendem Skript erstellt:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Create-PortableRelease.ps1
```

Die Ausgabe landet standardmaessig im Ordner `Ausgabe` neben dem Repository. Das Skript baut die Anzeigebilder und `TrafficView.exe` frisch, kopiert nur die freigegebenen Programmdateien und bricht ab, falls private Laufzeitdaten wie `TrafficView.settings.ini`, `Verbrauch.txt` oder Logs in der Portable-Ausgabe gefunden werden. Das ZIP enthaelt zusaetzlich `release-manifest.json` mit Version, Commit, Dateigroessen und SHA-256-Pruefsummen.

### ZIP-Integritaet nach Download pruefen

Jedes Portable-ZIP wird von einer `.sha256`-Datei begleitet. Nach dem Download kann die ZIP-Integritaet wie folgt geprueft werden:

```powershell
Get-FileHash .\TrafficView_Portable_1.4.32.zip -Algorithm SHA256
```

Der ausgegebene Hash muss mit dem Inhalt von `TrafficView_Portable_1.4.32.zip.sha256` uebereinstimmen.

### Alles pruefen

Vor einem Commit oder Release kann der komplette lokale Pruefpfad mit einem Befehl gestartet werden:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Run-AllTests.ps1
```

Der Lauf baut die App, fuehrt die Smoke-Tests aus und prueft beide Portable-Release-Skripte inklusive ZIP-Inhalt.

### Nutzerdaten sichern

Lokale Einstellungen und Verbrauchsdaten koennen vor einem Test oder Update mit folgendem Skript gesichert werden:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Backup-UserData.ps1
```

Eine Sicherung kann bei Bedarf mit `-Mode Restore -BackupPath <BackupOrdner>` wiederhergestellt werden.

### Release vorbereiten

Versionsnummern werden mit einem Skript konsistent in README und AssemblyInfo aktualisiert:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Bump-Version.ps1 -Version 1.4.28
```

Vor einem Release sollte zusaetzlich die manuelle UI-Checkliste unter `docs\ui-release-checklist.md` abgearbeitet und das Ergebnis in `docs\manual-test-log.md` notiert werden.

## English

TrafficView allows only one active instance during normal operation. A second launch shows an informational message and keeps the already running instance active.

### Portable -> Portable: keep data-usage history

When unpacking a new portable version and keeping the existing data-usage history, copy these files from the old portable folder into the new portable folder next to `TrafficView.exe`:

- `Verbrauch.txt`
- `Verbrauch.archiv.txt` if present
- all `Verbrauch.archiv.*.txt.gz` files if present
- `TrafficView.settings.ini`
- `TrafficView.settings.ini_` if present

This preserves current usage data, archived usage data, compressed monthly archives, and the saved adapter, calibration, and display settings. The `TrafficView.settings.ini_` file acts as a backup copy of the saved settings.

### Taskbar integration: toggle visible area

When TrafficView is placed on the taskbar, it starts there with only the right side of the display visible by default. If the overlay is dragged left on the taskbar and enough room is available, the full `both sections` view can appear again. Another light drag to the left on the taskbar can now switch the taskbar view back and forth between `both sections` and `right only`.

### Taskbar integration: local window order

In taskbar mode, TrafficView now keeps the display locally in front of the current taskbar without using global TopMost or forcing focus. Normal windows, including Windows Explorer and StartAllBack, are not pulled into foreground conflicts.

### Run all checks

Before a commit or release, the complete local verification path can be started with one command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Run-AllTests.ps1
```

This builds the app, runs the smoke tests, and verifies both portable release scripts including ZIP contents.

### Back Up User Data

Local settings and usage data can be backed up before a test or update:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Backup-UserData.ps1
```

Restore a backup with `-Mode Restore -BackupPath <BackupFolder>` when needed.

### Prepare a Release

Version numbers are updated consistently in README files and AssemblyInfo with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Bump-Version.ps1 -Version 1.4.28
```

Before publishing a release, also walk through `docs\ui-release-checklist.md` and note the result in `docs\manual-test-log.md`.
