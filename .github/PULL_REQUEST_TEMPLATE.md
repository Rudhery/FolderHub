## What this changes

<!-- One or two sentences. What is different after this is merged? -->

## Why

<!-- The problem it solves. Link the issue if there is one: Fixes #123 -->

## How it was verified

<!--
  Say what you actually ran, not what you intended to.
  - `dotnet test app/FolderHub.sln`
  - anything you exercised by hand, and on which Windows version
  - screenshots, if the window changed
-->

## Checklist

- [ ] `dotnet test app/FolderHub.sln` passes
- [ ] Behaviour that could not be unit tested was exercised by hand, and I said how above
- [ ] The README was updated, if this changes how FolderHub is used or configured
- [ ] Failures this code can hit are logged rather than swallowed silently

<!--
  Not required, but it helps: FolderHub tries to keep one rule — the folder is
  the configuration. If this adds state that lives outside the folder, say why
  it has to.
-->
