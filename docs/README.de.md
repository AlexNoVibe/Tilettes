# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · **Deutsch** · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

Ein Schnellstart-Panel für Windows: ein Kachelraster mit Verknüpfungen, Ordnern und Tabs, integrierte Fuzzy-Suche und ein Mini-Explorer mit eingebauter Konsole. Eine portable EXE, ohne Installation, .NET Framework 4.8 (WinForms).

Aktuelle Version: **v0.5** — Download: [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Changelog (English)](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md#changelog). Status: **beta**.

## Funktionen

- Kacheln 1×1…6×6, unbegrenzt viele verschiebbare Tabs, Ordner im Tab oder als Popup, Drag-and-drop aus dem Explorer, eigenes Raster, Symbol-Skalierung.
- Fuzzy-Suche über Namen, Metadaten, Pfade und Beschreibungen, Korrektur bei falschem Tastaturlayout (`руддщ` → `hello`).
- Mini-Explorer mit Breadcrumbs, Lesezeichen, Dateisuche und eingebauter `cmd.exe`-Konsole.
- Symbole und „Öffnen mit“-Regeln pro Dateityp, Import/Export.
- Tray-Symbol, Autostart, globaler Hotkey, native Explorer-Menüs, rahmenloses Fenster mit Rändern zum Vergrößern.
- Einmaliges Willkommensfenster und Update-Prüfung über GitHub Releases mit Plakette in der Ecke.

## Erster Start & Updates

- Das Willkommensfenster erscheint genau einmal (Beta-Hinweis, Sprachwahl, Einwilligung für Update-Prüfung, Beispielskacheln) und lässt sich in den Einstellungen erneut aufrufen.
- Die Update-Prüfung fragt GitHub Releases alle N Tage (Standard 3) — ausschließlich mit Einwilligung des Nutzers; bei einer neueren Version erscheint eine grüne „Update“-Plakette neben dem Einstellungen-Button. „Jetzt prüfen“ ist eine manuelle Prüfung.

## Spenden

Wenn Tilettes nützlich ist, kannst du die Entwicklung mit Krypto unterstützen. EVM-kompatible Netzteile teilen sich eine Adresse:

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

Weitere Möglichkeiten zu helfen: Bugs und Ideen in [Issues](https://github.com/AlexNoVibe/Tilettes/issues) melden, dem Repository einen Stern geben, weitererzählen.

## Build

```
build.bat
```

Benötigt wird ein beliebiges Windows mit .NET Framework 4.x — der Compiler ist Teil des Systems. Releases werden bei jedem `v*`-Tag automatisch von GitHub Actions erstellt: Der Workflow prüft den Build und hängt ein portables Zip an (Tilettes.exe + README + LICENSE); die automatischen Quellcode-Archive sind ebenfalls dabei. Die EXE kannst du auch selbst mit `build.bat` bauen.

Vollständige Dokumentation: [**README.md**](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) (English) · [README.ru.md](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) (Русский)
