# Changelog

## v0.6.0-beta — 2026-10-01

Performance and hardening release. / Релиз производительности и защиты.
Release assets are now plain exe files per CPU: `Tilettes.exe` (universal),
`Tilettes-x64.exe`, `Tilettes-x86.exe`. / К релизу прикладываются exe под разные
процессоры вместо zip-архива.

- **perf:** faster startup (lazy tab rendering, only the active tab is built), persistent icon cache (shell icons are extracted once per tile lifetime), search metadata collected once when an item is added; the mini explorer search field is disabled (code kept for re-enable) / быстрый старт, постоянный кэш иконок, метаданные поиска собираются один раз при добавлении
- **network:** paths on network shares never block the UI thread — icons and .lnk targets resolve in background / сетевые пути больше не блокируют интерфейс
- **fix:** an active skin now drives the light/dark palette everywhere — picking Мята no longer leaves dark windows; mint is the default theme, all fonts default to 14 / шкурка определяет тему во всех окнах; мята и шрифты 14 по умолчанию
- **first start:** on monitors with working area below 900px the default window and grid shrink proportionally to fit / на маленьких мониторах окно и сетка пропорционально уменьшаются
- **hardening:** strong name, VERSIONINFO, explicit manifest, Win-key capture is opt-in — 0 detections on VirusTotal / подпись, версия, манифест, Win-клавиша — по включению
- **build:** GitHub Actions attaches exe builds (AnyCPU/x86/x64) instead of a zip

**Full Changelog**: https://github.com/AlexNoVibe/Tilettes/compare/v0.5...v0.6.0-beta
