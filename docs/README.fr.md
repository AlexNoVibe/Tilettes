# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · **Français** · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

Un panneau de lancement rapide pour Windows : une grille de tuiles avec raccourcis, dossiers et onglets, une recherche floue intégrée et un mini-explorateur avec console intégrée. Un seul EXE portable, sans installation, .NET Framework 4.8 (WinForms).

Version actuelle : **v0.5** — téléchargement : [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Changelog (anglais)](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md#changelog). Statut : **bêta**.

## Fonctions

- Tuiles 1×1…6×6, onglets illimités déplaçables, dossiers dans l'onglet ou en popup, glisser-déposer depuis l'Explorateur, grille personnalisée, mise à l'échelle des icônes.
- Recherche floue sur les noms, métadonnées, chemins et descriptions, correction de la mauvaise disposition clavier (`руддщ` → `hello`).
- Mini-explorateur avec fil d'Ariane, favoris, recherche de fichiers et console `cmd.exe` intégrée.
- Icônes et règles « Ouvrir avec » par type de fichier, import/export.
- Icône de notification, démarrage automatique, raccourci global, menus natifs de l'Explorateur, fenêtre sans bordure redimensionnable.
- Fenêtre de bienvenue affichée une seule fois et vérification des mises à jour via GitHub Releases avec pastille dans le coin.

## Premier démarrage & mises à jour

- La fenêtre de bienvenue s'affiche une seule fois (note bêta, choix de la langue, consentement pour la vérification, tuiles d'exemple) et peut être réaffichée depuis les paramètres.
- La vérification interroge GitHub Releases tous les N jours (par défaut 3) — uniquement avec le consentement de l'utilisateur ; si une version plus récente existe, une pastille verte « Mettre à jour » apparaît à côté du bouton paramètres. « Vérifier maintenant » est une vérification manuelle.

## Faire un don

Si Tilettes vous est utile, vous pouvez soutenir le développement en crypto. Les réseaux compatibles EVM partagent une même adresse :

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

Autres façons d'aider : signaler bugs et idées dans [Issues](https://github.com/AlexNoVibe/Tilettes/issues), mettre une étoile au dépôt, parler de Tilettes.

## Compilation

```
build.bat
```

N'importe quel Windows avec .NET Framework 4.x suffit — le compilateur fait partie du système. Les versions sont créées automatiquement par GitHub Actions à chaque tag `v*` et ne contiennent que l'archive des sources (le workflow vérifie aussi la compilation) ; compilez l'exe vous-même avec `build.bat`.

Documentation complète : [**README.md**](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) (English) · [README.ru.md](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) (Русский)
