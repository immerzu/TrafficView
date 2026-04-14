# TrafficView 1.4.21

## Русский

При обычной работе TrafficView разрешает только один активный экземпляр. Повторный запуск показывает уведомление и оставляет уже запущенную копию активной.

### Portable -> Portable: перенос данных расхода

Если распаковывается новая portable-версия и нужно сохранить историю расхода трафика, скопируйте эти файлы из старой portable-папки в новую portable-папку рядом с `TrafficView.exe`:

- `Verbrauch.txt`
- `Verbrauch.archiv.txt`, если файл существует
- все файлы `Verbrauch.archiv.*.txt.gz`, если они существуют
- `TrafficView.settings.ini`
- `TrafficView.settings.ini_`, если файл существует

Так сохраняются текущие данные расхода, архивные данные, сжатые помесячные архивы, а также настройки адаптера, калибровки и режима отображения. Файл `TrafficView.settings.ini_` используется как резервная копия настроек.

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
