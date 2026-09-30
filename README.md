# Tilettes / Плиточки

**EN** — A fast-launch panel for Windows: a tile grid with shortcuts, folders and tabs, built-in fuzzy search and a mini explorer with an embedded console. Single portable EXE, no installer, .NET Framework 4.8 (WinForms).

**RU** — Панель быстрого запуска для Windows: сетка плиток с ярлыками, папками и вкладками, встроенный fuzzy-поиск и мини-проводник с консолью. Один портативный EXE, без установки, .NET Framework 4.8 (WinForms).

---

## [Documentation in English](#english) · [Документация на русском](#русский)

<a name="english"></a>
# English

## Overview

Tilettes replaces the desktop-shortcut mess with one borderless panel: tiles launch programs, folders hold groups, tabs organize everything. Every item can carry a description that is searchable and shown as a hover tooltip. The integrated mini explorer adds file browsing, bookmarks and a working console to the same launcher.

Current version: **v0.3** (see the repository history for the changelog).

## Features

- **Panel** — tiles sized 1×1…4×4, unlimited tabs (draggable, multi-row), folders opened in-place or as popups, drag-and-drop from Explorer, custom grid (columns/rows/transparency), icon scaling.
- **Search** — searches names, file names, program metadata (FileDescription / ProductName / CompanyName), full paths and user descriptions; fuzzy matching with adjustable accuracy and wrong-keyboard-layout correction (`руддщ` → `hello`); results are ranked by match quality and matched characters are highlighted.
- **Mini explorer** — breadcrumb navigation, folder/command/group bookmarks, file search (current folder or all fixed drives) with a background index, and an embedded `cmd.exe` console with command history, saved commands and Ctrl+wheel font zoom.
- **File type rules** — per-extension/per-mask icons and "open with" associations, import/export.
- **Desktop integration** — tray icon, autostart with Windows, global hotkey, native Explorer context menus, borderless window with edge resizing.

## Settings reference

All settings live in one dialog (⚙ button / tray menu) and are stored in `settings.ini`.

### Startup & window

| Setting | Range | Default | Description |
|---|---|---|---|
| Startup Size (W × H) | 200–4000 | 900 × 800 | Panel size on every launch. Resizing during a session is not persisted — only the position is. |
| Window Position (X, Y) | −4000…4000 | 100, 100 | Screen position on launch. Updated automatically when the window is moved. |
| Show window hotkey | presets + custom | Ctrl+J | Global hotkey that shows/activates the panel. Presets: None, Ctrl+J, Ctrl+Shift+J, Ctrl+Alt+J, Ctrl+K, Ctrl+Shift+K, Alt+J; a custom `Mod+Key` combination can be typed into `HotkeyShow` in the INI. |
| Language | ru / en | ru | UI language, applied immediately. |

### Grid & tiles

| Setting | Range | Default | Description |
|---|---|---|---|
| Grid Transparency | 0–255 | 50 | Alpha of the grid lines. 0 = invisible. Only drawn when the grid toggle (▦) is on. |
| Grid Columns | 1–100 | 16 | Horizontal cells. Tile positions snap to this grid. |
| Grid Rows | 1–100 | 16 | Vertical cells. |
| Def. Item Size | 1–4 | 2 | Size of newly added tiles (1×1 … 4×4 cells). |
| Icon Scale (%) | 25–400 | 100 | Icon size inside a tile, percent of the default. |
| Allow adding icons | on/off | on | Edit mode: dragging tiles, creating folders, dropping files. When off, tiles simply launch on click. |
| Light Theme | on/off | off | Light or dark color scheme. |

### Folders

| Setting | Range | Default | Description |
|---|---|---|---|
| Open folders in | Same window / Popup window | Same window | Clicking a folder navigates inside the tab or opens a popup above everything. |
| sec idle | 0–600 | 15 | Same-window mode only: automatically go back up after N seconds without mouse/keyboard activity. 0 = off. |

### Fonts

One row each for **Tiles**, **Tabs** and **UI**:

| Setting | Range | Default | Description |
|---|---|---|---|
| size | 6–24 | 9 | Font size for the group. |
| color swatch | any color | empty | Custom text color; empty = theme default. Applies to tiles' labels, tab captions or all UI text. |
| family | any installed font | Segoe UI | Font family for the group. |

### Search

| Setting | Range | Default | Description |
|---|---|---|---|
| Fuzzy accuracy (0–3) | 0–3 | 2 | 0 = substring matches only; 1–3 = increasingly tolerant typo/fuzzy matching. Digits count double, so numeric codes match strictly. |
| Search in metadata | on/off | on | File name, shortcut target, version info (description, product, company). |
| Search in full paths | on/off | on | The full path text, including parent folders. |
| Search in descriptions | on/off | on | User descriptions (right-click → Description…). |
| Search font: box | 7–30 | 9 | Font size of the search input. |
| Search font: results | 7–30 | 9 | Font size of result rows (row height follows the font). |

### Mini explorer (INI keys)

| Key | Range | Default | Description |
|---|---|---|---|
| Ctrl+Click a folder opens Mini Explorer | on/off | on | The Ctrl+click shortcut on folder tiles. |
| `MiniExplorerW/H/X/Y` | W≥760, H≥520 | auto | Window geometry, remembered on close. |
| `MiniExplorerBookmarks` | on/off | on | Side bookmarks panel visibility. |
| `MiniExplorerTopBar` | on/off | on | Horizontal bookmarks bar visibility. |
| `MiniExplorerConsole` | 15–85 | 40 | Console height as percent of the window. |
| `ConsoleFontSizeX10` | 60–280 | 85 | Console font size ×10 (85 = 8.5 pt), changed with Ctrl+wheel. |

### Autostart & tray

| Setting | Range | Default | Description |
|---|---|---|---|
| Autostart with Windows | on/off | off | Writes `HKCU\...\Run` ("Tilettes"). |
| After autostart - go to tray | on/off | off | Adds `--minimized`: the panel starts hidden in the tray. |
| Minimize instead of close | on/off | on | ✕ / Alt+F4 hides to tray (or minimizes) instead of exiting. Exit is in the tray menu. |
| Always keep tray icon | on/off | on | Tray icon visible at all times. |
| Remember active tab | on/off | on | Restores the last active tab on launch. |

### Backup

- **Save backup** — saves the whole `settings.ini` to any file.
- **Restore backup** — copies a saved INI back; takes effect when the settings dialog is closed with Save. Covers settings only, not `records.xml` / `bookmarks.xml` / `filetypes.xml`.

## Hotkeys & commands

### Main panel

| Keys / action | Result |
|---|---|
| Hotkey (default Ctrl+J) | Show / activate the panel. |
| Just type any text, or Ctrl+F | Open the panel search. |
| ↓ | Jump into the result list. |
| Enter | Open the selected result (folder → navigate, file → launch). |
| Esc | Close the search. |
| Click a tile | Launch the item; folder navigates (or popup, per settings). |
| Ctrl+Click a folder tile | Open the mini explorer (if enabled). |
| Drag a tile (edit mode) | Move it; drop onto a folder to move it inside. |
| Drop files onto the panel (edit mode) | Add as tiles (drop onto a folder to add inside). |
| Right-click a tile | Native Explorer menu plus: Description…, Size 1×1–4×4, Rename, Change Icon, Remove, Move out of folder, Open in Mini Explorer (folders). |
| Right-click a tab | Delete (last tab is protected), Rename, Toggle free/grid layout. |
| Drag a tab | Reorder within a row or move to another row. |
| Right-click empty panel | Create Folder, Settings. |
| ▦ / ✅ / ⚙ buttons | Grid visibility, edit mode, settings. |

### Mini explorer

| Keys / action | Result |
|---|---|
| Ctrl+L / F4 / Edit | Edit the path. |
| F5 | Refresh the folder. |
| Backspace | Up one level. |
| Alt+← / Alt+→ | Back / forward. |
| Enter / double-click | Open (folder navigates, file launches). |
| Esc | Exit search → cancel path edit → close the window. |
| Typing in the file list | Starts a search in the search box. |
| Down / Up (in search) | Move through results. |
| Ctrl+mouse wheel | Console font size (persisted). |
| Drag the splitter | Console height (persisted). |
| ≡ / ☰ buttons | Toggle side bookmarks panel / top bookmarks bar. |
| Right-click a file | Open, Show in Explorer, Copy path. |
| Right-click a folder | Open, Add to bookmarks, Open in Explorer. |
| Right-click empty space | Refresh, Copy folder path, Open in Explorer, Add current folder to bookmarks, Open console window here. |
| Right-click a bookmark | Edit command… (commands only), Rename…, Move up / Move down, Remove. |

### Console

Any single-line `cmd.exe` command can be typed and run (Enter or **Run**). The working directory is re-synced to the current folder before every command. **+ Save** stores the typed command as a bookmark (optionally inside a group); saved commands run on click. Buttons: **Clear** (wipe output), **Restart** (new cmd.exe), **New window** (a real console window at the current folder). Command history is available with ↑ / ↓ during the session.

## Limitations

- **Windows + .NET Framework 4.8 only** (GDI/WinForms). No per-monitor DPI awareness — the UI may blur on heavily scaled displays.
- **"All" search scope** indexes **fixed local drives only** (no USB/network drives), capped at **200 000 items per drive**; indexing runs in the background, so results grow while it works ("indexing: N" in the status line).
- **Panel search** shows the best **200** matches; **mini explorer search** returns up to **400**; a file list shows at most **800** entries per directory.
- **Console is `cmd.exe` only**: single-line commands; interactive/TUI programs (editors, pagers with key input) do not work properly; the output buffer auto-clears after ~150 000 characters; encoding follows the system OEM code page (e.g. CP866).
- **Global hotkey** is one letter/digit plus modifiers; registration fails with a balloon tip if another program already owns it.
- **Panel size resets to Startup Size on every launch** — only the position is remembered (by design).
- **Drag & drop and tile moving require edit mode** ("Allow adding icons" / ✅ button).
- Folder tiles preview at most **9** child icons; the folder popup shows at most **4** columns per row.
- `.lnk`/`.ico` items added to the panel are **copied into `ico\`** so they survive moving of the originals.
- **Backup covers settings only** — shortcuts (`records.xml`), bookmarks and file-type rules are not included.
- Rounded window corners are temporarily removed while resizing (technique to avoid flicker) and restored on release.
- The layout fix covers the EN↔RU QWERTY pair; other layouts are passed through untouched.
- **Single instance**: launching a second copy just shows the existing window.
- Folder auto-exit works only in the "Same window" mode and only while inside a folder.

## Build

Requires any Windows with .NET Framework 4.x (the compiler ships with the OS):

```
build.bat
```

or directly:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /win32icon:app.ico /out:Tilettes.exe src\*.cs
```

## Data files (created next to the EXE)

| File | Purpose |
|---|---|
| `settings.ini` | All settings |
| `records.xml` | Tabs, folders, shortcuts, descriptions |
| `bookmarks.xml` | Mini explorer bookmarks |
| `filetypes.xml` | File type rules |
| `ico\` | Copies of .lnk/.ico items and custom icons |

## Project layout (`src/`)

| File | Purpose |
|---|---|
| `Program.cs` | Main window: tabs, tiles, panel search, folder popups, single-instance |
| `MiniExplorerForm.cs` | Mini explorer: navigation, bookmarks, embedded console |
| `SearchCore.cs` | Disk indexing, bit-mask prefilter, fuzzy scoring |
| `PanelSearch.cs` | Searchable metadata of saved items |
| `Settings.cs` / `SettingsForm.cs` | Settings model and dialog |
| `FileTypes.cs` / `FileTypesForm.cs` | File type rules and their editors |
| `bookmarks.cs` | Bookmark storage |
| `loc.cs` | RU/EN localization |
| `IconExtractor.cs`, `NativeContextMenu.cs`, `IniFile.cs`, `Records.cs`, `apputil.cs` | Icons, native menus, INI I/O, records I/O, autostart/single-instance |

---

<a name="русский"></a>
# Русский

## Обзор

Плиточки (Tilettes) заменяют захламлённый рабочий стол одной безрамочной панелью: плитки запускают программы, папки группируют, вкладки наводят порядок. У каждого элемента есть описание — оно ищется поиском и всплывает подсказкой при наведении. Встроенный мини-проводник добавляет к лаунчеру просмотр файлов, закладки и рабочую консоль.

Текущая версия: **v0.3** (история изменений — в коммитах репозитория).

## Возможности

- **Панель** — плитки 1×1…4×4, неограниченные вкладки (перетаскиваются, в несколько рядов), папки внутри вкладки или всплывающим окном, drag-and-drop из Проводника, настраиваемая сетка (колонки/строки/прозрачность), масштаб иконок.
- **Поиск** — по именам, именам файлов, метаданным программ (FileDescription / ProductName / CompanyName), полным путям и пользовательским описаниям; fuzzy-поиск с настраиваемой точностью и исправлением неверной раскладки (`руддщ` → `hello`); результаты ранжируются по качеству совпадения, совпавшие символы подсвечиваются.
- **Мини-проводник** — навигация по «хлебным крошкам», закладки (папки / команды / группы), поиск файлов (текущая папка или все диски) с фоновым индексом, встроенная консоль `cmd.exe` с историей команд, сохранёнными командами и зумом шрифта Ctrl+колесом.
- **Правила типов файлов** — иконки и «открывать через» по расширению или маске, импорт/экспорт.
- **Интеграция с системой** — значок в трее, автозапуск с Windows, глобальная горячая клавиша, системные контекстные меню Проводника, безрамочное окно с ресайзом за края.

## Описание всех настроек

Все настройки — в одном окне (кнопка ⚙ / меню трея), хранятся в `settings.ini`.

### Запуск и окно

| Настройка | Диапазон | По умолчанию | Описание |
|---|---|---|---|
| Размер при запуске (W × H) | 200–4000 | 900 × 800 | Размер панели при каждом запуске. Изменение размера в сессии не запоминается — только позиция. |
| Позиция окна (X, Y) | −4000…4000 | 100, 100 | Позиция на экране при запуске. Обновляется автоматически при перемещении окна. |
| Горячая клавиша показа | пресеты + своя | Ctrl+J | Глобальная горячая клавиша показа/активации панели. Пресеты: None, Ctrl+J, Ctrl+Shift+J, Ctrl+Alt+J, Ctrl+K, Ctrl+Shift+K, Alt+J; свою комбинацию `Мод+Клавиша` можно вписать в `HotkeyShow` в INI. |
| Язык | ru / en | ru | Язык интерфейса, применяется сразу. |

### Сетка и плитки

| Настройка | Диапазон | По умолчанию | Описание |
|---|---|---|---|
| Прозрачность сетки | 0–255 | 50 | Альфа-канал линий сетки. 0 = невидима. Рисуется только при включённой сетке (кнопка ▦). |
| Колонки сетки | 1–100 | 16 | Горизонтальные ячейки. Позиции плиток привязаны к сетке. |
| Строки сетки | 1–100 | 16 | Вертикальные ячейки. |
| Размер элемента | 1–4 | 2 | Размер новых плиток (1×1 … 4×4 ячейки). |
| Масштаб иконок (%) | 25–400 | 100 | Размер иконки внутри плитки в процентах от стандартного. |
| Разрешать добавлять значки | вкл/выкл | вкл | Режим редактирования: перетаскивание плиток, создание папок, приём файлов. Когда выключен — плитки просто запускаются по клику. |
| Светлая тема | вкл/выкл | выкл | Светлая или тёмная цветовая схема. |

### Папки

| Настройка | Диапазон | По умолчанию | Описание |
|---|---|---|---|
| Папки при открытии | В этом же окне / Во всплывающем окне | В этом же окне | Клик по папке открывает её внутри вкладки или всплывающим окном поверх всего. |
| сек простоя | 0–600 | 15 | Только для режима «в этом же окне»: автоматически выйти из папки после N секунд без активности мыши/клавиатуры. 0 = выключено. |

### Шрифты

По строке на группу: **плитки**, **вкладки**, **интерфейс**.

| Настройка | Диапазон | По умолчанию | Описание |
|---|---|---|---|
| размер | 6–24 | 9 | Размер шрифта группы. |
| цвет | любой | пусто | Свой цвет текста; пусто = цвет темы. Действует на подписи плиток, названия вкладок или весь текст интерфейса. |
| гарнитура | любой установленный шрифт | Segoe UI | Шрифтовое семейство группы. |

### Поиск

| Настройка | Диапазон | По умолчанию | Описание |
|---|---|---|---|
| Точность fuzzy (0–3) | 0–3 | 2 | 0 = только подстроки; 1–3 — всё более терпимый fuzzy-поиск с опечатками. Цифры весят вдвое, поэтому числовые коды сопоставляются строго. |
| Искать в метаданных | вкл/выкл | вкл | Имя файла, цель ярлыка, сведения о версии (описание, продукт, компания). |
| Искать в полных путях | вкл/выкл | вкл | Текст полного пути, включая родительские папки. |
| Искать в описаниях | вкл/выкл | вкл | Пользовательские описания (ПКМ → Описание…). |
| Шрифт строки поиска | 7–30 | 9 | Размер шрифта поля поиска. |
| Шрифт результатов | 7–30 | 9 | Размер шрифта строк результатов (высота строки следует за шрифтом). |

### Мини-проводник (ключи INI)

| Ключ | Диапазон | По умолчанию | Описание |
|---|---|---|---|
| Ctrl+ЛКМ по папке — мини-проводник | вкл/выкл | вкл | Сочетание Ctrl+клик на плитке папки. |
| `MiniExplorerW/H/X/Y` | W≥760, H≥520 | авто | Геометрия окна, запоминается при закрытии. |
| `MiniExplorerBookmarks` | вкл/выкл | вкл | Видимость боковой панели закладок. |
| `MiniExplorerTopBar` | вкл/выкл | вкл | Видимость верхней полосы закладок. |
| `MiniExplorerConsole` | 15–85 | 40 | Высота консоли в процентах от окна. |
| `ConsoleFontSizeX10` | 60–280 | 85 | Размер шрифта консоли ×10 (85 = 8.5 pt), меняется Ctrl+колесом. |

### Автозагрузка и трей

| Настройка | Диапазон | По умолчанию | Описание |
|---|---|---|---|
| Автозапуск с Windows | вкл/выкл | выкл | Запись в `HKCU\...\Run` («Tilettes»). |
| После автозапуска — сразу в трей | вкл/выкл | выкл | Добавляет `--minimized`: панель стартует скрытой в трее. |
| Сворачивать в трей вместо закрытия | вкл/выкл | вкл | ✕ / Alt+F4 скрывает в трей (или минимизирует), а не завершает. Выход — в меню трея. |
| Держать значок в трее | вкл/выкл | вкл | Значок в трее виден всегда. |
| Запоминать активную вкладку | вкл/выкл | вкл | Восстанавливает последнюю активную вкладку при запуске. |

### Бекап

- **Сохранить бекап** — сохраняет весь `settings.ini` в выбранный файл.
- **Восстановить бекап** — копирует сохранённый INI обратно; применяется при закрытии окна настроек. Охватывает только настройки — не `records.xml` / `bookmarks.xml` / `filetypes.xml`.

## Горячие клавиши и команды

### Основная панель

| Клавиши / действие | Результат |
|---|---|
| Горячая клавиша (по умолчанию Ctrl+J) | Показать / активировать панель. |
| Просто начните печатать, или Ctrl+F | Открыть поиск по панели. |
| ↓ | Перейти к списку результатов. |
| Enter | Открыть выбранный результат (папка — перейти, файл — запустить). |
| Esc | Закрыть поиск. |
| Клик по плитке | Запуск элемента; папка открывается (или попап — по настройке). |
| Ctrl+клик по плитке папки | Открыть мини-проводник (если включён). |
| Перетаскивание плитки (режим редактирования) | Переместить; бросить на папку — переместится внутрь. |
| Бросок файлов на панель (режим редактирования) | Добавить плитками (бросок на папку — внутрь неё). |
| ПКМ по плитке | Системное меню Проводника плюс: Описание…, Размер 1×1–4×4, Переименовать, Сменить иконку, Удалить, Вынести из папки, Открыть в мини-проводнике (папки). |
| ПКМ по вкладке | Удалить (последнюю нельзя), Переименовать, Переключить свободная/сетка. |
| Перетаскивание вкладки | Порядок в ряду или перенос в другой ряд. |
| ПКМ по пустому месту панели | Создать папку, Настройки. |
| Кнопки ▦ / ✅ / ⚙ | Видимость сетки, режим редактирования, настройки. |

### Мини-проводник

| Клавиши / действие | Результат |
|---|---|
| Ctrl+L / F4 / Изменить | Редактировать путь. |
| F5 | Обновить папку. |
| Backspace | На уровень вверх. |
| Alt+← / Alt+→ | Назад / вперёд. |
| Enter / двойной клик | Открыть (папка — перейти, файл — запустить). |
| Esc | Выйти из поиска → отменить правку пути → закрыть окно. |
| Печать в списке файлов | Начинает поиск (фокус в поле поиска). |
| ↓ / ↑ (в поиске) | Перемещение по результатам. |
| Ctrl+колесо мыши | Размер шрифта консоли (сохраняется). |
| Перетаскивание сплиттера | Высота консоли (сохраняется). |
| Кнопки ≡ / ☰ | Боковая панель закладок / верхняя полоса закладок. |
| ПКМ по файлу | Открыть, Показать в Проводнике, Копировать путь. |
| ПКМ по папке | Открыть, Добавить в закладки, Открыть в Проводнике. |
| ПКМ по пустому месту | Обновить, Копировать путь папки, Открыть в Проводнике, Добавить текущую папку в закладки, Открыть окно консоли здесь. |
| ПКМ по закладке | Изменить команду… (только для команд), Переименовать…, Вверх / Вниз, Удалить. |

### Консоль

Любую однострочную команду `cmd.exe` можно ввести и выполнить (Enter или кнопка **Выполнить**). Перед каждой командой рабочая папка синхронизируется с текущей. **+ Команда** сохраняет введённую команду закладкой (опционально в группу); сохранённые команды запускаются кликом. Кнопки: **Очистить** (стереть вывод), **Перезапуск** (новый cmd.exe), **Новое окно** (настоящее окно консоли в текущей папке). История команд доступна по ↑ / ↓ в течение сессии.

## Ограничения

- **Только Windows + .NET Framework 4.8** (GDI/WinForms). Нет per-monitor DPI — на сильно масштабированных экранах интерфейс может быть слегка размытым.
- **Область «Везде»** индексирует **только локальные фиксированные диски** (USB/сеть не входят), лимит **200 000 объектов на диск**; индекс строится в фоне — результаты пополняются по мере работы (в строке статуса видно «индексация: N»).
- **Поиск по панели** показывает лучшие **200** совпадений; **поиск мини-проводника** — до **400**; в списке файлов — максимум **800** записей на папку.
- **Консоль — только `cmd.exe`**: однострочные команды; интерактивные/TUI-программы (редакторы, пейджеры с вводом с клавиатуры) корректно не работают; буфер вывода автоматически очищается после ~150 000 символов; кодировка — системная OEM (например, CP866).
- **Глобальная горячая клавиша** — одна буква/цифра плюс модификаторы; если комбинация уже занята другой программой, регистрация не удастся (появится всплывающее уведомление).
- **Размер панели сбрасывается к «Размеру при запуске» при каждом старте** — запоминается только позиция (сделано намеренно).
- **Перетаскивание плиток и приём файлов требуют режима редактирования** («Разрешать добавлять значки» / кнопка ✅).
- Плитка папки показывает не более **9** мини-иконок; попап папки — не более **4** колонок в ряду.
- Добавленные на панель `.lnk`/`.ico` **копируются в `ico\`**, чтобы не терялись при перемещении оригиналов.
- **Бекап охватывает только настройки** — ярлыки (`records.xml`), закладки и правила типов файлов в него не входят.
- Скруглённые углы окна временно отключаются при ресайзе (техника против мерцания) и возвращаются после.
- Исправление раскладки работает для пары EN↔RU (QWERTY/ЙЦУКЕН); другие раскладки проходят без изменений.
- **Один экземпляр**: повторный запуск просто показывает уже открытое окно.
- Авто-выход из папки по простою работает только в режиме «В этом же окне» и только находясь внутри папки.

## Сборка

Нужна любая Windows с .NET Framework 4.x (компилятор входит в состав системы):

```
build.bat
```

или напрямую:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /win32icon:app.ico /out:Tilettes.exe src\*.cs
```

## Файлы данных (создаются рядом с EXE)

| Файл | Назначение |
|---|---|
| `settings.ini` | Все настройки |
| `records.xml` | Вкладки, папки, ярлыки, описания |
| `bookmarks.xml` | Закладки мини-проводника |
| `filetypes.xml` | Правила по типам файлов |
| `ico\` | Копии .lnk/.ico и пользовательских иконок |

## Структура исходников (`src/`)

| Файл | Назначение |
|---|---|
| `Program.cs` | Главное окно: вкладки, плитки, поиск по панели, попапы папок, один экземпляр |
| `MiniExplorerForm.cs` | Мини-проводник: навигация, закладки, встроенная консоль |
| `SearchCore.cs` | Индексация дисков, битовые маски-префильтры, fuzzy-скоринг |
| `PanelSearch.cs` | Метаданные сохранённых элементов для поиска |
| `Settings.cs` / `SettingsForm.cs` | Модель и окно настроек |
| `FileTypes.cs` / `FileTypesForm.cs` | Правила по типам файлов и их редакторы |
| `bookmarks.cs` | Хранение закладок |
| `loc.cs` | Локализация RU/EN |
| `IconExtractor.cs`, `NativeContextMenu.cs`, `IniFile.cs`, `Records.cs`, `apputil.cs` | Иконки, системные меню, INI, хранение записей, автозапуск/один экземпляр |
