# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · **Polski** · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

Panel szybkiego uruchamiania dla Windows: siatka kafelków ze skrótami, folderami i kartami, wbudowane wyszukiwanie rozmyte i mini eksplorator z wbudowaną konsolą. Jedna przenośna plik EXE, bez instalacji, .NET Framework 4.8 (WinForms).

Aktualna wersja: **v0.5** — pobieranie: [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Dziennik zmian (ang.)](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md#changelog). Status: **beta**.

## Funkcje

- Kafelki 1×1…6×6, nieograniczone przestawialne karty, foldery w karcie albo w okienku, przeciąganie z Eksploratora, własna siatka, skala ikon.
- Wyszukiwanie rozmyte po nazwach, metadanych, ścieżkach i opisach, korekta złego układu klawiatury (`руддщ` → `hello`).
- Mini eksplorator ze ścieżką nawigacji, zakładkami, wyszukiwaniem plików i wbudowaną konsolą `cmd.exe`.
- Ikony i reguły „otwórz przez” wg typu pliku, import/eksport.
- Ikona w zasobniku, autostart, globalny skrót, natywne menu Eksploratora, okno bez ramki z resize przy krawędziach.
- Jednorazowe okno powitalne i sprawdzanie aktualizacji przez GitHub Releases z plakietką w rogu.

## Pierwsze uruchomienie i aktualizacje

- Okno powitalne pokazuje się dokładnie raz (informacja o becie, wybór języka, zgoda na sprawdzanie aktualizacji, przykładowe kafelki) i można je ponownie otworzyć z ustawień.
- Sprawdzanie aktualizacji pyta GitHub Releases co N dni (domyślnie 3) — wyłącznie za zgodą użytkownika; gdy istnieje nowsza wersja, obok przycisku ustawień pojawia się zielona plakietka „Aktualizuj”. „Sprawdź teraz” to sprawdzenie ręczne.

## Wsparcie

Jeśli Tilettes jest ci pomocny, możesz wesprzeć rozwój kryptowalutami. Sieci zgodne z EVM mają wspólny adres:

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

Inne formy pomocy: zgłaszanie błędów i pomysłów w [Issues](https://github.com/AlexNoVibe/Tilettes/issues), gwiazdka dla repozytorium, polecanie Tilettes.

## Budowa

```
build.bat
```

Wystarczy dowolny Windows z .NET Framework 4.x — kompilator jest częścią systemu. Wydania są tworzone automatycznie przez GitHub Actions przy każdym tagu `v*` i zawierają tylko archiwum źródeł (workflow dodatkowo sprawdza kompilację); exe buduj sam przez `build.bat`.

Pełna dokumentacja: [**README.md**](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) (English) · [README.ru.md](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) (Русский)
