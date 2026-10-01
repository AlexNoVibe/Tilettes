---
title: Tilettes
---

# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · **Italiano** · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

Un pannello di avvio rapido per Windows: una griglia di tessere con scorciatoie, cartelle e schede, ricerca fuzzy integrata e un mini esploratore con console incorporata. Un unico EXE portatile, senza installazione, .NET Framework 4.8 (WinForms).

Versione attuale: **v0.6.0-beta** — download: [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Changelog](#changelog). Stato: **beta**.

## Funzionalità

- **Pannello** — tessere da 1×1…6×6, schede illimitate (trascinabili, su più righe), cartelle aperte all'interno della scheda o in popup, drag-and-drop da Esplora risorse, griglia personalizzabile (colonne/righe/trasparenza), scala delle icone.
- **Ricerca** — cerca nei nomi, nei nomi dei file, nei metadati dei programmi (FileDescription / ProductName / CompanyName), nei percorsi completi e nelle descrizioni utente; corrispondenza fuzzy con accuratezza regolabile e correzione del layout di tastiera errato (`руддщ` → `hello`); i risultati sono ordinati per qualità della corrispondenza e i caratteri corrispondenti vengono evidenziati.
- **Mini esploratore** — navigazione a breadcrumb, segnalibri per cartelle/comandi/gruppi, ricerca dei file (cartella corrente o tutte le unità fisse) con un indice in background e una console `cmd.exe` incorporata con cronologia dei comandi, comandi salvati e zoom del font tramite Ctrl+rotella.
- **Regole per i tipi di file** — icone per estensione/maschera e associazioni «apri con», importazione/esportazione.
- **Integrazione con il desktop** — icona nell'area di notifica, avvio automatico con Windows, scorciatoia globale, menu contestuali nativi di Esplora risorse, finestra senza bordi con ridimensionamento dai bordi.
- **Primo avvio e aggiornamenti** — una finestra di benvenuto mostrata una sola volta (nota sulla beta, scelta della lingua, consenso al controllo aggiornamenti, tessere di esempio) e un controllo degli aggiornamenti tramite GitHub Releases con una targhetta nell'angolo quando esiste una versione più recente.

## Primo avvio e aggiornamenti

- **Finestra di benvenuto** (solo al primissimo avvio): un ringraziamento, un avviso sulla beta con un collegamento a [Issues](https://github.com/AlexNoVibe/Tilettes/issues), un mini-diagramma illustrato «trascina una scorciatoia → nasce una tessera», scelta della lingua (bandierine RU/EN), consenso al controllo aggiornamenti, indirizzi per le donazioni (clic per copiare) — e due pulsanti di uscita: semplice **Chiudi**, oppure **Chiudi e crea tessere di esempio** (Blocco note, Calcolatrice, Esplora risorse, Paint come tessere pronte). Può essere richiamata in qualsiasi momento tramite «Mostra di nuovo la finestra di benvenuto» nelle impostazioni.
- **Controllo aggiornamenti** — l'app interroga l'API pubblica di GitHub Releases una volta ogni N giorni (predefinito 3; anche il primo controllo avviene N giorni dopo il primissimo avvio, non subito). Nulla viene inviato ad alcun luogo e, con il controllo disattivato nelle impostazioni, non viene fatta alcuna richiesta di rete. Quando esiste un tag più recente, accanto al pulsante delle impostazioni compare una targhetta verde **⟳ Aggiorna** che apre la pagina di [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest). «Controlla adesso» nelle impostazioni esegue un controllo manuale a prescindere dall'intervallo (riporta il risultato in una finestra di messaggio). Per ora l'installazione automatica è uno stub (TODO).
- **Hook di test** — avvia l'app con `WINPANEL_MOCK_UPDATE=0.6` per far comparire la targhetta di aggiornamento come se esistesse una release più recente (senza alcuna rete).

## Riferimento impostazioni

Tutte le impostazioni sono riunite in un'unica finestra di dialogo (pulsante ⚙ / menu dell'area di notifica) e sono salvate in `settings.ini`.

### Avvio e finestra

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Dimensione all'avvio (W × H) | 200–4000 | 900 × 800 | Dimensione del pannello a ogni avvio. Il ridimensionamento durante una sessione non viene salvato — viene salvata solo la posizione. |
| Posizione finestra (X, Y) | −4000…4000 | 100, 100 | Posizione sullo schermo all'avvio. Si aggiorna automaticamente quando la finestra viene spostata. |
| Scorciatoia per mostrare la finestra | preset + personalizzata | Ctrl+Q | Scorciatoia globale che mostra/attiva il pannello. Scegli un preset (None, Ctrl+Q, Ctrl+Shift+Q, Alt+Q, Ctrl+J, …) oppure digita qualsiasi combinazione `Mod+Key` (Ctrl/Alt/Shift/Win + una lettera o una cifra) direttamente nel campo modificabile; l'input non interpretabile viene rifiutato con una spiegazione. |
| Lingua | ru / en | ru | Lingua dell'interfaccia, applicata immediatamente. |

### Griglia e tessere

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Trasparenza griglia | 0–255 | 50 | Alfa delle linee della griglia. 0 = invisibile. Disegnata solo quando l'interruttore della griglia (▦) è attivo. |
| Colonne griglia | 1–100 | 16 | Celle orizzontali. Le posizioni delle tessere si agganciano a questa griglia. |
| Righe griglia | 1–100 | 16 | Celle verticali. |
| Dim. elemento predefinita | 1–6 | 2 | Dimensione delle tessere appena aggiunte (1×1 … 6×6 celle). |
| Scala icone (%) | 25–400 | 100 | Dimensione dell'icona all'interno di una tessera, in percentuale rispetto al valore predefinito. |
| Consenti aggiunta di icone | attivo/disattivo | attivo | Modalità di modifica: trascinamento delle tessere, creazione di cartelle, rilascio di file. Quando è disattivo, le tessere si avviano semplicemente al clic. |
| Skin e tema | Nessuna (scura) / Chiara / skin | Nessuna (scura) | Tema classico scuro o chiaro, oppure una skin decorativa (colori propri + bordo della finestra). |

### Cartelle

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Apri le cartelle in | Stessa finestra / Finestra popup | Stessa finestra | Il clic su una cartella naviga al suo interno nella scheda oppure apre un popup sopra tutto. |
| sec di inattività | 0–600 | 15 | Solo per la modalità «stessa finestra»: torna automaticamente indietro dopo N secondi senza attività del mouse/della tastiera. 0 = disattivo. |

### Font

Una riga per ciascuno tra **Tessere**, **Schede** e **Interfaccia**:

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| dimensione | 6–24 | 9 | Dimensione del font per il gruppo. |
| colore | qualsiasi colore | vuoto | Colore del testo personalizzato; vuoto = predefinito del tema. Si applica alle etichette delle tessere, ai titoli delle schede o a tutto il testo dell'interfaccia. |
| famiglia | qualsiasi font installato | Segoe UI | Famiglia di font per il gruppo. |

### Ricerca

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Accuratezza fuzzy (0–3) | 0–3 | 2 | 0 = solo corrispondenze di sottostringa; 1–3 = corrispondenza fuzzy dei refusi via via più tollerante. Le cifre contano doppio, quindi i codici numerici vengono confrontati in modo rigoroso. |
| Cerca nei metadati | attivo/disattivo | attivo | Nome del file, destinazione della scorciatoia, informazioni sulla versione (descrizione, prodotto, azienda). |
| Cerca nei percorsi completi | attivo/disattivo | attivo | Il testo del percorso completo, incluse le cartelle superiori. |
| Cerca nelle descrizioni | attivo/disattivo | attivo | Descrizioni utente (clic destro → Descrizione…). |
| Font ricerca: casella | 7–30 | 9 | Dimensione del font della casella di ricerca. |
| Font ricerca: risultati | 7–30 | 9 | Dimensione del font delle righe dei risultati (l'altezza delle righe segue il font). |

### Aggiornamenti

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Controlla aggiornamenti automaticamente | attivo/disattivo | attivo | Interroga GitHub Releases per una versione più recente una volta ogni N giorni. Con la casella deselezionata non viene mai eseguito — nessuna richiesta di rete. |
| Controlla ogni N giorni | 1–365 | 3 | Frequenza del controllo. Il primo controllo avviene N giorni dopo il primissimo avvio. |
| Controlla adesso | pulsante | — | Interroga GitHub Releases immediatamente (il controllo manuale funziona anche con quello automatico disattivato). |
| Installa aggiornamenti automaticamente | attivo/disattivo | disattivo | **Stub (TODO)** — non ancora implementato. |
| ♥ Dona | pulsante | — | Elenco a comparsa dei portafogli (un clic copia l'indirizzo) più la sezione donazioni su GitHub. |
| Mostra di nuovo la finestra di benvenuto | pulsante | — | Riproponi la finestra di benvenuto del primo avvio. |

### Mini esploratore (chiavi INI)

| Chiave | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Ctrl+clic su una cartella apre il Mini esploratore | attivo/disattivo | attivo | La scorciatoia Ctrl+clic sulle tessere-cartella. |
| `MiniExplorerW/H/X/Y` | W≥760, H≥520 | auto | Geometria della finestra, memorizzata alla chiusura. |
| `MiniExplorerBookmarks` | attivo/disattivo | attivo | Visibilità del pannello laterale dei segnalibri. |
| `MiniExplorerTopBar` | attivo/disattivo | attivo | Visibilità della barra orizzontale dei segnalibri. |
| `MiniExplorerConsole` | 15–85 | 40 | Altezza della console in percentuale della finestra. |
| `ConsoleFontSizeX10` | 60–280 | 85 | Dimensione del font della console ×10 (85 = 8,5 pt), modificata con Ctrl+rotella. |

### Avvio automatico e area di notifica

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Avvio automatico con Windows | attivo/disattivo | disattivo | Scrive in `HKCU\...\Run` («Tilettes»). |
| Dopo l'avvio automatico - vai nell'area di notifica | attivo/disattivo | disattivo | Aggiunge `--minimized`: il pannello si avvia nascosto nell'area di notifica. |
| Minimizza invece di chiudere | attivo/disattivo | attivo | ✕ / Alt+F4 nasconde nell'area di notifica (o riduce a icona) invece di uscire. L'uscita si trova nel menu dell'area di notifica. |
| Mantieni sempre l'icona nell'area di notifica | attivo/disattivo | attivo | Icona nell'area di notifica sempre visibile. |
| Ricorda la scheda attiva | attivo/disattivo | attivo | Ripristina l'ultima scheda attiva all'avvio. |

### Backup e sincronizzazione del Menu Start

- **Backup adesso** — backup zip completo in `autoBackup\` (impostazioni, tessere, icone, segnalibri, cronologia di ricerca, exe); pianificato tramite «Backup ogni N giorni» (0 = disattivo), creato ~3 minuti dopo l'avvio quando la scadenza è raggiunta.
- **Salva backup (zip)** — lo stesso archivio in un file scelto dall'utente.
- **Ripristina archivio…** — si aspetta uno zip creato da Tilettes stesso; i file vengono estratti nella cartella di lavoro, `Tilettes.exe` non viene mai sostituito.
- **Sincronizza Menu Start adesso** / ogni N ore (0 = disattivo) — ricostruisce la scheda speculare del Menu Start.

## Scorciatoie e comandi

### Pannello principale

| Tasti / azione | Risultato |
|---|---|
| Scorciatoia globale (predefinita Ctrl+Q) | Mostra / attiva il pannello. |
| Digita semplicemente un testo, oppure Ctrl+F | Apri la ricerca del pannello. |
| ↓ | Passa all'elenco dei risultati. |
| Enter | Apri il risultato selezionato (cartella → naviga, file → avvia). |
| Esc | Chiudi la ricerca. |
| Clic su una tessera | Avvia l'elemento; la cartella naviga al suo interno (o popup, secondo le impostazioni). |
| Ctrl+clic su una tessera-cartella | Apri il mini esploratore (se attivato). |
| Trascina una tessera (modalità di modifica) | Spostala; rilasciala su una cartella per spostarla al suo interno. |
| Rilascia file sul pannello (modalità di modifica) | Aggiungili come tessere (rilascio su una cartella per aggiungerli al suo interno). |
| Clic destro su una tessera | Menu nativo di Esplora risorse più: Descrizione…, Dimensione 1×1–6×6, Rinomina, Cambia icona, Rimuovi, Sposta fuori dalla cartella, Apri nel Mini esploratore (cartelle). |
| Clic destro su una scheda | Elimina (l'ultima scheda è protetta), Rinomina, Alterna disposizione libera/griglia. |
| Trascina una scheda | Riordina all'interno di una riga o spostala in un'altra riga. |
| Clic destro su un punto vuoto del pannello | Crea cartella, Impostazioni. |
| Pulsanti ▦ / ✅ / ⚙ | Visibilità della griglia, modalità di modifica, impostazioni. |

### Mini esploratore

| Tasti / azione | Risultato |
|---|---|
| Ctrl+L / F4 / Modifica | Modifica il percorso. |
| F5 | Aggiorna la cartella. |
| Backspace | Salì di un livello. |
| Alt+← / Alt+→ | Indietro / avanti. |
| Enter / doppio clic | Apri (la cartella naviga, il file si avvia). |
| Esc | Esci dalla ricerca → annulla la modifica del percorso → chiudi la finestra. |
| Digitare nell'elenco dei file | Avvia una ricerca nella casella di ricerca. |
| Giù / Su (nella ricerca) | Spostati tra i risultati. |
| Ctrl+rotella del mouse | Dimensione del font della console (persistente). |
| Trascina il divisore | Altezza della console (persistente). |
| Pulsanti ≡ / ☰ | Attiva/disattiva il pannello laterale dei segnalibri / la barra superiore dei segnalibri. |
| Clic destro su un file | Apri, Mostra in Esplora risorse, Copia percorso. |
| Clic destro su una cartella | Apri, Aggiungi ai segnalibri, Apri in Esplora risorse. |
| Clic destro su uno spazio vuoto | Aggiorna, Copia percorso della cartella, Apri in Esplora risorse, Aggiungi la cartella corrente ai segnalibri, Apri qui una finestra della console. |
| Clic destro su un segnalibro | Modifica comando… (solo per i comandi), Rinomina…, Sposta su / Sposta giù, Rimuovi. |

### Console

Qualsiasi comando `cmd.exe` su riga singola può essere digitato ed eseguito (Enter oppure **Esegui**). La directory di lavoro viene risincronizzata con la cartella corrente prima di ogni comando. **+ Salva** memorizza il comando digitato come segnalibro (facoltativamente all'interno di un gruppo); i comandi salvati vengono eseguiti al clic. Pulsanti: **Pulisci** (cancella l'output), **Riavvia** (nuovo cmd.exe), **Nuova finestra** (una vera finestra della console nella cartella corrente). La cronologia dei comandi è disponibile con ↑ / ↓ durante la sessione.

## Limitazioni

- **Solo Windows + .NET Framework 4.8** (GDI/WinForms). Nessuna gestione DPI per monitor — l'interfaccia può apparire sfocata sugli schermi con forte ridimensionamento.
- **L'ambito di ricerca «Tutti»** indicizza **solo le unità locali fisse** (niente unità USB/di rete), con un limite di **200 000 elementi per unità**; l'indicizzazione avviene in background, quindi i risultati crescono mentre lavora («indicizzazione: N» nella riga di stato).
- La **ricerca del pannello** mostra le migliori **200** corrispondenze; la **ricerca del mini esploratore** ne restituisce fino a **400**; un elenco di file mostra al massimo **800** voci per directory.
- **La console è solo `cmd.exe`**: comandi su riga singola; i programmi interattivi/TUI (editor, pager con input da tastiera) non funzionano correttamente; il buffer di output si cancella da solo dopo ~150 000 caratteri; la codifica segue la tabella codici OEM di sistema (ad es. CP866).
- **La scorciatoia globale** è una lettera/cifra più i modificatori; la registrazione fallisce con un fumetto di notifica se un altro programma la usa già.
- **La dimensione del pannello torna alla Dimensione all'avvio a ogni avvio** — viene memorizzata solo la posizione (scelta voluta).
- **Il drag & drop e lo spostamento delle tessere richiedono la modalità di modifica** («Consenti aggiunta di icone» / pulsante ✅).
- Le tessere-cartella mostrano in anteprima al massimo **9** icone figlie; il popup della cartella mostra al massimo **4** colonne per riga.
- Gli elementi `.lnk`/`.ico` aggiunti al pannello vengono **copiati in `ico\`** in modo che sopravvivano allo spostamento degli originali.
- **Ripristina archivio** accetta solo zip creati da Tilettes («Backup adesso» / «Salva backup (zip)»).
- Gli angoli arrotondati della finestra vengono rimossi temporaneamente durante il ridimensionamento (tecnica per evitare lo sfarfallio) e ripristinati al rilascio.
- La correzione del layout copre solo la coppia EN↔RU QWERTY; gli altri layout passano inalterati.
- **Istanza singola**: avviare una seconda copia mostra semplicemente la finestra già esistente.
- L'uscita automatica dalla cartella funziona solo nella modalità «Stessa finestra» e solo quando si è all'interno di una cartella.

## Compilazione

Richiede un qualsiasi Windows con .NET Framework 4.x (il compilatore è incluso nel sistema operativo):

```
build.bat
```

oppure direttamente:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /win32icon:app.ico /out:Tilettes.exe src\*.cs
```

Le release vengono create automaticamente da GitHub Actions a ogni tag `v*`: il workflow compila le varianti exe con la stessa chiamata a csc e allega alla release dei semplici file exe (AnyCPU universale + x86 + x64 — senza zip); gli archivi automatici di GitHub con il codice sorgente sono comunque presenti nella release. Puoi anche compilare l'exe da te con `build.bat`.

## File dati (creati accanto all'EXE)

| File | Scopo |
|---|---|
| `settings.ini` | Tutte le impostazioni |
| `records.xml` | Schede, cartelle, scorciatoie, descrizioni |
| `bookmarks.xml` | Segnalibri del mini esploratore |
| `filetypes.xml` | Regole per i tipi di file |
| `ico\` | Copie degli elementi .lnk/.ico e delle icone personalizzate |

## Struttura del progetto (`src/`)

| File | Scopo |
|---|---|
| `Program.cs` | Finestra principale: schede, tessere, ricerca del pannello, popup delle cartelle, istanza singola |
| `MiniExplorerForm.cs` | Mini esploratore: navigazione, segnalibri, console incorporata |
| `SearchCore.cs` | Indicizzazione dei dischi, prefiltro a maschera di bit, punteggio fuzzy |
| `PanelSearch.cs` | Metadati ricercabili degli elementi salvati |
| `Settings.cs` / `SettingsForm.cs` | Modello delle impostazioni e finestra di dialogo |
| `FileTypes.cs` / `FileTypesForm.cs` | Regole per i tipi di file e i relativi editor |
| `bookmarks.cs` | Archiviazione dei segnalibri |
| `loc.cs` + `lang_*.cs` | Localizzazione: EN sorgente, RU in linea, tabelle ES/PT/DE/FR/IT/PL/ZH/JA |
| `UpdateChecker.cs` / `WelcomeForm.cs` | Controllo aggiornamenti (GitHub Releases) e finestra di benvenuto del primo avvio |
| `IconExtractor.cs`, `NativeContextMenu.cs`, `IniFile.cs`, `Records.cs`, `apputil.cs` | Icone, menu nativi, I/O INI, I/O dei record, avvio automatico/istanza singola/portafogli |

## Licenza

[MIT](LICENSE) — libero da usare, modificare e distribuire.

<a name="donate"></a>
## Donazioni

Se Tilettes ti è utile, puoi sostenere lo sviluppo in crypto. Le reti compatibili EVM condividono lo stesso indirizzo — invia sulla rete che preferisci:

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

<a name="changelog"></a>
## Changelog

### v0.6.0-beta (2026-10-01)

- Prestazioni: avvio più rapido (rendering differito delle schede — viene costruita solo la scheda attiva), cache delle icone persistente (le icone della shell vengono estratte una sola volta per l'intera vita di ogni tessera), metadati di ricerca raccolti una sola volta quando un elemento viene aggiunto; il campo di ricerca del mini esploratore è disattivato (il codice resta per una riattivazione futura).
- Le condivisioni di rete non bloccano mai il thread dell'interfaccia: icone e destinazioni .lnk sui percorsi di rete vengono risolte in background.
- Una skin attiva ora determina la palette chiara/scura in tutte le finestre (scegliere Mint non lascia più superfici scure); mint è il tema predefinito e tutti i font sono preimpostati a 14.
- Primo avvio: sui monitor con area di lavoro inferiore a 900px la finestra e la griglia predefinite si riducono proporzionalmente per adattarsi allo schermo.
- Hardening: strong name, VERSIONINFO, manifesto esplicito, la cattura del tasto Win è opt-in — 0 rilevamenti su VirusTotal.
- Gli asset delle release sono semplici file exe per CPU (AnyCPU/x86/x64) invece di un archivio zip; le note di release provengono da CHANGELOG.md.

### v0.5 (2026-09-30)

- Finestra di benvenuto al primo avvio (una sola volta per cartella dati): ringraziamento, avviso sulla beta + collegamento alle issue, mini-diagramma illustrato, scelta della lingua (bandierine RU/EN), consenso al controllo aggiornamenti, indirizzi per le donazioni (clic per copiare); uscita tramite «Chiudi» oppure «Chiudi e crea tessere di esempio» (Blocco note / Calcolatrice / Esplora risorse / Paint come tessere pronte). Richiamabile di nuovo dalle impostazioni.
- Controllo aggiornamenti: l'app interroga l'API pubblica di GitHub Releases per il tag più recente ogni N giorni (predefinito 3; anche il primo controllo avviene N giorni dopo l'installazione) — rigorosamente solo se l'utente lo ha consentito, altrimenti zero richieste di rete. Quando esiste una versione più recente, accanto al pulsante delle impostazioni compare una targhetta verde «Aggiorna» che apre la pagina delle release. Pulsante manuale «Controlla adesso» nelle impostazioni (riporta il risultato in una finestra di messaggio). L'auto-installazione è uno stub (TODO). Hook fittizio per testare la targhetta: `WINPANEL_MOCK_UPDATE=0.6`.
- Impostazioni: nuova sezione «Aggiornamenti» (interruttore del controllo, intervallo in giorni, pulsante di controllo immediato, stub di auto-installazione, riga donazioni con un menu a comparsa dei portafogli — un clic copia l'indirizzo — e pulsante per richiamare la finestra di benvenuto). Campo della scorciatoia modificabile: si può digitare qualsiasi combinazione Ctrl/Alt/Shift/Win + lettera/cifra (convalidata), il valore predefinito è diventato Ctrl+Q.
- Interfaccia del programma localizzata in **10 lingue**: inglese (sorgente), russo, spagnolo, portoghese, tedesco, francese, italiano, polacco, cinese (semplificato) e giapponese. La lingua si sceglie nelle impostazioni o tramite le bandierine disegnate nella finestra di benvenuto; le nuove lingue sono un file-tabella + una riga (vedi loc.cs).
- Elenco reale dei portafogli per le donazioni, raggruppato per blockchain: le reti EVM (ETH · Polygon · Base · Monad · HyperEVM) condividono un solo indirizzo; più Bitcoin, Solana e Sui.
- La versione è ora un'unica costante (`AppInfo.AppVersion`); viene mostrata nel tooltip dell'area di notifica e nella finestra di benvenuto.
- Infrastruttura GitHub: licenza MIT, FUNDING.yml (i collegamenti sponsor puntano alle ancore dei portafogli), README suddiviso in file per lingua (`README.md` EN + 9 traduzioni) per facilitarne l'estensione, workflow GitHub Actions (release con zip portatile sui tag `v*`), pagina di destinazione in docs/ per GitHub Pages. Tutti i messaggi di commit nella cronologia sono in inglese.

### v0.4 (2026-09-29)

- Rebrand: Tilettes / «Плиточки», nuova icona (risorsa exe + icona dell'area di notifica disegnata via codice).
- Funzionalità di backup completata: «Salva backup (zip)» comprime impostazioni, tessere, icone, segnalibri, cronologia di ricerca e l'exe; «Ripristina archivio» estrae gli zip creati dall'app nella cartella di lavoro senza sostituire Tilettes.exe; backup completi pianificati in autoBackup\.
- Rielaborazione della finestra delle impostazioni: ridimensionabile in verticale tramite una presa inferiore, descrizioni comando su ogni voce, campi X/Y etichettati per la dimensione all'avvio e la posizione della finestra, colonne/righe della griglia su una sola riga, casella combinata delle skin al posto della duplicata casella del tema chiaro.
- Correzioni alla scheda speculare del Menu Start (il contenuto delle cartelle non si ammassa più; layout automatico semplificato).
- Correzioni di layout per i font 14–20 (schede, altezza dello stato della ricerca, finestre di dialogo, barre del mini esploratore, gradino dell'angolo della finestra).

### v0.3 (2026-09-28)

- Sincronizzazione del Menu Start (scheda speculare, pianificata), riduzione progressiva degli interruttori della ricerca, barra delle impostazioni rapide della ricerca, modalità di modifica con selezione multipla, menu «Sposta su scheda», cattura del tasto Win, navigazione nei popup delle cartelle, evidenziazione dei percorsi nei risultati di ricerca.

### v0.1 – v0.2 (2026-09-27)

- Prime build del pannello di avvio rapido: tessere, schede, cartelle, ricerca del pannello, mini esploratore con console, regole per i tipi di file, area di notifica/avvio automatico/scorciatoia globale, personalizzazione della griglia.
