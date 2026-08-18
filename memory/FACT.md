# TrafficView — Projektgedächtnis (FACT.md)

Stand: 2026-08-18 (Architektur-Analyse + Selbst-Review aus Session „Explain this codebase's architecture")

## Projekt

TrafficView ist ein **Windows-Desktop-Netzwerkverbrauchs-Monitor** (C# / WinForms, .NET Framework 4.8.1, AnyCPU). Er misst den Datenverbrauch eines Netzwerkadapters, zeigt ihn als transparentes Overlay-Popup auf dem Desktop (inkl. Taskleisten-Integration) und protokolliert die Verbrauchshistorie. Keine externen NuGet-Pakete, nur System-Referenzen (System, System.Core, System.IO.Compression, System.Drawing, System.Windows.Forms).

## Technische Eckdaten (verifiziert 2026-08-18)

- **Version:** 1.4.33 — `AssemblyInfo.cs`: `AssemblyVersion("1.4.33.0")` + `AssemblyFileVersion("1.4.33.0")`, README: `# TrafficView 1.4.33`. Letzter Git-Tag: `v1.4.32-15-g0edecaf` (HEAD `0edecaf`).
- **Build-System:** `TrafficView.csproj` ist ein **klassisches (Nicht-SDK-)Projekt** mit manueller `<Compile Include="…">`-Liste. **WICHTIG: Neue `.cs`-Dateien müssen manuell in die Compile-Liste eingetragen werden**, sonst bauen sie nicht mit (Beleg 2026-08-18: `src/FileRetry.cs` + `src/TrafficSnapshotBaselinePolicy.cs` sind untracked, aber bereits in der Liste).
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
- Tests: `tests/Run-AllTests.ps1` orchestriert Build + `Run-SmokeTests.ps1` (EXE) + `Run-ToolingScriptTests.ps1` + `Run-ReleaseScriptTests.ps1`.
- UI-Release-Checkliste: `docs/ui-release-checklist.md`, manuelles Testlog: `docs/manual-test-log.md`.

## Git-/Repo-Konventionen

- `.gitignore` enthält u. a.: `bin/`, `obj/`, `_build_staging/`, `dist/`-Laufzeit-Artefakte (`dist/TrafficView.settings.ini`, `dist/Verbrauch.txt`, `dist/*.ini_`, `dist/*.lnk`, `dist/*.bak_*`), `/TrafficView.settings.ini`, `/TrafficView/` (Laufzeit-Logs), `/Verbrauch.txt`.
- **NICHT ignoriert:** `.reasonix/` — Vorsicht bei `git add -A` (würde Agenten-Artefakte committen).
- `TrafficView/Logs/TrafficView.log` liegt physisch im Quellbaum, ist aber via `/TrafficView/` ignoriert (0 getrackte Dateien in `TrafficView/`).

## Aktueller Arbeitsbaum (Stand 2026-08-18)

Siehe `memory/HANDOVER_20260818.md` — enthält die 16 modifizierten Dateien und 3 untracked Einträge (`src/FileRetry.cs`, `src/TrafficSnapshotBaselinePolicy.cs`, `.reasonix/`).
