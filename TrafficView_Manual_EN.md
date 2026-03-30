# TrafficView Manual

## 1. Purpose of the Program

`TrafficView` is a small Windows 11 tray application that shows current download and upload traffic in a compact movable display panel. The program continues to run in the tray and can show or hide the display when needed.

## 2. System Requirements

- Windows 11
- Desktop environment with an active system tray
- At least one active network adapter

## 3. Included Files

Depending on the project folder, the most important files include:

- `dist\\TrafficView.exe`
- `dist\\TrafficView.exe.config`
- `dist\\TrafficView.settings.ini`
- `dist\\TrafficView.languages.ini`
- `src\\Program.cs`
- `build.ps1`
- `TrafficView.manifest`
- `TrafficView.ico`
- `README.md`

## 4. Program Startup

The program is started through `TrafficView.exe`.

After startup, the following sequence is used:

1. The small display panel is shown in the foreground.
2. On the first start, a language selection window appears first.
3. After choosing and saving the language, the normal startup flow continues.
4. On the first start, the calibration window is then shown.
5. The program remains available in the Windows tray.

## 5. First Start and Language Selection

On the first start, `TrafficView` opens a dedicated language selection window before the rest of the program flow continues.

Available languages:

- German
- English
- Russian
- Simplified Chinese

After the language has been selected and saved, the window closes. The selection is stored in the settings file so it does not appear again on every start.

## 6. First Start and Calibration

`TrafficView` starts with default settings. To make the traffic ring behave meaningfully, calibration should be completed.

### Calibration Process

1. Select the desired network adapter in the calibration window.
2. Click `Start`.
3. The measurement runs for about `30 seconds`.
4. During the measurement, the dialog shows elapsed time as well as the measured `DL` and `UL` values.
5. After completion, the measured values are displayed.
6. Click `Save` to store the calibration data.

### Calibration Notes

- `Save adapter` stores the adapter selection.
- `Save` stores the measured calibration values.
- The calibration window remains open until calibration has finished and the values are saved.
- If calibration is opened again later, existing saved values are used as the starting point.

## 7. The Display Panel

The display panel is compact and split into two areas.

### Left Side

The left side shows the numeric values:

- `DL` = current download speed
- `UL` = current upload speed

The numeric values are smoothed so the display remains calmer and does not jump with every raw measurement.

### Right Side

The right side contains the visual traffic display:

- `Download` is shown in `orange`.
- `Upload` is shown in `green`.
- Both values are visualized as a ring.
- The ring uses a non-linear response so that smaller traffic changes remain visible.

### Ring Visualization

The ring display is built from segmented elements:

- Orange segments visualize download traffic.
- The green upload display uses the gaps between the orange download segments.
- The color gradient is preserved:
  - lower traffic = darker tone
  - higher traffic = brighter, more luminous tone

### Arrows in the Center

Two animated directional arrows are shown in the center of the ring:

- orange downward arrow for download
- neon green upward arrow for upload

This animation provides quick visual orientation.

## 8. Operation

### Tray Icon

The tray icon is located in the Windows notification area.

- `Left click` on the tray icon: show or hide the display panel
- `Right click` on the tray icon: open the program menu

### Left Click on the Display Panel

Left clicking directly on the display panel also opens the program menu.

### Moving the Display Panel

The display panel can be moved by dragging it with the left mouse button.

### Window State

- The display stays `TopMost`, meaning it remains in the foreground.
- Closing the visible display only hides the panel.
- The program itself remains active in the tray until `Exit` is selected.

## 9. Program Menu

The menu contains the following entries:

- `Show` or `Hide`
- `Calibration status`
- `Calibration (30 s)...`
- `Transparency`
- `Language`
- `Exit`

### Calibration Status

The `Calibration status` entry indicates the current state:

- `open`
- `adapter selected`
- `saved`

## 10. Transparency

The `Transparency` menu entry changes the visibility of the display panel.

Important:

- Transparency only affects the `blue background area` of the panel.
- The ring, arrows and numeric values remain visible and keep their glow.
- The value is stored immediately and loaded again after restart.

## 11. Settings and Storage

Program settings are stored in `TrafficView.settings.ini`, usually next to the EXE in the `dist` folder.

Stored values include:

- selected adapter
- adapter name
- calibration values
- first-start status
- language-selection status
- transparency value
- selected language

Typical entries:

- `AdapterId`
- `AdapterName`
- `CalibrationPeakBytesPerSecond`
- `CalibrationDownloadPeakBytesPerSecond`
- `CalibrationUploadPeakBytesPerSecond`
- `InitialCalibrationPromptHandled`
- `InitialLanguagePromptHandled`
- `TransparencyPercent`
- `LanguageCode`

## 12. Display and Technical Notes

`TrafficView` is designed for clean rendering under Windows 11.

Important technical characteristics:

- DPI support for higher scaling levels
- custom program icon
- layered custom-rendered background window
- reduced animation cost and cached drawing surfaces to lower CPU and graphics load

## 13. Usage Notes

- For a meaningful ring visualization, calibration should be completed after the first start.
- If the network adapter in use changes, calibration should be repeated.
- At very low activity levels, the display is intentionally more sensitive so small traffic movements remain visible.

## 14. Troubleshooting

### The program does not show meaningful ring values

Cause:
No valid calibration data has been saved yet.

Solution:
Open calibration from the menu, complete the measurement and save the result.

### The wrong adapter is being measured

Cause:
An unsuitable or inactive adapter was selected.

Solution:
Open calibration, choose the correct adapter, use `Save adapter`, then measure again and confirm with `Save`.

### The transparency effect feels unusual

Note:
Transparency only changes the blue background. The ring and the values intentionally remain opaque.

## 15. Important Files for Further Development

For further development, these files are the most important:

- `src\\Program.cs`
- `build.ps1`
- `TrafficView.manifest`
- `TrafficView.ico`
- `TrafficView.exe.config`
- `TrafficView.languages.ini`

## 16. Summary

`TrafficView` is a compact tray application for showing download and upload traffic in both numeric and graphical form. The display is movable, calibratable, DPI-aware, multilingual, and supports adjustable background transparency. Settings are stored persistently in an INI file.
