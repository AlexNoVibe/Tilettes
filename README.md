# Tilettes

**English** · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

<!-- Adding a language: create docs/README.<code>.md (translate), add
     lang_xx.cs (UI table keyed by the English strings, see loc.cs), then
     extend the language line above, the one at the top of every other README
     file and the Loc.Languages array. GitHub shows README.md (English) on the
     repo home; every other language lives in docs/ as one file plus one link. -->

A fast-launch panel for Windows: a tile grid with shortcuts, folders and tabs, built-in fuzzy search and a mini explorer with an embedded console. Single portable EXE, no installer, .NET Framework 4.8 (WinForms).

Current version: **v0.6.0-beta** — download from [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Changelog](#changelog). Status: **beta**.

## Features

- **Panel** — tiles sized 1×1…6×6, unlimited tabs (draggable, multi-row), folders opened in-place or as popups, drag-and-drop from Explorer, custom grid (columns/rows/transparency), icon scaling.
- **Search** — searches names, file names, program metadata (FileDescription / ProductName / CompanyName), full paths and user descriptions; optionally includes the mirrored Start Menu tab; fuzzy matching with adjustable accuracy and wrong-keyboard-layout correction (`руддщ` → `hello`); results are ranked by match quality and matched characters are highlighted; repeated queries get a history boost.
- **Mini explorer** — breadcrumb navigation, folder/command/group bookmarks, and an embedded `cmd.exe` console with command history, saved commands and Ctrl+wheel font zoom. (The file-search module is disabled since v0.6.0-beta — a stub is kept for a future re-enable.)
- **File type rules** — per-extension/per-mask icons and "open with" associations, import/export.
- **Desktop integration** — tray icon, autostart with Windows, global hotkey, optional Win-key capture, native Explorer context menus, borderless window with edge resizing.
- **Skins & extras** — decorative skins (own color palette + a rounded window border), a mirrored Start Menu tab rebuilt on a schedule, full backups into `autoBackup\`, panel search history.
- **First start & updates** — a one-time welcome window (beta note, language choice, update-check permission, example tiles) and an update check against GitHub Releases with a corner plate when a newer version exists.

## First start & updates

- **Welcome window** (only on the very first launch): a thank-you note, a beta warning with a link to [Issues](https://github.com/AlexNoVibe/Tilettes/issues), a painted "drag a shortcut → a tile" mini-diagram, language choice (10 flag buttons), the update-check permission, donation addresses (click to copy) — and two exit buttons: plain **Close**, or **Close & create example tiles** (Notepad, Calculator, Explorer, Paint as ready tiles). It can be replayed anytime via "Show the welcome window again" in the settings.
- **Update check** — the app asks the public GitHub Releases API once every N days (default 3; the first check also happens N days after the very first start, not immediately). Nothing is sent anywhere, and with the check disabled in settings no network request is made at all. When a newer tag exists, a green **⟳ Update** plate appears next to the settings button and opens the [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) page. "Check now" in the settings runs a manual check regardless of the interval (it reports the result in a message box). Automatic installation is a stub (TODO) for now.
- **Testing hook** — start the app with `WINPANEL_MOCK_UPDATE=0.6` to render the Update plate as if a newer release existed (no network involved).

## Settings reference

All settings live in one dialog (⚙ button / tray menu) and are stored in `settings.ini`.

### Startup & window

| Setting | Range | Default | Description |
|---|---|---|---|
| Startup Size (W × H) | 200–4000 | 900 × 800 | Panel size on every launch. Resizing during a session is not persisted — only the position is. |
| Window Position (X, Y) | −4000…4000 | 100, 100 | Screen position on launch. Updated automatically when the window is moved. |
| Show window hotkey | presets + custom | Ctrl+Q | Global hotkey that shows/activates the panel. Pick a preset (None, Ctrl+Q, Ctrl+Shift+Q, Alt+Q, Ctrl+J, …) or type any `Mod+Key` combination (Ctrl/Alt/Shift/Win + a letter or digit) right into the editable field; unparsable input is rejected with an explanation. |
| Language | en, ru, es, pt, de, fr, it, pl, zh, ja | ru | UI language (10 languages), applied immediately. |

### Grid & tiles

| Setting | Range | Default | Description |
|---|---|---|---|
| Grid Transparency | 0–255 | 50 | Alpha of the grid lines. 0 = invisible. Only drawn when the grid toggle (▦) is on. |
| Grid Columns | 1–100 | 16 | Horizontal cells. Tile positions snap to this grid. |
| Grid Rows | 1–100 | 16 | Vertical cells. |
| Def. Item Size | 1–6 | 2 | Size of newly added tiles (1×1 … 6×6 cells). |
| Icon Scale (%) | 25–400 | 100 | Icon size inside a tile, percent of the default. |
| Allow adding icons | on/off | on | Edit mode: dragging tiles, creating folders, dropping files. When off, tiles simply launch on click. |
| Tile label: two rows | on/off | on | Tall tiles wrap the label onto two rows (the alignment of the two rows — left/center/right — is set next to it). |
| Hide shortcut suffix | on/off | on | Display only: " - Shortcut" / " — ярлык" (dash variants, several languages) is hidden on the label; the stored name and search are untouched — untick to bring it back. |
| Hide file extension | on/off | on | Display only: the real extension of the item's path (.mp4 …) is hidden on the label; untick to bring it back. |
| Skin & theme | None (dark) / Light / skins | Mint | Classic dark or light theme, or a decorative skin with its own palette and a window border: Android, Night, Mint (the factory default). |

### Folders

| Setting | Range | Default | Description |
|---|---|---|---|
| Open folders in | Same window / Popup window | Same window | Clicking a folder navigates inside the tab or opens a popup above everything. |
| sec idle | 0–600 | 15 | Same-window mode only: automatically go back up after N seconds without mouse/keyboard activity. 0 = off. |

### Fonts

One row each for **Tiles**, **Tabs** and **UI**:

| Setting | Range | Default | Description |
|---|---|---|---|
| size | 6–24 | 14 | Font size for the group. |
| color swatch | any color | empty | Custom text color; empty = theme default. Applies to tiles' labels, tab captions or all UI text. |
| family | any installed font | Segoe UI | Font family for the group. |

### Search

| Setting | Range | Default | Description |
|---|---|---|---|
| Fuzzy accuracy (0–3) | 0–3 | 2 | 0 = substring matches only; 1–3 = increasingly tolerant typo/fuzzy matching. Digits count double, so numeric codes match strictly. |
| Search in metadata | on/off | on | File name, shortcut target, version info (description, product, company). |
| Search in full paths | on/off | on | The full path text, including parent folders. |
| Search in descriptions | on/off | on | User descriptions (right-click → Description…). |
| Search in the Start tab | on/off | on | Include the mirrored Start Menu tab in panel search. |
| Search font: box | 7–30 | 14 | Font size of the search input. |
| Search font: results | 7–30 | 14 | Font size of result rows (row height follows the font). |

### Updates

| Setting | Range | Default | Description |
|---|---|---|---|
| Check for updates automatically | on/off | on | Ask GitHub Releases for a newer version once every N days. Never runs when unchecked — no network request at all. |
| Check every N days | 1–365 | 3 | How often to check. The first check happens N days after the very first start. |
| Check now | button | — | Ask GitHub Releases immediately (manual check works even with the automatic one off). |
| Install updates automatically | on/off | off | **Stub (TODO)** — not implemented yet. |
| ♥ Donate | button | — | Popup wallet list (a click copies the address) plus the GitHub donate section. |
| Show the welcome window again | button | — | Replay the first-start welcome window. |

### Mini explorer (INI keys)

| Key | Range | Default | Description |
|---|---|---|---|
| Ctrl+Click a folder opens Mini Explorer | on/off | on | The Ctrl+click shortcut on folder tiles. |
| `MiniExplorerW/H/X/Y` | W≥760, H≥520 | auto | Window geometry, remembered on close. |
| `MiniExplorerBookmarks` | on/off | on | Side bookmarks panel visibility. |
| `MiniExplorerTopBar` | on/off | on | Horizontal bookmarks bar visibility. |
| `MiniExplorerConsole` | 15–85 | 40 | Console height as percent of the window. |
| `ConsoleFontSizeX10` | 60–280 | 140 | Console font size ×10 (140 = 14 pt), changed with Ctrl+wheel. |

### Autostart & tray

| Setting | Range | Default | Description |
|---|---|---|---|
| Autostart with Windows | on/off | off | Writes `HKCU\...\Run` ("Tilettes"). |
| After autostart - go to tray | on/off | off | Adds `--minimized`: the panel starts hidden in the tray. |
| Minimize instead of close | on/off | on | ✕ / Alt+F4 hides to tray (or minimizes) instead of exiting. Exit is in the tray menu. |
| Always keep tray icon | on/off | on | Tray icon visible at all times. |
| Remember active tab | on/off | on | Restores the last active tab on launch. |
| Capture the Start button (Win) | on/off | off | A solo Win press shows the panel instead of the Start menu (low-level keyboard hook); Win+key chords pass through. Opt-in — off by default. |

### Backup & Start Menu sync

- **Backup now** — full backup zip into `autoBackup\` (settings, tiles, icons, bookmarks, search history, the exe); scheduled by "Backup every N days" (default 7, 0 = off), created ~3 minutes after launch when due.
- **Save backup (zip)** — the same archive into a user-chosen file.
- **Restore archive…** — expects a zip created by Tilettes itself; files unpack into the working folder, `Tilettes.exe` is never replaced.
- **Sync Start Menu now** / every N hours (default 24, 0 = off) — rebuilds the mirrored Start Menu tab.

## Hotkeys & commands

### Main panel

| Keys / action | Result |
|---|---|
| Hotkey (default Ctrl+Q) | Show / activate the panel. |
| Solo Win press (opt-in) | Show / hide the panel instead of the Start menu — enable "Capture the Start button (Win)" in settings. |
| Just type any text, or Ctrl+F | Open the panel search. |
| ↓ | Jump into the result list. |
| Enter | Open the selected result (folder → navigate, file → launch). |
| Esc | Close the search. |
| Click a tile | Launch the item; folder navigates (or popup, per settings). |
| Ctrl+Click a folder tile | Open the mini explorer (if enabled). |
| Drag a tile (edit mode) | Move it; drop onto a folder to move it inside. |
| Drop files onto the panel (edit mode) | Add as tiles (drop onto a folder to add inside). |
| Right-click a tile | Native Explorer menu plus: Description…, Size 1×1–6×6, Rename, Change Icon, Remove, Move out of folder, Move to tab ▸, Open in Mini Explorer (folders). |
| Right-click a tab | Delete (last tab is protected), Rename, Toggle free/grid layout. |
| Drag a tab | Reorder within a row or move to another row. |
| Right-click empty panel | Create Folder, Settings. |
| ▦ / ✅ / ⚙ buttons | Grid visibility, edit mode, settings. ✅ is three-state: off / edit / multi-select — in multi-select click tiles to pick several, then bulk-remove or move them via the right-click menu. |

### Mini explorer

| Keys / action | Result |
|---|---|
| Ctrl+L / F4 / Edit | Edit the path. |
| F5 | Refresh the folder. |
| Backspace | Up one level. |
| Alt+← / Alt+→ | Back / forward. |
| Enter / double-click | Open (folder navigates, file launches). |
| Esc | Cancel path edit → close the window. |
| Ctrl+mouse wheel | Console font size (persisted). |
| Drag the splitter | Console height (persisted). |
| ≡ / ☰ buttons | Toggle side bookmarks panel / top bookmarks bar. |
| Right-click a file | Open, Show in Explorer, Copy path. |
| Right-click a folder | Open, Add to bookmarks, Open in Explorer. |
| Right-click empty space | Refresh, Copy folder path, Open in Explorer, Add current folder to bookmarks, Open console window here. |
| Right-click a bookmark | Edit command… (commands only), Rename…, Move up / Move down, Remove. |

### Console

Any single-line `cmd.exe` command can be typed and run (Enter or **Run**). The working directory is re-synced to the current folder before every command. **+ Save** stores the typed command as a bookmark (optionally inside a group); saved commands run on click. Buttons: **Clear** (wipe output), **Restart** (new cmd.exe), **New window** (a real console window at the current folder). Command history is available with ↑ / ↓ during the session.

## Limitations

- **Windows + .NET Framework 4.8 only** (GDI/WinForms). No per-monitor DPI awareness — the UI may blur on heavily scaled displays.
- **Mini explorer file search is disabled** since v0.6.0-beta: the disk-indexing module (fixed local drives only, capped at 200 000 items per drive) is kept as a stub for a future re-enable. Panel search runs over the cached metadata of saved items only — no disk indexing.
- **Panel search** shows the best **200** matches; a file list shows at most **800** entries per directory.
- **Console is `cmd.exe` only**: single-line commands; interactive/TUI programs (editors, pagers with key input) do not work properly; the output buffer auto-clears after ~150 000 characters; encoding follows the system OEM code page (e.g. CP866).
- **Global hotkey** is one letter/digit plus modifiers; registration fails with a balloon tip if another program already owns it.
- **Panel size resets to Startup Size on every launch** — only the position is remembered (by design).
- **Drag & drop and tile moving require edit mode** ("Allow adding icons" / ✅ button).
- Folder tiles preview at most **9** child icons; the folder popup shows at most **4** columns per row.
- `.lnk`/`.ico` items added to the panel are **copied into `ico\`** so they survive moving of the originals.
- **Restore archive** accepts only zips created by Tilettes ("Backup now" / "Save backup (zip)").
- Rounded window corners are temporarily removed while resizing (technique to avoid flicker) and restored on release.
- The layout fix covers the EN↔RU QWERTY pair; other layouts are passed through untouched.
- **Single instance**: launching a second copy just shows the existing window.
- Folder auto-exit works only in the "Same window" mode and only while inside a folder.
- **Photo/video tile previews** come from the Windows shell thumbnail cache. A **freshly added video** may show a generic icon until Explorer generates its preview (open the containing folder once in Explorer). Once the preview has been shown it is kept in the app's own cache and survives cache eviction; if the preview never appears, it means the system lacks the codec for that file (e.g. HEVC without the extension).

## Build

Requires any Windows with .NET Framework 4.x (the compiler ships with the OS):

```
build.bat          rem → Tilettes.exe (universal AnyCPU)
build.bat x86      rem → Tilettes-x86.exe
build.bat x64      rem → Tilettes-x64.exe
```

or directly:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ /win32icon:app.ico /win32manifest:app.manifest /keyfile:Tilettes.snk /out:Tilettes.exe src\*.cs
```

Releases are created automatically by GitHub Actions on every `v*` tag: the workflow builds the exe variants with the same csc call and attaches plain exe files to the release (AnyCPU universal + x86 + x64 — no zip); GitHub's automatic Source code archives are on the release as well. You can also build the exe yourself with `build.bat`.

## Antivirus false positives

Some antivirus products occasionally flag `Tilettes.exe` with a generic heuristic detection (unsigned small utilities that install a global keyboard hook, parse `.lnk` shortcuts and extract shell icons fit the pattern heuristics dislike). Treat such a detection as a **false positive** until proven otherwise — and you do not have to trust the shipped binary, because everything is verifiable:

- The **source code is fully open** in this repository — every line that ends up in the exe is here.
- **Releases are built automatically by GitHub Actions** from the tagged commit on Microsoft-hosted runners (`.github/workflows/build.yml`). Nothing is uploaded by hand: the exe attached to a release is compiled from exactly the source you see at that tag.
- You can **build the exe yourself** with `build.bat` (the C# compiler ships with Windows) and run your own build instead of the downloaded one.
- The strong name key (`Tilettes.snk`) is generated for the build only; a strong name proves assembly identity, not a vendor's trust — look at the code and the build pipeline instead.
- Since v0.6.16 the app also **does not offer releases younger than 24 hours** (its own update check), so a freshly published exe does not spread during its first day while antivirus cloud verdicts settle.

## Data files (created next to the EXE)

| File | Purpose |
|---|---|
| `settings.ini` | All settings |
| `records.xml` | Tabs, folders, shortcuts, descriptions |
| `bookmarks.xml` | Mini explorer bookmarks |
| `filetypes.xml` | File type rules |
| `searchHistory.xml` | Panel search history ("past search") |
| `ico\` | Copies of .lnk/.ico items and custom icons |
| `iconcache\` | Persistent icon cache (icons extracted once per tile lifetime) |
| `autoBackup\` | Scheduled full-backup zips |
| `log.txt` | Application log (repeated lines are deduplicated) |

## Project layout (`src/`)

| File | Purpose |
|---|---|
| `Program.cs` | Main window: tabs, tiles, panel search, folder popups, single-instance |
| `miniexplorerform.cs` | Mini explorer: navigation, bookmarks, embedded console |
| `searchcore.cs` | Fuzzy scoring, keyboard-layout variants and bit-mask prefilter (the panel search engine); its disk-indexing part (mini explorer file search) is currently disabled |
| `panelsearch.cs` | Searchable metadata of saved items |
| `Settings.cs` / `SettingsForm.cs` | Settings model and dialog |
| `filetypes.cs` / `filetypesform.cs` | File type rules and their editors |
| `bookmarks.cs` | Bookmark storage |
| `Skins.cs` | Decorative skins: palettes and the window border |
| `StartMenuSync.cs` + `ShellItemApi.cs` | Mirrored Start Menu tab, including UWP/Store apps |
| `BackupManager.cs` + `ZipWriter.cs` / `ZipReader.cs` | Scheduled and manual backups |
| `SearchHistory.cs` | Panel search history ("past search", result boosting) |
| `AppLog.cs` | Deduplicated `log.txt` writer |
| `loc.cs` + `lang_*.cs` | Localization: EN source, RU inline, ES/PT/DE/FR/IT/PL/ZH/JA tables |
| `UpdateChecker.cs` / `WelcomeForm.cs` | Update check (GitHub Releases) and the first-start welcome window |
| `IconExtractor.cs`, `NativeContextMenu.cs`, `IniFile.cs`, `Records.cs`, `apputil.cs` | Icons, native menus, INI I/O, records I/O, autostart/single-instance/wallets |
| `AssemblyInfo.cs` | VERSIONINFO / assembly metadata |

## License

[MIT](LICENSE) — free to use, modify and distribute.

<a name="donate"></a>
## Donate

If Tilettes is useful, you can support the development with crypto. EVM-compatible networks share one address — send on whichever network is convenient:

<a name="donate-evm"></a>
### EVM — Ethereum · Polygon · Base · Monad · HyperEVM

```
0xf84897FA0b74083c16865315A5b148f4d92e6C2a
```

<a name="donate-btc"></a>
### Bitcoin (BTC)

```
bc1qu9cf5uqc5wxqwde8mk378xwdlnjatvmhxhvat5
```

<a name="donate-sol"></a>
### Solana (SOL)

```
7ffCFnJBNVaF268FsZGKBPEWe3UNrWbasgt3aidiCw68
```

<a name="donate-sui"></a>
### Sui (SUI)

```
0x3ca194b355bb00a1f5f646786407ebbcdaee361c6f56fb92f8df9abd73b0c3b1
```

Other ways to help: report bugs and ideas in [Issues](https://github.com/AlexNoVibe/Tilettes/issues), star the repository, spread the word.

<a name="changelog"></a>
## Changelog

### v0.6.1 (2026-10-01)

- Version is shown in the top-right corner of the settings window.
- Hold **Ctrl** — tiles show their full untruncated names (the label font shrinks to fit); back to normal when released. Toggle in the settings ("Hold Ctrl — show full names on tiles").
- Tile tooltips redesigned: description (or the full name when there is no description) + a separator + the full paths; `.lnk` items show both the shortcut and its resolved target.
- Media previews: a preview, once obtained, is kept in the app's own cache and survives Windows thumbnail-cache eviction; dead-link right-click now shows the tile's own actions instead of doing nothing; Media Foundation frame extractor kept as a fail-soft fallback (see REPORT.md).
- b2.bat closes the running app before compiling.

### v0.6.0-beta (2026-10-01)

- Performance: faster startup (lazy tab rendering — only the active tab is built), persistent icon cache (shell icons are extracted once per tile lifetime), search metadata collected once when an item is added; the mini explorer search field is disabled (code kept for re-enable).
- Network shares never block the UI thread: icons and .lnk targets on network paths resolve in background.
- An active skin now drives the light/dark palette everywhere (picking Mint no longer leaves dark windows); mint is the default theme and all fonts default to 14.
- First start: on monitors with working area below 900px the default window and grid shrink proportionally to fit.
- Hardening: strong name, VERSIONINFO, explicit manifest, Win-key capture is opt-in — 0 detections on VirusTotal.
- Release assets are plain exe files per CPU (AnyCPU/x86/x64) instead of a zip archive; release notes come from CHANGELOG.md.

### v0.5 (2026-09-30)

- First-start welcome window (once per data folder): thanks, beta notice + issue link, painted mini-diagram, language choice (RU/EN flags), update-check permission, donation addresses (click to copy); exit via "Close" or "Close & create example tiles" (Notepad / Calculator / Explorer / Paint as ready tiles). Re-playable from the settings.
- Update check: the app asks the public GitHub Releases API for the latest tag every N days (default 3; the first check also happens N days after the install) — strictly only when the user allowed it, zero network requests otherwise. When a newer version exists a green "Update" plate appears next to the settings button and opens the releases page. Manual "Check now" button in the settings (reports the result in a message box). Auto-install is a stub (TODO). Mock hook for testing the plate: `WINPANEL_MOCK_UPDATE=0.6`.
- Settings: new "Updates" section (check toggle, interval in days, check-now button, auto-install stub, donate line with a popup wallet menu — a click copies the address — and a welcome-window replay button). Editable hotkey field: any Ctrl/Alt/Shift/Win + letter/digit combination can be typed in (validated), default changed to Ctrl+Q.
- Program UI localized into **10 languages**: English (source), Russian, Spanish, Portuguese, German, French, Italian, Polish, Chinese (simplified) and Japanese. The language is picked in the settings or via the painted flags in the welcome window; new languages are one table file + one line (see loc.cs).
- Real donation wallet list grouped by chain: EVM networks (ETH · Polygon · Base · Monad · HyperEVM) share one address; plus Bitcoin, Solana and Sui.
- Version is now a single constant (`AppInfo.AppVersion`); the tray tooltip and the welcome window show it.
- GitHub infrastructure: MIT license, FUNDING.yml (sponsor links to wallet anchors), README split into per-language files (`README.md` EN + 9 translations) for easy extension, GitHub Actions workflow (release with a portable zip on `v*` tags), docs landing page for GitHub Pages. All commit messages in the history are English.

### v0.4 (2026-09-29)

- Rebrand: Tilettes / «Плиточки», new icon (exe resource + code-drawn tray icon).
- Backup feature completed: "Save backup (zip)" packs settings, tiles, icons, bookmarks, search history and the exe; "Restore archive" unpacks app-created zips into the working folder without replacing Tilettes.exe; scheduled full backups into autoBackup\.
- Settings dialog rework: vertically resizable via a bottom grip, tooltips on every item, labeled X/Y fields for startup size and window position, grid columns/rows on one row, skin combobox replaces the duplicated light-theme checkbox.
- Start Menu mirror tab fixes (folder contents no longer clump; simplified auto-layout).
- Layout fixes for fonts 14–20 (tabs, search status height, dialogs, mini explorer bars, window corner step).

### v0.3 (2026-09-28)

- Start Menu sync (mirror tab, scheduled), search toggle shrink ladders, quick search settings strip, multi-select edit mode, "Move to tab" menus, Win-key capture, folder popup navigation, search result path highlighting.

### v0.1 – v0.2 (2026-09-27)

- Initial builds of the launcher panel: tiles, tabs, folders, panel search, mini explorer with console, file type rules, tray/autostart/hotkey, grid customization.
