# GitHub Backup Snapshot 2026-04-09

Dieser Branch dient als zusaetzliche GitHub-Sicherung fuer lokale Daten, die nicht im regulaeren `main`-Stand enthalten waren.

## In GitHub aufgenommen

- Aktueller Buildstand aus `dist/TrafficView.exe`
- Lokale Skin-Arbeitsdateien aus `Skin_Work/`
- Vergleichsbilder:
  - `SkinCompare_08_09.png`
  - `SkinCompare_08_09_10.png`
- Lokale Tooling-Metadatei:
  - `.codex/tooling-memory.json`
- Archivierte Top-Level-Release-Dateien unter `backup/top-level-releases/`
- Komprimierte lokale Versionsstaende:
  - `backup/version-archives/TrafficView-1.4.14.zip`
  - `backup/version-archives/TrafficView-1.4.15.zip`

## Nur dokumentiert, nicht nach GitHub gespiegelt

Diese Datenmengen wurden bewusst nicht direkt in das GitHub-Repo uebernommen, weil sie fuer ein normales Git-Repo zu gross oder zu unhandlich sind:

- `_codex_versions/`
  - ca. 17.97 GB
  - ca. 243 Snapshots
- `Archiv/Versionsstaende/TrafficView 1.4.16/`
  - ca. 21.54 GB
- `Archiv/Versionsstaende/old/`
  - ca. 213.10 MB

## Zweck

Wenn lokal Daten verloren gehen, stellt dieser Branch eine zusaetzliche Sicherung fuer den wichtigsten aktiven Arbeitsstand und die handhabbaren lokalen Zusatzdaten dar.
