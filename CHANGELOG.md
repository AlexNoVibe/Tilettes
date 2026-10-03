# Changelog

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
