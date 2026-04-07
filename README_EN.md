# TrafficView 1.2.21

Small Windows 11 tray application that shows current download and upload speed in a compact movable display panel.

On the first start, the program opens a language selection window first. After the language has been saved, the calibration dialog is shown and the normal startup flow continues.

## Highlights

- Keeps the display window visible when recalibration is started from the already open popup menu
- Restores the display window to its previous position after recalibration
- Test version with a very subtle 3D-style border effect on the display window
- Refined traffic ring geometry for a more balanced inner diameter
- Traffic ring moved 2 pixels to the right for better use of the available display area
- Meter diameter increased to 39 px
- Centered calibration button row
- Correct button width update after switching to `Remeasure`
- Multilingual user interface with German, English, Russian, and Simplified Chinese

## Start

- In a development or final project folder: run `dist\\TrafficView.exe`
- In a delivery folder: run `TrafficView.exe`

## Release

- The active release format is the portable ZIP package only.
- Create it via `portable-release.ps1`
- Output goes to `..\\Ausgabe\\TrafficView.Portable.<Version>.zip`
- An installer is not part of the active release workflow.

## Operation

- Left click the tray icon: show or hide the display
- Right click the tray icon: open the menu with `Show` and `Exit`
- `Esc` or `x` in the popup: hide the window, keep the application running in the tray

## Technical Notes

- Build via `build.ps1` using the available .NET Framework `csc.exe` compiler
- DPI-aware manifest and runtime configuration are included
- Calibration stores separate download and upload peaks
- The display panel can be moved with the left mouse button
- The ring visualization uses segmented orange download indicators and green upload indicators
- Settings, configuration, and language files must stay next to the executable in a delivery folder

## Included Documentation

- German overview: `README.md`
- English overview: `README_EN.md`
- German manual: `TrafficView_Handbuch.md`
- English manual: `TrafficView_Manual_EN.md`
