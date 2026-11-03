# 🖱️ Mouse Travel

A tiny Windows tray app that turns your mouse movement into meters and kilometers, shown on a rolling car odometer.

<p>
  <img src="docs/popup-dark.png" width="260" alt="Today's trip popup, dark mode">
  <img src="docs/popup-light.png" width="260" alt="Today's trip popup, light mode">
</p>

- **Lives in the system tray:** a few MB of RAM, a single small `.exe`, no installer, no extra runtime
- **A rolling odometer** for today's trip, plus a little road to your next milestone
- **Milestone pop-ups** at 500 m and 1 km, then every km after that, plus all-time ones like "half marathon"
- **A weekly share card:** a 1080×1080 image saved to `Pictures\Mouse Travel` and copied to your clipboard
- **Automatic screen detection** of each monitor's real size (adjust anytime under **Screens**)
- Follows Windows **light/dark mode** and the **Animation effects** setting

<img src="docs/card.png" width="420" alt="Weekly share card">

## Download
Grab `MouseTravel.exe` from the [latest release](../../releases/latest) and double-click it. That's it.

Windows SmartScreen may warn you the first time because the app isn't code-signed. Click **More info → Run anyway**.

## Use it
- **Left-click** the mint mouse in the tray to see today, this week and all-time totals
- **Right-click** for share, screens, start with Windows, and quit

The numbers are deliberately rough: it measures how far the cursor travels across your screens, using each screen's physical size.

<img src="docs/setup-dark.png" width="360" alt="Screens window">

## Your data
Stored only on your PC in `%APPDATA%\MouseTravel` (`days.csv` holds one line per day). Nothing is sent anywhere. To back up your history, copy that folder.

## Build from source
Requires Windows 10 or 11, nothing else. This uses the C# compiler that ships with Windows (.NET Framework 4.x):

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

The whole app is in [`src/MouseTravel.cs`](src/MouseTravel.cs).
