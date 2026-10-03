---
title: Tilettes
---

# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · **Polski** · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

<!-- Dodawanie języka: utwórz docs/README.<kod>.md (przetłumacz), dodaj
     lang_xx.cs (tabela interfejsu z kluczami po angielsku, patrz loc.cs), a
     następnie rozszerz wiersz języków powyżej, ten na początku każdego innego
     pliku README oraz tablicę Loc.Languages. GitHub pokazuje README.md
     (angielski) na stronie głównej repozytorium; każdy pozostały język
     znajduje się w docs/ jako jeden plik plus jeden odnośnik. -->

Panel szybkiego uruchamiania dla Windows: siatka kafelków ze skrótami, folderami i kartami, wbudowane wyszukiwanie rozmyte i mini eksplorator z wbudowaną konsolą. Jeden przenośny plik EXE, bez instalatora, .NET Framework 4.8 (WinForms).

![Tilettes — okno główne](screenshot_main.png)

Aktualna wersja: **v1.0** — pobieranie: [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Dziennik zmian](#changelog). Status: **beta**.

## Atuty

**Kafelki i karty** — kafelki 1×1…6×6; skórki (Mint, Night, Android) oraz motywy jasny/ciemny; własne czcionki i kolory; nieograniczona liczba przestawialnych kart w wielu wierszach, układ: swobodny/siatka; foldery wewnątrz karty albo w wyskakujących oknach; przewijalne dodatkowe wiersze poniżej siatki; tryb edycji z wielokrotnym zaznaczaniem oraz przeciąganie i upuszczanie; własna nazwa, opis (uwzględniany w wyszukiwaniu) i ikona dla każdego kafelka; podglądy zdjęć/wideo na kafelkach.

**Wyszukiwanie** — natychmiastowe wyszukiwanie rozmyte po nazwach, metadanych programów (opis / produkt / firma), pełnych ścieżkach i własnych opisach; działa na buforowanych metadanych (bez skanowania dysków); korekta złego układu klawiatury (`знерщи` → `python`); wyniki w dwóch blokach — przeszłe zapytania (z prawdziwymi ikonami, zapamiętywane między sesjami, z podbiciem) na górze, zwykłe wyniki poniżej; każde źródło można włączyć lub wyłączyć.

**Lekkość** — jeden przenośny exe o rozmiarze ~0,5 MB (517 KB), wszystkie dane obok niego; brak zależności poza wbudowanym w Windows .NET Framework; ~30 MB pamięci RAM; ikony są wyodrębniane z powłoki dokładnie raz na cały okres życia kafelka do samooczyszczającego się `iconcache\`; uruchamianie nigdy nie blokuje panelu (oddzielne procesy, ścieżki sieciowe w tle); przy starcie renderowana jest tylko aktywna karta.

**Mini eksplorator** — nawigacja breadcrumb, zakładki folderów/poleceń/grup, wbudowana konsola `cmd.exe` (historia, czcionka zmieniana przez Ctrl+kółko myszy); ulubione polecenia uruchamiane jednym kliknięciem, gdzie `%1` = przeglądany folder (`wt -d "%1"`); reguły „otwórz przez” według rozszerzenia lub maski, z importem/eksportem.

**System** — lustrzana karta Menu Start (z aplikacjami UWP/Store) synchronizowana według harmonogramu; globalny skrót klawiszowy, opcjonalne przechwytywanie klawisza Win, zasobnik, autostart; natywne menu kontekstowe Eksploratora; zaplanowane i ręczne kopie zapasowe z przywracaniem jednym kliknięciem; interfejs w 10 językach.

**Open source** — w pełni otwarty kod źródłowy; wydania budowane przez GitHub Actions z tagu + SHA256SUMS.txt; dokładnie jedno wywołanie sieciowe w całej aplikacji (sprawdzanie aktualizacji jest opt-in); zero telemetrii.

## Funkcje

- **Panel** — kafelki 1×1…6×6, nieograniczona liczba kart (przestawialnych, w wielu wierszach), foldery otwierane w miejscu albo w wyskakujących oknach, przeciąganie z Eksploratora, własna siatka (kolumny/wiersze/przezroczystość), skalowanie ikon.
- **Wyszukiwanie** — przeszukuje nazwy, nazwy plików, metadane programów (FileDescription / ProductName / CompanyName), pełne ścieżki i opisy użytkownika; opcjonalnie obejmuje lustrzaną kartę Menu Start; dopasowanie rozmyte z regulowaną dokładnością i korektą złego układu klawiatury (`руддщ` → `hello`); wyniki są uszeregowane według jakości dopasowania, a dopasowane znaki są podświetlane; powtarzane zapytania dostają podbicie z historii; wpisane zapytanie dzieli wyniki na dwa bloki — zapamiętane przeszłe zapytania na górze, zwykłe wyniki poniżej (pełne duplikaty są scalane); wiersze z przeszłych wyszukiwań pokazują prawdziwe ikony zapamiętanych elementów.
- **Mini eksplorator** — nawigacja breadcrumb, zakładki folderów/poleceń/grup (polecenie może zawierać `%1`, które rozwija się do przeglądanego folderu — np. `wt -d "%1"` otwiera Windows Terminal dokładnie tam) oraz wbudowana konsola `cmd.exe` z historią poleceń, zapisanymi poleceniami i zmianą rozmiaru czcionki przez Ctrl+kółko myszy. (Moduł wyszukiwania plików jest wyłączony od v0.6.0-beta — zachowano zaślepkę na wypadek ponownego włączenia.)
- **Reguły typów plików** — ikony i powiązania „otwórz przez” według rozszerzenia lub maski, import/eksport.
- **Integracja z systemem** — ikona w zasobniku, autostart z systemem Windows, globalny skrót klawiszowy, opcjonalne przechwytywanie klawisza Win, natywne menu kontekstowe Eksploratora, okno bez obramowania ze zmianą rozmiaru przy krawędziach.
- **Skórki i dodatki** — dekoracyjne skórki (własna paleta kolorów + zaokrąglone obramowanie okna), lustrzana karta Menu Start przebudowywana według harmonogramu, pełne kopie zapasowe do `autoBackup\`, historia wyszukiwania w panelu.
- **Pierwsze uruchomienie i aktualizacje** — jednorazowe okno powitalne (narysowany diagram „trzy źródła → siatka kafelków”, wybór języka, zgoda na sprawdzanie aktualizacji, odnośnik wsparcia, przykładowe kafelki) oraz sprawdzanie aktualizacji przez GitHub Releases z plakietką w rogu, gdy istnieje nowsza wersja.

## Pierwsze uruchomienie i aktualizacje

- **Okno powitalne** (tylko przy samym pierwszym uruchomieniu): podziękowanie, informacja, że możliwe są błędy i niedociągnięcia, z odnośnikiem do [Issues](https://github.com/AlexNoVibe/Tilettes/issues), narysowany mini-diagram (karta folderu, pliku .exe i skrótu .lnk → strzałka → siatka kafelków z komórką-widmem «+»), wybór języka (10 przycisków z flagami), zgoda na sprawdzanie aktualizacji, odnośnik **Wesprzyj autora** do [sekcji wsparcia](https://github.com/AlexNoVibe/Tilettes#donate) — oraz dwa przyciski wyjścia: zwykłe **Zamknij** albo **Zamknij i utwórz przykładowe kafelki** (Notatnik, Kalkulator, Eksplorator, Paint jako gotowe kafelki). Można je ponownie przywołać w każdej chwili przez „Pokaż ponownie okno powitalne” w ustawieniach.
- **Sprawdzanie aktualizacji** — aplikacja odpytuje publiczne API GitHub Releases raz na N dni (domyślnie 3; pierwsze sprawdzenie również następuje N dni po samym pierwszym uruchomieniu, a nie od razu). Nic nie jest nigdzie wysyłane, a przy wyłączonej opcji w ustawieniach nie wykonuje się żadnego żądania sieciowego. Gdy istnieje nowszy tag, obok przycisku ustawień pojawia się zielona plakietka **⟳ Aktualizacja** i otwiera stronę [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest). Opcja „Sprawdź teraz” w ustawieniach wykonuje sprawdzenie ręczne niezależnie od interwału (wynik jest raportowany w oknie komunikatu). Automatyczna instalacja jest na razie zaślepką (TODO).
- **Haczyk testowy** — uruchom aplikację z `WINPANEL_MOCK_UPDATE=0.6`, aby wyrenderować plakietkę aktualizacji tak, jakby istniało nowsze wydanie (bez korzystania z sieci).

## Opis wszystkich ustawień

Wszystkie ustawienia mieszczą się w jednym oknie dialogowym (przycisk ⚙ / menu zasobnika) i są przechowywane w `settings.ini`.

### Uruchamianie i okno

| Ustawienie | Zakres | Domyślnie | Opis |
|---|---|---|---|
| Rozmiar przy starcie (W × H) | 200–4000 | 900 × 800 | Rozmiar panelu przy każdym uruchomieniu. Zmiana rozmiaru w trakcie sesji nie jest zapamiętywana — zapamiętywana jest tylko pozycja. |
| Pozycja okna (X, Y) | −4000…4000 | 100, 100 | Pozycja na ekranie przy uruchomieniu. Aktualizowana automatycznie przy przenoszeniu okna. |
| Skrót pokazywania okna | presety + własny | Ctrl+Q | Globalny skrót klawiszowy pokazujący/aktywujący panel. Wybierz preset (Brak, Ctrl+Q, Ctrl+Shift+Q, Alt+Q, Ctrl+J, …) albo wpisz dowolną kombinację `Modyfikator+Klawisz` (Ctrl/Alt/Shift/Win + litera lub cyfra) bezpośrednio w edytowalnym polu; wpis, którego nie da się odczytać, jest odrzucany wraz z wyjaśnieniem. |
| Język | en, ru, es, pt, de, fr, it, pl, zh, ja | ru | Język interfejsu (10 języków), stosowany natychmiast. |

### Siatka i kafelki

| Ustawienie | Zakres | Domyślnie | Opis |
|---|---|---|---|
| Przezroczystość siatki | 0–255 | 50 | Alfa linii siatki. 0 = niewidoczna. Rysowana tylko wtedy, gdy włączony jest przełącznik siatki (▦). |
| Kolumny siatki | 1–100 | 16 | Komórki w poziomie. Pozycje kafelków przyciągają do tej siatki. |
| Wiersze siatki | 1–100 | 16 | Komórki w pionie. |
| Domyślny rozmiar elementu | 1–6 | 2 | Rozmiar nowo dodawanych kafelków (1×1 … 6×6 komórek). |
| Skala ikon (%) | 25–400 | 100 | Rozmiar ikony wewnątrz kafelka w procentach wartości domyślnej. |
| Zezwalaj na dodawanie ikon | wł./wył. | wł. | Tryb edycji: przeciąganie kafelków, tworzenie folderów, upuszczanie plików. Gdy wyłączone, kafelki po prostu uruchamiają się po kliknięciu. |
| Etykieta kafelka: dwa wiersze | wł./wył. | wł. | Wysokie kafelki łamią etykietę na dwa wiersze (wyrównanie obu wierszy — do lewej/środka/prawej — ustawia się obok). |
| Ukryj sufiks skrótu | wł./wył. | wł. | Tylko wyświetlanie: „ - Shortcut” / „ — ярлык” (warianty myślników, kilka języków) jest ukrywane na etykiecie; zapisana nazwa i wyszukiwanie pozostają nietknięte — odznacz, aby przywrócić. |
| Ukryj rozszerzenie pliku | wł./wył. | wł. | Tylko wyświetlanie: prawdziwe rozszerzenie ścieżki elementu (.mp4 …) jest ukrywane na etykiecie; odznacz, aby przywrócić. |
| Skórka i motyw | Brak (ciemny) / Jasny / skórki | Mint | Klasyczny ciemny albo jasny motyw, lub dekoracyjna skórka z własną paletą i obramowaniem okna: Android, Night, Mint (fabrycznie domyślna). |

### Foldery

| Ustawienie | Zakres | Domyślnie | Opis |
|---|---|---|---|
| Otwieraj foldery w | To samo okno / Wyskakujące okno | To samo okno | Kliknięcie folderu nawiguje wewnątrz karty albo otwiera wyskakujące okno ponad wszystkim. |
| sek. bezczynności | 0–600 | 15 | Tylko w trybie „To samo okno”: automatyczny powrót wyżej po N sekundach bez aktywności myszy/klawiatury. 0 = wyłączone. |

### Czcionki

Po jednym wierszu dla grup: **Kafelki**, **Karty** i **Interfejs**:

| Ustawienie | Zakres | Domyślnie | Opis |
|---|---|---|---|
| rozmiar | 6–24 | 14 | Rozmiar czcionki dla grupy. |
| próbka koloru | dowolny kolor | puste | Własny kolor tekstu; puste = domyślny kolor motywu. Dotyczy etykiet kafelków, tytułów kart albo całego tekstu interfejsu. |
| krój | dowolna zainstalowana czcionka | Segoe UI | Rodzina czcionek dla grupy. |

### Wyszukiwanie

| Ustawienie | Zakres | Domyślnie | Opis |
|---|---|---|---|
| Dokładność dopasowania rozmytego (0–3) | 0–3 | 2 | 0 = tylko trafienia podciągu; 1–3 = coraz bardziej tolerancyjne dopasowanie literówek/rozmyte. Cyfry liczą się podwójnie, więc kody numeryczne dopasowywane są rygorystycznie. |
| Szukaj w metadanych | wł./wył. | wł. | Nazwa pliku, cel skrótu, informacje o wersji (opis, produkt, firma). |
| Szukaj w pełnych ścieżkach | wł./wył. | wł. | Tekst pełnej ścieżki, łącznie z folderami nadrzędnymi. |
| Szukaj w opisach | wł./wył. | wł. | Opisy użytkownika (klik prawym → Opis…). |
| Szukaj w karcie Start | wł./wył. | wł. | Uwzględnia lustrzaną kartę Menu Start w wyszukiwaniu w panelu. |
| Czcionka wyszukiwania: pole | 7–30 | 14 | Rozmiar czcionki pola wyszukiwania. |
| Czcionka wyszukiwania: wyniki | 7–30 | 14 | Rozmiar czcionki wierszy wyników (wysokość wiersza zależy od czcionki). |

### Aktualizacje

| Ustawienie | Zakres | Domyślnie | Opis |
|---|---|---|---|
| Automatycznie sprawdzaj aktualizacje | wł./wył. | wł. | Odpytuj GitHub Releases o nowszą wersję raz na N dni. Przy odznaczonej opcji nigdy nie działa — zero żądań sieciowych. |
| Sprawdzaj co N dni | 1–365 | 3 | Jak często sprawdzać. Pierwsze sprawdzenie następuje N dni po samym pierwszym uruchomieniu. |
| Sprawdź teraz | przycisk | — | Odpytaj GitHub Releases natychmiast (sprawdzenie ręczne działa nawet przy wyłączonej automatyce). |
| Automatycznie instaluj aktualizacje | wł./wył. | wył. | **Zaślepka (TODO)** — jeszcze niezaimplementowane. |
| ♥ Dotacja | przycisk | — | Otwiera w przeglądarce sekcję wsparcia na GitHubie ([README → Wsparcie](https://github.com/AlexNoVibe/Tilettes#donate)). |
| Pokaż ponownie okno powitalne | przycisk | — | Ponowne wyświetlenie okna pierwszego uruchomienia. |

### Mini eksplorator (klucze INI)

| Klucz | Zakres | Domyślnie | Opis |
|---|---|---|---|
| Ctrl+klik na folderze otwiera mini eksplorator | wł./wył. | wł. | Skrót Ctrl+klik na kafelku folderu. |
| `MiniExplorerW/H/X/Y` | W≥760, H≥520 | automatycznie | Geometria okna, zapamiętywana przy zamykaniu. |
| `MiniExplorerBookmarks` | wł./wył. | wł. | Widoczność bocznego panelu zakładek. |
| `MiniExplorerTopBar` | wł./wył. | wł. | Widoczność górnego paska zakładek. |
| `MiniExplorerConsole` | 15–85 | 40 | Wysokość konsoli jako procent okna. |
| `ConsoleFontSizeX10` | 60–280 | 140 | Rozmiar czcionki konsoli ×10 (140 = 14 pt), zmieniany Ctrl+kółkiem myszy. |

### Autostart i zasobnik

| Ustawienie | Zakres | Domyślnie | Opis |
|---|---|---|---|
| Autostart z systemem Windows | wł./wył. | wył. | Zapisuje wpis `HKCU\...\Run` („Tilettes”). |
| Po autostarcie — schowaj w zasobniku | wł./wył. | wył. | Dodaje `--minimized`: panel startuje ukryty w zasobniku. |
| Minimalizuj zamiast zamykać | wł./wył. | wł. | ✕ / Alt+F4 chowa do zasobnika (lub minimalizuje) zamiast kończyć działanie. Wyjście znajduje się w menu zasobnika. |
| Zawsze pokazuj ikonę zasobnika | wł./wył. | wł. | Ikona w zasobniku widoczna przez cały czas. |
| Zapamiętuj aktywną kartę | wł./wył. | wł. | Przywraca ostatnio aktywną kartę przy uruchomieniu. |
| Przechwytuj przycisk Start (Win) | wł./wył. | wył. | Samotne naciśnięcie Win pokazuje panel zamiast menu Start (niskopoziomowy hook klawiatury); kombinacje Win+klawisz przechodzą dalej. Opt-in — domyślnie wyłączone. |

### Kopia zapasowa i synchronizacja Menu Start

- **Utwórz kopię teraz** — pełna kopia zapasowa zip do `autoBackup\` (ustawienia, kafelki, ikony, zakładki, historia wyszukiwania, exe); harmonogramem rządzi opcja „Kopia zapasowa co N dni” (domyślnie 7, 0 = wyłączone), kopia powstaje ok. 3 minuty po uruchomieniu, gdy nadejdzie termin.
- **Zapisz kopię (zip)** — to samo archiwum do pliku wybranego przez użytkownika.
- **Przywróć z archiwum…** — oczekuje archiwum zip utworzonego przez samego Tilettesa; pliki rozpakowują się do folderu roboczego, `Tilettes.exe` nigdy nie jest zastępowany.
- **Synchronizuj Menu Start teraz** / co N godzin (domyślnie 24, 0 = wyłączone) — przebudowuje lustrzaną kartę Menu Start.

## Skróty klawiszowe i polecenia

### Panel główny

| Klawisze / działanie | Rezultat |
|---|---|
| Skrót klawiszowy (domyślnie Ctrl+Q) | Pokazanie / aktywacja panelu. |
| Samotne naciśnięcie Win (opt-in) | Pokazanie / ukrycie panelu zamiast menu Start — włącz w ustawieniach opcję „Przechwytuj przycisk Start (Win)”. |
| Po prostu zacznij pisać tekst, albo Ctrl+F | Otwarcie wyszukiwania w panelu. |
| ↓ | Skok do listy wyników. |
| Enter | Otwarcie wybranego wyniku (folder → nawigacja, plik → uruchomienie). |
| Esc | Zamknięcie wyszukiwania. |
| Kliknięcie kafelka | Uruchomienie elementu; folder nawiguje (albo wyskakuje okno — wg ustawień). |
| Ctrl+kliknięcie kafelka folderu | Otwarcie mini eksploratora (jeśli włączony). |
| Przeciągnięcie kafelka (tryb edycji) | Przeniesienie; upuszczenie na folder przenosi go do środka. |
| Upuszczenie plików na panel (tryb edycji) | Dodanie ich jako kafelków (upuszczenie na folder dodaje do jego środka). |
| Klik prawym na kafelku | Natywne menu Eksploratora plus: Opis…, Rozmiar 1×1–6×6, Zmień nazwę, Zmień ikonę, Usuń, Wynieś z folderu, Przenieś na kartę ▸, Otwórz w mini eksploratorze (foldery). |
| Klik prawym na karcie | Usunięcie (ostatnia karta jest chroniona), zmiana nazwy, przełączenie układu: swobodny/siatka. |
| Przeciągnięcie karty | Zmiana kolejności w obrębie wiersza albo przeniesienie do innego wiersza. |
| Klik prawym na pustym miejscu panelu | Utwórz folder, Ustawienia. |
| Przyciski ▦ / ✅ / ⚙ | Widoczność siatki, tryb edycji, ustawienia. ✅ ma trzy stany: wył. / edycja / wielokrotne zaznaczanie — przy wielokrotnym zaznaczaniu klikaj kafelki, aby wybrać kilka, a następnie usuń je zbiorczo albo przenieś przez menu z kliknięcia prawym przyciskiem. |

### Mini eksplorator

| Klawisze / działanie | Rezultat |
|---|---|
| Ctrl+L / F4 / Edytuj | Edycja ścieżki. |
| F5 | Odświeżenie folderu. |
| Backspace | O poziom wyżej. |
| Alt+← / Alt+→ | Wstecz / do przodu. |
| Enter / podwójne kliknięcie | Otwarcie (folder — nawigacja, plik — uruchomienie). |
| Esc | Anulowanie edycji ścieżki → zamknięcie okna. |
| Ctrl+kółko myszy | Rozmiar czcionki konsoli (zapamiętywany). |
| Przeciągnięcie separatora | Wysokość konsoli (zapamiętywana). |
| Przyciski ≡ / ☰ | Przełącza boczny panel zakładek / górny pasek zakładek. |
| Klik prawym na pliku | Otwórz, Pokaż w Eksploratorze, Kopiuj ścieżkę. |
| Klik prawym na folderze | Otwórz, Dodaj do zakładek, Otwórz w Eksploratorze. |
| Klik prawym na pustym miejscu | Odśwież, Kopiuj ścieżkę folderu, Otwórz w Eksploratorze, Dodaj bieżący folder do zakładek, Otwórz okno konsoli tutaj. |
| Klik prawym na zakładce | Edytuj polecenie… (tylko polecenia), Zmień nazwę…, Przenieś w górę / w dół, Usuń. |

### Konsola

Dowolne jednowierszowe polecenie `cmd.exe` można wpisać i wykonać (Enter albo przycisk **Uruchom**). Katalog roboczy jest przed każdym poleceniem synchronizowany z bieżącym folderem. **+ Zapisz** zapamiętuje wpisane polecenie jako zakładkę (opcjonalnie wewnątrz grupy); zapisane polecenia uruchamia się kliknięciem, a polecenie może zawierać `%1` — przeglądany folder (domyślnie zasiana grupa „CMD” zawiera zakładkę `wt -d "%1"` do otwierania Windows Terminal dokładnie tam). Przyciski: **Wyczyść** (wyczyść wyjście), **Restart** (nowy cmd.exe), **Nowe okno** (prawdziwe okno konsoli w bieżącym folderze). Historia poleceń jest dostępna przez ↑ / ↓ w trakcie sesji.

## Ograniczenia

- **Tylko Windows + .NET Framework 4.8** (GDI/WinForms). Brak obsługi DPI per-monitor — na mocno skalowanych ekranach interfejs może być rozmyty.
- **Wyszukiwanie plików w mini eksploratorze jest wyłączone** od v0.6.0-beta: moduł indeksowania dysków (tylko lokalne dyski stałe, limit 200 000 elementów na dysk) zachowano jako zaślepkę na wypadek ponownego włączenia. Wyszukiwanie w panelu działa wyłącznie na buforowanych metadanych zapisanych elementów — bez indeksowania dysków.
- **Wyszukiwanie w panelu** pokazuje najlepsze **200** trafień; lista plików pokazuje maksymalnie **800** wpisów na folder.
- **Konsola obsługuje wyłącznie `cmd.exe`**: polecenia jednowierszowe; programy interaktywne/TUI (edytory, pagery przyjmujące dane z klawiatury) nie działają poprawnie; bufor wyjścia czyści się automatycznie po ~150 000 znaków; kodowanie zgodne z systemową stroną kodową OEM (np. CP866).
- **Globalny skrót klawiszowy** to jedna litera/cyfra plus modyfikatory; rejestracja kończy się niepowodzeniem z dymkiem, gdy kombinacja jest już zajęta przez inny program.
- **Rozmiar panelu resetuje się do Rozmiaru przy starcie przy każdym uruchomieniu** — zapamiętywana jest tylko pozycja (celowo).
- **Przeciąganie i upuszczanie oraz przenoszenie kafelków wymagają trybu edycji** („Zezwalaj na dodawanie ikon” / przycisk ✅).
- Kafelki folderów podglądają maksymalnie **9** ikon potomnych; wyskakujące okno folderu pokazuje maksymalnie **4** kolumny w wierszu.
- Elementy `.lnk`/`.ico` dodane na panel są **kopiowane do `ico\`**, aby przetrwały przeniesienie oryginałów.
- **Przywracanie z archiwum** przyjmuje wyłącznie archiwa zip utworzone przez Tilettes („Utwórz kopię teraz” / „Zapisz kopię (zip)”).
- Zaokrąglone narożniki okna są tymczasowo usuwane w trakcie zmiany rozmiaru (technika zapobiegająca migotaniu) i przywracane po puszczeniu przycisku.
- Korekta układu klawiatury obejmuje parę EN↔RU QWERTY; inne układy przechodzą bez zmian.
- **Pojedyncza instancja**: uruchomienie drugiej kopii jedynie pokazuje już otwarte okno.
- Automatyczne wyjście z folderu po bezczynności działa tylko w trybie „To samo okno” i tylko wtedy, gdy znajdujesz się w folderze.
- **Podglądy kafelków zdjęć/wideo** pochodzą z systemowej pamięci podręcznej miniatur Windows. **Świeżo dodane wideo** może pokazywać ogólną ikonę, dopóki Eksplorator nie wygeneruje podglądu (otwórz raz folder zawierający plik w Eksploratorze). Gdy podgląd raz się pojawi, jest przechowywany we własnej pamięci podręcznej aplikacji i przetrwa opróżnienie pamięci podręcznej; jeśli podgląd nigdy się nie pojawia, oznacza to, że system nie ma kodeka dla tego pliku (np. HEVC bez rozszerzenia).

## Kompilacja

Wymagany jest dowolny Windows z .NET Framework 4.x (kompilator jest dostarczany wraz z systemem):

```
build.bat          rem → Tilettes.exe (uniwersalny AnyCPU)
build.bat x86      rem → Tilettes-x86.exe
build.bat x64      rem → Tilettes-x64.exe
```

albo bezpośrednio:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ /win32icon:app.ico /win32manifest:app.manifest /keyfile:Tilettes.snk /out:Tilettes.exe src\*.cs
```

Wydania są tworzone automatycznie przez GitHub Actions przy każdym tagu `v*`: workflow buduje warianty exe tym samym wywołaniem csc i dołącza do wydania zwykłe pliki exe (uniwersalny AnyCPU + x86 + x64 — bez zip); na wydaniu są również automatyczne archiwa kodu źródłowego od GitHuba. exe możesz też zbudować samodzielnie przez `build.bat`.

## Fałszywe alarmy antywirusów

Niektóre programy antywirusowe okazjonalnie oznaczają `Tilettes.exe` generycznym wykryciem heurystycznym (niepodpisane małe narzędzia instalujące globalny hook klawiatury, parsujące skróty `.lnk` i wyodrębniające ikony powłoki pasują do wzorca, którego heurystyki nie lubią). Potraktuj takie wykrycie jako **fałszywy alarm**, dopóki nie zostanie udowodnione co innego — i nie musisz ufać dostarczonemu plikowi binarnemu, ponieważ wszystko da się zweryfikować:

- **Kod źródłowy jest w pełni otwarty** w tym repozytorium — każdy wiersz, który trafia do exe, jest tutaj.
- **Wydania są budowane automatycznie przez GitHub Actions** z oznaczonego tagiem commita na runnerach hostowanych przez Microsoft (`.github/workflows/build.yml`). Nic nie jest wgrywane ręcznie: exe dołączony do wydania jest kompilowany z dokładnie tego źródła, które widzisz pod tym tagiem.
- Możesz **zbudować exe samodzielnie** przez `build.bat` (kompilator C# jest dostarczany z Windows) i uruchamiać własną wersję zamiast pobranej.
- Klucz silnej nazwy (`Tilettes.snk`) jest generowany tylko na potrzeby kompilacji; silna nazwa poświadcza tożsamość zestawu, a nie zaufanie do dostawcy — zamiast tego spójrz na kod i potok kompilacji.
- Od v0.6.16 aplikacja dodatkowo **nie oferuje wydań młodszych niż 24 godziny** (przy własnym sprawdzaniu aktualizacji), dzięki czemu świeżo opublikowany exe nie rozprzestrzenia się przez pierwszy dzień, dopóki ustalają się werdykty chmur antywirusowych.

## Pliki danych (tworzone obok pliku EXE)

| Plik | Przeznaczenie |
|---|---|
| `settings.ini` | Wszystkie ustawienia |
| `records.xml` | Karty, foldery, skróty, opisy |
| `bookmarks.xml` | Zakładki mini eksploratora |
| `filetypes.xml` | Reguły typów plików |
| `searchHistory.xml` | Historia wyszukiwania w panelu („przeszłe wyszukiwania”) |
| `ico\` | Kopie elementów .lnk/.ico i własnych ikon |
| `iconcache\` | Trwała pamięć podręczna ikon (ikony wyodrębniane raz na okres życia kafelka) |
| `autoBackup\` | Zaplanowane pełne kopie zapasowe zip |
| `log.txt` | Dziennik aplikacji (powtarzające się wiersze są deduplikowane) |

## Struktura projektu (`src/`)

| Plik | Przeznaczenie |
|---|---|
| `Program.cs` | Główne okno: karty, kafelki, wyszukiwanie w panelu, wyskakujące okna folderów, pojedyncza instancja |
| `miniexplorerform.cs` | Mini eksplorator: nawigacja, zakładki, wbudowana konsola |
| `searchcore.cs` | Punktacja rozmyta, warianty układu klawiatury i prefiltr oparty na maskach bitowych (silnik wyszukiwania panelu); jego część indeksująca dyski (wyszukiwanie plików w mini eksploratorze) jest obecnie wyłączona |
| `panelsearch.cs` | Metadane zapisanych elementów pod wyszukiwanie |
| `Settings.cs` / `SettingsForm.cs` | Model ustawień i okno dialogowe |
| `filetypes.cs` / `filetypesform.cs` | Reguły typów plików i ich edytory |
| `bookmarks.cs` | Przechowywanie zakładek |
| `Skins.cs` | Dekoracyjne skórki: palety i obramowanie okna |
| `StartMenuSync.cs` + `ShellItemApi.cs` | Lustrzana karta Menu Start, w tym aplikacje UWP/Store |
| `BackupManager.cs` + `ZipWriter.cs` / `ZipReader.cs` | Kopie zapasowe: zaplanowane i ręczne |
| `SearchHistory.cs` | Historia wyszukiwania w panelu („przeszłe wyszukiwania”, podbijanie wyników) |
| `AppLog.cs` | Zapis dziennika `log.txt` z deduplikacją |
| `loc.cs` + `lang_*.cs` | Lokalizacja: EN — źródło, RU — inline, ES/PT/DE/FR/IT/PL/ZH/JA — tabele |
| `UpdateChecker.cs` / `WelcomeForm.cs` | Sprawdzanie aktualizacji (GitHub Releases) i okno powitalne pierwszego uruchomienia |
| `IconExtractor.cs`, `NativeContextMenu.cs`, `IniFile.cs`, `Records.cs`, `apputil.cs` | Ikony, natywne menu, wejście/wyjście INI, wejście/wyjście rekordów, autostart/pojedyncza instancja/portfele |
| `AssemblyInfo.cs` | VERSIONINFO / metadane zestawu |

## Licencja

[MIT](LICENSE) — wolno używać, modyfikować i rozpowszechniać.

<a name="donate"></a>
## Wsparcie

Jeśli Tilettes jest ci pomocny, możesz wesprzeć rozwój kryptowalutą. Sieci zgodne z EVM współdzielą jeden adres — wyślij środki przez dowolną wygodną sieć:

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

Inne formy pomocy: zgłaszanie błędów i pomysłów w [Issues](https://github.com/AlexNoVibe/Tilettes/issues), gwiazdka dla repozytorium, polecanie Tilettes dalej.

<a name="changelog"></a>
## Dziennik zmian

### v0.6.1 (2026-10-01)

- Wersja jest pokazywana w prawym górnym rogu okna ustawień.
- Przytrzymanie **Ctrl** — kafelki pokazują swoje pełne, nieprzycięte nazwy (czcionka etykiety zmniejsza się, aby tekst się zmieścił); po puszczeniu klawisza wraca normalny wygląd. Przełącznik w ustawieniach („Przytrzymaj Ctrl — pokazuj pełne nazwy na kafelkach”).
- Przeprojektowane dymki podpowiedzi kafelków: opis (albo pełna nazwa, gdy opisu brak) + separator + pełne ścieżki; elementy `.lnk` pokazują zarówno skrót, jak i jego rozwiązany cel.
- Podglądy mediów: podgląd, raz uzyskany, jest przechowywany we własnej pamięci podręcznej aplikacji i przetrwa opróżnienie systemowej pamięci podręcznej miniatur Windows; klik prawym na kafelku z martwym odnośnikiem pokazuje teraz własne akcje kafelka zamiast nic nie robić; ekstraktor klatek Media Foundation zachowany jako awaryjny mechanizm zapasowy (patrz REPORT.md).
- b2.bat zamyka działającą aplikację przed kompilacją.

### v0.6.0-beta (2026-10-01)

- Wydajność: szybszy start (leniwe renderowanie kart — budowana jest tylko aktywna karta), trwała pamięć podręczna ikon (ikony powłoki wyodrębniane są raz na cały okres życia kafelka), metadane wyszukiwania zbierane raz przy dodaniu elementu; pole wyszukiwania mini eksploratora jest wyłączone (kod zachowany na wypadek ponownego włączenia).
- Zasoby sieciowe nigdy nie blokują wątku interfejsu: ikony i cele .lnk na ścieżkach sieciowych są rozwiązywane w tle.
- Aktywna skórka steruje teraz paletą jasną/ciemną wszędzie (wybór skórki Miętowej nie zostawia już ciemnych okien); mięta to motyw domyślny, a wszystkie czcionki mają domyślnie rozmiar 14.
- Pierwsze uruchomienie: na monitorach z obszarem roboczym poniżej 900 pikseli domyślne okno i siatka są proporcjonalnie zmniejszane, aby się zmieścić.
- Utrwalenie aplikacji: silna nazwa (strong name), VERSIONINFO, jawny manifest, przechwytywanie klawisza Win wyłącznie na życzenie (opt-in) — 0 wykryć na VirusTotal.
- Zasobami wydania są zwykłe pliki exe dla poszczególnych architektur (AnyCPU/x86/x64) zamiast archiwum zip; notatki wydania pochodzą z CHANGELOG.md.

### v0.5 (2026-09-30)

- Okno powitalne przy pierwszym uruchomieniu (raz na folder danych): podziękowanie, ostrzeżenie o becie + odnośnik do zgłoszeń, narysowany mini-diagram, wybór języka (flagi RU/EN), zgoda na sprawdzanie aktualizacji, adresy wsparcia (kliknięcie kopiuje); wyjście przez „Zamknij” albo „Zamknij i utwórz przykładowe kafelki” (Notatnik / Kalkulator / Eksplorator / Paint jako gotowe kafelki). Można je ponownie przywołać z ustawień.
- Sprawdzanie aktualizacji: aplikacja odpytuje publiczne API GitHub Releases o najnowszy tag raz na N dni (domyślnie 3; pierwsze sprawdzenie również następuje N dni po instalacji) — wyłącznie za zgodą użytkownika, w przeciwnym razie zero żądań sieciowych. Gdy istnieje nowsza wersja, obok przycisku ustawień pojawia się zielona plakietka „Aktualizacja”, otwierająca stronę wydań. Ręczny przycisk „Sprawdź teraz” w ustawieniach (wynik jest pokazywany w oknie komunikatu). Automatyczna instalacja to zaślepka (TODO). Haczyk testowy do plakietki: `WINPANEL_MOCK_UPDATE=0.6`.
- Ustawienia: nowa sekcja „Aktualizacje” (przełącznik sprawdzania, interwał w dniach, przycisk „Sprawdź teraz”, zaślepka automatycznej instalacji, wiersz dotacji z wyskakującym menu portfeli — kliknięcie kopiuje adres — oraz przycisk ponownego wywołania okna powitalnego). Edytowalne pole skrótu klawiszowego: można wpisać dowolną kombinację Ctrl/Alt/Shift/Win + litera/cyfra (z walidacją), wartość domyślną zmieniono na Ctrl+Q.
- Interfejs programu przetłumaczony na **10 języków**: angielski (źródłowy), rosyjski, hiszpański, portugalski, niemiecki, francuski, włoski, polski, chiński (uproszczony) i japoński. Język wybiera się w ustawieniach albo flagami w oknie powitalnym; nowy język to jeden plik z tabelą + jeden wiersz (patrz loc.cs).
- Prawdziwa lista portfeli wsparcia pogrupowana według sieci: sieci EVM (ETH · Polygon · Base · Monad · HyperEVM) współdzielą jeden adres; do tego Bitcoin, Solana i Sui.
- Wersja to teraz jedna stała (`AppInfo.AppVersion`); pokazywana w dymku zasobnika i w oknie powitalnym.
- Infrastruktura GitHub: licencja MIT, FUNDING.yml (odnośniki sponsora do kotwic portfeli), README rozbite na pliki według języków (`README.md` EN + 9 tłumaczeń) dla łatwej rozbudowy, workflow GitHub Actions (wydanie z przenośnym zip przy tagach `v*`), strona docelowa dokumentacji dla GitHub Pages. Wszystkie komunikaty commitów w historii są po angielsku.

### v0.4 (2026-09-29)

- Rebranding: Tilettes / «Плиточки», nowa ikona (zasób exe + rysowana kodem ikona zasobnika).
- Ukończono kopie zapasowe: „Zapisz kopię (zip)” pakuje ustawienia, kafelki, ikony, zakładki, historię wyszukiwania i exe; „Przywróć z archiwum” rozpakowuje archiwa utworzone przez aplikację do folderu roboczego bez zastępowania Tilettes.exe; pełne kopie zapasowe według harmonogramu do autoBackup\.
- Przebudowa okna ustawień: pionowa zmiana rozmiaru przez dolny uchwyt, dymki podpowiedzi przy każdym elemencie, podpisane pola X/Y rozmiaru startowego i pozycji okna, kolumny/wiersze siatki w jednym wierszu, lista rozwijana skórek zamiast zduplikowanego przełącznika jasnego motywu.
- Poprawki lustrzanej karty Menu Start (zawartość folderów już się nie skleja; uproszczony automatyczny układ).
- Poprawki układu dla czcionek 14–20 (karty, wysokość statusu wyszukiwania, okna dialogowe, paski mini eksploratora, stopień narożnika okna).

### v0.3 (2026-09-28)

- Synchronizacja Menu Start (lustrzana karta, według harmonogramu), kaskadowe zmniejszanie przełączników wyszukiwania, pasek szybkich ustawień wyszukiwania, tryb edycji z wielokrotnym zaznaczaniem, menu „Przenieś na kartę”, przechwytywanie klawisza Win, nawigacja w wyskakujących oknach folderów, podświetlanie ścieżek w wynikach wyszukiwania.

### v0.1 – v0.2 (2026-09-27)

- Pierwsze kompilacje panelu uruchamiania: kafelki, karty, foldery, wyszukiwanie w panelu, mini eksplorator z konsolą, reguły typów plików, zasobnik/autostart/skrót klawiszowy, konfiguracja siatki.
