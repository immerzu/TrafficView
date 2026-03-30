# TrafficView Handbuch

## 1. Zweck des Programms

`TrafficView` ist ein kleines Windows-Programm fuer Windows 11, das die aktuelle Download- und Upload-Geschwindigkeit in einem kompakten, frei verschiebbaren Anzeigefeld darstellt. Das Programm arbeitet im Tray-Bereich weiter und blendet das Anzeigefeld bei Bedarf ein oder aus.

## 2. Systemvoraussetzungen

- Windows 11
- Desktop-Betrieb mit aktivem Infobereich / Tray
- Mindestens ein aktiver Netzwerkadapter

## 3. Lieferumfang

Zum Programm gehoeren je nach Ordnerstand unter anderem:

- `dist\TrafficView.exe`
- `dist\TrafficView.exe.config`
- `dist\TrafficView.settings.ini`
- `src\Program.cs`
- `build.ps1`
- `TrafficView.manifest`
- `TrafficView.ico`
- `README.md`

## 4. Start des Programms

Das Programm wird ueber `TrafficView.exe` gestartet.

Nach dem Start passiert Folgendes:

1. Das kleine Anzeigefeld wird sofort im Vordergrund eingeblendet.
2. Beim ersten Start erscheint zusaetzlich das Kalibrationsfenster.
3. Das Programm legt sich mit seinem Symbol in den Tray-Bereich.

## 5. Erststart und Kalibration

Beim ersten Start arbeitet `TrafficView` mit Grundeinstellungen. Damit die Ringanzeige die Netzwerklast sinnvoll darstellen kann, sollte eine Kalibration durchgefuehrt werden.

### Ablauf der Kalibration

1. Im Kalibrationsfenster den gewuenschten Netzwerkadapter auswaehlen.
2. Auf `Starten` klicken.
3. Die Messung laeuft ca. `30 Sekunden`.
4. Waehrenddessen zeigt der Dialog den Ablauf der Zeit sowie die gemessenen `DL`- und `UL`-Werte an.
5. Nach Abschluss werden die ermittelten Werte angezeigt.
6. Mit `Speichern` werden die Kalibrationsdaten uebernommen.

### Hinweise zur Kalibration

- `Adapter speichern` speichert die Adapterauswahl.
- `Speichern` uebernimmt die gemessenen Kalibrationswerte.
- Das Kalibrationsfenster bleibt bis zum Abschluss und Speichern geoeffnet.
- Beim erneuten Oeffnen der Kalibration werden bereits vorhandene Werte als Ausgangspunkt beruecksichtigt.

## 6. Das Anzeigefeld

Das Anzeigefeld ist kompakt gehalten und besteht aus zwei Bereichen.

### Linker Bereich

Links werden die numerischen Werte angezeigt:

- `DL` = aktuelle Download-Geschwindigkeit
- `UL` = aktuelle Upload-Geschwindigkeit

Die numerischen Werte sind geglaettet, damit die Anzeige ruhiger wirkt. Dabei wird nicht jeder einzelne Rohwert direkt gezeigt, sondern ein sinnvoll geglaetteter Verlauf ueber mehrere Sekunden.

### Rechter Bereich

Rechts befindet sich die visuelle Traffic-Anzeige:

- Der `Download` wird in `Orange` dargestellt.
- Der `Upload` wird in `Gruen` dargestellt.
- Beide Werte werden ringfoermig visualisiert.
- Der Ring arbeitet nichtlinear, damit kleine Traffic-Aenderungen deutlicher sichtbar werden.

### Ringdarstellung

Die Ringanzeige besteht aus segmentierten Elementen:

- Orangefarbene Segmente visualisieren den Download.
- Die gruene Upload-Anzeige nutzt die Zwischenraeume der Download-Segmente.
- Der Farbverlauf bleibt erhalten:
  - niedriger Traffic = dunklerer Farbton
  - hoher Traffic = hellerer, leuchtenderer Farbton

### Pfeile in der Mitte

In der Mitte des Rings befinden sich zwei animierte Richtungspfeile:

- orangefarbener Pfeil nach unten fuer Download
- neongruener Pfeil nach oben fuer Upload

Diese Animation dient der schnellen optischen Orientierung.

## 7. Bedienung

### Tray-Symbol

Das Tray-Symbol befindet sich im Infobereich von Windows.

- `Linksklick` auf das Tray-Symbol: Anzeigefeld ein- oder ausblenden
- `Rechtsklick` auf das Tray-Symbol: Programmmenue oeffnen

### Linksklick im Anzeigefeld

Ein Linksklick direkt auf das Anzeigefeld oeffnet ebenfalls das Programmmenue.

### Verschieben des Anzeigefeldes

Das Anzeigefeld kann mit gedrueckter linker Maustaste verschoben werden.

### Fensterzustand

- Das Programmfenster bleibt `TopMost`, also im Vordergrund.
- Das Schliessen des sichtbaren Anzeigefeldes blendet nur die Anzeige aus.
- Das Programm selbst bleibt im Tray aktiv, bis es ueber `Beenden` geschlossen wird.

## 8. Programmmenue

Das Menue enthaelt folgende Punkte:

- `Anzeigen` bzw. `Ausblenden`
- `Kalibrationsstatus`
- `Kalibration (30 s)...`
- `Transparenz`
- `Beenden`

### Kalibrationsstatus

Der Menuepunkt `Kalibrationsstatus` zeigt den aktuellen Zustand:

- `offen`
- `Adapter gewaehlt`
- `gespeichert`

## 9. Transparenz

Ueber den Menuepunkt `Transparenz` laesst sich die Sichtbarkeit des Anzeigefelds einstellen.

Wichtig:

- Die Transparenz wirkt nur auf den `blauen Hintergrundbereich` des Anzeigefelds.
- Ringanzeige, Pfeile und numerische Werte bleiben dabei sichtbar und verlieren nicht ihre Leuchtkraft.
- Die Einstellung wird direkt gespeichert und nach einem Neustart wieder geladen.

## 10. Einstellungen und Speicherung

Die Programmeinstellungen werden in der Datei `TrafficView.settings.ini` gespeichert. Sie liegt im Regelfall im `dist`-Ordner neben der EXE.

Gespeichert werden unter anderem:

- ausgewaehlter Adapter
- Adaptername
- Kalibrationswerte
- Status des Erststarts
- Transparenzwert

Typische Eintraege:

- `AdapterId`
- `AdapterName`
- `CalibrationPeakBytesPerSecond`
- `CalibrationDownloadPeakBytesPerSecond`
- `CalibrationUploadPeakBytesPerSecond`
- `InitialCalibrationPromptHandled`
- `TransparencyPercent`

## 11. Darstellung und Technik

`TrafficView` ist auf eine saubere Darstellung unter Windows 11 ausgelegt.

Wichtige technische Merkmale:

- DPI-Unterstuetzung fuer hohe Skalierungen
- eigenes Programmsymbol
- Hintergrunddarstellung ueber selbst gerendertes Layer-Fenster
- sparsamere Animation und gecachte Zeichenflaechen zur Reduzierung von CPU- und Grafiklast

## 12. Hinweise zur Nutzung

- Fuer eine aussagekraeftige Ringanzeige sollte nach dem ersten Start eine Kalibration durchgefuehrt werden.
- Wenn sich der genutzte Netzwerkadapter aendert, sollte erneut kalibriert werden.
- Bei sehr geringer Aktivitaet reagiert die Anzeige bewusst empfindlicher, damit kleine Traffic-Bewegungen sichtbar bleiben.

## 13. Fehlerbehebung

### Das Programm zeigt keine sinnvollen Ringwerte

Ursache:
Es wurden noch keine passenden Kalibrationsdaten gespeichert.

Loesung:
Kalibration ueber das Menue erneut durchfuehren und mit `Speichern` abschliessen.

### Der falsche Adapter wird gemessen

Ursache:
Es ist ein ungeeigneter oder nicht aktiver Adapter ausgewaehlt.

Loesung:
Kalibration oeffnen, passenden Adapter auswaehlen, `Adapter speichern`, anschliessend neu messen und `Speichern`.

### Die Transparenz wirkt ungewohnt

Hinweis:
Die Transparenz aendert nur den blauen Hintergrund. Ring und Werte bleiben absichtlich deckend.

## 14. Dateiuebersicht fuer die Weiterarbeit

Fuer die Weiterentwicklung sind insbesondere diese Dateien wichtig:

- `src\Program.cs`
- `build.ps1`
- `TrafficView.manifest`
- `TrafficView.ico`
- `TrafficView.exe.config`

## 15. Kurzfassung

`TrafficView` ist ein kompaktes Tray-Programm zur Anzeige von Download und Upload in numerischer und grafischer Form. Die Anzeige ist verschiebbar, kalibrierbar, DPI-faehig und in ihrer Hintergrund-Transparenz anpassbar. Die Einstellungen werden dauerhaft in einer INI-Datei gespeichert.
