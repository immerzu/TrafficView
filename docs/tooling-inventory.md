# Tooling Inventory

Stand: 2026-04-04  
Projekt: `TrafficView 1.4.17`

Diese Datei dient als Arbeitsgedächtnis für bereits vorhandene, nachinstallierte und tatsächlich verwendete Werkzeuge rund um TrafficView, Skin-Erzeugung und Build.

## Prüfgrundlage

Die Angaben unten wurden lokal geprüft über:

- Dateisystemprüfung mit PowerShell (`Test-Path`, `Get-ChildItem`, `Get-Item`)
- Versionsabfragen über die jeweilige Binary (`--version` oder API)
- ComfyUI-API unter `http://127.0.0.1:8188/system_stats`
- Einsicht in Projektdateien wie [`build.ps1`](D:\Codex\TrafficView_Moi\TrafficView%201.4.16\build.ps1)

Wenn eine Information nicht sicher feststellbar war, ist sie als `unbekannt` markiert.

## Inventar

| Name | Kategorie | Status | Version | Installationspfad | Binary / Einstiegspunkt | Paketquelle / Installationsmethode | Verwendungszweck im Projekt | Künftig bevorzugt wiederverwenden |
|---|---|---|---|---|---|---|---|---|
| PowerShell 7 | Systemsoftware | Vorinstalliert / vorhanden | `7.6.0` | `C:\Program Files\PowerShell\7` | `C:\Program Files\PowerShell\7\pwsh.exe` | unbekannt, lokal über `Get-Command pwsh` geprüft | Hauptshell für Build, Dateiprüfung, Skripte, ComfyUI-Steuerung | Ja |
| Git for Windows | Systemsoftware | Vorinstalliert / vorhanden | `2.53.0.windows.2` | `C:\Program Files\Git` | `C:\Program Files\Git\cmd\git.exe` | unbekannt, lokal über `git --version` und `Get-Command git` geprüft | Repo-Status, Commit, Push, Versionsstand | Ja |
| .NET Framework C# Compiler | Systemsoftware | Vorinstalliert / vorhanden | `4.8.9221.0` | `C:\WINDOWS\Microsoft.NET\Framework64\v4.0.30319` | `C:\WINDOWS\Microsoft.NET\Framework64\v4.0.30319\csc.exe` | Bestandteil von .NET Framework / Windows, lokal über Dateiversion geprüft | Wird von [`build.ps1`](D:\Codex\TrafficView_Moi\TrafficView%201.4.12\build.ps1) zum Kompilieren von TrafficView verwendet | Ja |
| Python 3.13 (System) | Systemsoftware | Vorinstalliert / vorhanden | `3.13.12` | `C:\Users\lolo\AppData\Local\Programs\Python\Python313` | `C:\Users\lolo\AppData\Local\Programs\Python\Python313\python.exe` | unbekannt, lokal über `python --version` und `Get-Command python` geprüft | Allgemeine Python-Laufzeit; in diesem Projekt bisher nicht die bevorzugte Laufzeit für ComfyUI | Nur wenn nötig |
| dotnet CLI | Systemsoftware | Vorinstalliert / vorhanden | Binary `10.0.526.15411`; SDK lokal nicht verfügbar | `C:\Program Files\dotnet` | `C:\Program Files\dotnet\dotnet.exe` | unbekannt, lokal über `Get-Command dotnet` geprüft | Für TrafficView aktuell nicht der bevorzugte Buildpfad; `build.ps1` nutzt stattdessen `csc.exe` | Nein, nur bei Bedarf |
| ComfyUI | Zusatzsoftware | Nachinstalliert / eingerichtet | `0.18.2` | `D:\Codex\TrafficView_Moi\Zusatz_Tools\ComfyUI\ComfyUI` | `D:\Codex\TrafficView_Moi\Zusatz_Tools\ComfyUI\ComfyUI\main.py` | lokale Portable-Installation, jetzt zentral unter `D:\Codex\TrafficView_Moi\Zusatz_Tools\ComfyUI`; alte Pfade bleiben per Junction kompatibel | Zentrale Bildgenerierung und `img2img` für TrafficView-Skins | Ja, bevorzugt |
| ComfyUI Embedded Python | Zusatzsoftware | Mit ComfyUI installiert / verwendet | `3.13.11` | `D:\Codex\TrafficView_Moi\Zusatz_Tools\ComfyUI\python_embeded` | `D:\Codex\TrafficView_Moi\Zusatz_Tools\ComfyUI\python_embeded\python.exe` | Bestandteil der ComfyUI-Installation, lokal per `--version` geprüft | Bevorzugte Laufzeit zum Starten von ComfyUI | Ja |
| ComfyUI HTTP Service | Service | Verwendet | API meldet ComfyUI `0.18.2` | laufend auf `127.0.0.1:8188` | `http://127.0.0.1:8188/system_stats` | lokal gestarteter Dienst, geprüft per HTTP-API | Render-Queue, Workflow-Ausführung, System- und GPU-Status | Ja |
| SDXL Base 1.0 Checkpoint | Modellpaket | Verwendet | unbekannt | `D:\Codex\TrafficView_Moi\Zusatz_Tools\Models\checkpoints` | `D:\Codex\TrafficView_Moi\Zusatz_Tools\Models\checkpoints\sd_xl_base_1.0.safetensors` | lokal vorhanden; Download-Herkunft in dieser Datei nicht sicher feststellbar | Hauptmodell für Skin-Rendering in ComfyUI | Ja |
| SDXL VAE | Modellpaket | Verwendet / vorhanden | unbekannt | `D:\Codex\TrafficView_Moi\Zusatz_Tools\Models\vae` | `D:\Codex\TrafficView_Moi\Zusatz_Tools\Models\vae\sdxl_vae.safetensors` | lokal vorhanden; Download-Herkunft nicht sicher feststellbar | VAE für SDXL-Decoding in ComfyUI | Ja |
| ControlNet Canny | Modellpaket | Nachinstalliert / vorhanden | unbekannt | `D:\Codex\TrafficView_Moi\Zusatz_Tools\Models\controlnet` | `D:\Codex\TrafficView_Moi\Zusatz_Tools\Models\controlnet\control_v11p_sd15_canny_fp16.safetensors` | im Projektverlauf nachinstalliert; genaue Downloadquelle hier nicht verifiziert | Für künftige Kanten-/Konturstabilisierung bei Skin-Arbeit vorgesehen | Ja |
| TrafficView Model Path Mapping | Hilfskonfiguration | Nachinstalliert / eingerichtet | unbekannt | `D:\Codex\TrafficView_Moi\Zusatz_Tools\ComfyUI\ComfyUI` | `D:\Codex\TrafficView_Moi\Zusatz_Tools\ComfyUI\ComfyUI\extra_model_paths.yaml` | lokal angelegt, Inhalt geprüft | Bindet zentrale Modellordner (`checkpoints`, `vae`, `loras`, `controlnet`) in ComfyUI ein | Ja |
| Inno Setup Compiler Einstieg | Zusatzsoftware | Eingerichtet / vorhanden | `6.7.1` | `D:\Codex\TrafficView_Moi\Zusatz_Tools\InnoSetup` | `D:\Codex\TrafficView_Moi\Zusatz_Tools\InnoSetup\ISCC.cmd` | Wrapper auf lokal installierte Inno-Setup-Binary unter `%LOCALAPPDATA%` | Nur noch historisch fuer alte Installer; nicht Teil des aktiven Portable-Release-Ablaufs | Nein |
| TrafficView Skin Workflow Scripts | Projekt-Hilfswerkzeuge | Nachinstalliert / erstellt | projektintern | `D:\Codex\!Grafikhilfen\Workflows` | z. B. `Prepare-TrafficView-SkinInput.ps1`, `Import-TrafficView-SkinResult.ps1`, `Publish-TrafficView-SkinToDist.ps1` | im Projektverlauf lokal erstellt | Vorbereitung, Import und Veröffentlichung von Skin-Assets | Ja |
| UI-Skin-System Arbeitsordner | Projekt-Hilfsstruktur | Nachinstalliert / erstellt | projektintern | `D:\Codex\!Grafikhilfen\UI-Skin-System` | Ordnerstruktur, kein einzelnes Binary | im Projektverlauf lokal erstellt | Ablage für Render, Workflows, Varianten und Spezifikationen | Ja |

## Nicht gefunden / bewusst als unbekannt markiert

- `IPAdapter`-Modelle:
  - geprüft über `D:\Codex\TrafficView_Moi\Zusatz_Tools\Models\ipadapter`
  - Ergebnis: Ordner nicht vorhanden
- genaue Downloadquelle von `sd_xl_base_1.0.safetensors`:
  - lokal nicht sicher feststellbar
- genaue Downloadquelle von `sdxl_vae.safetensors`:
  - lokal nicht sicher feststellbar
- genaue Downloadquelle von `control_v11p_sd15_canny_fp16.safetensors`:
  - lokal nicht sicher feststellbar

## Arbeitsregeln für künftige Änderungen

1. Für Skin-Erzeugung zuerst ComfyUI unter `D:\Codex\TrafficView_Moi\Zusatz_Tools\ComfyUI` prüfen und wiederverwenden.
2. Modelle zuerst unter `D:\Codex\TrafficView_Moi\Zusatz_Tools\Models` suchen, bevor neue Downloads begonnen werden.
3. Zusätzliche Software für dieses Projekt künftig bevorzugt unter `D:\Codex\TrafficView_Moi\Zusatz_Tools` ablegen.
4. Bereits vorhandene Workflow-Helfer unter `D:\Codex\!Grafikhilfen\Workflows` bevorzugt wiederverwenden.
5. TrafficView weiterhin über [`build.ps1`](D:\Codex\TrafficView_Moi\TrafficView%201.4.16\build.ps1) und `csc.exe` bauen, nicht primär über `dotnet`.
6. Vor neuer Zusatzsoftware immer diese Datei prüfen, um Redundanzen zu vermeiden.
