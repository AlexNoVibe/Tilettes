---
title: Tilettes
---

# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · **Deutsch** · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

<!-- Sprache hinzufügen: docs/README.<code>.md anlegen (übersetzen), dann
     lang_xx.cs hinzufügen (UI-Tabelle mit den englischen Zeichenketten als
     Schlüssel, siehe loc.cs) und schließlich die Sprachzeile oben, die Zeile
     am Kopf jeder anderen README-Datei und das Array Loc.Languages erweitern.
     GitHub zeigt auf der Repo-Startseite README.md (Englisch); alle anderen
     Sprachen liegen in docs/ — je eine Datei plus ein Link. -->

Ein Schnellstart-Panel für Windows: ein Kachelraster mit Verknüpfungen, Ordnern und Tabs, integrierte Fuzzy-Suche und ein Mini-Explorer mit eingebauter Konsole. Eine einzelne portable EXE, ohne Installation, .NET Framework 4.8 (WinForms).

![Tilettes — das Hauptfenster](screenshot_main.png)

Aktuelle Version: **v1.1.0** — Download über [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Changelog](#changelog). Status: **Beta**.

## Highlights

**Kacheln & Tabs** — Kacheln 1×1…6×6; Skins (Mint, Night, Android) plus helle/dunkle Themes; eigene Schriften und Farben; unbegrenzt viele verschiebbare mehrzeilige Tabs, Frei/Raster-Layout; Ordner im Tab oder als Popups; scrollbare Zusatzzeilen unterhalb des Rasters; Mehrfachauswahl im Bearbeitungsmodus und Drag-and-drop; eigener Name, Beschreibung (durchsuchbar) und Symbol pro Kachel; Foto-/Video-Vorschauen in Kacheln.

**Suche** — sofortige Fuzzy-Suche über Namen, Programmmetadaten (Beschreibung / Produkt / Firma), vollständige Pfade und eigene Beschreibungen; läuft über zwischengespeicherte Metadaten (ohne Festplatten-Scans); Korrektur eines falschen Tastaturlayouts (`знерщи` → `python`); Ergebnisse in zwei Blöcken — frühere Anfragen (mit echten Symbolen, sitzungsübergreifend gemerkt, geboostet) oben, reguläre Ergebnisse darunter; jede Quelle lässt sich ein-/ausschalten.

**Leichtgewichtig** — eine einzige portable EXE von ~0,5 MB (517 KB), alle Daten daneben; keine Abhängigkeiten außer dem in Windows enthaltenen .NET Framework; ~30 MB RAM; Symbole werden aus der Shell genau einmal pro Kachel-Lebensdauer in einen selbst bereinigenden `iconcache\` extrahiert; Starts blockieren das Panel nie (entkoppelte Prozesse, Netzwerkpfade im Hintergrund); beim Start wird nur der aktive Tab aufgebaut.

**Mini-Explorer** — Breadcrumbs, Lesezeichen für Ordner/Befehle/Gruppen, eine eingebettete `cmd.exe`-Konsole (Befehlsverlauf, Schriftzoom per Ctrl+Mausrad); Lieblingsbefehle starten mit einem Klick, mit `%1` = dem gerade durchsuchten Ordner (`wt -d "%1"`); „Öffnen mit“-Regeln pro Erweiterung oder Maske, mit Import/Export.

**System** — ein gespiegelter Startmenü-Tab (einschließlich UWP-/Store-Apps), der nach Zeitplan synchronisiert wird; globaler Hotkey, optionales Abfangen der Win-Taste, Tray, Autostart; native Explorer-Kontextmenüs; geplante und manuelle Backups mit Wiederherstellung per Klick; Oberfläche in 10 Sprachen.

**Open Source** — vollständig quelloffen; Releases werden von GitHub Actions aus dem Tag gebaut, plus SHA256SUMS.txt; genau ein Netzwerkaufruf in der gesamten App (die Opt-in-Update-Prüfung); keine Telemetrie.

## Funktionen

- **Panel** — Kacheln in den Größen 1×1…6×6, unbegrenzt viele Tabs (verschiebbar, mehrzeilig), Ordner, die sich direkt im Tab oder als Popup öffnen lassen, Drag-and-drop aus dem Explorer, frei konfigurierbares Raster (Spalten/Zeilen/Transparenz), Symbolskalierung.
- **Suche** — durchsucht Namen, Dateinamen, Programmmetadaten (FileDescription / ProductName / CompanyName), vollständige Pfade und Benutzerbeschreibungen; optional wird der gespiegelte Startmenü-Tab einbezogen; Fuzzy-Abgleich mit einstellbarer Genauigkeit und Korrektur eines falschen Tastaturlayouts (`руддщ` → `hello`); Ergebnisse werden nach Trefferqualität sortiert, übereinstimmende Zeichen werden hervorgehoben; wiederholte Anfragen erhalten einen Verlaufsbonus; eine getippte Anfrage teilt die Ergebnisse in zwei Blöcke — gemerkte frühere Anfragen oben, reguläre Ergebnisse darunter (vollständige Duplikate werden zusammengefasst); Zeilen früherer Suchen zeigen die echten Symbole der gemerkten Elemente.
- **Mini-Explorer** — Breadcrumb-Navigation, Lesezeichen für Ordner/Befehle/Gruppen (ein Befehl darf `%1` enthalten, das durch den gerade durchsuchten Ordner ersetzt wird — z. B. öffnet `wt -d "%1"` Windows Terminal direkt dort), und eine eingebettete `cmd.exe`-Konsole mit Befehlsverlauf, gespeicherten Befehlen und Schriftzoom per Ctrl+Mausrad. (Das Dateisuche-Modul ist seit v0.6.0-beta deaktiviert — ein Stub bleibt zur späteren Reaktivierung erhalten.)
- **Dateityp-Regeln** — Symbole und „Öffnen mit“-Zuordnungen pro Erweiterung oder Maske, Import/Export.
- **Desktop-Integration** — Tray-Symbol, Autostart mit Windows, globaler Hotkey, optionales Abfangen der Win-Taste, native Explorer-Kontextmenüs, rahmenloses Fenster mit Größenänderung an den Rändern.
- **Skins & Extras** — dekorative Skins (eigene Farbpalette + abgerundeter Fensterrand), ein nach Zeitplan neu aufgebauter gespiegelter Startmenü-Tab, vollständige Backups nach `autoBackup\`, Suchverlauf des Panels.
- **Erster Start & Updates** — ein einmaliges Willkommensfenster (ein gemaltes Diagramm „drei Quellen → Kachelraster“, Sprachwahl, Einwilligung zur Update-Prüfung, ein Unterstützungs-Link, Beispielskacheln) und eine Update-Prüfung gegen GitHub Releases mit einer Plakette in der Ecke, sobald es eine neuere Version gibt.

## Erster Start & Updates

- **Willkommensfenster** (nur beim allerersten Start): ein Dankeschön, ein Hinweis, dass Bugs und raue Kanten möglich sind, mit Link zu [Issues](https://github.com/AlexNoVibe/Tilettes/issues), ein gemaltes Mini-Diagramm (eine Ordner-, eine .exe- und eine .lnk-Karte → Pfeil → das Kachelraster mit einer „+“-Geisterzelle), Sprachwahl (10 Flaggen-Buttons), die Einwilligung zur Update-Prüfung, ein **Autor unterstützen**-Link zum [Spendenabschnitt](https://github.com/AlexNoVibe/Tilettes#donate) — und zwei Buttons zum Beenden: einfach **Schließen** oder **Schließen & Beispielskacheln erstellen** (Editor, Rechner, Explorer und Paint als fertige Kacheln). Es lässt sich jederzeit über „Willkommensfenster erneut anzeigen“ in den Einstellungen wieder aufrufen.
- **Update-Prüfung** — die App fragt die öffentliche GitHub-Releases-API alle N Tage ab (Standard 3; die erste Prüfung erfolgt ebenfalls N Tage nach dem allerersten Start, nicht sofort). Es wird nichts versendet, und mit deaktivierter Prüfung in den Einstellungen wird gar keine Netzwerkanfrage gestellt. Existiert ein neuerer Tag, erscheint eine grüne **⟳ Update**-Plakette neben dem Einstellungen-Button und öffnet die Seite [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest). „Jetzt prüfen“ in den Einstellungen führt unabhängig vom Intervall eine manuelle Prüfung durch (das Ergebnis wird in einem Meldungsfenster angezeigt). Die automatische Installation ist vorerst ein Stub (TODO).
- **Test-Hook** — App mit `WINPANEL_MOCK_UPDATE=0.6` starten, um die Update-Plakette anzuzeigen, als existierte eine neuere Version (ganz ohne Netzwerk).

## Einstellungsreferenz

Alle Einstellungen finden sich in einem Dialog (⚙-Button / Tray-Menü) und werden in `settings.ini` gespeichert.

### Start & Fenster

| Einstellung | Bereich | Standard | Beschreibung |
|---|---|---|---|
| Startgröße (B × H) | 200–4000 | 900 × 800 | Panelgröße bei jedem Start. Größenänderungen während einer Sitzung werden nicht gespeichert — nur die Position. |
| Fensterposition (X, Y) | −4000…4000 | 100, 100 | Bildschirmposition beim Start. Wird beim Verschieben des Fensters automatisch aktualisiert. |
| Hotkey zum Anzeigen des Fensters | Presets + eigene | Ctrl+Q | Globaler Hotkey, der das Panel anzeigt/aktiviert. Wähle einen Preset (None, Ctrl+Q, Ctrl+Shift+Q, Alt+Q, Ctrl+J, …) oder tippe eine beliebige `Mod+Taste`-Kombination (Ctrl/Alt/Shift/Win + Buchstabe oder Ziffer) direkt in das editierbare Feld; nicht auswertbare Eingaben werden mit einer Erklärung abgelehnt. |
| Sprache | en, ru, es, pt, de, fr, it, pl, zh, ja | ru | Sprache der Oberfläche (10 Sprachen), wird sofort übernommen. |

### Raster & Kacheln

| Einstellung | Bereich | Standard | Beschreibung |
|---|---|---|---|
| Raster-Transparenz | 0–255 | 50 | Alphawert der Rasterlinien. 0 = unsichtbar. Wird nur gezeichnet, wenn der Raster-Schalter (▦) aktiv ist. |
| Raster-Spalten | 1–100 | 16 | Horizontale Zellen. Kachelpositionen rasten an diesem Raster ein. |
| Raster-Zeilen | 1–100 | 16 | Vertikale Zellen. |
| Std. Elementgröße | 1–6 | 2 | Größe neu hinzugefügter Kacheln (1×1 … 6×6 Zellen). |
| Symbolskalierung (%) | 25–400 | 100 | Symbolgröße innerhalb einer Kachel, in Prozent des Standardwerts. |
| Symbole hinzufügen erlauben | ein/aus | ein | Bearbeitungsmodus: Kacheln verschieben, Ordner anlegen, Dateien ablegen. Wenn aus, starten Kacheln einfach per Klick. |
| Kachel-Beschriftung: zwei Zeilen | ein/aus | ein | Hohe Kacheln brechen die Beschriftung auf zwei Zeilen um (die Ausrichtung der beiden Zeilen — links/mitte/rechts — wird daneben eingestellt). |
| Verknüpfungs-Suffix ausblenden | ein/aus | ein | Nur Anzeige: „ - Shortcut“ / „ — ярлык“ (Bindestrich-Varianten, mehrere Sprachen) wird auf der Beschriftung ausgeblendet; der gespeicherte Name und die Suche bleiben unberührt — Häkchen entfernen, um ihn wieder anzuzeigen. |
| Dateierweiterung ausblenden | ein/aus | ein | Nur Anzeige: Die echte Erweiterung des Elementpfads (.mp4 …) wird auf der Beschriftung ausgeblendet; Häkchen entfernen, um sie wieder anzuzeigen. |
| Skin & Thema | Ohne (dunkel) / Hell / Skins | Mint | Klassisches dunkles oder helles Theme, oder ein dekorativer Skin mit eigener Palette und Fensterrand: Android, Night, Mint (werksseitiger Standard). |

### Ordner

| Einstellung | Bereich | Standard | Beschreibung |
|---|---|---|---|
| Ordner öffnen in | Gleiches Fenster / Popup-Fenster | Gleiches Fenster | Ein Klick auf einen Ordner navigiert innerhalb des Tabs oder öffnet ein Popup über allem. |
| Sek. Inaktivität | 0–600 | 15 | Nur im Modus „Gleiches Fenster“: nach N Sekunden ohne Maus-/Tastaturaktivität automatisch eine Ebene nach oben zurück. 0 = aus. |

### Schriftarten

Je eine Zeile für **Kacheln**, **Tabs** und **Oberfläche**:

| Einstellung | Bereich | Standard | Beschreibung |
|---|---|---|---|
| Größe | 6–24 | 14 | Schriftgröße der Gruppe. |
| Farbfeld | beliebige Farbe | leer | Eigene Textfarbe; leer = Theme-Standard. Gilt für Kachelbeschriftungen, Tab-Titel oder den gesamten Text der Oberfläche. |
| Familie | beliebige installierte Schrift | Segoe UI | Schriftfamilie der Gruppe. |

### Suche

| Einstellung | Bereich | Standard | Beschreibung |
|---|---|---|---|
| Fuzzy-Genauigkeit (0–3) | 0–3 | 2 | 0 = nur Teilstring-Treffer; 1–3 = zunehmend toleranter Tippfehler-/Fuzzy-Abgleich. Ziffern zählen doppelt, daher werden numerische Codes streng abgeglichen. |
| In Metadaten suchen | ein/aus | ein | Dateiname, Verknüpfungsziel, Versionsinformationen (Beschreibung, Produkt, Firma). |
| In vollständigen Pfaden suchen | ein/aus | ein | Der Text des vollständigen Pfads, einschließlich übergeordneter Ordner. |
| In Beschreibungen suchen | ein/aus | ein | Benutzerbeschreibungen (Rechtsklick → Beschreibung…). |
| Im Start-Tab suchen | ein/aus | ein | Bezieht den gespiegelten Startmenü-Tab in die Panel-Suche ein. |
| Suchschrift: Eingabefeld | 7–30 | 14 | Schriftgröße des Suchfelds. |
| Suchschrift: Ergebnisse | 7–30 | 14 | Schriftgröße der Ergebniszeilen (die Zeilenhöhe folgt der Schrift). |

### Updates

| Einstellung | Bereich | Standard | Beschreibung |
|---|---|---|---|
| Updates automatisch prüfen | ein/aus | ein | Fragt GitHub Releases alle N Tage nach einer neueren Version. Läuft bei deaktiviertem Häkchen nie — keinerlei Netzwerkanfrage. |
| Alle N Tage prüfen | 1–365 | 3 | Wie oft geprüft wird. Die erste Prüfung erfolgt N Tage nach dem allerersten Start. |
| Jetzt prüfen | Button | — | Fragt GitHub Releases sofort ab (die manuelle Prüfung funktioniert auch bei ausgeschalteter Automatik). |
| Updates automatisch installieren | ein/aus | aus | **Stub (TODO)** — noch nicht implementiert. |
| ♥ Spenden | Button | — | Öffnet den GitHub-Spendenabschnitt ([README → Spenden](https://github.com/AlexNoVibe/Tilettes#donate)) im Browser. |
| Willkommensfenster erneut anzeigen | Button | — | Ruft das Willkommensfenster des ersten Starts erneut auf. |

### Mini-Explorer (INI-Schlüssel)

| Schlüssel | Bereich | Standard | Beschreibung |
|---|---|---|---|
| Ctrl+Klick auf einen Ordner öffnet den Mini-Explorer | ein/aus | ein | Der Ctrl+Klick-Kurzbefehl auf Ordnerkacheln. |
| `MiniExplorerW/H/X/Y` | W≥760, H≥520 | automatisch | Fenstergeometrie, wird beim Schließen gespeichert. |
| `MiniExplorerBookmarks` | ein/aus | ein | Sichtbarkeit des seitlichen Lesezeichen-Panels. |
| `MiniExplorerTopBar` | ein/aus | ein | Sichtbarkeit der horizontalen Lesezeichen-Leiste. |
| `MiniExplorerConsole` | 15–85 | 40 | Konsolenhöhe in Prozent des Fensters. |
| `ConsoleFontSizeX10` | 60–280 | 140 | Konsolenschriftgröße ×10 (140 = 14 pt), geändert mit Ctrl+Mausrad. |

### Autostart & Tray

| Einstellung | Bereich | Standard | Beschreibung |
|---|---|---|---|
| Autostart mit Windows | ein/aus | aus | Schreibt `HKCU\...\Run` („Tilettes“). |
| Nach Autostart in den Tray | ein/aus | aus | Fügt `--minimized` hinzu: das Panel startet versteckt im Tray. |
| Minimieren statt Schließen | ein/aus | ein | ✕ / Alt+F4 versteckt im Tray (oder minimiert), statt das Programm zu beenden. Beenden geht über das Tray-Menü. |
| Tray-Symbol immer behalten | ein/aus | ein | Das Tray-Symbol ist jederzeit sichtbar. |
| Aktiven Tab merken | ein/aus | ein | Stellt beim Start den zuletzt aktiven Tab wieder her. |
| Start-Button abfangen (Win) | ein/aus | aus | Ein einzelnes Drücken von Win zeigt das Panel statt des Startmenüs (Low-Level-Tastatur-Hook); Win+Taste-Kombinationen werden durchgereicht. Opt-in — standardmäßig aus. |

### Backup & Startmenü-Sync

- **Jetzt Backup erstellen** — vollständiges Backup-Zip nach `autoBackup\` (Einstellungen, Kacheln, Symbole, Lesezeichen, Suchverlauf, die EXE); geplant über „Backup alle N Tage“ (Standard 7, 0 = aus), wird ca. 3 Minuten nach dem Start erstellt, wenn es fällig ist.
- **Backup speichern (Zip)** — dasselbe Archiv in eine selbst gewählte Datei.
- **Archiv wiederherstellen…** — erwartet ein Zip, das von Tilettes selbst erstellt wurde; die Dateien werden in den Arbeitsordner entpackt, `Tilettes.exe` wird niemals ersetzt.
- **Startmenü jetzt synchronisieren** / alle N Stunden (Standard 24, 0 = aus) — baut den gespiegelten Startmenü-Tab neu auf.

## Hotkeys & Befehle

### Hauptpanel

| Tasten / Aktion | Ergebnis |
|---|---|
| Hotkey (Standard Ctrl+Q) | Panel anzeigen / aktivieren. |
| Einzelnes Drücken von Win (Opt-in) | Panel anzeigen/ausblenden statt des Startmenüs — aktiviere „Start-Button abfangen (Win)“ in den Einstellungen. |
| Einfach Text eingeben, oder Ctrl+F | Öffnet die Panel-Suche. |
| ↓ | In die Ergebnisliste springen. |
| Enter | Öffnet das ausgewählte Ergebnis (Ordner → navigieren, Datei → starten). |
| Esc | Schließt die Suche. |
| Klick auf eine Kachel | Startet das Element; ein Ordner navigiert (oder Popup, je nach Einstellung). |
| Ctrl+Klick auf eine Ordnerkachel | Öffnet den Mini-Explorer (falls aktiviert). |
| Kachel ziehen (Bearbeitungsmodus) | Verschieben; auf einen Ordner fallen lassen, um die Kachel hinein zu verschieben. |
| Dateien auf das Panel ziehen (Bearbeitungsmodus) | Als Kacheln hinzufügen (auf einen Ordner fallen lassen, um sie darin hinzuzufügen). |
| Rechtsklick auf eine Kachel | Natives Explorer-Menü plus: Beschreibung…, Größe 1×1–6×6, Umbenennen, Symbol ändern, Entfernen, Aus Ordner herausverschieben, Auf Tab verschieben ▸, Im Mini-Explorer öffnen (Ordner). |
| Rechtsklick auf einen Tab | Löschen (der letzte Tab ist geschützt), Umbenennen, Frei/Raster-Layout umschalten. |
| Tab ziehen | Innerhalb einer Zeile neu anordnen oder in eine andere Zeile verschieben. |
| Rechtsklick auf die leere Panel-Fläche | Ordner erstellen, Einstellungen. |
| Buttons ▦ / ✅ / ⚙ | Raster-Sichtbarkeit, Bearbeitungsmodus, Einstellungen. ✅ hat drei Zustände: aus / Bearbeiten / Mehrfachauswahl — bei der Mehrfachauswahl klickst du Kacheln an, um mehrere auszuwählen, und entfernst oder verschiebst sie dann gesammelt über das Rechtsklick-Menü. |

### Mini-Explorer

| Tasten / Aktion | Ergebnis |
|---|---|
| Ctrl+L / F4 / Bearbeiten | Pfad bearbeiten. |
| F5 | Ordner aktualisieren. |
| Backspace | Eine Ebene nach oben. |
| Alt+← / Alt+→ | Zurück / vor. |
| Enter / Doppelklick | Öffnen (Ordner navigiert, Datei startet). |
| Esc | Pfadbearbeitung abbrechen → Fenster schließen. |
| Ctrl+Mausrad | Konsolenschriftgröße (wird gespeichert). |
| Trennleiste ziehen | Konsolenhöhe (wird gespeichert). |
| Buttons ≡ / ☰ | Seitliches Lesezeichen-Panel / obere Lesezeichen-Leiste umschalten. |
| Rechtsklick auf eine Datei | Öffnen, Im Explorer anzeigen, Pfad kopieren. |
| Rechtsklick auf einen Ordner | Öffnen, Zu Lesezeichen hinzufügen, Im Explorer öffnen. |
| Rechtsklick auf freie Fläche | Aktualisieren, Ordnerpfad kopieren, Im Explorer öffnen, Aktuellen Ordner zu Lesezeichen hinzufügen, Konsolenfenster hier öffnen. |
| Rechtsklick auf ein Lesezeichen | Befehl bearbeiten… (nur bei Befehlen), Umbenennen…, Nach oben / nach unten, Entfernen. |

### Konsole

Jeder einzeilige `cmd.exe`-Befehl kann eingegeben und ausgeführt werden (Enter oder **Ausführen**). Das Arbeitsverzeichnis wird vor jedem Befehl erneut mit dem aktuellen Ordner synchronisiert. **+ Speichern** legt den eingegebenen Befehl als Lesezeichen ab (optional in einer Gruppe); gespeicherte Befehle starten per Klick, und ein Befehl darf `%1` enthalten — den gerade durchsuchten Ordner (eine werkseitig angelegte Standardgruppe „CMD“ liefert das Lesezeichen `wt -d "%1"`, um Windows Terminal direkt dort zu öffnen). Buttons: **Leeren** (Ausgabe löschen), **Neustart** (neues cmd.exe), **Neues Fenster** (ein echtes Konsolenfenster im aktuellen Ordner). Die Befehlshistorie ist während der Sitzung mit ↑ / ↓ verfügbar.

## Einschränkungen

- **Nur Windows + .NET Framework 4.8** (GDI/WinForms). Keine Per-Monitor-DPI-Unterstützung — die Oberfläche kann auf stark skalierten Displays unscharf wirken.
- **Die Dateisuche des Mini-Explorers ist deaktiviert** (seit v0.6.0-beta): Das Festplatten-Indizierungsmodul (nur feste lokale Laufwerke, begrenzt auf 200 000 Einträge pro Laufwerk) bleibt als Stub zur späteren Reaktivierung erhalten. Die Panel-Suche läuft ausschließlich über die zwischengespeicherten Metadaten der gespeicherten Elemente — keine Festplatten-Indizierung.
- **Die Panel-Suche** zeigt die besten **200** Treffer; eine Dateiliste zeigt höchstens **800** Einträge pro Verzeichnis.
- **Die Konsole ist nur `cmd.exe`**: einzeilige Befehle; interaktive/TUI-Programme (Editoren, Pager mit Tastatureingabe) funktionieren nicht richtig; der Ausgabepuffer leert sich nach ~150 000 Zeichen automatisch; die Kodierung folgt der OEM-Codepage des Systems (z. B. CP866).
- **Der globale Hotkey** besteht aus einem Buchstaben oder einer Ziffer plus Modifikatoren; ist die Kombination bereits von einem anderen Programm belegt, schlägt die Registrierung mit einer Sprechblasenmeldung fehl.
- **Die Panelgröße wird bei jedem Start auf die Startgröße zurückgesetzt** — nur die Position wird gemerkt (gewollt so).
- **Drag-and-drop und das Verschieben von Kacheln erfordern den Bearbeitungsmodus** („Symbole hinzufügen erlauben“ / Button ✅).
- Ordnerkacheln zeigen höchstens **9** untergeordnete Symbole; das Ordner-Popup zeigt höchstens **4** Spalten pro Zeile.
- `.lnk`/`.ico`-Elemente, die zum Panel hinzugefügt werden, werden **nach `ico\` kopiert**, damit sie das Verschieben der Originale überstehen.
- **Archiv wiederherstellen** akzeptiert nur Zips, die von Tilettes erstellt wurden („Jetzt Backup erstellen“ / „Backup speichern (Zip)“).
- Abgerundete Fensterecken werden beim Ändern der Fenstergröße vorübergehend entfernt (Technik gegen Flackern) und beim Loslassen wiederhergestellt.
- Die Tastaturlayout-Korrektur deckt das Paar EN↔RU (QWERTY) ab; andere Layouts werden unverändert durchgereicht.
- **Einzelinstanz**: ein zweiter Start zeigt nur das bereits geöffnete Fenster.
- Der automatische Ausstieg aus Ordnern funktioniert nur im Modus „Gleiches Fenster“ und nur, solange man sich in einem Ordner befindet.
- **Foto-/Video-Vorschauen in Kacheln** stammen aus dem Windows-Shell-Thumbnail-Cache. Ein **frisch hinzugefügtes Video** zeigt unter Umständen ein generisches Symbol, bis der Explorer seine Vorschau erzeugt hat (öffne den übergeordneten Ordner einmal im Explorer). Sobald die Vorschau einmal angezeigt wurde, bleibt sie im eigenen Cache der App erhalten und übersteht das Aufräumen des Caches; erscheint die Vorschau nie, fehlt dem System der Codec für diese Datei (z. B. HEVC ohne Erweiterung).

## Build

Erforderlich ist ein beliebiges Windows mit .NET Framework 4.x (der Compiler ist im Betriebssystem enthalten):

```
build.bat          rem → Tilettes.exe (universal AnyCPU)
```

oder direkt:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ /win32icon:app.ico /win32manifest:app.manifest /keyfile:Tilettes.snk /out:Tilettes.exe src\*.cs
```

Releases werden automatisch von GitHub Actions bei jedem `v*`-Tag erstellt: Der Workflow baut die universelle EXE mit demselben csc-Aufruf und hängt eine einzige einfache EXE-Datei an das Release an (ohne Zip) samt `SHA256SUMS.txt` mit ihrer Prüfsumme (die Prüfsumme steht auch in den Release-Hinweisen); die automatischen Quellcode-Archive von GitHub liegen ebenfalls im Release. Der universelle AnyCPU-Build läuft als 64-Bit-Prozess auf 64-Bit-Windows und als 32-Bit-Prozess auf 32-Bit-Windows. Die EXE kannst du auch selbst mit `build.bat` bauen.

## Antivirus-Fehlalarme

Einige Antivirus-Produkte kennzeichnen `Tilettes.exe` gelegentlich mit einer generischen Heuristik-Erkennung (unsignierte kleine Hilfsprogramme, die einen globalen Tastatur-Hook installieren, `.lnk`-Verknüpfungen parsen und Shell-Symbole extrahieren, entsprechen genau den Mustern, die Heuristiken nicht mögen). Behandle eine solche Erkennung als **Fehlalarm**, bis das Gegenteil bewiesen ist — und du musst der ausgelieferten Binärdatei nicht einfach vertrauen, denn alles ist überprüfbar:

- Der **Quellcode ist vollständig offen** in diesem Repository — jede Zeile, die in der EXE landet, ist hier enthalten.
- **Releases werden automatisch von GitHub Actions gebaut** — aus dem getaggten Commit, auf von Microsoft gehosteten Runnern (`.github/workflows/build.yml`). Nichts wird von Hand hochgeladen: Die an ein Release angehängte EXE wird aus exakt dem Quellcode kompiliert, den du bei diesem Tag siehst.
- Du kannst die EXE **selbst bauen** — mit `build.bat` (der C#-Compiler ist in Windows enthalten) — und deinen eigenen Build statt der heruntergeladenen Datei verwenden.
- Der Strong-Name-Schlüssel (`Tilettes.snk`) wird nur für den Build erzeugt; ein Strong Name belegt die Identität der Assembly, nicht das Vertrauen in einen Anbieter — sieh dir stattdessen den Code und die Build-Pipeline an.
- Seit v0.6.16 bietet die App bei ihrer eigenen Update-Prüfung außerdem **keine Releases an, die jünger als 24 Stunden sind**, damit sich eine frisch veröffentlichte EXE in ihrem ersten Tag nicht verbreitet, während sich die Cloud-Urteile der Antivirus-Programme noch festlegen.

## Datendateien (werden neben der EXE erstellt)

| Datei | Zweck |
|---|---|
| `settings.ini` | Alle Einstellungen |
| `records.xml` | Tabs, Ordner, Verknüpfungen, Beschreibungen |
| `bookmarks.xml` | Lesezeichen des Mini-Explorers |
| `filetypes.xml` | Dateityp-Regeln |
| `searchHistory.xml` | Suchverlauf des Panels („frühere Suchen“) |
| `ico\` | Kopien von .lnk/.ico-Elementen und eigenen Symbolen |
| `iconcache\` | Dauerhafter Symbol-Cache (Symbole werden einmal pro Kachel-Lebensdauer extrahiert) |
| `autoBackup\` | Geplante vollständige Backup-Zips |
| `log.txt` | Anwendungslog (sich wiederholende Zeilen werden dedupliziert) |

## Projektstruktur (`src/`)

| Datei | Zweck |
|---|---|
| `Program.cs` | Hauptfenster: Tabs, Kacheln, Panel-Suche, Ordner-Popups, Einzelinstanz |
| `miniexplorerform.cs` | Mini-Explorer: Navigation, Lesezeichen, eingebettete Konsole |
| `searchcore.cs` | Fuzzy-Bewertung, Tastaturlayout-Varianten und Bitmasken-Vorfilter (die Engine der Panel-Suche); der Festplatten-Indizierungsteil (Dateisuche des Mini-Explorers) ist derzeit deaktiviert |
| `panelsearch.cs` | Durchsuchbare Metadaten der gespeicherten Elemente |
| `Settings.cs` / `SettingsForm.cs` | Einstellungsmodell und Einstellungsdialog |
| `filetypes.cs` / `filetypesform.cs` | Dateityp-Regeln und ihre Editoren |
| `bookmarks.cs` | Lesezeichen-Speicher |
| `Skins.cs` | Dekorative Skins: Paletten und der Fensterrand |
| `StartMenuSync.cs` + `ShellItemApi.cs` | Gespiegelter Startmenü-Tab, einschließlich UWP-/Store-Apps |
| `BackupManager.cs` + `ZipWriter.cs` / `ZipReader.cs` | Geplante und manuelle Backups |
| `SearchHistory.cs` | Suchverlauf des Panels („frühere Suchen“, Ergebnis-Boosting) |
| `AppLog.cs` | Schreiber für die deduplizierte `log.txt` |
| `loc.cs` + `lang_*.cs` | Lokalisierung: EN als Quelle, RU inline, ES/PT/DE/FR/IT/PL/ZH/JA als Tabellen |
| `UpdateChecker.cs` / `WelcomeForm.cs` | Update-Prüfung (GitHub Releases) und das Willkommensfenster beim ersten Start |
| `IconExtractor.cs`, `NativeContextMenu.cs`, `IniFile.cs`, `Records.cs`, `apputil.cs` | Symbole, native Menüs, INI-I/O, Datensatz-I/O, Autostart/Einzelinstanz/Wallets |
| `AssemblyInfo.cs` | VERSIONINFO / Assembly-Metadaten |

## Lizenz

[MIT](LICENSE) — kann frei verwendet, geändert und verbreitet werden.

<a name="donate"></a>
## Spenden

Wenn Tilettes nützlich ist, kannst du die Entwicklung mit Kryptowährung unterstützen. EVM-kompatible Netzwerke teilen sich eine Adresse — sende in dem Netzwerk, das gerade praktisch ist:

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

<a name="changelog"></a>
## Changelog

### v0.6.1 (2026-10-01)

- Die Version wird oben rechts im Einstellungsfenster angezeigt.
- Hältst du **Ctrl** gedrückt, zeigen Kacheln ihre vollständigen, ungekürzten Namen (die Beschriftungsschrift verkleinert sich, damit alles hineinpasst); beim Loslassen ist wieder alles normal. Umschalter in den Einstellungen („Ctrl gedrückt halten — volle Namen auf Kacheln anzeigen“).
- Kachel-Tooltips neu gestaltet: Beschreibung (oder der vollständige Name, wenn es keine Beschreibung gibt) + ein Trenner + die vollständigen Pfade; `.lnk`-Elemente zeigen sowohl die Verknüpfung als auch ihr aufgelöstes Ziel.
- Medien-Vorschauen: Eine einmal erhaltene Vorschau bleibt im eigenen Cache der App erhalten und übersteht das Aufräumen des Windows-Thumbnail-Caches; Rechtsklick auf einen toten Link zeigt jetzt die eigenen Aktionen der Kachel, statt nichts zu tun; der Media-Foundation-Frame-Extraktor bleibt als fehlertoleranter Fallback erhalten (siehe REPORT.md).
- b2.bat beendet die laufende App vor dem Kompilieren.

### v0.6.0-beta (2026-10-01)

- Leistung: schnellerer Start (verzögertes Rendering der Tabs — nur der aktive Tab wird aufgebaut), dauerhafter Symbol-Cache (Shell-Symbole werden einmal pro Kachel-Lebensdauer extrahiert), Suchmetadaten werden einmal beim Hinzufügen eines Elements erfasst; das Suchfeld des Mini-Explorers ist deaktiviert (der Code bleibt zur Reaktivierung erhalten).
- Netzwerkfreigaben blockieren den UI-Thread nie: Symbole und .lnk-Ziele auf Netzwerkpfaden werden im Hintergrund aufgelöst.
- Ein aktiver Skin steuert jetzt überall die helle/dunkle Palette (die Auswahl von Mint hinterlässt keine dunklen Fenster mehr); Mint ist das Standard-Theme und alle Schriften sind standardmäßig 14.
- Erster Start: Auf Monitoren mit einem Arbeitsbereich unter 900px schrumpfen Standardfenster und Raster proportional, damit alles hineinpasst.
- Härtung: Strong Name, VERSIONINFO, explizites Manifest, das Abfangen der Win-Taste ist Opt-in — 0 Erkennungen auf VirusTotal.
- Release-Assets sind einfache EXE-Dateien je CPU (AnyCPU/x86/x64) statt eines Zip-Archivs; die Release-Hinweise stammen aus CHANGELOG.md.

### v0.5 (2026-09-30)

- Willkommensfenster beim ersten Start (einmal pro Datenordner): Dankeschön, Beta-Hinweis + Issue-Link, gemaltes Mini-Diagramm, Sprachwahl (RU/EN-Flaggen), Einwilligung zur Update-Prüfung, Spendenadressen (Klick kopiert); Beenden über „Schließen“ oder „Schließen & Beispielskacheln erstellen“ (Editor / Rechner / Explorer / Paint als fertige Kacheln). Erneut aufrufbar aus den Einstellungen.
- Update-Prüfung: Die App fragt die öffentliche GitHub-Releases-API alle N Tage nach dem neuesten Release-Tag (Standard 3; die erste Prüfung erfolgt ebenfalls N Tage nach der Installation) — strikt nur mit Zustimmung des Nutzers, sonst keinerlei Netzwerkanfragen. Existiert eine neuere Version, erscheint eine grüne „Update“-Plakette neben dem Einstellungen-Button und öffnet die Releases-Seite. Manueller „Jetzt prüfen“-Button in den Einstellungen (meldet das Ergebnis in einem Meldungsfenster). Auto-Installation ist ein Stub (TODO). Test-Hook für die Plakette: `WINPANEL_MOCK_UPDATE=0.6`.
- Einstellungen: neuer Abschnitt „Updates“ (Prüfungsschalter, Intervall in Tagen, „Jetzt prüfen“-Button, Auto-Installations-Stub, Spendenzeile mit aufklappendem Wallet-Menü — ein Klick kopiert die Adresse — und ein Button zum erneuten Aufruf des Willkommensfensters). Editierbares Hotkey-Feld: beliebige Kombination aus Ctrl/Alt/Shift/Win + Buchstabe/Ziffer kann eingegeben werden (mit Validierung), Standard auf Ctrl+Q geändert.
- Die Programm-Oberfläche ist in **10 Sprachen** verfügbar: Englisch (Quelle), Russisch, Spanisch, Portugiesisch, Deutsch, Französisch, Italienisch, Polnisch, Chinesisch (vereinfacht) und Japanisch. Die Sprache wird in den Einstellungen oder über die gemalten Flaggen im Willkommensfenster gewählt; eine neue Sprache = eine Tabellendatei + eine Zeile (siehe loc.cs).
- Echte Spenden-Wallet-Liste, gruppiert nach Chain: EVM-Netzwerke (ETH · Polygon · Base · Monad · HyperEVM) teilen sich eine Adresse; dazu Bitcoin, Solana und Sui.
- Die Version ist jetzt eine einzelne Konstante (`AppInfo.AppVersion`); das Tray-Tooltip und das Willkommensfenster zeigen sie.
- GitHub-Infrastruktur: MIT-Lizenz, FUNDING.yml (Sponsor-Links auf die Wallet-Anker), README in Dateien pro Sprache aufgeteilt (`README.md` EN + 9 Übersetzungen) für einfache Erweiterung, GitHub-Actions-Workflow (Release mit portabler Zip bei `v*`-Tags), Landingpage in docs/ für GitHub Pages. Alle Commit-Meldungen im Verlauf sind auf Englisch.

### v0.4 (2026-09-29)

- Rebranding: Tilettes / «Плиточки», neues Symbol (EXE-Ressource + codegezeichnetes Tray-Symbol).
- Backup-Funktion vervollständigt: „Backup speichern (Zip)“ packt Einstellungen, Kacheln, Symbole, Lesezeichen, Suchverlauf und die EXE; „Archiv wiederherstellen“ entpackt von der App erstellte Zips in den Arbeitsordner, ohne Tilettes.exe zu ersetzen; geplante vollständige Backups nach autoBackup\.
- Überarbeiteter Einstellungsdialog: vertikal in der Größe veränderbar über einen unteren Griff, Tooltips zu jedem Element, beschriftete X/Y-Felder für Startgröße und Fensterposition, Raster-Spalten/Zeilen in einer Zeile, Skin-Combobox ersetzt das doppelte Kontrollkästchen für das helle Theme.
- Korrekturen am Startmenü-Spiegel-Tab (Ordnerinhalte klumpen nicht mehr; vereinfachtes Auto-Layout).
- Layout-Korrekturen für Schriften 14–20 (Tabs, Höhe des Suchstatus, Dialoge, Leisten des Mini-Explorers, Stufe der Fensterecke).

### v0.3 (2026-09-28)

- Startmenü-Sync (Spiegel-Tab, nach Zeitplan), Schrumpfstufen der Such-Umschalter, Schnelleinstellungs-Leiste der Suche, Mehrfachauswahl im Bearbeitungsmodus, Menüs „Auf Tab verschieben“, Abfangen der Win-Taste, Navigation in Ordner-Popups, Pfadhervorhebung in Suchergebnissen.

### v0.1 – v0.2 (2026-09-27)

- Erste Builds des Launcher-Panels: Kacheln, Tabs, Ordner, Panel-Suche, Mini-Explorer mit Konsole, Dateityp-Regeln, Tray/Autostart/Hotkey, Raster-Anpassung.
