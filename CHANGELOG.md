# Changelog

## v1.1.4 — 2026-10-06

- **Bulk actions in the red multi-select mode**: the right-click menu over selected tiles gains four items — "Description..." opens one dialog (titled "N selected", seeded from the first selected tile) and lands the text on every selected tile; "Size" carries the same 1×1–6×6 submenu a single folder tile has, applied to all selected (grid tiles are re-fitted into their cells); "Change Icon" picks one icon file and assigns it to every selected tile with the usual copy-into-ico consolidation; "Open containing folder" reveals where the selection lives — one Explorer window per distinct parent folder, so ten tiles from one directory open one window, not ten. Selected tiles now also carry a red checkmark badge (a red disc with a white check, scaling with the tile) in the top-right corner next to the red outline. All items reuse existing menu strings, so the ten languages are covered without new keys.

**Full Changelog**: https://github.com/AlexNoVibe/Tilettes/compare/v1.1.3...v1.1.4

## v1.1.3 — 2026-10-06

- **"Sync user Start"**: a button in Settings (next to the Start Menu sync) and a "close & sync" answer in the first-start welcome window copy your pinned Windows 10 Start tiles — groups, sizes, positions — into a new ordinary tab ("User Start" / «Пользовательский Пуск»; running it again rebuilds that tab). The layout comes from one PowerShell call (`Export-StartLayout` + `Get-StartApps` in a single process, hard 20 s timeout because `Export-StartLayout` hangs forever on systems with a broken Start layer), tile targets resolve to native shortcuts when one exists (a Start Menu scan matches shortcuts by their AppUserModel ID), UWP tiles go through `shell:AppsFolder` paths the panel already launches and icons, Windows tile shapes map onto the square grid (small→1, medium→2, wide→3, large→4), and groups stack in their original order with collisions moved to the nearest free cell. Windows 11 (no tiles) answers with an explanation; pinned Edge sites and Start tile folders cannot be exported by Windows at all and are reported in the final count. No confirmation dialogs on the way: the button does its thing and the report tells what happened. Localized in ten languages.
- **Quieter backups**: a finished backup no longer posts a Windows notification — routine success toasts just piled up in the notification center. The zip name lands in log.txt; failures still warn (both scheduled and manual backups).

## v1.1.2 — 2026-10-06

- **UWP apps in the Start-menu clone on locked-down systems**: on machines where the shell serves an empty AppsFolder (Windows' own Get-StartApps comes back empty there too), the mirror's "Apps (system)" folder used to stay empty silently. When the shell view enumerates as empty, a registry fallback lists the per-user package repository instead — package family and AppId from the two registry layouts (the newer `Applications\<AUMID>` one and the legacy AppId-key one), display names resolved through SHLoadIndirectString, system-only packages and hidden `Global.*` entries filtered out — and log.txt now records why the shell view came up empty. Launches of the mirrored apps go through a three-step chain (the Windows 8+ ApplicationActivationManager COM, then plain ShellExecute, then an explorer.exe relay), each attempt logged, instead of a plain file open that cannot work for ACL-locked packaged apps.
- **UWP shortcuts survive the sync**: a .lnk whose target resolves to an empty string — how shortcuts to packaged apps resolve — was dropped as a dead link on every Start-menu sync, so copied Calculator/Alarms shortcuts never made it into the clone. An empty target now keeps the shortcut (launching it still works); only a shortcut naming a real missing file is filtered, and the mirror keeps skipping non-shortcut files exactly as before.

**Full Changelog**: https://github.com/AlexNoVibe/Tilettes/compare/v1.1.1...v1.1.2

## v1.1.1 — 2026-10-05

- **Fewer antivirus false positives**: the console interrupt kills the shell process directly instead of spawning `taskkill /T /F`; the recon-style default bookmarks are gone (process list, ipconfig, netstat, IPv4 addresses, running services, tar); the show-time DWM cloak now wraps only freshly recreated window handles.
- **Platform exes**: releases now ship `Tilettes_x86.exe` and `Tilettes_x64.exe` instead of one universal AnyCPU build; `build.bat` produces the same pair.
- **Compact release pages**: a release's notes now carry only its own changelog section with a download hint on top and checksums, not the whole multi-page changelog.

**Full Changelog**: https://github.com/AlexNoVibe/Tilettes/compare/v1.1.0...v1.1.1

## v1.1.0 — 2026-10-05

- **No more black flash on show**: showing the panel from a minimized state (hotkey, Win key, tray) used to composite its stale surface for several frames — a black block where the tiles go — until the first paint landed. Worse, on machines where the virtual-desktop COM class is not registered (0x80040154), the re-home to the current desktop falls back to recreating the window, and a freshly recreated window used to pop up unpainted. The whole show now happens off screen: the window is hidden first (so a recreation stays invisible), cloaked (DWM cloaking: invisible to the desktop, but alive and painting), shown and restored, force-painted synchronously with all its children, and only then uncloaked — plus one more full paint right after the uncloak, so a freshly bound DWM surface can never present black. The first frame on screen is already the finished panel. An already visible panel skips all of this (cloaking it would just blink), systems without DWM cloaking fall back to the plain show, and log.txt gains a one-line "Show:" diagnostic (recreate/cloak/paint ms) for this path.
- **Aura look**: back to the solid translucent fill — one uniform tint with a hard rounded edge, the way the Windows 10 Start tiles are tinted — instead of the v1.0.2 soft glow; the lightest available tint is lighter now (the transparency floor dropped to ~5% opacity), so the very light auras of that kind are reachable.
- **Aura dialog**: the slider's leftmost position is now "Value from settings" — the tile follows the global transparency setting again; the line under the slider and the live preview say so, and a "Standard value" button jumps there. Making the slider match the global value by hand no longer secretly stores "follow" — it keeps an honest override.

**Full Changelog**: https://github.com/AlexNoVibe/Tilettes/compare/v1.0.2...v1.1.0

## v1.0.2 — 2026-10-04

- **Damaged data files**: if records.xml (or bookmarks, file-type rules, search history) cannot be read, the broken file is kept next to the exe as `<name>.broken-<date-time>`, a fresh default is created in its place, and the user is told once, briefly — a tray balloon when the icon is around, a small dialog otherwise; details go to log.txt. Save failures are logged now too.
- **Launch failures**: a tile whose file cannot be opened answers with a short localized notice («Cannot open "name"») — a tray balloon when possible, a small dialog otherwise — with the technical reason only in log.txt; the raw English "Error opening file" box is gone.
- **Aura transparency setting**: a global "Aura Transp. (%)" row in the settings changes the transparency of all auras at once; the per-tile aura dialog can still override a single tile (a value matching the global one stores no override). Old transparency values baked into aura colors migrate: the former default follows the global setting, any other keeps acting as that tile's own.
- **Softer aura**: auras are painted as a soft glow — strongest in the middle of the tile, melting to transparent at the rounded edge — instead of a hard-edged solid fill; the aura dialog's live preview shows the same.
- **Any-key hotkey**: the "Show window hotkey" field is now click-and-press: press any combination (a letter, digit, F1–F24 or Space, with Ctrl/Alt/Shift/Win) and it becomes the hotkey; Esc or the × button sets None.
- **Console on Ctrl+right-click**: a folder tile (in the panel and in the folder popup) opens in a console command from the settings — "%1" is the folder path, e.g. `wt -d "%1"`; empty = off (the regular context menu shows). The "Open folders with" hint now explains %1 explicitly, and the mini explorer's "Open in Explorer" honors the configured folder manager for directories.
- **Search limits**: up to 10 rows in the past-search block (was 8) and the 30 most relevant regular results (was 200).
- **Grid capacity**: extra rows below the grid go up to 500 now — tens of thousands of cells for one tab; overflow protection stays as the safety net.
- **Quieter Start Menu sync**: a sync that finds no changes no longer rewrites records.xml or rebuilds the whole panel (that was a multi-second UI hiccup once a day); search metadata is collected after the data reload instead of being thrown away; one COM object serves every shortcut of a sync instead of one per shortcut.
- **One universal exe in releases**: GitHub Actions ships only the AnyCPU Tilettes.exe (it runs as a 64-bit process on 64-bit Windows and as a 32-bit one on 32-bit Windows) with its SHA256 checksum; the x86/x64 variants are gone.

**Also new in this release:**

- **Tile aura**: every tile (and folder tile) can get its own translucent background color — right-click a tile → "Aura color..." — preset swatches, any custom color via the system picker, and a transparency slider with a live preview; multi-select applies one color to the whole selection at once.
- **Multi-select menu**: the red-checkmark mode gained "Move out of folder" (raise every selected tile one level) and "Aura color..." next to remove / move-to-tab.
- **Mini explorer tabs**: folders open as tabs inside the existing explorer window instead of overwriting its navigation (Ctrl+click, context menu and panel search alike); per-tab back/forward history, "+" opens a tab, middle-click or the tab context menu closes one, closing the last tab closes the window.
- **Bookmarks panel**: its width is adjustable by a splitter and remembered; command bookmarks show their display name with the command in a tooltip (a name is asked when saving a command); bookmarks reorder by drag-and-drop — drop below a group header to put an entry inside the group.
- **Console fix**: streaming commands (`docker stats`, progress bars) no longer flood the console — refresh frames (`\r`) update the last line in place, ANSI escape codes are stripped, runs of empty lines collapse, and a repeating plain-`\n` block (the `docker stats` table) is detected by its header and **redrawn in place**: the console shows one live-updating table like a real terminal; a new **Stop** button kills the running command's whole process tree and restarts the console.
- **Working directory on launch**: every launch path (tiles, search, folder views, the mini explorer, context-menu "Open") now pins the working directory the way a double-click in Explorer does — a `.bat` keeps finding the files next to it (`.env`, configs), an `.exe` its resources, and a shortcut's own "Start in" is respected (an empty "Start in" falls back to the target's folder); previously the child inherited Tilettes' own working directory, so scripts launched from the panel lost their surroundings.
- **Virtual desktops**: summoning the panel (hotkey, tray, Win key, a second launch) no longer drags the whole view back to the desktop the panel was left on — it re-homes itself to the desktop you are on before activating (a precise move through the public virtual-desktop COM when the system provides it, a window recreation where the component is missing); the same applies to a mini explorer left open on another desktop.

**Full Changelog**: https://github.com/AlexNoVibe/Tilettes/compare/v1.0...v1.0.2

## v1.0 — 2026-10-04

First stable release — everything since v0.6.0-beta, briefly:

- **Welcome window redesigned**: a painted diagram (a folder, an .exe and a .lnk card → the tile grid) and a hint naming the corner checkmark as the add/edit toggle; the window re-measures every text, so no localization clips its lines (the long Russian ones included).
- **Search**: a typed query now shows two blocks — remembered past queries on top (real icons, kept between sessions, boosted) and regular results below, with captions and a divider; full duplicates collapse; wrong keyboard layout fixed (`руддщ` → `hello`); still no disk indexing — instant results over cached metadata.
- **Tiles & UI**: hold Ctrl for full untruncated tile names; redesigned tooltips (description + full paths, `.lnk` target included); photo/video previews are kept in an app-owned cache that survives system thumbnail eviction; a floating scrollbar pill on tab panels; extra scrollable tile rows below the grid; single/double click for opening tiles and results is now a choice; the version is shown in the settings window.
- **Mini explorer**: command bookmarks expand `%1` to the browsed folder (`wt -d "%1"` opens Windows Terminal right there, a seeded CMD group ships the example); switching tabs no longer hangs an open folder; the panel's own folder groups are excluded from search results.
- **Updates & integrity**: releases younger than 24 hours are not offered yet (antivirus cloud verdicts settle during the first day); releases carry `SHA256SUMS.txt`; full VERSIONINFO metadata in the exe.
- **Privacy**: the crypto wallets are gone from the app — supporting the author is one link to the GitHub donate section; still exactly one optional network call (the update check), no telemetry.
- **Fixes**: scheduled backups no longer pop a tray notification on success (a failure still warns); switching tabs resets scroll to the top; the settings dialog, fonts 14–20 layouts and the Start Menu mirror received smaller layout repairs.

**Full Changelog**: https://github.com/AlexNoVibe/Tilettes/compare/v0.6.0-beta...v1.0

## v0.6.0-beta — 2026-10-01

Performance and hardening release. Release assets are now plain exe files per CPU: `Tilettes.exe` (universal), `Tilettes-x64.exe`, `Tilettes-x86.exe`.

- **perf:** faster startup (lazy tab rendering, only the active tab is built), persistent icon cache (shell icons are extracted once per tile lifetime), search metadata collected once when an item is added; the mini explorer search field is disabled (code kept for re-enable)
- **network:** paths on network shares never block the UI thread — icons and .lnk targets resolve in background
- **fix:** an active skin now drives the light/dark palette everywhere — picking Mint no longer leaves dark windows; mint is the default theme, all fonts default to 14
- **first start:** on monitors with working area below 900px the default window and grid shrink proportionally to fit
- **hardening:** strong name, VERSIONINFO, explicit manifest, Win-key capture is opt-in — 0 detections on VirusTotal
- **build:** GitHub Actions attaches exe builds (AnyCPU/x86/x64) instead of a zip

**Full Changelog**: https://github.com/AlexNoVibe/Tilettes/compare/v0.5...v0.6.0-beta
