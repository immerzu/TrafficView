# TrafficView 1.2.21

Kleine Windows-Tray-App fuer Windows 11, die aktuelle Download- und Upload-Geschwindigkeit anzeigt.

Beim ersten Start erscheint jetzt zuerst ein eigenes Fenster zur Sprachauswahl. Nach Auswahl und Speichern der Sprache schliesst sich dieses Fenster, danach laeuft der restliche Programmstart wie gewohnt weiter.

Wird die Kalibration aus dem bereits sichtbaren Popup-Menue gestartet, bleibt das Anzeigefenster in dieser Version sichtbar und verschwindet nicht mehr sofort.
Das Anzeigefenster wird nach einer erneuten Kalibration in dieser Version wieder an seiner vorherigen Position eingeblendet, statt scheinbar vom Bildschirm zu verschwinden.
Die Randbegrenzung des Anzeigefensters nutzt in dieser Testversion einen sehr dezenten 3D-Effekt mit leicht aufgehellter Oberseite und etwas dunklerer Unterkante.
Der Innendurchmesser des orange-gruenen Traffic-Rings ist in dieser Version wieder um 2 Pixel vergroessert.
Der dunkle Innenkreis und das mittige Pfeil-Icon bleiben dabei unveraendert gross.
Der orange-gruene Traffic-Ring ist in dieser Version selbst breiter gezeichnet, damit sein Innendurchmesser kleiner wird.
Der dunkle Innenkreis und das mittige Pfeil-Icon behalten dabei ihre bisherige Groesse.
Der Traffic-Ring auf der rechten Seite ist in dieser Version noch einmal um 2 Pixel weiter nach rechts verschoben.
Der Traffic-Ring auf der rechten Seite ist in dieser Version leicht vergroessert und nutzt jetzt einen Durchmesser von 39 Pixeln.
Die Buttonleiste im Kalibrationsdialog wird jetzt mittig im Fenster ausgerichtet, damit links und rechts der gleiche Aussenabstand bleibt.
Der linke Aktionsbutton im Kalibrationsdialog passt seine Breite jetzt auch nach dem Wechsel auf `Neu messen` korrekt an, damit die Beschriftung nicht abgeschnitten wirkt.

Der Traffic-Ring auf der rechten Seite ist in dieser Version um etwa drei Pixel weiter nach rechts gesetzt, damit der freie Platz im Anzeigenfeld besser genutzt wird.
Die Ringanzeige reagiert in dieser Version leicht geglaettet: steigender Traffic wird schnell uebernommen, fallender Traffic etwas ruhiger nachgefuehrt. Die numerischen Werte links bleiben davon unberuehrt.
Die orangefarbene Download-Darstellung ist in dieser Version leuchtstaerker abgestimmt: Ring, Pfeil, Sparkline und Texte wirken heller und kraeftiger, ohne das Layout zu veraendern.
Die Tray-Menueanzeige ist in dieser Version korrigiert: Nach Linksklick auf das Tray-Symbol oeffnet das Popup nicht mehr versehentlich sofort das gemeinsame Menue, und waehrend ein Menue offen ist, pausiert die TopMost-Nachfuehrung des Popups.
Der Erststart-Dialog fuer die Programmsprache nutzt jetzt eine DPI-sichere Buttongroesse, damit `Speichern` nicht mehr abgeschnitten dargestellt wird.
Der Kalibrationsdialog nutzt jetzt flachere Aktionsbuttons und einen zusaetzlichen Button `Abbrechen`, damit sich das Fenster ohne erneute Kalibration wieder schliessen laesst.
Die Buttonhoehe im Kalibrationsdialog richtet sich jetzt kompakt, aber DPI-sicher nach der aktuellen Windows-Schrift, damit die Beschriftungen nicht mehr unten abgeschnitten werden.
Die Buttonleiste im Kalibrationsdialog nutzt jetzt nur noch textbasierte AutoSize-Spalten ohne vorgeschaltete Spacer-Spalte, damit `Starten` und `Adapter speichern` vollständig dargestellt werden.
Der Kalibrationsdialog erweitert seine Breite jetzt bei Bedarf passend zur kompletten Buttonleiste, damit rechts keine Buttons wie `Speichern` oder `Abbrechen` mehr abgeschnitten werden.

## Starten

- Doppelklick auf `dist\TrafficView.exe`
- oder `build.ps1` ausfuehren und danach `dist\TrafficView.exe` starten

## Bedienung

- Linksklick auf das Tray-Symbol: Fenster anzeigen oder ausblenden
- Rechtsklick auf das Tray-Symbol: Menue mit `Anzeigen` und `Beenden`
- `Esc` oder `x` im Fenster: nur das Fenster ausblenden, App bleibt im Tray aktiv

## Technisches

- Versionsordner enthaelt nur die noetigen Dateien fuer Weiterbearbeitung, Build und Ruecksprung auf aeltere Versionen
- Build ohne .NET SDK ueber den vorhandenen `csc.exe`-Compiler aus dem .NET Framework
- DPI-Manifest in `TrafficView.manifest` und Laufzeit-Konfiguration in `TrafficView.exe.config`
- Messung ueber aktive Netzwerkschnittstellen mit `GetIPv4Statistics()`
- Fenster groesse `102 x 56` Pixel mit hellem Rand und dunkelblauem Hintergrund
- `DL` und `UL` fuer mehr Platz im Anzeigenfeld
- Kraeftigere Orange-/Gruentoene fuer Download und Upload
- Linksklick im Anzeigenfeld oeffnet das Programmmenue
- Menuepunkt `Kalibrierung (30 s)...` mit Adapterauswahl
- Eigenes Programmsymbol im Tray
- Beim Programmstart erscheint das Fenster sofort im Vordergrund und bleibt `TopMost`
- Popup-Layout skaliert jetzt mit hohen DPI-Werten sauber mit
- Das Anzeigefeld laesst sich mit gedrueckter linker Maustaste verschieben
- Schriften werden pixelgenau ueber den DPI-Wert skaliert, damit sie nicht uebergross erscheinen
- Die Geschwindigkeitswerte sind gegenueber 1.0.8 um etwa 30 Prozent vergroessert
- Das Menue nutzt wieder eine gut lesbare Windows-Menueschrift
- Die DL- und UL-Werte sind horizontal zentriert angeordnet
- Der Kalibrierungsdialog nutzt jetzt ein flexibles Layout und bleibt unter hoher DPI lesbar
- DL- und UL-Texte sind weiter nach links gerueckt, damit rechts Platz fuer die Visualisierung bleibt
- Rechts sitzt die Traffic-Visualisierung im eigenen Kreisfeld
- Beim ersten Start wird jetzt zur Kalibration aufgefordert
- Das Kalibrationsfenster hat jetzt einen eigenen Bestaetigen-Schritt nach der Messung
- Die Visualisierung reagiert jetzt weicher und flaechiger auf Traffic-Aenderungen
- Die Erststart-Abfrage wird jetzt als erledigt gespeichert und taucht danach nicht staendig wieder auf
- Der Kreis ist groesser und etwas weiter nach links versetzt
- Download und Upload werden jetzt getrennt in einem Doppelring dargestellt
- Im Kalibrationsfenster kann die Adapterauswahl jetzt separat gespeichert werden
- Der gemessene Kalibrationswert wird jetzt ueber einen Button `Speichern` uebernommen
- Der Kreis rechts ist noch einmal vergroessert und etwas weiter nach links versetzt
- Die Visualisierung ist feiner abgestimmt und zeigt kleine bis mittlere Aenderungen deutlicher
- Download und Upload nutzen jetzt getrennte Kalibrationsspitzen, damit auch Gruen den Kreis voll ausfuellen kann
- Die EXE bekommt jetzt ein eingebettetes Programmsymbol fuer die Windows-Explorer-Ansicht
- Der rechte Visualisierer zeigt jetzt einen orangefarbenen Aussenring fuer DL und einen gruenen Innenring fuer UL
- Nach einer Kalibration zeigt das Programm jetzt direkt Adapter-, DL- und UL-Werte an und bleibt bis zum Klick auf `Speichern` geoeffnet
- Im Programmmenue ist jetzt sichtbar, ob die Kalibration offen, nur der Adapter gewaehlt oder bereits gespeichert ist
- Beim Erststart erscheint jetzt direkt das echte Kalibrationsfenster statt einer separaten `Ja/Nein`-Zwischenabfrage
- Das Kalibrationsfenster ist fuer hohe DPI-Werte hoeher und mit stabiler Button-Zeile aufgebaut, damit `Speichern` sichtbar bleibt
- Das Kalibrationsfenster passt seine Groesse jetzt staerker am echten Inhalt an und holt sich beim Anzeigen aktiv in den Vordergrund
- Die untere Kalibrationsleiste nutzt jetzt ein festes Drei-Spalten-Layout, damit `Starten`, `Adapter speichern` und `Speichern` immer sichtbar bleiben
- Nach Messende und nach dem Speichern gibt es keine zusaetzlichen blockierenden Meldungsfenster mehr; die Rueckmeldung bleibt direkt im Kalibrationsdialog
- Das kleine Anzeigefenster wird jetzt sofort beim Programmstart nach vorne geholt und bleibt auch beim Erststart mit Kalibration sichtbar
- Der orangefarbene Download-Ring hellt sich jetzt entlang seines sichtbaren Bogens auf: fruehe Segmente bleiben dunkler, spaetere Segmente werden im Verlauf heller
- Der gruene Upload-Ring hellt sich jetzt ebenfalls entlang seines sichtbaren Bogens auf: am Anfang sattes Gruen, spaetere Segmente zunehmend heller
- Die Ringanzeige bleibt jetzt auch mit Farbverlauf sauber auf die kalibrierte 100-Prozent-Grenze begrenzt und laeuft nicht mehr sichtbar ueber
- Beim Erststart und nach dem Kalibrationsdialog wird das kleine Anzeigefenster jetzt zusaetzlich aktiv in den Vordergrund geholt
- Die Ringanzeige nutzt jetzt eine sanfte, nichtlineare Ausschlagkurve, damit kleine DL- und UL-Bewegungen deutlich sichtbarer werden als zuvor
- Download- und Upload-Ring sind jetzt heller und leuchtender abgestimmt, ohne sonstige Aenderungen am Verhalten
- Der helle Außenrand des Anzeigefelds ist entfernt, damit die Anzeige ohne weißen Rahmen wirkt
- Die Außenkanten des Fensters werden jetzt weicher und sauberer gezeichnet, damit der Rand nicht mehr abgefressen wirkt
- Das Menü enthaelt jetzt einen Punkt `Transparenz`, ueber den sich die Transparenz des gesamten Anzeigefelds von 0 bis 100 Prozent einstellen und speichern laesst
- Die Transparenzeinstellung wird jetzt direkt ueber die Windows-Alpha-Ebene des Popup-Fensters angewendet und wirkt dadurch sichtbar auf das gesamte Anzeigefeld
- Transparenz bleibt jetzt auch beim Adapter-Speichern, bei Kalibration und nach Neustart erhalten und wird zusaetzlich wieder direkt ueber `Opacity` auf das gesamte Popup angewendet
- Der Transparenzdialog uebergibt jetzt den echten aktuellen Reglerwert direkt und zeigt die Transparenz bereits waehrend des Schiebens als Live-Vorschau an
- Transparenz wird jetzt waehrend des Schiebens direkt gespeichert; dadurch bleibt der eingestellte Wert auch nach dem Schliessen des Dialogs und nach Neustart erhalten
- Die numerischen DL- und UL-Anzeigen werden jetzt ueber eine gewichtete 3-Sekunden-Glaettung beruhigt, damit die Ansicht sichtbar ruhiger bleibt
- Download- und Upload-Ring sind jetzt als gleichmaessig segmentierte Ringanzeige mit kleinen Luecken aufgebaut; der Farbverlauf von dunkel nach heller bleibt entlang des gefuellten Verlaufs erhalten
- Upload belegt jetzt im gleichen Ring die Zwischenraeume der orangefarbenen Download-Segmente und nutzt damit genau die freien Stellen zwischen den DL-Bloecken
- Das kleine Anzeigefenster wird beim Start jetzt mehrmals kurz aktiv nach vorne geholt, damit es beim Erststart nicht hinter anderen Fenstern verschwindet
- Der Build erzeugt die `TrafficView.settings.ini` jetzt immer neu mit sauberen Standardwerten, statt alte Werte aus einer kopierten Vorversion mitzuschleppen
- In der Kreismitte sitzen jetzt ein animierter orangefarbener Download-Pfeil nach unten und ein animierter neongruener Upload-Pfeil nach oben
- Die animierten Pfeile in der Kreismitte sind jetzt etwas schmaler und minimal kuerzer abgestimmt
- Die Transparenzeinstellung wirkt jetzt nur noch auf die blaue Fensterflaeche; Ring, Pfeile und numerische Werte bleiben dabei bewusst voll sichtbar
- Das Popup wird jetzt vollstaendig selbst gerendert, damit die Transparenz wirklich nur auf den blauen Hintergrund wirkt und Vordergrundelemente voll deckend bleiben
- Rendering und Animation sind jetzt sparsamer: der statische Fensterunterbau wird gecacht, die zusammengesetzte Layer-Flaeche wiederverwendet und die Pfeil-Animation laeuft nur noch bei sichtbarem Fenster mit reduziertem Takt
- Die Pfeile in der Kreismitte bleiben bei minimalem Traffic jetzt ruhig und beginnen erst bei spuerbar zunehmender Last weich mit einer leichten Bewegung
- Der Kreis rechts ist jetzt leicht vergroessert und etwas weiter nach rechts gesetzt, damit der freie Platz im Anzeigenfeld besser genutzt wird
- Das Anzeigefeld sichert seinen TopMost-Status jetzt auch waehrend des laufenden Betriebs regelmaessig nach, damit es sichtbar im Vordergrund bleibt und nicht nur beim Start nach vorne geholt wird
- Modale Dialoge wie `Kalibration` und `Transparenz` pausieren jetzt die aggressive Vordergrund-Sicherung des Overlays, damit Auswahl und Bedienung nicht mehr sofort wieder ueberschrieben werden
- Waerend der Kalibration bleibt das Anzeigefeld jetzt weiterhin sichtbar im TopMost-Bereich; nur das aktive Nach-vorne-Druecken wird pausiert, damit der Dialog trotzdem bedienbar bleibt
- Unterhalb der DL-/UL-Anzeige sitzt jetzt eine kleine Verlaufsanzeige, die die letzten Sekunden fuer Download und Upload als Mini-Trendlinie im Overlay visualisiert
- Im Menue gibt es jetzt eine Sprachauswahl mit Deutsch, Englisch, Russisch und Chinesisch (vereinfacht); die Oberflaechentexte werden ueber eine externe Sprachdatei geladen
