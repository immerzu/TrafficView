# Simple display mode asset source

Dieser Ordner enthaelt die Quellbilder fuer die Simple-Anzeige.

Die fertigen Laufzeitdateien liegen unter:

`F:\Codex\TrafficView_Moi\TrafficView\DisplayModeAssets\Simple`

Erzeugung:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File F:\Codex\TrafficView_Moi\TrafficView\Build-DisplayModeAssets.ps1
```

## Masterdateien

- `TrafficView.panel.master.png`: Quelle fuer die Panel-Hintergruende.
- `TrafficView.center_core.master.png`: Quelle fuer die Mitte der Kreisanzeige.

Hinweis: `TrafficView.panel.master.png` ist eine leere Basis-Anzeigetafel ohne
statische Lampenpunkte, ohne festen Kreisring und ohne eingebaute Live-Elemente.
Die TrafficView-Logiken zeichnen Zahlen, Graph, Kreis und Effekte zur Laufzeit;
die Quelle bleibt deshalb bewusst in diesem Asset-Source-Ordner.

## Laufzeitdateien

Aus `TrafficView.panel.master.png` werden diese Dateien erzeugt:

- `TrafficView.panel.90.png` mit `92 x 50 px`
- `TrafficView.panel.png` mit `102 x 56 px`
- `TrafficView.panel.110.png` mit `112 x 62 px`
- `TrafficView.panel.125.png` mit `128 x 70 px`
- `TrafficView.panel.150.png` mit `153 x 84 px`

`TrafficView.center_core.master.png` wird als `TrafficView.center_core.png` uebernommen.
