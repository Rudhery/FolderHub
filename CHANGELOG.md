# Changelog

Notable changes, newest first. Dates are the day the version was cut.

## [Unreleased]

Nothing yet.

## [1.0.0] — 2026-09-09

First release.

### The hub

- Reads `.lnk`, `.url`, `.exe`, `.bat`, `.cmd`, `.ps1`, `.appref-ms` and `.msc`
  from a folder and lays them out as cards.
- Watches the folder, so adding or removing a shortcut updates the window live.
- Icons at 96px through `IShellItemImageFactory`, resolving `.lnk` targets so the
  card shows the program's own icon rather than one with a shortcut arrow.
- Windows 11 acrylic, rounded corners and dark mode through DWM.
- The grid picks its column count to stay close to square while keeping the last
  row full, and the window sizes itself to match.
- Type to filter — accent-insensitive — arrows to move, Enter to launch.

### Tabs

- Several folders in one hub, one tab each, configured under `tabs`.
- Loading is lazy where it costs: a cheap count on startup sizes the window,
  while the listing and icon extraction wait until a tab is opened.
- Drop a folder on the window to add a tab, right-click one to remove it.
- `Ctrl+Tab`, `Ctrl+Shift+Tab` and `Ctrl+1`…`Ctrl+9`.

### Ordering

- Manual, name ascending or descending, and most recent.
- Dragging a card renames the files with a `01 - ` prefix, so the order lives in
  the folder and stays readable from Explorer.

### Resident mode

- Tray icon and a configurable global hotkey, `Ctrl+Alt+Space` by default.
- Single instance: a second launch asks the running one to show itself.
- Optional start with Windows.
- Hiding trims the working set — 29 MB in the tray, 0 ms of CPU while idle, and
  61–73 ms from keypress to window.

### Packaging

- Per-user Inno Setup installer, no elevation, with the Start Menu shortcut you
  pin to the taskbar.
- Single-file build, framework-dependent or self-contained.

### Customising

- `themeFile` points at a `ResourceDictionary` that overrides colours and card
  size without recompiling.
- Failures are written to `%APPDATA%\FolderHub\folderhub.log`.

[Unreleased]: https://github.com/Rudhery/FolderHub/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/Rudhery/FolderHub/releases/tag/v1.0.0
