# TrafficView UI-Release-Checkliste

Diese Checkliste deckt die Punkte ab, die Smoke-Tests und CI nicht verlaesslich sehen.

## Start und Instanzschutz

- `dist\TrafficView.exe` starten.
- Zweiten Start ausloesen und pruefen, dass nur der Hinweis erscheint.
- Tray-Icon sichtbar und Kontextmenue oeffnet sauber.

## Anzeige und Taskleiste

- Standard-Anzeige sichtbar, Werte aktualisieren sich.
- Anzeige verschieben und App neu starten: Position bleibt erhalten.
- Taskleistenintegration einschalten.
- Taskleistenmodus rechts, links und beide Bereiche kurz pruefen.
- Normale Fenster duerfen nicht durch TrafficView in den Vordergrundkonflikt geraten.

## Skins und Anzeige-Modi

- Anzeige-Modus `Standard`, `Simple` und `Simple blue` wechseln.
- Skin-Menue oeffnen und aktuellen Skin erneut auswaehlen.
- Geschuetzter Standardskin darf nicht geloescht werden.

## Einstellungen und Verbrauch

- Transparenz, Skalierung und Sprache kurz umschalten.
- Verbrauchsuebersicht oeffnen.
- CSV-Export der Verbrauchsdaten testen.
- App beenden und neu starten: Einstellungen bleiben erhalten.

## Portable-Paket

- `TrafficView_Portable_<version>.zip` frisch entpacken.
- EXE aus dem entpackten Ordner starten.
- Pruefen, dass keine privaten Laufzeitdaten im Paket enthalten sind.
