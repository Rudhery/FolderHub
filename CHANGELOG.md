# Changelog

Notable changes, newest first. Dates are the day the version was cut.

## [Unreleased]

### The main window, restyled

- Search moved out of the header into a full-width row of its own, with a drawn
  `/` key that actually focuses it (`Oem2` on US layouts, `AbntC1` on ABNT2).
- Tabs became capsules carrying a small tile and the shortcut count, with a
  `Tab` hint on the right. `Tab` on its own now switches hubs.
- Shorter cards (118px) with a 40px tile; the selected ring went to 3px.
- Header buttons at 28px, plus a settings button — it opens the config folder
  until the settings screen exists.
- The footer says which hub you are in.
- The grid now ends on a whole row. Cutting mid-row raised a scrollbar over a
  few leftover pixels and left half a row against the footer; the scrollbar also
  floats in the window padding instead of touching the last column of cards.

### A settings screen

Reachable from the gear in the header, and from the tray menu — which is the way
in when the hub is resident and hidden.

- Five sections: Hubs, General, Shortcuts, Appearance, About.
- No OK button. Every change is written and applied at once: it edits the same
  `HubConfig` the app runs on and saves, and saving already tells the hub to
  reconcile. Adding a hub grows the tab strip; raising the column limit resizes
  the window while you watch.
- Hubs can be added, renamed, reordered and removed, up to eight — past that the
  tab strip stops fitting. Clearing a name goes back to the folder's own, so
  renaming it in Explorer keeps showing through.
- The hotkey is recorded rather than typed: click and press the combination. It
  refuses one without a modifier because Windows refuses it too, and it says when
  a hotkey is set but not working — the hub is not resident so nothing is
  listening, or another program already owns the combination.
- `HotKeyText` now writes what `GlobalHotKey` reads, and a test walks the whole
  keyboard proving the round trip. The two used to be separate copies, and a
  divergence between them produced no error at all — the hotkey just stopped
  working.
- New controls behind it: `HubToggle`, `HubOption`, `HubStepper`, `HubHotKeyBox`,
  plus styles for text fields and selects.

### A design layer over WPF

New `Controls/` and a split theme, so a new screen writes what it wants rather
than how it is drawn.

- `HubTheme.xaml` holds every colour, radius, font and measurement; keys are the
  contract an external theme overrides. `HubControls.xaml` holds the templates
  and contains no literal values.
- `HubCard`, `HubTabItem`, `HubIcon`, `HubKey`, `HubIconButton` — real controls
  with a default style, so `<hub:HubCard Variant="Raised" Spacing="Medium" />`
  is all a screen needs. The card carries both an icon-and-label layout and a
  free-content one.
- `HubSpacing.Gap` gives panels the CSS `gap` that WPF lacks, on a 4px scale.
- `HubMotion` is the single source of timing, read from XAML by `x:Static`.
- Card and tab states moved to `VisualStateManager`: 21 hand-written animations
  became 9, and the third storyboard that only existed to animate back to normal
  is gone.
- `HubGlyph` names the icons, replacing raw codepoints and a font family
  repeated at every use.

### Fixed

- Tab capsules rendered as stretched ovals. `border-radius: 999px` works in CSS
  because the browser clamps the radius to half the side; WPF does not clamp, so
  the radius is now half the capsule height.
- The tab tile reused the card's opaque grey, which at 14px read as a flat light
  chip beside the name. It is now a low translucent white that recedes, and the
  5px mark inside carries the state.
- `Themes/Dark.xaml` shipped a duplicated token block. WPF resolves the later
  definition, so the stale values were the ones in effect.

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
