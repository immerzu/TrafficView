# Regressionstest Dialoge

## Ziel

Dieser Kurztest deckt die derzeit empfindlichsten Dialoge und die zuletzt geaenderten Bereiche ab:

- Kalibration
- Datenverbrauch
- Transparenz
- Programmsprache

Der Test soll sicherstellen, dass keine Texte, Buttons oder Eingabefelder abgeschnitten sind, keine unnoetigen Scrollleisten erscheinen und Export-/Statusanzeigen mit dem sichtbaren UI uebereinstimmen.

## Testumgebung

- Windows mit normaler Skalierung
- optional zusaetzlich mit 125 % und 150 % Skalierung
- frischer Start von `TrafficView.exe`

## 1. Kalibration

### Startzustand

- Fenster oeffnen
- pruefen, ob keine vertikale oder horizontale Scrollleiste sichtbar ist
- pruefen, ob Info-Text, ComboBox, Fortschrittsbalken, Status, alle vier Buttons und Footer voll sichtbar sind
- pruefen, ob unten kein unnoetig grosser Leerraum bleibt
- pruefen, ob das Fenster waehrend des Oeffnens nicht springt oder seine Groesse aendert

### Laufender Test

- `Starten` klicken
- pruefen, ob der gruene Balken von links nach rechts laeuft
- pruefen, ob die Fenstergroesse waehrend der Kalibration gleich bleibt
- pruefen, ob Statuszeile lesbar bleibt und nichts ueberlappt
- pruefen, ob `Speichern` erst nach Abschluss sinnvoll aktiv wird

### Abschluss

- pruefen, ob der Balken am Ende ganz rechts steht
- pruefen, ob das Fenster nach Abschluss nicht groesser oder kleiner wird
- pruefen, ob `Neu messen`, `Speichern` und `Abbrechen` voll sichtbar bleiben
- pruefen, ob Footer-Logo und Speedtest-Links nicht abgeschnitten sind

## 2. Datenverbrauch

### Startzustand

- Fenster oeffnen
- pruefen, ob `Internetverbindung`, Adaptername und Tabelle voll sichtbar sind
- pruefen, ob `CSV exportieren...`, `Verbrauchsdaten loeschen` und `OK` sofort sichtbar sind
- pruefen, ob keine Buttons abgeschnitten oder gequetscht wirken

### Tabelle

- pruefen, ob die Spalten `Taeglich`, `Woechentlich`, `Monatlich` lesbar sind
- pruefen, ob die Zeilen `Upload`, `Download`, `Gesamt` lesbar sind
- pruefen, ob Werte nicht ueber Zellraender laufen

### CSV-Export

- `CSV exportieren...` klicken
- CSV speichern und in Excel oder Editor oeffnen
- pruefen, ob nur sichtbare Zusammenfassungsdaten exportiert werden
- pruefen, ob `Beginn`, `Ende`, `Upload`, `Download`, `Gesamt` enthalten sind
- pruefen, ob die Spalten `Taeglich`, `Woechentlich`, `Monatlich` korrekt enthalten sind
- pruefen, ob Werte mit dem Fensterinhalt uebereinstimmen

## 3. Transparenz

### Startzustand

- Dialog oeffnen
- pruefen, ob Hinweistext, Prozentanzeige, Slider, `Speichern` und `Schliessen` voll sichtbar sind
- pruefen, ob keine Scrollleisten sichtbar sind
- pruefen, ob keine ueberschuessigen Leerflaechen unten oder rechts entstehen

### Bedienung

- Slider bewegen
- pruefen, ob Prozentanzeige sauber aktualisiert wird
- pruefen, ob Fenstergroesse stabil bleibt
- pruefen, ob Buttons nicht wandern oder abgeschnitten werden

## 4. Programmsprache

### Startzustand

- Dialog oeffnen
- pruefen, ob Info-Text, Label `Sprache`, ComboBox und `Speichern` voll sichtbar sind
- pruefen, ob der rechte Rand nicht abgeschnitten ist
- pruefen, ob keine Scrollleisten sichtbar sind

### Bedienung

- Sprache wechseln
- pruefen, ob Beschriftungen und Button weiter voll sichtbar bleiben
- pruefen, ob die Fenstergroesse stabil bleibt

## 5. Querchecks

- App einmal bei 100 %, 125 % und 150 % testen, wenn moeglich
- auf abgeschnittene Texte, springende Groessen und unnoetige Leerflaechen achten
- nach jedem Dialogtest App schliessen und erneut starten, um Startlayoutfehler zu erkennen

## 6. Freigabekriterien

Ein Build gilt fuer diese Dialoge als unauffaellig, wenn:

- keine Scrollleisten unerwartet erscheinen
- keine Buttons, Labels, Links oder Eingabefelder abgeschnitten sind
- Fenstergroessen bei statischen Dialogen stabil bleiben
- CSV-Export den sichtbaren Fensterdaten entspricht
- bei 100 % und mindestens einer hoeheren Skalierung keine offensichtlichen Layoutfehler auftreten
