<img src="docs/logo.png" width="96" alt="Mouse Travel logo">

# Mouse Travel

A tiny Windows tray app that turns your mouse movement into meters and kilometers, shown on a rolling car odometer.

<p>
  <a href="https://github.com/Mridul957/mouse-travel/releases/latest/download/MouseTravel.exe"><img src="https://img.shields.io/badge/Download%20for%20Windows-MouseTravel.exe-FFC93C?style=for-the-badge&logo=windows&logoColor=161616&labelColor=FFC93C" alt="Download for Windows" height="44"></a>
</p>

<p>
  <a href="https://github.com/Mridul957/mouse-travel/releases/latest"><img src="https://img.shields.io/github/v/release/Mridul957/mouse-travel?label=latest&color=161616" alt="Latest version"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-161616" alt="Windows 10 and 11">
  <img src="https://img.shields.io/badge/size-under%20100%20KB-161616" alt="Under 100 KB">
</p>

**Download → double-click → Install.** That's it. ([what happens when you install](#download-and-install))

<p>
  <img src="docs/popup-dark.png" width="260" alt="Today's trip popup, dark mode">
  <img src="docs/popup-light.png" width="260" alt="Today's trip popup, light mode">
</p>

- **Lives in the system tray:** a few MB of RAM, a single small `.exe`, no installer, no extra runtime
- **A rolling odometer** for today's trip and a little road to your next milestone
- **A Screen Time-style week:** Mon–Sun bars, your daily average with a dashed avg line, and how you compare to last week, plus your **lifetime** distance
- **Sync across PCs:** point each PC at the same Google Drive or OneDrive folder and Today, Week and Lifetime add up all your computers (each PC writes only its own file, so there are no sync conflicts)
- **Milestone pop-ups** at 500 m and 1 km, then every km after that, plus lifetime ones like "half marathon". They're Mouse Travel's own cards, so they work even with Windows notifications off, never steal focus, and wait while you're in a full-screen app.
- **A weekly share card:** a 1080×1080 image saved to `Pictures\Mouse Travel` and copied to your clipboard
- **Automatic screen detection** of each monitor's real size (adjust anytime under **Screens**)
- Follows Windows **light/dark mode** and the **Animation effects** setting

<img src="docs/toast-dark.png" width="380" alt="Milestone pop-up">

<img src="docs/card.png" width="420" alt="Weekly share card">

## Download and install
1. Download **[MouseTravel.exe](https://github.com/Mridul957/mouse-travel/releases/latest/download/MouseTravel.exe)** (the latest release).
2. Double-click it and press **Install**. No admin password is needed.

It installs just for you into `%LOCALAPPDATA%\Programs\Mouse Travel`, adds itself to the **Start menu**, starts with Windows (optional), and appears in **Settings → Apps** with an **Uninstall** button. You can delete the downloaded file afterwards.

- **Updating:** download a newer `MouseTravel.exe` and double-click it. It updates in place and keeps your history.
- **Uninstalling:** Settings → Apps → Mouse Travel → Uninstall. Your history is kept unless you choose to delete it.
- **Portable use:** put a file called `portable.txt` next to the exe and it runs from where it is, without installing.

Windows SmartScreen may warn you the first time because the app isn't code-signed. Click **More info → Run anyway**.

## Use it
- **Left-click** the yellow lane-driver logo in the tray to see today, the week chart and your lifetime
- **Hover** over it for Today, This week and Lifetime at a glance
- **Right-click** for share, screens, start with Windows, sync across PCs, showing this week's km on the icon, and quit

A full illustrated guide is in [How Mouse Travel works.pdf](How%20Mouse%20Travel%20works.pdf).

The numbers are deliberately rough: it measures how far the cursor travels across your screens, using each screen's physical size.

<img src="docs/setup-dark.png" width="360" alt="Screens window">

## Your data
Stored only on your PC. Nothing is sent anywhere.

- **`Mouse Travel history.txt`**, next to `MouseTravel.exe`: a plain notepad file with every day, this week and your lifetime total. It's refreshed every minute and on every start, and Mouse Travel reads it back as a backup.
- **`%APPDATA%\MouseTravel`**: the app's own data (`days.csv`, one line per day), a `backup` folder with a copy for each of the last 7 days, and a small `log.txt`.

Saving never lowers a day's distance: before writing, Mouse Travel merges what's already on disk and in the history file. To move or back up your history, copy the history file (or the AppData folder).

## Build from source
Requires Windows 10 or 11, nothing else. This uses the C# compiler that ships with Windows (.NET Framework 4.x):

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

The whole app is in [`src/MouseTravel.cs`](src/MouseTravel.cs).
