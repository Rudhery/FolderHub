# Security

## Reporting

Open a [security advisory](https://github.com/Rudhery/FolderHub/security/advisories/new),
or email rudhery.hotz@outlook.com. Please do not open a public issue for
something exploitable.

## What FolderHub touches

Worth knowing when judging a report:

- **It launches whatever the folder points at.** That is the whole feature. A
  shortcut in your hub folder runs with your privileges, exactly as it would if
  you double-clicked it in Explorer.
- **It renames files in the hub folder** when you reorder cards, and copies or
  creates `.lnk` files there when you drop something onto the window. Never
  outside that folder.
- **It writes to `%APPDATA%\FolderHub`** — config, log, and the handle of the
  running instance.
- **It writes one registry value**, `HKCU\...\CurrentVersion\Run`, and only when
  you turn on "start with Windows". Never anything machine-wide.
- **It installs per user**, into `%LOCALAPPDATA%`, with no elevation.
- **`themeFile` is loaded as XAML**, which can instantiate types. It is a path
  you put in your own config, so it carries the same trust as the config itself
  — but do not point it at a file you did not write.
- **It makes no network requests.** Nothing is sent anywhere, and there is no
  update check.

## Scope

The installer is not code-signed, so Windows SmartScreen will warn on first run.
That is expected, not a vulnerability.
