# Changelog

Notable changes, newest first. Dates are the day the version was cut.

## [Unreleased]

### Search-first layout

- **The search line is the top of the window.** Title and path rows are gone;
  the placeholder names the hub ("Buscar em Trabalho"), and typing anywhere
  lands in the search.
- **Hubs moved to a side rail**, each with a colour by position
  (`HubPalette`) and its initial. It replaces the tab strip and holds more hubs.
  The rail hides in single-folder mode and the settings gear moves to the
  search line.
- **Icons without a tile.** The grid is a plain `ListBox` with the `LaunchCell`
  template: the shell icon on its own, a soft fill on hover and the new
  `Accent`/`AccentSoft` tokens on the selected item — the only colour in an
  otherwise monochrome palette. Densities were resized for it (104×108 default).
- **"Mais usados"**: the four most launched shortcuts of the last 30 days, from
  `UsageStats` (`usage.json` next to the config). Hidden when the folder has
  four shortcuts or fewer.
- **Typing switches the grid for a result list** (`Results`), showing file name
  and kind. The grid stays `Hidden` underneath so the window keeps its height.
  `Tab` carries the query to the next hub.
- **Action bar** replaces the footer: the selected shortcut, *Abrir* and an
  actions menu (`Ctrl+K`) with run as administrator (`Ctrl+Enter`), reveal in
  folder (`Alt+Enter`) and copy path (`Ctrl+Shift+C`).

### Manual order no longer renames your shortcuts

- **Dragging a card saves the order in `order.json`**, next to the config, keyed by
  file name. It used to rename the files to `01 - Steam.lnk`; an app update then
  recreated `Steam.lnk` and the hub showed two Steams, one more on every update.
- Old prefixed folders are converted once: the prefix is dropped, the order kept, and
  a duplicate an update already left behind goes to the Recycle Bin.
- New shortcuts go to the end. *Save this order to the folder* is now *Pin this order*.

### First run, and what changed after an update

Two screens the app was missing at both ends of an install.

- **First run is guided, in three steps**: pick the folder, set the hotkey,
  see the hub. It was a bare Windows folder picker — it never said what the
  folder would become, and cancelling it closed the app without a word. The two
  settings that decide whether this works as a launcher (the global hotkey and
  starting with Windows) were reachable only from the settings screen, which is
  exactly where someone who just installed will not look.
- It writes the whole config at the end rather than step by step, so giving up
  halfway leaves nothing behind.
- **First run and a folder that went missing are different problems**, and both
  used to land in the same picker. A hub configured on a disconnected drive now
  still just asks for another folder — being welcomed again would say the
  configuration had been lost.
- **After an update, what changed** — four highlights, once, dismissed on the
  first click. The version comes from the assembly, so the screen cannot
  announce a version that is not running, and `seenVersion` in the config is
  what makes it appear exactly once. Someone who just installed does not see it:
  the guided install already spoke.
- The text comes from **`RELEASE_NOTES.md`**, one `## <version>` block with
  `### Features` and `### Fix` sections, embedded in the exe at build time. The
  same file is the GitHub release body — one text for the same person, whoever
  downloaded the installer. Embedded rather than fetched from the releases API
  because the screen only ever shows the notes of the version that is running,
  and that version is this build: the network would add nothing but a request
  the app promises not to make.
- A tag the file does not know about still renders, with its own name and a
  neutral icon, so inventing one breaks nothing.

### Added

- `HubSteps` — the progress dashes, one per step, the current one wider. The
  number of steps is the code's business, not the screen's.
- `HotKeyText.Parts` breaks a combination into the key names as they read on the
  keyboard — "Space" becomes "Espaço". One way only, and deliberately: what gets
  written to the config is still `TryFormat`'s output, because that is what
  `GlobalHotKey.TryParse` has to read back. A test pins both sides.
- `HubHotKeyBox` exposes those keys already knowing which is last, so the
  template can draw them one by one without a stray `+` at the end.
- The accent button has a disabled state — no fill, faint text. It is the only
  filled element on a screen, and a dimmed white rectangle is still what the eye
  goes to.
- `FOLDERHUB_CONFIG_DIR` moves the config directory. It exists for the tests.

### Fixed

- **`HubSpacing.Gap` inside an `ItemsPanelTemplate` threw at runtime** —
  "VisualTree of ItemsPanelTemplate must be a single element", in a message box,
  on opening the app. The gap now goes on the item template, where it belongs,
  and `HubSpacing` documents the trap.
- The smoke tests could not have caught that: `ApplyTemplate`, and even
  `Measure`/`Arrange` on a window with no handle, do not expand the panel of an
  `ItemsControl`. They now call `Show()`.
- Because those windows are real, dismissing one writes configuration — so a
  test run rewrote the developer's own `%APPDATA%` config. The what's-new screen
  now records the version on dismissal rather than on `Closed` (which also fires
  for a programmatic close), and the test assembly redirects the config
  directory.
- A button revealed after the window was built sat flush against its neighbour:
  `HubSpacing` computes the gap from the visible children and does not
  recompute, which is what `HubSpacing.Refresh` is for.

## [1.1.0] — 2026-09-10

### The main window, restyled

- Search moved out of the header into a full-width row of its own, with a drawn
  `/` key that actually focuses it (`Oem2` on US layouts, `AbntC1` on ABNT2).
- Tabs became capsules carrying a small tile and the shortcut count, with a
  `Tab` hint on the right. `Tab` on its own now switches hubs.
- Shorter cards (118px) with a 40px tile; the selected ring went to 3px.
- Header buttons at 28px, plus a settings button.
- The footer says which hub you are in.
- The grid now ends on a whole row. Cutting mid-row raised a scrollbar over a
  few leftover pixels and left half a row against the footer; the scrollbar also
  floats in the window padding instead of touching the last column of cards.

### A settings screen

Reachable from the gear in the header, and from the tray menu — which is the way
in when the hub is resident and hidden.

- Five sections: Hubs, General, Shortcuts, Appearance, About.
- Nothing waits for an OK; the button in the corner only closes the window. Every
  change is written and applied at once: it edits the same `HubConfig` the app
  runs on and saves, and saving already tells the hub to reconcile. Adding a hub
  grows the tab strip; raising the column limit resizes the window while you
  watch.
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

### A light theme, and appearance that applies live

- `Themes/Light.xaml` redefines the theme keys and nothing else, loaded over the
  dark theme when `themeMode` is `Light`. A `themeFile` of your own still wins,
  because it is loaded last. The light theme inverts the ink rather than bringing
  a palette: near-white surface, the same greys, `#14161A` at matching opacities.
- The theme is picked up at startup, not swapped under a live window: a running
  hub has already resolved brushes into visuals that a dictionary swap does not
  reach. The screen says so, and offers to restart the app for you — it comes
  back on the Appearance section it left, through `--foreground --appearance`.
- A system theme that follows Windows is shown disabled rather than hidden.
- The DWM backdrop follows the theme. Leaving dark mode on under the light theme
  left the glass white with a grey halo around it.
- **Three card densities** — compact, default, large. The numbers live in
  `CardDensity` rather than the theme, because the code that sizes the window
  needs them before anything is drawn. The label keeps two lines in all three, so
  a long name cannot change the card's height with the density.
- **Transparency as a slider**, 0 to 100. Windows acrylic exposes no level, so
  this is the opacity of the surface the app paints over it.
- **Reduce motion**, which drops the entry and transition animations.
- Density and transparency, unlike the theme, do reach what is already on screen:
  `LiveTheme` rewrites their keys in the live resource dictionary — and the
  elements that read them use `DynamicResource`. The theme's own values are
  remembered on the first call, so returning to Default restores what a theme
  file asked for rather than the number built into the app.

### The settings screen, second pass

- `HubGroup` — a titled block of rows with one line of explanation. The design
  repeats that structure eight times; written by hand it was eight chances for
  the spacing to drift.
- The window is genuinely round now. It asks for a 22px corner, and with the DWM
  rounding at 8 a dark wedge was left outside the arc. `HubWindow` takes an
  `acrylic` flag: with it off the window is really transparent and the content's
  own border defines the shape. Its surface is 92% opaque, so there was almost no
  acrylic showing through anyway — and the drop shadow the design asks for comes
  back with it.
- `HubKey` carries its own radius: the same key is small in the hub's hints and
  larger in settings, where it is the main element of the row.
- The tray menu and the header gear can open straight into Appearance.
- Language and automatic updates are shown disabled rather than hidden: the
  interface is Portuguese-only for now, and there is no update channel yet.

### Behaviour worth configuring

- **One folder or several** — a working mode, not a removal: the other folders
  stay in the config, they just stop being shown.
- **Reopen on the hub you were in**, remembered as you switch.
- **Bare `Tab` switches hubs**, and can be turned off so only `Ctrl+Tab` does.
- **The folder path and the shortcut count** can each be hidden.

### Tests

- Smoke tests over what the compiler does not check: the resource dictionaries
  and the window templates loading, in both themes. A `StaticResource` pointing
  at a key that no longer exists compiles perfectly and throws when the window
  opens. 66 tests to 96.

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

[Unreleased]: https://github.com/Rudhery/FolderHub/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/Rudhery/FolderHub/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/Rudhery/FolderHub/releases/tag/v1.0.0
