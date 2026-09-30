# Changelog

## v0.6.0-beta — 2026-10-01

Performance and hardening release. Release assets are now plain exe files per CPU: `Tilettes.exe` (universal), `Tilettes-x64.exe`, `Tilettes-x86.exe`.

- **perf:** faster startup (lazy tab rendering, only the active tab is built), persistent icon cache (shell icons are extracted once per tile lifetime), search metadata collected once when an item is added; the mini explorer search field is disabled (code kept for re-enable)
- **network:** paths on network shares never block the UI thread — icons and .lnk targets resolve in background
- **fix:** an active skin now drives the light/dark palette everywhere — picking Mint no longer leaves dark windows; mint is the default theme, all fonts default to 14
- **first start:** on monitors with working area below 900px the default window and grid shrink proportionally to fit
- **hardening:** strong name, VERSIONINFO, explicit manifest, Win-key capture is opt-in — 0 detections on VirusTotal
- **build:** GitHub Actions attaches exe builds (AnyCPU/x86/x64) instead of a zip

**Full Changelog**: https://github.com/AlexNoVibe/Tilettes/compare/v0.5...v0.6.0-beta
