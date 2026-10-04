---
title: Tilettes
---

# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · **Italiano** · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

<!-- Per aggiungere una lingua: crea docs/README.<codice>.md (tradotto), aggiungi
     lang_xx.cs (tabella dell'interfaccia con le stringhe inglesi come chiavi, vedi loc.cs), poi
     estendi la riga delle lingue qui sopra, quella in cima a ogni altro file README
     e l'array Loc.Languages. GitHub mostra README.md (inglese) nella home del
     repository; ogni altra lingua vive in docs/ come un unico file più un collegamento. -->

Un pannello di avvio rapido per Windows: una griglia di tessere con scorciatoie, cartelle e schede, ricerca fuzzy integrata e un mini esploratore con console incorporata. Un unico EXE portatile, senza installazione, .NET Framework 4.8 (WinForms).

![Tilettes — la finestra principale](screenshot_main.png)

Versione attuale: **v1.1.0** — download da [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Changelog](#changelog). Stato: **beta**.

## Punti di forza

**Tessere e schede** — tessere da 1×1…6×6; skin (Mint, Night, Android) oltre ai temi chiaro/scuro; font e colori personalizzati; schede illimitate, trascinabili e su più righe, disposizione libera/griglia; cartelle aperte all'interno della scheda o in popup; righe extra scorrevoli sotto la griglia; modalità di modifica con selezione multipla e drag-and-drop; un nome, una descrizione (ricercabile) e un'icona personalizzati per ogni tessera; anteprime foto/video sulle tessere.

**Ricerca** — ricerca fuzzy istantanea su nomi, metadati dei programmi (descrizione / prodotto / azienda), percorsi completi e descrizioni utente; opera sui metadati memorizzati nella cache (nessuna scansione dei dischi); layout di tastiera errato corretto (`знерщи` → `python`); risultati in due blocchi — le query passate (con icone reali, ricordate tra le sessioni, potenziate) in alto, i risultati normali sotto; ogni sorgente può essere attivata o disattivata.

**Leggero** — un unico exe portatile da ~0,5 MB (517 KB), tutti i dati accanto ad esso; nessuna dipendenza oltre al .NET Framework incluso in Windows; ~30 MB di RAM; le icone vengono estratte dalla shell esattamente una volta per l'intera vita di ogni tessera in una `iconcache\` che si ripulisce da sola; gli avvii non bloccano mai il pannello (processi staccati, percorsi di rete in background); all'avvio viene renderizzata solo la scheda attiva.

**Mini esploratore** — breadcrumb, segnalibri per cartelle/comandi/gruppi, una console `cmd.exe` incorporata (cronologia, zoom del font tramite Ctrl+rotella); i comandi preferiti si eseguono con un clic, con `%1` = la cartella in esplorazione (`wt -d "%1"`); regole «apri con» per estensione/maschera con importazione/esportazione.

**Sistema** — una scheda speculare del Menu Start (incluse le app UWP/Store) sincronizzata a intervalli pianificati; scorciatoia globale, cattura facoltativa del tasto Win, area di notifica, avvio automatico; menu contestuali nativi di Esplora risorse; backup pianificati e manuali con ripristino con un clic; interfaccia in 10 lingue.

**Open source** — codice sorgente completamente aperto; release compilate da GitHub Actions a partire dal tag + SHA256SUMS.txt; esattamente una chiamata di rete in tutta l'app (il controllo aggiornamenti, previo consenso); nessuna telemetria.

## Funzionalità

- **Pannello** — tessere da 1×1…6×6, schede illimitate (trascinabili, su più righe), cartelle aperte all'interno della scheda o in popup, drag-and-drop da Esplora risorse, griglia personalizzabile (colonne/righe/trasparenza), scala delle icone.
- **Ricerca** — cerca nei nomi, nei nomi dei file, nei metadati dei programmi (FileDescription / ProductName / CompanyName), nei percorsi completi e nelle descrizioni utente; include facoltativamente la scheda speculare del Menu Start; corrispondenza fuzzy con accuratezza regolabile e correzione del layout di tastiera errato (`руддщ` → `hello`); i risultati sono ordinati per qualità della corrispondenza e i caratteri corrispondenti vengono evidenziati; le query ripetute ricevono un potenziamento basato sulla cronologia; una query digitata divide i risultati in due blocchi — le query passate memorizzate in alto, i risultati normali sotto (i duplicati esatti vengono accorpati); le righe delle ricerche passate mostrano le icone reali degli elementi memorizzati.
- **Mini esploratore** — navigazione a breadcrumb, segnalibri per cartelle/comandi/gruppi (un comando può contenere `%1`, che viene espanso con la cartella in esplorazione — ad es. `wt -d "%1"` apre Windows Terminal direttamente lì) e una console `cmd.exe` incorporata con cronologia dei comandi, comandi salvati e zoom del font tramite Ctrl+rotella. (Il modulo di ricerca dei file è disattivato dalla v0.6.0-beta — ne resta uno stub per una futura riattivazione.)
- **Regole per i tipi di file** — icone per estensione/maschera e associazioni «apri con», importazione/esportazione.
- **Integrazione con il desktop** — icona nell'area di notifica, avvio automatico con Windows, scorciatoia globale, cattura facoltativa del tasto Win, menu contestuali nativi di Esplora risorse, finestra senza bordi con ridimensionamento dai bordi.
- **Skin ed extra** — skin decorative (palette di colori propria + bordo arrotondato della finestra), scheda speculare del Menu Start ricostruita a intervalli pianificati, backup completi in `autoBackup\`, cronologia della ricerca del pannello.
- **Primo avvio e aggiornamenti** — una finestra di benvenuto mostrata una sola volta (un diagramma illustrato «tre sorgenti → griglia di tessere», scelta della lingua, consenso al controllo aggiornamenti, un collegamento di supporto, tessere di esempio) e un controllo degli aggiornamenti tramite GitHub Releases con una targhetta nell'angolo quando esiste una versione più recente.

## Primo avvio e aggiornamenti

- **Finestra di benvenuto** (solo al primissimo avvio): un ringraziamento, una nota sulla possibilità di bug e spigoli ancora grezzi con un collegamento a [Issues](https://github.com/AlexNoVibe/Tilettes/issues), un mini-diagramma illustrato (una cartella, un file .exe e una scheda .lnk → freccia → la griglia di tessere con una cella fantasma «+»), scelta della lingua (10 pulsanti a bandierina), il consenso al controllo aggiornamenti, un collegamento **Sostieni l'autore** alla [sezione donazioni](https://github.com/AlexNoVibe/Tilettes#donate) — e due pulsanti di uscita: semplice **Chiudi**, oppure **Chiudi e crea tessere di esempio** (Blocco note, Calcolatrice, Esplora risorse, Paint come tessere pronte). Può essere richiamata in qualsiasi momento tramite «Mostra di nuovo la finestra di benvenuto» nelle impostazioni.
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
| Lingua | en, ru, es, pt, de, fr, it, pl, zh, ja | ru | Lingua dell'interfaccia (10 lingue), applicata immediatamente. |

### Griglia e tessere

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Trasparenza griglia | 0–255 | 50 | Alfa delle linee della griglia. 0 = invisibile. Disegnata solo quando l'interruttore della griglia (▦) è attivo. |
| Colonne griglia | 1–100 | 16 | Celle orizzontali. Le posizioni delle tessere si agganciano a questa griglia. |
| Righe griglia | 1–100 | 16 | Celle verticali. |
| Dim. elemento predefinita | 1–6 | 2 | Dimensione delle tessere appena aggiunte (1×1 … 6×6 celle). |
| Scala icone (%) | 25–400 | 100 | Dimensione dell'icona all'interno di una tessera, in percentuale rispetto al valore predefinito. |
| Consenti aggiunta di icone | attivo/disattivo | attivo | Modalità di modifica: trascinamento delle tessere, creazione di cartelle, rilascio di file. Quando è disattivo, le tessere si avviano semplicemente al clic. |
| Etichetta tessera: due righe | attivo/disattivo | attivo | Le tessere alte dispongono l'etichetta su due righe (l'allineamento delle due righe — sinistra/centro/destra — si imposta accanto). |
| Nascondi suffisso scorciatoia | attivo/disattivo | attivo | Solo visualizzazione: « - Shortcut» / « — ярлык» (varianti del trattino, diverse lingue) viene nascosto nell'etichetta; il nome memorizzato e la ricerca restano invariati — deseleziona per ripristinarlo. |
| Nascondi estensione del file | attivo/disattivo | attivo | Solo visualizzazione: la vera estensione del percorso dell'elemento (.mp4 …) viene nascosta nell'etichetta; deseleziona per ripristinarla. |
| Skin e tema | Nessuna (scura) / Chiara / skin | Mint | Tema classico scuro o chiaro, oppure una skin decorativa con la sua palette di colori e un bordo della finestra: Android, Night, Mint (predefinita di fabbrica). |

### Cartelle

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Apri le cartelle in | Stessa finestra / Finestra popup | Stessa finestra | Il clic su una cartella naviga al suo interno nella scheda oppure apre un popup sopra tutto. |
| sec di inattività | 0–600 | 15 | Solo per la modalità «stessa finestra»: torna automaticamente indietro dopo N secondi senza attività del mouse/della tastiera. 0 = disattivo. |

### Font

Una riga per ciascuno tra **Tessere**, **Schede** e **Interfaccia**:

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| dimensione | 6–24 | 14 | Dimensione del font per il gruppo. |
| campione colore | qualsiasi colore | vuoto | Colore del testo personalizzato; vuoto = predefinito del tema. Si applica alle etichette delle tessere, ai titoli delle schede o a tutto il testo dell'interfaccia. |
| famiglia | qualsiasi font installato | Segoe UI | Famiglia di font per il gruppo. |

### Ricerca

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Accuratezza fuzzy (0–3) | 0–3 | 2 | 0 = solo corrispondenze di sottostringa; 1–3 = corrispondenza fuzzy dei refusi via via più tollerante. Le cifre contano doppio, quindi i codici numerici vengono confrontati in modo rigoroso. |
| Cerca nei metadati | attivo/disattivo | attivo | Nome del file, destinazione della scorciatoia, informazioni sulla versione (descrizione, prodotto, azienda). |
| Cerca nei percorsi completi | attivo/disattivo | attivo | Il testo del percorso completo, incluse le cartelle superiori. |
| Cerca nelle descrizioni | attivo/disattivo | attivo | Descrizioni utente (clic destro → Descrizione…). |
| Cerca nella scheda Start | attivo/disattivo | attivo | Includi la scheda speculare del Menu Start nella ricerca del pannello. |
| Font ricerca: casella | 7–30 | 14 | Dimensione del font della casella di ricerca. |
| Font ricerca: risultati | 7–30 | 14 | Dimensione del font delle righe dei risultati (l'altezza delle righe segue il font). |

### Aggiornamenti

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Controlla aggiornamenti automaticamente | attivo/disattivo | attivo | Interroga GitHub Releases per una versione più recente una volta ogni N giorni. Con la casella deselezionata non viene mai eseguito — nessuna richiesta di rete. |
| Controlla ogni N giorni | 1–365 | 3 | Frequenza del controllo. Il primo controllo avviene N giorni dopo il primissimo avvio. |
| Controlla adesso | pulsante | — | Interroga GitHub Releases immediatamente (il controllo manuale funziona anche con quello automatico disattivato). |
| Installa aggiornamenti automaticamente | attivo/disattivo | disattivo | **Stub (TODO)** — non ancora implementato. |
| ♥ Dona | pulsante | — | Apre nel browser la sezione donazioni di GitHub ([README → Donazioni](https://github.com/AlexNoVibe/Tilettes#donate)). |
| Mostra di nuovo la finestra di benvenuto | pulsante | — | Riproponi la finestra di benvenuto del primo avvio. |

### Mini esploratore (chiavi INI)

| Chiave | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Ctrl+clic su una cartella apre il Mini esploratore | attivo/disattivo | attivo | La scorciatoia Ctrl+clic sulle tessere-cartella. |
| `MiniExplorerW/H/X/Y` | W≥760, H≥520 | auto | Geometria della finestra, memorizzata alla chiusura. |
| `MiniExplorerBookmarks` | attivo/disattivo | attivo | Visibilità del pannello laterale dei segnalibri. |
| `MiniExplorerTopBar` | attivo/disattivo | attivo | Visibilità della barra orizzontale dei segnalibri. |
| `MiniExplorerConsole` | 15–85 | 40 | Altezza della console in percentuale della finestra. |
| `ConsoleFontSizeX10` | 60–280 | 140 | Dimensione del font della console ×10 (140 = 14 pt), modificata con Ctrl+rotella. |

### Avvio automatico e area di notifica

| Impostazione | Intervallo | Predefinito | Descrizione |
|---|---|---|---|
| Avvio automatico con Windows | attivo/disattivo | disattivo | Scrive in `HKCU\...\Run` («Tilettes»). |
| Dopo l'avvio automatico - vai nell'area di notifica | attivo/disattivo | disattivo | Aggiunge `--minimized`: il pannello si avvia nascosto nell'area di notifica. |
| Minimizza invece di chiudere | attivo/disattivo | attivo | ✕ / Alt+F4 nasconde nell'area di notifica (o riduce a icona) invece di uscire. L'uscita si trova nel menu dell'area di notifica. |
| Mantieni sempre l'icona nell'area di notifica | attivo/disattivo | attivo | Icona nell'area di notifica sempre visibile. |
| Ricorda la scheda attiva | attivo/disattivo | attivo | Ripristina l'ultima scheda attiva all'avvio. |
| Cattura il pulsante Start (Win) | attivo/disattivo | disattivo | Una pressione isolata di Win mostra il pannello al posto del menu Start (hook di tastiera di basso livello); le combinazioni Win+tasto passano attraverso. Funzione da attivare esplicitamente — disattiva per impostazione predefinita. |

### Backup e sincronizzazione del Menu Start

- **Backup adesso** — backup zip completo in `autoBackup\` (impostazioni, tessere, icone, segnalibri, cronologia di ricerca, exe); pianificato tramite «Backup ogni N giorni» (predefinito 7, 0 = disattivo), creato ~3 minuti dopo l'avvio quando la scadenza è raggiunta.
- **Salva backup (zip)** — lo stesso archivio in un file scelto dall'utente.
- **Ripristina archivio…** — si aspetta uno zip creato da Tilettes stesso; i file vengono estratti nella cartella di lavoro, `Tilettes.exe` non viene mai sostituito.
- **Sincronizza Menu Start adesso** / ogni N ore (predefinito 24, 0 = disattivo) — ricostruisce la scheda speculare del Menu Start.

## Scorciatoie e comandi

### Pannello principale

| Tasti / azione | Risultato |
|---|---|
| Scorciatoia globale (predefinita Ctrl+Q) | Mostra / attiva il pannello. |
| Pressione isolata di Win (opt-in) | Mostra / nasconde il pannello al posto del menu Start — attiva «Cattura il pulsante Start (Win)» nelle impostazioni. |
| Digita semplicemente un testo, oppure Ctrl+F | Apri la ricerca del pannello. |
| ↓ | Passa all'elenco dei risultati. |
| Enter | Apri il risultato selezionato (cartella → naviga, file → avvia). |
| Esc | Chiudi la ricerca. |
| Clic su una tessera | Avvia l'elemento; la cartella naviga al suo interno (o popup, secondo le impostazioni). |
| Ctrl+clic su una tessera-cartella | Apri il mini esploratore (se attivato). |
| Trascina una tessera (modalità di modifica) | Spostala; rilasciala su una cartella per spostarla al suo interno. |
| Rilascia file sul pannello (modalità di modifica) | Aggiungili come tessere (rilascio su una cartella per aggiungerli al suo interno). |
| Clic destro su una tessera | Menu nativo di Esplora risorse più: Descrizione…, Dimensione 1×1–6×6, Rinomina, Cambia icona, Rimuovi, Sposta fuori dalla cartella, Sposta nella scheda ▸, Apri nel Mini esploratore (cartelle). |
| Clic destro su una scheda | Elimina (l'ultima scheda è protetta), Rinomina, Alterna disposizione libera/griglia. |
| Trascina una scheda | Riordina all'interno di una riga o spostala in un'altra riga. |
| Clic destro su un punto vuoto del pannello | Crea cartella, Impostazioni. |
| Pulsanti ▦ / ✅ / ⚙ | Visibilità della griglia, modalità di modifica, impostazioni. ✅ è a tre stati: disattivo / modifica / selezione multipla — nella selezione multipla fai clic sulle tessere per sceglierne diverse, poi rimuovile o spostale in blocco tramite il menu del clic destro. |

### Mini esploratore

| Tasti / azione | Risultato |
|---|---|
| Ctrl+L / F4 / Modifica | Modifica il percorso. |
| F5 | Aggiorna la cartella. |
| Backspace | Salì di un livello. |
| Alt+← / Alt+→ | Indietro / avanti. |
| Enter / doppio clic | Apri (la cartella naviga, il file si avvia). |
| Esc | Annulla la modifica del percorso → chiudi la finestra. |
| Ctrl+rotella del mouse | Dimensione del font della console (persistente). |
| Trascina il divisore | Altezza della console (persistente). |
| Pulsanti ≡ / ☰ | Attiva/disattiva il pannello laterale dei segnalibri / la barra superiore dei segnalibri. |
| Clic destro su un file | Apri, Mostra in Esplora risorse, Copia percorso. |
| Clic destro su una cartella | Apri, Aggiungi ai segnalibri, Apri in Esplora risorse. |
| Clic destro su uno spazio vuoto | Aggiorna, Copia percorso della cartella, Apri in Esplora risorse, Aggiungi la cartella corrente ai segnalibri, Apri qui una finestra della console. |
| Clic destro su un segnalibro | Modifica comando… (solo per i comandi), Rinomina…, Sposta su / Sposta giù, Rimuovi. |

### Console

Qualsiasi comando `cmd.exe` su riga singola può essere digitato ed eseguito (Enter oppure **Esegui**). La directory di lavoro viene risincronizzata con la cartella corrente prima di ogni comando. **+ Salva** memorizza il comando digitato come segnalibro (facoltativamente all'interno di un gruppo); i comandi salvati vengono eseguiti al clic, e un comando può contenere `%1` — la cartella in esplorazione (un gruppo «CMD» predefinito fornito con l'app include un segnalibro `wt -d "%1"` per aprire Windows Terminal direttamente lì). Pulsanti: **Pulisci** (cancella l'output), **Riavvia** (nuovo cmd.exe), **Nuova finestra** (una vera finestra della console nella cartella corrente). La cronologia dei comandi è disponibile con ↑ / ↓ durante la sessione.

## Limitazioni

- **Solo Windows + .NET Framework 4.8** (GDI/WinForms). Nessuna gestione DPI per monitor — l'interfaccia può apparire sfocata sugli schermi con forte ridimensionamento.
- **La ricerca dei file del mini esploratore è disattivata** dalla v0.6.0-beta: il modulo di indicizzazione dei dischi (solo unità locali fisse, con un limite di 200 000 elementi per unità) resta come stub per una futura riattivazione. La ricerca del pannello opera solo sui metadati memorizzati nella cache degli elementi salvati — nessuna indicizzazione dei dischi.
- La **ricerca del pannello** mostra le migliori **200** corrispondenze; un elenco di file mostra al massimo **800** voci per directory.
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
- **Le anteprime delle tessere foto/video** provengono dalla cache delle anteprime della shell di Windows. Un **video appena aggiunto** può mostrare un'icona generica finché Esplora risorse non genera la sua anteprima (apri una volta la cartella che lo contiene in Esplora risorse). Una volta mostrata, l'anteprima viene conservata nella cache dell'app stessa e sopravvive allo svuotamento della cache; se l'anteprima non compare mai, significa che il sistema non ha il codec per quel file (ad es. HEVC senza l'estensione).

## Compilazione

Richiede un qualsiasi Windows con .NET Framework 4.x (il compilatore è incluso nel sistema operativo):

```
build.bat          rem → Tilettes.exe (universal AnyCPU)
```

oppure direttamente:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ /win32icon:app.ico /win32manifest:app.manifest /keyfile:Tilettes.snk /out:Tilettes.exe src\*.cs
```

Le release vengono create automaticamente da GitHub Actions a ogni tag `v*`: il workflow compila l'exe universale con la stessa chiamata a csc e allega alla release un unico semplice file exe (senza zip) con un `SHA256SUMS.txt` del relativo checksum (il checksum è aggiunto anche alle note di release); gli archivi automatici di GitHub con il codice sorgente sono presenti anch'essi nella release. La build universale AnyCPU viene eseguita come processo a 64 bit su Windows a 64 bit e come processo a 32 bit su Windows a 32 bit. Puoi anche compilare l'exe da te con `build.bat`.

## Falsi positivi degli antivirus

Alcuni prodotti antivirus segnalano talvolta `Tilettes.exe` con un rilevamento euristico generico (le piccole utility non firmate che installano un hook di tastiera globale, analizzano scorciatoie `.lnk` ed estraggono icone della shell corrispondono proprio allo schema che le euristiche non amano). Tratta un simile rilevamento come un **falso positivo** finché non viene dimostrato il contrario — e non devi fidarti del binario distribuito, perché tutto è verificabile:

- Il **codice sorgente è completamente aperto** in questo repository — ogni riga che finisce nell'exe è qui.
- **Le release vengono compilate automaticamente da GitHub Actions** a partire dal commit con tag, su runner ospitati da Microsoft (`.github/workflows/build.yml`). Nulla viene caricato a mano: l'exe allegato a una release è compilato esattamente dal sorgente che vedi a quel tag.
- Puoi **compilare l'exe da te** con `build.bat` (il compilatore C# è incluso in Windows) ed eseguire la tua build al posto di quella scaricata.
- La chiave di strong name (`Tilettes.snk`) viene generata solo per la compilazione; uno strong name dimostra l'identità dell'assembly, non la affidabilità di un fornitore — guarda invece il codice e la pipeline di compilazione.
- Dalla v0.6.16 l'app inoltre **non propone release più giovani di 24 ore** (nel proprio controllo aggiornamenti), così un exe appena pubblicato non si diffonde durante il suo primo giorno, nel tempo che i verdetti cloud degli antivirus impiegano ad assestarsi.

## File dati (creati accanto all'EXE)

| File | Scopo |
|---|---|
| `settings.ini` | Tutte le impostazioni |
| `records.xml` | Schede, cartelle, scorciatoie, descrizioni |
| `bookmarks.xml` | Segnalibri del mini esploratore |
| `filetypes.xml` | Regole per i tipi di file |
| `searchHistory.xml` | Cronologia della ricerca del pannello («ricerche passate») |
| `ico\` | Copie degli elementi .lnk/.ico e delle icone personalizzate |
| `iconcache\` | Cache persistente delle icone (le icone vengono estratte una sola volta per l'intera vita di ogni tessera) |
| `autoBackup\` | Zip dei backup completi pianificati |
| `log.txt` | Log dell'applicazione (le righe ripetute vengono deduplicate) |

## Struttura del progetto (`src/`)

| File | Scopo |
|---|---|
| `Program.cs` | Finestra principale: schede, tessere, ricerca del pannello, popup delle cartelle, istanza singola |
| `miniexplorerform.cs` | Mini esploratore: navigazione, segnalibri, console incorporata |
| `searchcore.cs` | Punteggio fuzzy, varianti di layout di tastiera e prefiltro a maschera di bit (il motore di ricerca del pannello); la sua parte di indicizzazione dei dischi (ricerca dei file del mini esploratore) è attualmente disattivata |
| `panelsearch.cs` | Metadati ricercabili degli elementi salvati |
| `Settings.cs` / `SettingsForm.cs` | Modello delle impostazioni e finestra di dialogo |
| `filetypes.cs` / `filetypesform.cs` | Regole per i tipi di file e i relativi editor |
| `bookmarks.cs` | Archiviazione dei segnalibri |
| `Skins.cs` | Skin decorative: palette e bordo della finestra |
| `StartMenuSync.cs` + `ShellItemApi.cs` | Scheda speculare del Menu Start, incluse le app UWP/Store |
| `BackupManager.cs` + `ZipWriter.cs` / `ZipReader.cs` | Backup pianificati e manuali |
| `SearchHistory.cs` | Cronologia della ricerca del pannello («ricerche passate», potenziamento dei risultati) |
| `AppLog.cs` | Scrittore del `log.txt` con deduplica |
| `loc.cs` + `lang_*.cs` | Localizzazione: EN sorgente, RU in linea, tabelle ES/PT/DE/FR/IT/PL/ZH/JA |
| `UpdateChecker.cs` / `WelcomeForm.cs` | Controllo aggiornamenti (GitHub Releases) e finestra di benvenuto del primo avvio |
| `IconExtractor.cs`, `NativeContextMenu.cs`, `IniFile.cs`, `Records.cs`, `apputil.cs` | Icone, menu nativi, I/O INI, I/O dei record, avvio automatico/istanza singola/portafogli |
| `AssemblyInfo.cs` | Metadati VERSIONINFO / assembly |

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

### v0.6.1 (2026-10-01)

- La versione viene mostrata nell'angolo in alto a destra della finestra delle impostazioni.
- Tenendo premuto **Ctrl** — le tessere mostrano i loro nomi completi non troncati (il font dell'etichetta si riduce per adattarsi); al rilascio torna tutto normale. Interruttore nelle impostazioni («Tieni premuto Ctrl — mostra i nomi completi sulle tessere»).
- Tooltip delle tessere ridisegnati: descrizione (o il nome completo in assenza di descrizione) + un separatore + i percorsi completi; gli elementi `.lnk` mostrano sia la scorciatoia che la sua destinazione risolta.
- Anteprime multimediali: un'anteprima, una volta ottenuta, viene conservata nella cache dell'app stessa e sopravvive allo svuotamento della cache delle anteprime di Windows; il clic destro su un collegamento morto ora mostra le azioni proprie della tessera invece di non fare nulla; l'estrattore di fotogrammi di Media Foundation resta come fallback degradato (vedi REPORT.md).
- b2.bat chiude l'app in esecuzione prima di compilare.

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
