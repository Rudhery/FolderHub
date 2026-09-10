# Contributing

## Getting it running

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). Everything
else the app uses ships with Windows.

```powershell
git clone https://github.com/Rudhery/FolderHub.git
cd FolderHub

dotnet test app/FolderHub.sln     # 96 tests, about a second
cd app
dotnet run --project src/FolderHub
```

To build what people actually download:

```powershell
.\publish.ps1                 # dist\FolderHub.exe, ~700 KB
.\publish.ps1 -SelfContained  # ~124 MB, no runtime needed
.\publish.ps1 -Installer      # + the setup (needs Inno Setup 6)
```

## The one rule

**The folder is the configuration.** No app registry, no database, no list of
programs in the source. It is why ordering is stored by renaming files with a
`01 - ` prefix instead of in a settings file: the order you set by dragging is
the order you see in Explorer, and either one can drive the other.

A change that moves state out of the folder needs a reason. Most do not have one.

## Where things go

```
app/src/FolderHub/
  Services/      logic that does not know a window exists — this is the testable part
  Models/        AppItem, HubTab
  Interop/       every P/Invoke, in one file
  MainWindow.*   the window, split by concern (tabs, reorder, drag & drop, tray)
  Views/         the windows themselves, over the shared HubWindow shell
  Controls/      the design layer: real controls with a default style
  Themes/        every colour, measurement and template — tokens, no literals
app/tests/       xUnit, over the Services
```

A new screen should be able to say `<hub:HubCard Variant="Raised" />` and get
the design for free. If you need a colour or a size, add a key to `HubTheme.xaml`
and use it — a literal in a template is how two screens start drifting apart.

If you find yourself adding logic to `MainWindow`, check whether it can be a
pure function in `Services` instead. That is where the tests can reach it, and
it is how the grid maths, the search and the path shortening ended up there.

## Style

Nothing exotic — the file you are editing is the guide. Some habits worth
matching:

- comments explain **why**, not what. The interesting ones in this codebase are
  the WPF traps: `ShowInTaskbar` recreating the HWND, the scrollbar stealing a
  column, cross-fading card states instead of stacking translucent colours.
- failures are swallowed on purpose so a broken shortcut cannot take the hub
  down — but they go through `Log.Warn` first. A silent `catch { }` is a bug.
- comments and UI strings are in Portuguese; the README and this file are in
  English. Match what is around you.

## Tests

Anything that can be a pure function should have a test. Anything that touches
the shell, the window or the registry generally cannot be tested and does not
need a token test written for it.

The renaming in `ManualOrder` moves real files around, so it is the one place
worth being paranoid — including the case where one file's target name is
another file's current name.

## Verifying UI behaviour

`WindowSmokeTests` loads the resource dictionaries and window templates for real,
in both themes, because a `StaticResource` pointing at a key that no longer exists
compiles perfectly and throws when the window opens. That is as far as the tests
go into the UI: they prove a window can be built, not that it behaves.

Behaviour that only exists in a real window — the global hotkey, drag-to-reorder,
tab switching — has been verified by driving the app with PowerShell and Win32
calls, then reading `folderhub.log` and comparing screenshots. If you change one
of those, say in the PR what you actually ran.
