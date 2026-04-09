# Anzeigentafel-Produktionsvorlage

Projekt: `TrafficView`  
Zweck: wiederverwendbare Mastervorlage für neue Grundtafeln, auf die später nur noch Overlay-Informationen für `DL`, `UL`, Graph und Kreis gelegt werden.

## Zielformat

- Endformat: `1530 x 840 px`
- Seitenverhältnis: `51 : 28`
- Ausgabeformat: `PNG`
- Alpha: erlaubt, aber Außenkante muss sauber und fransenfrei sein

## Produktionsregel

Für ComfyUI wird bevorzugt in einer exakt proportionalen, modellfreundlichen Vorstufe gearbeitet:

- Generationsformat: `1224 x 672 px`
- Verhältnis: ebenfalls `51 : 28`
- beide Kanten sind durch `8` teilbar
- danach sauberes Upscaling auf:
  - `1530 x 840 px`

Upscale-Faktor:

- `1.25`

## Grundform

- horizontale, abgerundete Hightech-Tafel
- klare Außenkontur
- gleichmäßige Rundungen
- kein asymmetrisches Fantasy-Panel
- sauberer Desktop-Schatten erlaubt

## Pflichtzonen auf der Mastertafel (1530 x 840)

### 1. Außenkontur

- Außenrand darf dekorativ und hochwertig sein
- helle Glanzkanten erlaubt
- keine Fransen, kein Pixelrauschen
- Randstärke optisch moderat halten

Empfohlene Eckrundung:

- optisch etwa `70-95 px`

### 2. Linker Informationsblock

Zweck:

- Trägerfläche für `DL` und `UL`
- muss lokal ruhig und kontraststabil bleiben

Empfohlene Zone:

- `x = 70 bis 710`
- `y = 80 bis 610`

Regeln:

- dunkler als der Rest der Tafel
- keine harten Reflexe direkt hinter Text
- keine hochfrequenten Texturen
- Glaswirkung erlaubt, aber gedämpft

### 3. Download-Zeile

Empfohlene reservierte Nutzfläche:

- `x = 110 bis 650`
- `y = 120 bis 285`

Regeln:

- Hintergrund lokal glatt
- hoher Helligkeitsabstand zum späteren Text
- keine diagonalen Lichtstreifen durch den Zahlenkern

### 4. Upload-Zeile

Empfohlene reservierte Nutzfläche:

- `x = 110 bis 650`
- `y = 350 bis 520`

Regeln:

- identisch ruhig wie Download-Bereich
- keine Spiegelung direkt durch die Mitte der Ziffern

### 5. Untere Graph-Zone

Empfohlene reservierte Nutzfläche:

- `x = 85 bis 760`
- `y = 620 bis 770`

Regeln:

- leicht dekorativ erlaubt
- aber visuell untergeordnet
- keine dominante Lichtkante direkt unter dem Graph

### 6. Rechte Kreiszone

Zweck:

- Trägerfläche für den großen Traffic-Kreis

Empfohlene Kreiszone:

- Mittelpunkt ungefähr: `x = 1175`, `y = 415`
- nutzbarer Außendurchmesser ungefähr: `410 bis 470 px`

Regeln:

- Bereich lokal klar lesbar
- Hintergrund und Ring dürfen sich nicht visuell auflösen
- keine unruhigen Hintergrundmuster mit ähnlicher Segmentlogik
- Kreiszone darf heller und technisch präziser wirken als links

## Materialregeln

- Stil: `glossy tech glass`, `polished acrylic`, `premium desktop HUD`
- erlaubt:
  - subtile Spiegelungen
  - Lichtkanten
  - weiche Reflexbänder
  - kontrollierte Tiefenebenen
- nicht erlaubt:
  - milchige Nebelschicht
  - chaotische Neonmuster
  - helle Reflexe im Zahlenkern
  - Texturrauschen in Funktionszonen

## Prioritätsregel

Wichtiger als jede Dekoration:

1. Lesbarkeit `DL`
2. Lesbarkeit `UL`
3. Klarheit des Kreises
4. erst danach Materialeffekte

## Produktionshinweis für neue Skins

Wenn eine neue Grundtafel erzeugt wird, sollte sie bereits enthalten:

- Außenform
- Rand
- Schatten
- linke ruhige Info-Fläche
- rechte Kreisfläche
- untere Graph-Zone
- hochwertige Materialwirkung

Nicht fest einbrennen, wenn dynamisches Overlay geplant ist:

- echte Live-Zahlen
- variable Einheiten
- dynamische Kreis-Symbole
- dynamische Graphwerte

## Zugehörige Vorlagendateien

- Der frühere Template-Bestand ist im aktuellen Projektordner nicht mehr enthalten.
- Wenn diese Vorlage erneut produktiv genutzt wird, sollten Workflow-JSON und Prompt-Set zuerst aus Archiv oder externem Workflow-Bestand wiederhergestellt und dann in einen projektlokalen Pfad überführt werden.
