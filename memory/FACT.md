# TrafficView — Projektgedächtnis (FACT.md)

Stand: 2026-08-18 (nacht) — Release v1.4.35 abgeschlossen; Gesamtüberblick in `memory/HANDOVER_20260818_v1.4.35.md`.

## Projekt

TrafficView ist ein **Windows-Desktop-Netzwerkverbrauchs-Monitor** (C# / WinForms, .NET Framework 4.8.1, AnyCPU). Er misst den Datenverbrauch eines Netzwerkadapters, zeigt ihn als transparentes Overlay-Popup auf dem Desktop (inkl. Taskleisten-Integration) und protokolliert die Verbrauchshistorie. Keine externen NuGet-Pakete, nur System-Referenzen (System, System.Core, System.IO.Compression, System.Drawing, System.Windows.Forms).

## Technische Eckdaten (verifiziert 2026-08-18)

- **Version:** 1.4.35 — `AssemblyInfo.cs`: `AssemblyVersion("1.4.35.0")` + `AssemblyFileVersion("1.4.35.0")`, README: `# TrafficView 1.4.35`. Letzter Git-Tag: `v1.4.35`.
- **Build-System:** `TrafficView.csproj` ist ein **klassisches (Nicht-SDK-)Projekt** mit manueller `<Compile Include="…">`-Liste. **WICHTIG: Neue `.cs`-Dateien müssen manuell in die Compile-Liste eingetragen werden**, sonst bauen sie nicht mit (Beleg 2026-08-18: `src/FileRetry.cs` + `src/TrafficSnapshotBaselinePolicy.cs` mussten nachgetragen werden; committet in `fd3247a`).
- **Kein Hauptfenster:** Die App lebt im Tray (`NotifyIcon`); Dialoge sind `UsageSummaryForm`, `CalibrationForm`, `DialogForms`.

## Architektur (3 Schichten)

```
Program (Bootstrap, Single-Instance, Exception-Hooks, High-DPI)
  → TrafficViewContext (ApplicationContext, 7 partial-Dateien) — Koordinator
      → TrafficPopupForm (58 partial-Dateien) — Overlay-Anzeige
  → Domain/Persistenz: NetworkSnapshot, TrafficUsageLog, MonitorSettings, AppStorage, UiLanguage, Skins
```

### 1. `Program.Main` (`src/Program.cs`)
Benannter Mutex `Local\TrafficView.SingleInstance` erzwingt genau eine Instanz (2. Start → Hinweis-MessageBox). Registriert `Application.ThreadException` + `AppDomain.UnhandledException`-Logging, `DpiHelper.EnableHighDpiSupport()`, startet `Application.Run(new TrafficViewContext())`.

### 2. `TrafficViewContext` (7 partial-Dateien)
`TrafficViewContext.cs` + `.Popup`, `.Settings`, `.Usage`, `.Skins`, `.Branding`, `.Diagnostics`. Lädt `MonitorSettings`, erzeugt Popup + `NotifyIcon`, baut **ein gemeinsames `ContextMenuStrip`** (Overlay-Linksklick UND Tray-Rechtsklick nutzen dasselbe Menü), verdrahtet Popup-Events, persistiert Settings beim Exit (`ExitThreadCore`).

### 3. `TrafficPopupForm` (58 partial-Dateien)
Rahmenloses, transparentes, `TopMost`-Overlay. Organisiert über **GENAU 6 Timer** (verifiziert, nicht 5!):
1. `refreshTimer` (1 s) — Netzwerk-Snapshot, Raten, Events
2. `animationTimer` (140 ms) — Glanz-/Aktivitäts-Animationen
3. `topMostGuardTimer` — TopMost-Platzierung nachziehen
4. `taskbarMonitorTimer` — Taskleisten-Integration
5. `taskbarRefreshDebounceTimer` — Taskleisten-Refresh-Entprellung
6. `manualDragMoveTimer` — Maus-Ziehen

partial-Dateien nach Belangen gruppiert: `Monitoring*`, `Rendering*/Rings*/Sparkline*/TextPanels*`, `Taskbar*` (größter Block: Platzierung, Bands, Z-Order, Fensterklassen-Erkennung), `Lifecycle*`, `Input*`, `Appearance*`, `VisualEffects*`, `State`, `Surface*`.

## Datenfluss (1-Sekunden-Zyklus)

1. `RefreshTimer_Tick` → `NetworkSnapshot.Capture(settings)` — P/Invoke `iphlpapi!GetIfEntry2` (`MIB_IF_ROW2`), ergänzt um `NetworkInterface.GetAllNetworkInterfaces()`.
2. **Auto-Adapter-Auswahl mit Hysterese:** Adapterwechsel wird erst nach **3 bestätigenden Samples** übernommen (`AutomaticAdapterSwitchConfirmationSamples = 3`).
3. Differenz zum letzten Sample → Roh-Rate Byte/s → `TrafficRateSmoothing` (gewichteter gleitender Mittelwert, `DisplaySmoothingSampleCount`/`DisplaySmoothingWeights`) → Rendering + Events `RatesUpdated` / `TrafficUsageMeasured`.
4. Verbrauch via `TrafficUsageLog.QueueUsage` in lock-geschützten **Pending-Puffer**, verzögerter `FlushPending()` als Zeile `UTC|AdapterKey|Download|Upload` (InvariantCulture) in `Verbrauch.txt`.

## Verbrauchs-Persistenz (`TrafficUsageLog`)

- Dateien: `Verbrauch.txt` (aktiv, Limit `MaxActiveUsageFileBytes` = 2 MB), `Verbrauch.archiv.txt`, `Verbrauch.archiv.*.txt.gz` (komprimierte Monatsarchive).
- `GetSummaries` aggregiert Tages-/Wochen-/Monats-Summen aus Archiven + aktiver Datei + Pending-Puffer; CSV-Export blockiert Ziele, die mit internen Verbrauchsdateien kollidieren.
- `ClearAll` löscht über „Delete-Backups" (Verschieben → Löschen, bei Fehler Restore) — defensiv.

## Einstellungen & Speicherorte

- `MonitorSettings` ist **immutable** (alle Änderungen via `With…()`-Kopiermethoden, z. B. `WithPopupLocation`), persistiert als `TrafficView.settings.ini` + Backup `TrafficView.settings.ini_`.
- `AppStorage` unterscheidet **Portable-Modus** (Marker-Datei `TrafficView.portable` → alles neben der EXE) von installiertem Modus (`%LocalAppData%\TrafficView`), erkennt Cloud-Sync-Ordner (OneDrive, Dropbox, Google Drive, iCloudDrive) und warnt bei Schreibproblemen.
- Sprachen: `UiLanguage` + `TrafficView.languages.ini`. Skins: Ordner mit `skin.ini` + PNGs (`PanelSkinCatalog`, `PanelSkinDefinition`, `SkinPathPolicy`); `Import-Skin.ps1` mit Größen-/Dimensions-Limits.

## Build & Tooling (PowerShell-zentriert)

- `build.ps1`, `Create-PortableRelease.ps1` (Ausgabe-Ordner **neben** dem Repo, `release-manifest.json` mit Version/Commit/SHA-256, begleitende `.sha256`-Dateien), `Bump-Version.ps1`, `Backup-CodeRepository.ps1`, `Backup-UserData.ps1`, `Build-DisplayModeAssets.ps1`, `Import-Skin.ps1`, `portable-release.ps1`.
- **Dauerregel:** Portable-Release-Ausgabe dauerhaft nach `F:\001_Coding_Projekte\TrafficView_Moi\01_Ausgabe` (Nutzerregel 2026-08-18) — nicht das Skript-Default `Ausgabe`; ggf. `Create-PortableRelease.ps1 -OutputRoot F:\001_Coding_Projekte\TrafficView_Moi\01_Ausgabe` verwenden.
- Tests: `tests/Run-AllTests.ps1` orchestriert Build + `Run-SmokeTests.ps1` + `Run-ToolingScriptTests.ps1` + `Run-ReleaseScriptTests.ps1`. **WICHTIG (belegt): `Run-AllTests.ps1` baut zuerst (`build.ps1`, Zeile 26); `Run-SmokeTests.ps1` startet NUR die selbst kompilierte Test-EXE (`TrafficView.SmokeTests.exe` aus `src/`) — `dist\TrafficView.exe` wird von den Tests nicht benötigt** (deshalb ist sie seit `a59386b` nicht mehr versioniert).
- **Compiler (belegt):** `build.ps1` nutzt den Legacy-`csc.exe` aus `Framework64\v4.0.30319` („for C# 5", Version 4.8.9221.0) — **kein `/deterministic`, kein `/pathmap` unterstützt** (via `csc /help` verifiziert) → Builds sind nicht byte-identisch (PE-Timestamp/MVID variieren). Deterministisch nur mit Roslyn-Compiler (nicht lokal vorhanden; nicht geprüft).
- UI-Release-Checkliste: `docs/ui-release-checklist.md`, manuelles Testlog: `docs/manual-test-log.md` (enthält seit `27a8b2f` den Abschnitt 1.4.34).

## Git-/Repo-Konventionen

- `.gitignore` enthält u. a.: `bin/`, `obj/`, `_build_staging/`, `dist/`-Laufzeit-Artefakte (`dist/TrafficView.settings.ini`, `dist/Verbrauch.txt`, `dist/Verbrauch.archiv.*.txt.gz`, `dist/*.ini_`, `dist/*.lnk`, `dist/*.bak_*`), **`dist/TrafficView.exe`** (seit `a59386b` — Build-Artefakt wird NICHT mehr versioniert; `dist/TrafficView.new.exe` war schon ignoriert), `.reasonix/` (seit `82961ac`), `/TrafficView.settings.ini`, `/TrafficView/` (Laufzeit-Logs), `/Verbrauch.txt`.
- `TrafficView/Logs/TrafficView.log` liegt physisch im Quellbaum, ist aber via `/TrafficView/` ignoriert (0 getrackte Dateien in `TrafficView/`).
- **Release-/Tag-Muster:** Tags werden auf den fachlichen Release-Stand gesetzt; `v1.4.34` wurde nachträglich per Force-Push auf den Fix-Commit verschoben (GitHub-Release folgt dem Tag automatisch); `v1.4.35` wurde direkt auf den Release-Commit gesetzt (kein Force-Push nötig). `git describe` nach Release: `v1.4.35-<n>-g<commit>`.

## Release v1.4.34 (2026-08-18, abgeschlossen)

- **Commits (chronologisch, alle gepusht):** `01c9724` (docs-Pfade) → `82961ac` (.gitignore `.reasonix/`) → `fd3247a` (Release 1.4.33-Bündel: Baseline-Policy, File-Retry, Diagnostics, Calibration-Härtung) → `1de29e2` (Bump 1.4.34) → `662706d` (dist-Refresh) → `63cdc18` (Projektgedächtnis) → `27a8b2f` (manual-test-log 1.4.34) → `6b96ab4` (Doku-Sync) → `18e9c90` (Fix W1–W3+H1: Manual.txt/dist-Versionen, FACT.md-HEAD entfernt) → `a59386b` (dist/TrafficView.exe aus Versionierung genommen).
- **Tags:** `v1.4.33` → `fd3247a`; `v1.4.34` → `18e9c90` (ursprünglich `662706d`, nach Fix-Commit per Force-Push verschoben; GitHub-Release folgt dem Tag).
- **GitHub-Release:** „TrafficView v1.4.34" (https://github.com/immerzu/TrafficView/releases/tag/v1.4.34), Asset `TrafficView_Portable_1.4.34.zip`. **OFFENER PUNKT: Das Release-Asset ist noch das ERSTE ZIP (506.817 B, Build vor `18e9c90`) — das finale ZIP (506.810 B, Manifest commit `18e9c90`) wurde nicht als Asset hochgeladen** (nicht freigegeben gewesen).
- **Portable-ZIP (final):** `F:\001_Coding_Projekte\TrafficView_Moi\01_Ausgabe\TrafficView_Portable_1.4.34.zip` — 506.810 Bytes, SHA-256 `c854ed4995966221a4fe0a2ac0a99a0c15714148b61557dc7104fa564104ce80` (= `.sha256`), `release-manifest.json`: version `1.4.34`, commit `18e9c90…`, 21 ZIP-Einträge; Inhalt geprüft (Manual.txt 1.4.34, README ohne 1.4.32/1.4.28-Reste).
- **gh CLI:** v2.97.0 verfügbar, authentifiziert als `immerzu` (Scopes gist/read:org/repo/workflow) — Releases via `gh release create <tag> <asset> --title … --notes-file …` erstellen.

## Release v1.4.35 (2026-08-18, abgeschlossen)

- **Commits:** `7dcd37f` (feat: refine center arrow pulse and meter gloss intensity and speed — Optionen A+C, Gloss+20 %, Geschwindigkeit+20 %) → `9e97b5f` (chore: Bump version to 1.4.35; nur Versions-/Doku-Dateien: README, README_EN, AssemblyInfo, Manual.txt, dist/Manual.txt, dist/README.md).
- **Tag:** `v1.4.35` (annotiert, „Release v1.4.35") → `9e97b5f`, gepusht (kein Force-Push).
- **GitHub-Release:** „TrafficView v1.4.35" (https://github.com/immerzu/TrafficView/releases/tag/v1.4.35), **2 Assets**: `TrafficView_Portable_1.4.35.zip` (506.824 B) + `TrafficView_Portable_1.4.35.zip.sha256` (99 B).
- **Portable-ZIP:** `F:\001_Coding_Projekte\TrafficView_Moi\01_Ausgabe\TrafficView_Portable_1.4.35.zip` — SHA-256 `4d4a65f66eb06d776686c2dc64a8835f4f9f55077d721224d29b445691abcda2` (= `.sha256`), Manifest `version 1.4.35`, `commit 9e97b5f…` (Build NACH dem Release-Commit, daher korrekt); Manual.txt im ZIP = 1.4.35.
- **Prozess-Lektion (wiederholt bestätigt):** `Bump-Version.ps1` deckt Manual.txt + dist/Manual.txt NICHT ab → manuell mitziehen; `dist/README.md` aus Root-README ableiten; Portable-Build IMMER nach dem Release-Commit ausführen, damit das Manifest den finalen Commit nennt.
- **gh CLI:** v2.97.0 verfügbar, authentifiziert als `immerzu` (Scopes gist/read:org/repo/workflow) — Releases via `gh release create <tag> <asset>… --title … --notes-file …` erstellen.

## Aktueller Arbeitsbaum (Stand 2026-08-18 nacht)

- `main` == `origin/main` == `9e97b5f`; Tag v1.4.35 auf `9e97b5f`; Arbeitsbaum sauber.
- Offene Punkte aus der Lese-Prüfung (bewusst NICHT behoben): H2 (F:\Codex-Pfade in `DisplayModeAssetSources\*`, 4 Dateien), H6 (README_EN unvollständig), H7 (Skript-Default `Ausgabe` vs. Dauerregel `01_Ausgabe`), H8 (Bump-Beispiel 1.4.26). Historisch: `memory/HANDOVER_20260818.md` (v1.4.33-Stand) und `memory/HANDOVER_20260818_v1.4.34.md` als Momentaufnahmen.
