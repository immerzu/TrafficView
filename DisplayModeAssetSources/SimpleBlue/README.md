# Simple blue display mode asset source

Dieser Ordner enthaelt die Quellbilder fuer die Anzeige `Simple blue`.

Die fertigen Laufzeitdateien liegen unter:

`F:\Codex\TrafficView_Moi\TrafficView\DisplayModeAssets\SimpleBlue`

Erzeugung:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File F:\Codex\TrafficView_Moi\TrafficView\Build-DisplayModeAssets.ps1
```

## Masterdateien

- `TrafficView.panel.master.png`: leere glaenzend blaue Basis-Anzeigetafel.
- `TrafficView.center_core.master.png`: Quelle fuer die Mitte der Kreisanzeige.

## Laufzeitdateien

Aus `TrafficView.panel.master.png` werden diese Dateien erzeugt:

- `TrafficView.panel.90.png` mit `92 x 50 px`
- `TrafficView.panel.png` mit `102 x 56 px`
- `TrafficView.panel.110.png` mit `112 x 62 px`
- `TrafficView.panel.125.png` mit `128 x 70 px`
- `TrafficView.panel.150.png` mit `153 x 84 px`

`TrafficView.center_core.master.png` wird als `TrafficView.center_core.png` uebernommen.
