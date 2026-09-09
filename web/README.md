# web

The FolderHub landing page. **Not built yet** — this folder is the placeholder
for it.

## What it is for

A single page that does what the README cannot: show the thing moving. Someone
who lands here should understand FolderHub in ten seconds and leave with the
installer.

Rough shape:

- the pitch in one line — *a folder is the configuration*
- the hub in motion: the hotkey opening it, typing to filter, dragging a card
  and the file being renamed in Explorer behind it
- the three ideas worth a section each: the folder as config, tabs with lazy
  loading, ordering that lives in the filenames
- a download button pointing at the latest release
- a link to the repository

## Open decisions

Nothing is chosen yet:

- **stack** — plain HTML/CSS is enough for one static page and deploys anywhere;
  Astro or Next add tooling that a single page may not need
- **hosting** — GitHub Pages fits, and Actions can publish this folder on push
- **language** — the app UI is Portuguese, the README is bilingual; the page can
  be one or both

## Design

Whatever it becomes, it should look like the app it is selling: the palette and
type are in `../app/src/FolderHub/Themes/Dark.xaml`, and the Design section of
the root README has the values written out.
