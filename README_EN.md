# TrafficView 1.4.25

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

## Run All Checks

Before a commit or release, the complete local verification path can be started with one command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File F:\Codex\TrafficView_Moi\TrafficView\tests\Run-AllTests.ps1
```

This builds the app, runs the smoke tests, and verifies both portable release scripts including ZIP contents.
