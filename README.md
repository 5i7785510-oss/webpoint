# Webpoint

A lightweight browser profile dispatcher for Windows 11. When you click a web link, Webpoint presents a Fluent dark-themed picker so you can choose which browser profile to open the link with.

---

## Highlights

- **Instant Dispatch**: Uses a local background Named Pipe worker to display the profile picker in under 20 milliseconds.
- **Fluent Dark UI**: Matches Windows 11 design language with smooth corners, subtle drop shadows, and dark styling.
- **Multi-Monitor Friendly**: Entire window can be dragged and repositioned across any display.
- **Configurable Profiles**: Supports Microsoft Edge profiles (e.g. Work, Personal), Google Chrome profiles, and private browsing modes (Edge InPrivate, Chrome Incognito).
- **Zero External Dependencies**: Built with native C# and WPF using the .NET Framework compiler already included in Windows.

---

## Project Structure

```text
├── assets/             # Application icons (multi-tier ICO, PNG, SVG) and preview assets
├── scripts/
│   ├── Install.ps1     # Registers Webpoint as a default browser handler in Windows
│   └── Uninstall.ps1   # Unregisters Webpoint and cleans up file associations
├── src/
│   ├── App.cs          # Full source code (Named Pipe IPC, WPF UI, profile launcher)
│   └── settings.json   # Default user preferences (layout mode, private links, icons)
├── build.ps1           # PowerShell compilation script using built-in csc.exe
└── .gitignore
```

---

## Building from Source

To compile `Webpoint.exe`:

```powershell
.\build.ps1
```

The script compiles `src\App.cs` into `bin\Webpoint.exe` and bundles the application icon.

---

## Installation

Run the included installer to register Webpoint with Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install.ps1
```

To set Webpoint as your system default link handler:
1. Open Windows **Settings** > **Apps** > **Default apps**.
2. Select **Webpoint** and set it as the default for `HTTP` and `HTTPS` protocols.

---

## Uninstallation

To remove Webpoint and restore your browser preferences:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Uninstall.ps1
```
