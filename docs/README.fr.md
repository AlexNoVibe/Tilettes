---
title: Tilettes
---

# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · **Français** · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

Un panneau de lancement rapide pour Windows : une grille de tuiles avec raccourcis, dossiers et onglets, une recherche floue intégrée et un mini-explorateur avec console intégrée. Un seul EXE portable, sans installation, .NET Framework 4.8 (WinForms).

Version actuelle : **v0.6.0-beta** — téléchargement : [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Journal des modifications](#changelog). Statut : **bêta**.

## Fonctionnalités

- **Panneau** — tuiles de 1×1 à 6×6, onglets illimités (déplaçables, sur plusieurs lignes), dossiers ouverts sur place ou en fenêtres contextuelles, glisser-déposer depuis l'Explorateur, grille personnalisable (colonnes/lignes/transparence), mise à l'échelle des icônes.
- **Recherche** — porte sur les noms, les noms de fichiers, les métadonnées des programmes (FileDescription / ProductName / CompanyName), les chemins complets et les descriptions utilisateur ; correspondance floue avec précision réglable et correction de la mauvaise disposition clavier (`руддщ` → `hello`) ; les résultats sont classés par qualité de correspondance et les caractères correspondants sont surlignés.
- **Mini-explorateur** — navigation par fil d'Ariane, favoris (dossiers / commandes / groupes), recherche de fichiers (dossier courant ou tous les disques fixes) avec index en arrière-plan, et console `cmd.exe` intégrée avec historique des commandes, commandes enregistrées et zoom de police par Ctrl+molette.
- **Règles de types de fichiers** — icônes et associations « ouvrir avec » par extension ou par masque, import/export.
- **Intégration au bureau** — icône dans la zone de notification, démarrage automatique avec Windows, raccourci global, menus contextuels natifs de l'Explorateur, fenêtre sans bordure redimensionnable par les bords.
- **Premier démarrage & mises à jour** — une fenêtre de bienvenue affichée une seule fois (note bêta, choix de la langue, consentement pour la vérification des mises à jour, tuiles d'exemple) et une vérification des mises à jour auprès de GitHub Releases avec une pastille dans le coin lorsqu'une version plus récente existe.

## Premier démarrage & mises à jour

- **Fenêtre de bienvenue** (uniquement au tout premier lancement) : un mot de remerciement, un avertissement bêta avec un lien vers [Issues](https://github.com/AlexNoVibe/Tilettes/issues), un mini-diagramme dessiné « glissez un raccourci → une tuile », le choix de la langue (drapeaux RU/EN), le consentement pour la vérification des mises à jour, les adresses de don (clic pour copier) — et deux boutons de sortie : simplement **Fermer**, ou **Fermer & créer des tuiles d'exemple** (Bloc-notes, Calculatrice, Explorateur, Paint en tuiles prêtes à l'emploi). Elle peut être réaffichée à tout moment via « Réafficher la fenêtre de bienvenue » dans les paramètres.
- **Vérification des mises à jour** — l'application interroge l'API publique de GitHub Releases une fois tous les N jours (par défaut 3 ; la première vérification a également lieu N jours après le tout premier démarrage, pas immédiatement). Rien n'est envoyé nulle part, et si la case est décochée dans les paramètres, aucune requête réseau n'est faite du tout. Lorsqu'un tag plus récent existe, une pastille verte **⟳ Mettre à jour** apparaît à côté du bouton des paramètres et ouvre la page [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest). « Vérifier maintenant » dans les paramètres lance une vérification manuelle indépendamment de l'intervalle (le résultat est affiché dans une boîte de message). L'installation automatique est pour l'instant un stub (TODO).
- **Hook de test** — démarrez l'application avec `WINPANEL_MOCK_UPDATE=0.6` pour afficher la pastille « Mettre à jour » comme si une version plus récente existait (aucun réseau impliqué).

## Référence des paramètres

Tous les paramètres se trouvent dans une seule boîte de dialogue (bouton ⚙ / menu de la zone de notification) et sont stockés dans `settings.ini`.

### Démarrage & fenêtre

| Paramètre | Plage | Valeur par défaut | Description |
|---|---|---|---|
| Taille au démarrage (L × H) | 200–4000 | 900 × 800 | Taille du panneau à chaque lancement. Le redimensionnement en cours de session n'est pas conservé — seule la position l'est. |
| Position de la fenêtre (X, Y) | −4000…4000 | 100, 100 | Position à l'écran au lancement. Mise à jour automatiquement lorsque la fenêtre est déplacée. |
| Raccourci d'affichage de la fenêtre | préréglages + personnalisé | Ctrl+Q | Raccourci global qui affiche/active le panneau. Choisissez un préréglage (None, Ctrl+Q, Ctrl+Shift+Q, Alt+Q, Ctrl+J, …) ou saisissez n'importe quelle combinaison `Mod+Touche` (Ctrl/Alt/Shift/Win + une lettre ou un chiffre) directement dans le champ éditable ; une saisie non analysable est rejetée avec une explication. |
| Langue | ru / en | ru | Langue de l'interface, appliquée immédiatement. |

### Grille & tuiles

| Paramètre | Plage | Valeur par défaut | Description |
|---|---|---|---|
| Transparence de la grille | 0–255 | 50 | Alpha des lignes de la grille. 0 = invisible. Dessinée uniquement lorsque la bascule de grille (▦) est activée. |
| Colonnes de la grille | 1–100 | 16 | Cellules horizontales. Les positions des tuiles s'alignent sur cette grille. |
| Lignes de la grille | 1–100 | 16 | Cellules verticales. |
| Taille par défaut des éléments | 1–6 | 2 | Taille des tuiles nouvellement ajoutées (1×1 … 6×6 cellules). |
| Échelle des icônes (%) | 25–400 | 100 | Taille de l'icône dans une tuile, en pourcentage de la valeur par défaut. |
| Autoriser l'ajout d'icônes | activé/désactivé | activé | Mode d'édition : déplacement des tuiles, création de dossiers, dépôt de fichiers. Désactivé, les tuiles se contentent de se lancer au clic. |
| Habillage & thème | Aucun (sombre) / Clair / habillages | Aucun (sombre) | Thème classique sombre ou clair, ou habillage décoratif (couleurs propres + bordure de fenêtre). |

### Dossiers

| Paramètre | Plage | Valeur par défaut | Description |
|---|---|---|---|
| Ouvrir les dossiers dans | Même fenêtre / Fenêtre contextuelle | Même fenêtre | Un clic sur un dossier navigue à l'intérieur de l'onglet ou ouvre une fenêtre contextuelle au-dessus de tout. |
| s d'inactivité | 0–600 | 15 | Mode « Même fenêtre » uniquement : remonter automatiquement après N secondes sans activité souris/clavier. 0 = désactivé. |

### Polices

Une ligne chacun pour les **tuiles**, les **onglets** et l'**interface** :

| Paramètre | Plage | Valeur par défaut | Description |
|---|---|---|---|
| taille | 6–24 | 9 | Taille de police du groupe. |
| pastille de couleur | n'importe quelle couleur | vide | Couleur de texte personnalisée ; vide = valeur du thème par défaut. S'applique aux libellés des tuiles, aux titres des onglets ou à tout le texte de l'interface. |
| famille | n'importe quelle police installée | Segoe UI | Famille de police du groupe. |

### Recherche

| Paramètre | Plage | Valeur par défaut | Description |
|---|---|---|---|
| Précision floue (0–3) | 0–3 | 2 | 0 = correspondances de sous-chaînes uniquement ; 1–3 = correspondance floue de plus en plus tolérante aux fautes de frappe. Les chiffres comptent double, donc les codes numériques sont appariés strictement. |
| Rechercher dans les métadonnées | activé/désactivé | activé | Nom du fichier, cible du raccourci, informations de version (description, produit, société). |
| Rechercher dans les chemins complets | activé/désactivé | activé | Le texte du chemin complet, y compris les dossiers parents. |
| Rechercher dans les descriptions | activé/désactivé | activé | Descriptions utilisateur (clic droit → Description…). |
| Police de recherche : champ | 7–30 | 9 | Taille de police du champ de recherche. |
| Police de recherche : résultats | 7–30 | 9 | Taille de police des lignes de résultats (la hauteur des lignes suit la police). |

### Mises à jour

| Paramètre | Plage | Valeur par défaut | Description |
|---|---|---|---|
| Vérifier automatiquement les mises à jour | activé/désactivé | activé | Demander à GitHub Releases s'il existe une version plus récente, une fois tous les N jours. Ne s'exécute jamais si la case est décochée — aucune requête réseau du tout. |
| Vérifier tous les N jours | 1–365 | 3 | Fréquence de la vérification. La première vérification a lieu N jours après le tout premier démarrage. |
| Vérifier maintenant | bouton | — | Interroger GitHub Releases immédiatement (la vérification manuelle fonctionne même si l'automatique est désactivée). |
| Installer les mises à jour automatiquement | activé/désactivé | désactivé | **Stub (TODO)** — pas encore implémenté. |
| ♥ Faire un don | bouton | — | Liste contextuelle de portefeuilles (un clic copie l'adresse) plus la section don de GitHub. |
| Réafficher la fenêtre de bienvenue | bouton | — | Rejouer la fenêtre de bienvenue du premier démarrage. |

### Mini-explorateur (clés INI)

| Clé | Plage | Valeur par défaut | Description |
|---|---|---|---|
| Ctrl+clic sur un dossier ouvre le mini-explorateur | activé/désactivé | activé | Le raccourci Ctrl+clic sur les tuiles de dossier. |
| `MiniExplorerW/H/X/Y` | W≥760, H≥520 | auto | Géométrie de la fenêtre, mémorisée à la fermeture. |
| `MiniExplorerBookmarks` | activé/désactivé | activé | Visibilité du panneau latéral de favoris. |
| `MiniExplorerTopBar` | activé/désactivé | activé | Visibilité de la barre de favoris horizontale. |
| `MiniExplorerConsole` | 15–85 | 40 | Hauteur de la console en pourcentage de la fenêtre. |
| `ConsoleFontSizeX10` | 60–280 | 85 | Taille de police de la console ×10 (85 = 8,5 pt), modifiée avec Ctrl+molette. |

### Démarrage automatique & zone de notification

| Paramètre | Plage | Valeur par défaut | Description |
|---|---|---|---|
| Démarrage automatique avec Windows | activé/désactivé | désactivé | Écrit dans `HKCU\...\Run` (« Tilettes »). |
| Après le démarrage automatique - aller dans la zone de notification | activé/désactivé | désactivé | Ajoute `--minimized` : le panneau démarre masqué dans la zone de notification. |
| Réduire au lieu de fermer | activé/désactivé | activé | ✕ / Alt+F4 masque dans la zone de notification (ou réduit) au lieu de quitter. La sortie se fait via le menu de la zone de notification. |
| Toujours garder l'icône de notification | activé/désactivé | activé | Icône de notification visible en permanence. |
| Mémoriser l'onglet actif | activé/désactivé | activé | Restaure le dernier onglet actif au lancement. |

### Sauvegarde & synchronisation du menu Démarrer

- **Sauvegarder maintenant** — zip de sauvegarde complet dans `autoBackup\` (paramètres, tuiles, icônes, favoris, historique de recherche, l'exe) ; programmé par « Sauvegarder tous les N jours » (0 = désactivé), créé ~3 minutes après le lancement lorsque le délai est atteint.
- **Enregistrer la sauvegarde (zip)** — la même archive dans un fichier choisi par l'utilisateur.
- **Restaurer une archive…** — attend un zip créé par Tilettes lui-même ; les fichiers sont décompressés dans le dossier de travail, `Tilettes.exe` n'est jamais remplacé.
- **Synchroniser le menu Démarrer** maintenant / toutes les N heures (0 = désactivé) — reconstruit l'onglet miroir du menu Démarrer.

## Raccourcis clavier & commandes

### Panneau principal

| Touches / action | Résultat |
|---|---|
| Raccourci clavier (par défaut Ctrl+Q) | Afficher / activer le panneau. |
| Tapez simplement du texte, ou Ctrl+F | Ouvrir la recherche du panneau. |
| ↓ | Aller à la liste des résultats. |
| Enter | Ouvrir le résultat sélectionné (dossier → naviguer, fichier → lancer). |
| Esc | Fermer la recherche. |
| Clic sur une tuile | Lancer l'élément ; un dossier ouvre la navigation (ou une fenêtre contextuelle, selon les paramètres). |
| Ctrl+clic sur une tuile de dossier | Ouvrir le mini-explorateur (si activé). |
| Déplacer une tuile (mode d'édition) | La déplacer ; la déposer sur un dossier pour la placer à l'intérieur. |
| Déposer des fichiers sur le panneau (mode d'édition) | Les ajouter comme tuiles (déposer sur un dossier pour les ajouter à l'intérieur). |
| Clic droit sur une tuile | Menu natif de l'Explorateur plus : Description…, Taille 1×1–6×6, Renommer, Changer l'icône, Supprimer, Sortir du dossier, Ouvrir dans le mini-explorateur (dossiers). |
| Clic droit sur un onglet | Supprimer (le dernier onglet est protégé), Renommer, Basculer libre/grille. |
| Déplacer un onglet | Réordonner dans une ligne ou déplacer vers une autre ligne. |
| Clic droit sur une zone vide du panneau | Créer un dossier, Paramètres. |
| Boutons ▦ / ✅ / ⚙ | Visibilité de la grille, mode d'édition, paramètres. |

### Mini-explorateur

| Touches / action | Résultat |
|---|---|
| Ctrl+L / F4 / Modifier | Modifier le chemin. |
| F5 | Rafraîchir le dossier. |
| Backspace | Monter d'un niveau. |
| Alt+← / Alt+→ | Précédent / suivant. |
| Enter / double-clic | Ouvrir (un dossier navigue, un fichier se lance). |
| Esc | Quitter la recherche → annuler la modification du chemin → fermer la fenêtre. |
| Saisie dans la liste de fichiers | Démarre une recherche dans le champ de recherche. |
| Bas / Haut (dans la recherche) | Se déplacer dans les résultats. |
| Ctrl+molette de la souris | Taille de police de la console (conservée). |
| Déplacer le séparateur | Hauteur de la console (conservée). |
| Boutons ≡ / ☰ | Basculer le panneau latéral de favoris / la barre de favoris supérieure. |
| Clic droit sur un fichier | Ouvrir, Afficher dans l'Explorateur, Copier le chemin. |
| Clic droit sur un dossier | Ouvrir, Ajouter aux favoris, Ouvrir dans l'Explorateur. |
| Clic droit sur une zone vide | Rafraîchir, Copier le chemin du dossier, Ouvrir dans l'Explorateur, Ajouter le dossier courant aux favoris, Ouvrir une fenêtre de console ici. |
| Clic droit sur un favori | Modifier la commande… (commandes uniquement), Renommer…, Monter / Descendre, Supprimer. |

### Console

N'importe quelle commande `cmd.exe` sur une seule ligne peut être saisie et exécutée (Enter ou **Exécuter**). Le répertoire de travail est resynchronisé sur le dossier courant avant chaque commande. **+ Enregistrer** stocke la commande saisie comme favori (éventuellement dans un groupe) ; les commandes enregistrées s'exécutent au clic. Boutons : **Effacer** (vider la sortie), **Redémarrer** (nouveau cmd.exe), **Nouvelle fenêtre** (une vraie fenêtre de console dans le dossier courant). L'historique des commandes est disponible avec ↑ / ↓ pendant la session.

## Limitations

- **Windows + .NET Framework 4.8 uniquement** (GDI/WinForms). Pas de prise en compte du DPI par moniteur — l'interface peut apparaître floue sur les écrans fortement mis à l'échelle.
- **L'étendue de recherche « Tout »** n'indexe que les **disques locaux fixes** (pas les lecteurs USB/réseau), avec un plafond de **200 000 éléments par disque** ; l'indexation s'exécute en arrière-plan, donc les résultats s'étoffent pendant qu'elle travaille (« indexation : N » dans la ligne d'état).
- **La recherche du panneau** affiche les **200** meilleures correspondances ; **la recherche du mini-explorateur** renvoie jusqu'à **400** ; une liste de fichiers affiche au plus **800** entrées par répertoire.
- **La console est `cmd.exe` uniquement** : commandes sur une seule ligne ; les programmes interactifs/TUI (éditeurs, pagers avec saisie clavier) ne fonctionnent pas correctement ; le tampon de sortie s'efface automatiquement après ~150 000 caractères ; l'encodage suit la page de code OEM du système (par ex. CP866).
- **Le raccourci global** est une lettre/un chiffre plus des modificateurs ; l'enregistrement échoue avec une bulle de notification si un autre programme le possède déjà.
- **La taille du panneau revient à la taille de démarrage à chaque lancement** — seule la position est mémorisée (c'est voulu).
- **Le glisser-déposer et le déplacement des tuiles exigent le mode d'édition** (« Autoriser l'ajout d'icônes » / bouton ✅).
- Les tuiles de dossier affichent au plus **9** icônes enfants ; la fenêtre contextuelle d'un dossier affiche au plus **4** colonnes par ligne.
- Les éléments `.lnk`/`.ico` ajoutés au panneau sont **copiés dans `ico\`** afin de survivre au déplacement des originaux.
- **Restaurer une archive** n'accepte que les zips créés par Tilettes (« Sauvegarder maintenant » / « Enregistrer la sauvegarde (zip) »).
- Les coins arrondis de la fenêtre sont temporairement retirés pendant le redimensionnement (technique pour éviter le scintillement) et restaurés au relâchement.
- La correction de disposition couvre la paire EN↔RU QWERTY ; les autres dispositions passent sans modification.
- **Instance unique** : lancer une seconde copie affiche simplement la fenêtre existante.
- La sortie automatique des dossiers fonctionne uniquement en mode « Même fenêtre » et uniquement lorsqu'on se trouve dans un dossier.

## Compilation

Nécessite n'importe quel Windows avec .NET Framework 4.x (le compilateur fait partie du système) :

```
build.bat
```

ou directement :

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /win32icon:app.ico /out:Tilettes.exe src\*.cs
```

Les versions sont créées automatiquement par GitHub Actions à chaque tag `v*` : le workflow compile les variantes de l'exe avec le même appel csc et attache des fichiers exe simples à la version (AnyCPU universel + x86 + x64 — sans zip) ; les archives « Source code » automatiques de GitHub sont également présentes sur la version. Vous pouvez aussi compiler l'exe vous-même avec `build.bat`.

## Fichiers de données (créés à côté de l'EXE)

| Fichier | Rôle |
|---|---|
| `settings.ini` | Tous les paramètres |
| `records.xml` | Onglets, dossiers, raccourcis, descriptions |
| `bookmarks.xml` | Favoris du mini-explorateur |
| `filetypes.xml` | Règles de types de fichiers |
| `ico\` | Copies des éléments .lnk/.ico et icônes personnalisées |

## Structure du projet (`src/`)

| Fichier | Rôle |
|---|---|
| `Program.cs` | Fenêtre principale : onglets, tuiles, recherche du panneau, fenêtres contextuelles de dossiers, instance unique |
| `MiniExplorerForm.cs` | Mini-explorateur : navigation, favoris, console intégrée |
| `SearchCore.cs` | Indexation des disques, préfiltre à masque de bits, notation floue |
| `PanelSearch.cs` | Métadonnées consultables des éléments enregistrés |
| `Settings.cs` / `SettingsForm.cs` | Modèle de paramètres et boîte de dialogue |
| `FileTypes.cs` / `FileTypesForm.cs` | Règles de types de fichiers et leurs éditeurs |
| `bookmarks.cs` | Stockage des favoris |
| `loc.cs` + `lang_*.cs` | Localisation : source EN, RU en ligne, tables ES/PT/DE/FR/IT/PL/ZH/JA |
| `UpdateChecker.cs` / `WelcomeForm.cs` | Vérification des mises à jour (GitHub Releases) et fenêtre de bienvenue du premier démarrage |
| `IconExtractor.cs`, `NativeContextMenu.cs`, `IniFile.cs`, `Records.cs`, `apputil.cs` | Icônes, menus natifs, E/S INI, E/S des enregistrements, démarrage auto/instance unique/portefeuilles |

## Licence

[MIT](LICENSE) — libre d'utilisation, de modification et de distribution.

<a name="donate"></a>
## Faire un don

Si Tilettes vous est utile, vous pouvez soutenir le développement en crypto-monnaie. Les réseaux compatibles EVM partagent une même adresse — envoyez sur le réseau qui vous convient :

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

Autres façons d'aider : signaler bugs et idées dans [Issues](https://github.com/AlexNoVibe/Tilettes/issues), mettre une étoile au dépôt, faire connaître Tilettes.

<a name="changelog"></a>
## Journal des modifications

### v0.6.0-beta (2026-10-01)

- Performances : démarrage plus rapide (rendu paresseux des onglets — seul l'onglet actif est construit), cache d'icônes persistant (les icônes shell sont extraites une seule fois par durée de vie d'une tuile), métadonnées de recherche collectées une seule fois à l'ajout d'un élément ; le champ de recherche du mini-explorateur est désactivé (code conservé pour une réactivation éventuelle).
- Les partages réseau ne bloquent jamais le thread de l'interface : les icônes et les cibles .lnk sur les chemins réseau sont résolues en arrière-plan.
- Un habillage actif pilote désormais la palette claire/sombre partout (choisir Mint ne laisse plus de fenêtres sombres) ; Mint est le thème par défaut et toutes les polices valent 14 par défaut.
- Premier démarrage : sur les moniteurs dont la zone de travail fait moins de 900px, la fenêtre et la grille par défaut se réduisent proportionnellement pour s'adapter.
- Durcissement : strong name, VERSIONINFO, manifeste explicite, capture de la touche Win en opt-in — 0 détection sur VirusTotal.
- Les fichiers de version sont des exe simples par CPU (AnyCPU/x86/x64) au lieu d'une archive zip ; les notes de version proviennent de CHANGELOG.md.

### v0.5 (2026-09-30)

- Fenêtre de bienvenue au premier démarrage (une fois par dossier de données) : remerciements, avertissement bêta + lien vers les issues, mini-diagramme dessiné, choix de la langue (drapeaux RU/EN), consentement pour la vérification des mises à jour, adresses de don (clic pour copier) ; sortie via « Fermer » ou « Fermer & créer des tuiles d'exemple » (Bloc-notes / Calculatrice / Explorateur / Paint en tuiles prêtes à l'emploi). Rejouable depuis les paramètres.
- Vérification des mises à jour : l'application interroge l'API publique de GitHub Releases pour le dernier tag tous les N jours (par défaut 3 ; la première vérification a également lieu N jours après l'installation) — strictement uniquement avec l'accord de l'utilisateur, zéro requête réseau sinon. Lorsqu'une version plus récente existe, une pastille verte « Mettre à jour » apparaît à côté du bouton des paramètres et ouvre la page des versions. Bouton manuel « Vérifier maintenant » dans les paramètres (le résultat est affiché dans une boîte de message). L'installation automatique est un stub (TODO). Hook de test pour la pastille : `WINPANEL_MOCK_UPDATE=0.6`.
- Paramètres : nouvelle section « Mises à jour » (bascule de vérification, intervalle en jours, bouton « Vérifier maintenant », stub d'installation automatique, ligne de don avec un menu contextuel de portefeuilles — un clic copie l'adresse — et un bouton pour rejouer la fenêtre de bienvenue). Champ de raccourci éditable : toute combinaison Ctrl/Alt/Shift/Win + lettre/chiffre peut être saisie (avec validation), la valeur par défaut passe à Ctrl+Q.
- Interface du programme localisée en **10 langues** : anglais (source), russe, espagnol, portugais, allemand, français, italien, polonais, chinois (simplifié) et japonais. La langue se choisit dans les paramètres ou via les drapeaux dessinés de la fenêtre de bienvenue ; ajouter une langue = un fichier-table + une ligne (voir loc.cs).
- Véritable liste de portefeuilles de don groupée par chaîne : les réseaux EVM (ETH · Polygon · Base · Monad · HyperEVM) partagent une adresse ; plus Bitcoin, Solana et Sui.
- La version est désormais une constante unique (`AppInfo.AppVersion`) ; l'info-bulle de la zone de notification et la fenêtre de bienvenue l'affichent.
- Infrastructure GitHub : licence MIT, FUNDING.yml (liens de sponsoring vers les ancres des portefeuilles), README divisé en fichiers par langue (`README.md` EN + 9 traductions) pour une extension facile, workflow GitHub Actions (version avec un zip portable sur les tags `v*`), page d'accueil dans docs/ pour GitHub Pages. Tous les messages de commit de l'historique sont en anglais.

### v0.4 (2026-09-29)

- Rebranding : Tilettes / «Плиточки», nouvelle icône (ressource exe + icône de notification dessinée par le code).
- Fonction de sauvegarde achevée : « Enregistrer la sauvegarde (zip) » regroupe paramètres, tuiles, icônes, favoris, historique de recherche et l'exe ; « Restaurer une archive » décompresse les zips créés par l'application dans le dossier de travail sans remplacer Tilettes.exe ; sauvegardes complètes programmées dans autoBackup\.
- Refonte de la boîte de dialogue des paramètres : redimensionnable verticalement via une poignée en bas, info-bulles sur chaque élément, champs X/Y libellés pour la taille de démarrage et la position de la fenêtre, colonnes/lignes de la grille sur une seule ligne, liste déroulante d'habillages à la place de la case à cocher de thème clair en doublon.
- Corrections de l'onglet miroir du menu Démarrer (le contenu des dossiers ne s'agglutine plus ; mise en page automatique simplifiée).
- Corrections de mise en page pour les polices 14–20 (onglets, hauteur de l'état de la recherche, boîtes de dialogue, barres du mini-explorateur, marche du coin de la fenêtre).

### v0.3 (2026-09-28)

- Synchronisation du menu Démarrer (onglet miroir, programmée), échelles de réduction des bascules de recherche, bande de réglages rapides de la recherche, mode multi-sélection, menus « Déplacer vers l'onglet », capture de la touche Win, navigation dans les fenêtres contextuelles de dossiers, surlignage des chemins dans les résultats de recherche.

### v0.1 – v0.2 (2026-09-27)

- Premières compilations du panneau lanceur : tuiles, onglets, dossiers, recherche du panneau, mini-explorateur avec console, règles de types de fichiers, zone de notification/démarrage automatique/raccourci, personnalisation de la grille.
