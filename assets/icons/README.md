# Webpoint – App Icon

## windows/
- `Webpoint.ico`: the main app icon. Contains 16, 24, 32, 48, 64, 128 and 256 px.
  Each size has its own level of detail so the icon never looks muddy:
  - 256 px and up: full detail (window dots, profile avatars, home/globe/work symbols, profile badge)
  - 96–128 px: window dots, avatars, link and badge (tiny symbols removed)
  - 48–64 px: window dots and a bold link only
  - 24–32 px: taller hand-drawn cards with a clear chain link
  - 16 px: silhouette only (three coloured card tops and the holder)
  The 16 px and 24–32 px sizes are simplified drawings so they stay sharp in the taskbar and the "Open with" list.
  - WinForms/WPF: set it as the project's Application Icon (`<ApplicationIcon>Webpoint.ico</ApplicationIcon>`).
  - Electron: `icon: 'Webpoint.ico'` in the BrowserWindow / electron-builder `win.icon`.
  - Win32/C++: reference it in the `.rc` file as `IDI_APPICON ICON "Webpoint.ico"`.

## msix-assets/  (only needed for MSIX packaging or the Microsoft Store)
Copy into your package's `Assets` folder; names follow the Windows conventions:
- `Square44x44Logo.*`: taskbar, Start menu list, "Open with" (targetsize and altform-unplated variants included)
- `Square150x150Logo.*`: Start tile
- `Wide310x150Logo.*`: wide tile
- `StoreLogo.*`: Store listing / installer

## png/
Transparent PNGs from 16 to 1024 px, for websites, installers, docs and so on.

## svg/
Editable sources, one per detail level:
- `icon_256plus_full.svg`
- `icon_96-128.svg`
- `icon_48-64.svg`
- `icon_24-32.svg`
- `icon_16.svg`

## Colours
Chrome-style #EA4335 / #FBBC04 / #34A853 · Edge-style #0B57D0 → #1AA7EC → #3CCB8C ·
Firefox-style #FFC93C → #FF9A1F → #FF6B1A · Holder (silver) #F1F3F8 → #C3C9D5 → #8F98AA

Note: the card colours evoke well-known browsers but contain no browser logos.
Get a quick trademark review before using the icon in Store listings or marketing.
