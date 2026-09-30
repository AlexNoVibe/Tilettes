# Tilettes

[English](README.md) · [Русский](README.ru.md) · [Español](README.es.md) · [Português](README.pt.md) · [Deutsch](README.de.md) · [Français](README.fr.md) · **Italiano** · [Polski](README.pl.md) · [中文 (简体)](README.zh.md) · [日本語](README.ja.md)

Un pannello di avvio rapido per Windows: una griglia di tessere con scorciatoie, cartelle e schede, ricerca fuzzy integrata e un mini esploratore con console incorporata. Un unico EXE portatile, senza installazione, .NET Framework 4.8 (WinForms).

Versione attuale: **v0.5** — download: [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Changelog (inglese)](README.md#changelog). Stato: **beta**.

## Funzioni

- Tessere 1×1…6×6, schede illimitate spostabili, cartelle nella scheda o in popup, trascinamento da Esplora risorse, griglia personalizzabile, scala delle icone.
- Ricerca fuzzy su nomi, metadati, percorsi e descrizioni, correzione del layout di tastiera sbagliato (`руддщ` → `hello`).
- Mini esploratore con breadcrumb, segnalibri, ricerca file e console `cmd.exe` incorporata.
- Icone e regole "apri con" per tipo di file, importazione/esportazione.
- Icona nell'area di notifica, avvio automatico, scorciatoia globale, menu nativi di Esplora risorse, finestra senza bordi ridimensionabile.
- Finestra di benvenuto mostrata una sola volta e controllo aggiornamenti via GitHub Releases con targhetta nell'angolo.

## Primo avvio e aggiornamenti

- La finestra di benvenuto appare una sola volta (nota beta, scelta della lingua, consenso al controllo aggiornamenti, tessere di esempio) e può essere riaperta dalle impostazioni.
- Il controllo aggiornamenti interroga GitHub Releases ogni N giorni (predefinito 3) — solo con il consenso dell'utente; se esiste una versione più recente appare una targhetta verde «Aggiorna» accanto al pulsante impostazioni. «Controlla adesso» è un controllo manuale.

## Donazioni

Se Tilettes ti è utile, puoi sostenere lo sviluppo in crypto. Le reti compatibili EVM condividono lo stesso indirizzo:

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

Altri modi per aiutare: segnalare bug e idee in [Issues](https://github.com/AlexNoVibe/Tilettes/issues), mettere una stella al repository, spargere la voce.

## Compilazione

```
build.bat
```

Basta un qualsiasi Windows con .NET Framework 4.x — il compilatore è incluso nel sistema. Le versioni vengono compilate automaticamente da GitHub Actions a ogni tag `v*`.

Documentazione completa: [**README.md**](README.md) (English) · [README.ru.md](README.ru.md) (Русский)
