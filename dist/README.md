# TrafficView 1.4.25

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

Die Quellbilder fuer die Simple-Anzeige liegen unter `F:\Codex\TrafficView_Moi\TrafficView\DisplayModeAssetSources\Simple`.

Die fertigen Laufzeitbilder fuer die App werden mit folgendem Skript erzeugt:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File F:\Codex\TrafficView_Moi\TrafficView\Build-DisplayModeAssets.ps1
```

Die erzeugten Dateien liegen danach unter `F:\Codex\TrafficView_Moi\TrafficView\DisplayModeAssets\Simple` und werden beim Build nach `F:\Codex\TrafficView_Moi\TrafficView\dist\DisplayModeAssets\Simple` kopiert.

Die Anzeige `Simple blue` nutzt denselben Logikaufbau wie `Simple`, aber eigene blaue Bilddateien unter `F:\Codex\TrafficView_Moi\TrafficView\DisplayModeAssets\SimpleBlue`.

### Saubere Portable-Ausgabe erstellen

Eine weitergabefaehige Portable-Version ohne lokale Einstellungen, Verbrauchsdaten und Logs wird mit folgendem Skript erstellt:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File F:\Codex\TrafficView_Moi\TrafficView\Create-PortableRelease.ps1
```

Die Ausgabe landet standardmaessig unter `F:\Codex\TrafficView_Moi\Ausgabe`. Das Skript baut die Anzeigebilder und `TrafficView.exe` frisch, kopiert nur die freigegebenen Programmdateien und bricht ab, falls private Laufzeitdaten wie `TrafficView.settings.ini`, `Verbrauch.txt` oder Logs in der Portable-Ausgabe gefunden werden.

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
