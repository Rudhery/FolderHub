<div align="center">

<img src="docs/icon.png" width="96" alt="FolderHub">

# FolderHub

**A folder is the configuration.**
A small Windows 11 launcher that turns any folder of shortcuts into a visual hub.

[![build](https://github.com/Rudhery/FolderHub/actions/workflows/build.yml/badge.svg)](https://github.com/Rudhery/FolderHub/actions/workflows/build.yml)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/)

[Português](README.pt-BR.md)

<img src="docs/hub.png" width="820" alt="FolderHub window">

</div>

---

## The idea

Point FolderHub at a folder. It reads the name and icon of every shortcut inside,
lays them out as cards, and launches whatever you click.

There is no app registry, no database, no list of programs in the source. **Drop a
shortcut in the folder and it shows up. Delete it and it's gone.** The folder you
already curate in Explorer *is* the configuration, and the app watches it live.

That constraint drives the whole design — including how ordering works: dragging a
card renames the files with a `01 - `, `02 - ` prefix, so the order you set in the
app is the order you see in Explorer, and vice versa.

## Features

- **Global hotkey** — `Ctrl+Alt+Space` opens the hub from anywhere, resident in the tray.
- **Reads any shortcut** — `.lnk`, `.url`, `.exe`, `.bat`, `.cmd`, `.ps1`, `.appref-ms`, `.msc`.
- **Real Windows 11 acrylic**, rounded corners and dark mode via DWM.
- **Sharp icons** — 256px through the Shell API instead of the blurry 32px most launchers settle for.
- **Adaptive grid** — the column count and window size follow the number of shortcuts.
- **Type to filter**, arrow keys to move, `Enter` to launch.
- **Drag to reorder**, saved into the folder itself as filename prefixes.
- **Drag and drop** — drop a folder to switch hubs, drop programs to add them.
- **Tabs** — several folders in one hub, each loaded only when you open it.
- **Many hubs** — one shortcut per folder, each pinnable to the taskbar.
- **Live** — add or remove a shortcut while the hub is open and it updates itself.

## Install

Grab `FolderHub-Setup-x.y.z.exe` from [Releases](https://github.com/Rudhery/FolderHub/releases)
and run it. It installs per-user, so **no admin prompt**, and offers to start with
Windows so the hotkey is always there.

Or build it yourself:

```powershell
git clone https://github.com/Rudhery/FolderHub.git
cd FolderHub
.\publish.ps1                 # dist\FolderHub.exe, ~700 KB, needs the .NET 10 Desktop Runtime
.\publish.ps1 -SelfContained  # ~124 MB, runs on any Windows 11 with nothing installed
.\publish.ps1 -Installer      # self-contained build + the setup (needs Inno Setup 6)
```

First run asks which folder to use and remembers it.

## Using it

### Pin it to the taskbar

The installer puts a shortcut in the Start Menu — right-click it and choose
**Pin to taskbar**. Pinning a bare `.exe` is unreliable on Windows 11, and a pinned
`.exe` always starts with no arguments, which is exactly what you don't want when
you keep more than one hub.

### Tabs

A hub is not one folder. List them in the config and each becomes a tab:

```jsonc
"tabs": [
  { "path": "D:\\Work",  "name": "Work" },
  { "path": "D:\\Games", "name": "Games" },
  { "path": "D:\\Tools" }               // no name = the folder's own name
]
```

Drop a folder on the window to add one, right-click a tab to remove it. With a
single tab the strip stays hidden, so a one-folder hub looks exactly as before.

**Loading is lazy where it actually costs.** On startup each tab does a cheap
count — just reading file extensions, no file is opened — and that is what sizes
the window to the largest tab, so it never resizes when you switch. The real
listing, and above all the icon extraction, happens the first time you open a
tab. After that the icons live in that tab's own cache, so coming back is instant.

<img src="docs/tabs.png" width="700" alt="tabs">

### More than one hub

Pass a folder and make one shortcut per hub:

```powershell
FolderHub.exe "D:\Games"
FolderHub.exe "D:\Work\Tools"

# or let the helper build them for you
.\tools\create-shortcut.ps1 -Folder "D:\Games" -Name "Games Hub" -Desktop
```

The argument never overwrites the default folder saved in the config.

### Resident mode

Resident mode is what makes the hotkey exist at all: it keeps FolderHub in the tray
listening, instead of quitting after a launch.

| Flag | Effect |
|---|---|
| `--resident` | resident and visible - what the installer runs at the end |
| `--background` | resident and hidden - what the logon entry uses |

Either one is remembered, so from then on opening FolderHub any way you like keeps
the hotkey alive. You can also toggle it by right-clicking the hub's header.

While resident there is only ever one process — clicking a pinned shortcut just
asks the running one to show itself, in the folder that shortcut points at.

### Ordering

The sort button in the header offers **Manual**, **Name (A→Z)**, **Name (Z→A)** and
**Most recent**, plus *Save this order to the folder*.

In manual mode you drag cards around, and FolderHub renames the files:

```
01 - Steam.lnk        ->  Steam
02 - Discord.lnk      ->  Discord
03 - OBS.lnk          ->  OBS
```

The prefix never shows on the card. Ordering by hand in Explorer still works, and
so do `01 -`, `01.`, `01_` and `01)`.

### Keyboard

| Key | Action |
|---|---|
| `Ctrl+Alt+Space` | show / hide the hub (resident mode) |
| type | filter |
| `←` `↑` `→` `↓` | move between cards |
| `Enter` | launch the selected card |
| `Esc` | clear the search; if already empty, dismiss |
| `F5` | reload and re-read the icons |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | next / previous tab |
| `Ctrl+1` … `Ctrl+9` | jump straight to a tab |
| `Ctrl+O` | change the active tab's folder |
| right-click a card | run as administrator / show in folder |
| right-click the header | resident mode, start with Windows, open the config |
| double-click the header | open the folder in Explorer |

## Configuration

`%APPDATA%\FolderHub\config.json`

```jsonc
{
  "tabs": [                    // one folder per tab
    { "path": "D:\\Games", "name": "Games" },
    { "path": "D:\\Work" }
  ],
  "background": true,          // stay in the tray listening for the hotkey
  "hotKey": "Ctrl+Alt+Space",  // e.g. "Alt+Q", "Win+Shift+H"
  "closeAfterLaunch": true,    // dismiss after opening an app
  "closeOnBlur": false,        // dismiss when the window loses focus
  "sort": "Manual",            // Manual | NameAsc | NameDesc | Recent
  "maxColumns": 7
}
```

An older `"folderPath"` is migrated to a single tab on first run.
Whatever the app swallows quietly is recorded in
`%APPDATA%\FolderHub\folderhub.log`.

## Design

The interface follows a monochrome, cool-toned system with **no accent colour** —
every bit of hierarchy comes from white at different opacities over the acrylic.

| | |
|---|---|
| surface | `rgba(26,28,32,0.86)` over acrylic, border `rgba(255,255,255,0.07)` |
| card | `rgba(255,255,255,0.028)` · hover `0.065` · selected `0.075` + a 2px ring |
| icon tile | `#22252A` → `#282C32` → `#2C3138`, 44px, radius 12 |
| text | `#EEF1F4` at 100 / 88 / 58 / 42% |
| transition | 150 ms on background and border only — no lift, no glow |
| type | Manrope 400/500/600 · JetBrains Mono for metadata |

Both fonts are embedded in the executable (SIL OFL, licences in `src/FolderHub/Assets/Fonts`),
so the app never depends on what's installed on the machine.

The window corners are the ~8px Windows rounds them to, not a custom radius: a larger
one would mean clipping the window myself, and the system acrylic would be lost with it.

The icon — three stacked bars, the shortcuts inside the hub — is generated art:

```powershell
python tools/make-icon.py     # writes Assets/folderhub.ico, 16px through 256px
```

## How it works

WPF on .NET 10, no external dependencies.

```
src/FolderHub/
  App.xaml.cs               startup, arguments, single instance
  MainWindow.xaml(.cs)      layout, cards, adaptive grid, search, drag & drop
  MainWindow.Tabs.cs        tabs and their lazy loading
  MainWindow.Background.cs  tray, global hotkey, show/hide
  Themes/Dark.xaml          palette and styles
  Services/
    FolderScanner.cs        reads the folder, applies the sort mode
    IconLoader.cs           high-resolution icon extraction
    ManualOrder.cs          persists the order by renaming files
    Launcher.cs             runs the shortcut
    ShortcutWriter.cs       creates .lnk when you drop something in
    GlobalHotKey.cs         RegisterHotKey and the "Ctrl+Alt+Space" parser
    TrayIcon.cs             Shell_NotifyIcon
    SingleInstance.cs       mutex + message to the running instance
    WindowEffects.cs        acrylic, rounded corners, dark mode
    Log.cs                  file log, so swallowed failures leave a trace
  Interop/Native.cs         DWM, Shell, GDI, user32
tests/FolderHub.Tests/      xUnit
tools/                      icon generator, shortcut helper
installer/FolderHub.iss     Inno Setup
```

A few decisions worth calling out:

- **Icons at 256px.** `IShellItemImageFactory` instead of the 32px `ExtractAssociatedIcon`.
  For `.lnk` the target is resolved first, so the card shows the program's clean icon
  without the shortcut arrow baked on top; for `.url` the `IconFile` entry is read directly.
- **Icons load on a dedicated STA thread**, because Shell COM is apartment-threaded.
  The window opens immediately and icons arrive as they're ready.
- **Card states are stacked layers that cross-fade.** Each layer carries the exact colour
  from the design; compositing one translucent colour over another would add the alphas
  and land lighter than specified.
- **The scrollbar is an overlay.** WPF's default one takes 10px of width when it appears —
  enough for the grid to lose a column, reflow, and then need to scroll even more.
- **Window height comes from the content** (`SizeToContent`) with the grid at an explicit
  height. Estimating the chrome in pixels depends on font metrics, and being 2px off was
  enough to trigger the problem above.
- **Reordering renames in two passes**, because one file's target name can be another's
  current name. It rolls back if anything fails midway.
- **`ShowInTaskbar` is decided before the handle exists** — changing it later makes WPF
  recreate the HWND, which would silently orphan the global hotkey and the message hook.

## Tests

```powershell
dotnet test
```

40 tests over the pure logic: what the scanner picks up, the four sort modes (including
natural ordering, so `Item2` comes before `Item10`), the manual-order renaming with its
collision case, the hotkey parser, and the config migration from single folder to tabs.

## Why not a Windows service?

Because a service can't do this. Services run in session 0, isolated from the desktop
since Windows Vista — no UI, no access to the user's session, and no way to register a
hotkey for it. Everything that "runs in the background" with a tray icon and a shortcut
key is a normal user-session process started at logon, which is what resident mode is.

## License

MIT — see [LICENSE](LICENSE).
