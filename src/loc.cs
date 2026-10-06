using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WinPanel
{
    // Tiny localization helper. The UI is written in English; Loc.S("text")
    // returns the translation for the current language (default: Russian).
    // A dictionary is provided so the most visible strings can be translated
    // centrally (Loc.Walk applies it to a whole control tree).
    //
    // 10 languages: English is the source of truth, Russian is inlined as the
    // second argument of S(en, ru) everywhere in the code, the other eight
    // live in lang_*.cs tables keyed by the exact English string. Adding a
    // language = one new lang_xx.cs file + one entry here (Languages, Table).
    public static class Loc
    {
        public static string Lang = "en";

        // Supported UI codes (order = settings combo / welcome flag order).
        public static readonly string[] Languages = { "en", "ru", "es", "pt", "de", "fr", "it", "pl", "zh", "ja" };

        public static bool IsSupported(string code)
        {
            foreach (var l in Languages) if (string.Equals(l, code, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool IsRu
        {
            get { return string.Equals(Lang, "ru", StringComparison.OrdinalIgnoreCase); }
        }

        public static bool IsEn
        {
            get { return string.Equals(Lang, "en", StringComparison.OrdinalIgnoreCase); }
        }

        private static Dictionary<string, string> Table()
        {
            switch (Lang)
            {
                case "ru": return Ru;
                case "es": return LangEs.Table;
                case "pt": return LangPt.Table;
                case "de": return LangDe.Table;
                case "fr": return LangFr.Table;
                case "it": return LangIt.Table;
                case "pl": return LangPl.Table;
                case "zh": return LangZh.Table;
                case "ja": return LangJa.Table;
                default: return null;
            }
        }

        // Native names for the settings combo and the welcome window tooltips
        // (index-aligned with Languages).
        public static string NativeName(string code)
        {
            switch (code)
            {
                case "en": return "English";
                case "ru": return "Русский";
                case "es": return "Español";
                case "pt": return "Português";
                case "de": return "Deutsch";
                case "fr": return "Français";
                case "it": return "Italiano";
                case "pl": return "Polski";
                case "zh": return "中文 (简体)";
                case "ja": return "日本語";
                default: return code;
            }
        }

        public static string S(string en, string ru)
        {
            if (IsRu) return ru;
            string t;
            var table = Table();
            if (table != null && table.TryGetValue(en, out t)) return t;
            return en;
        }

        public static string S(string en)
        {
            if (IsEn) return en;
            string t;
            var table = Table();
            if (table != null && table.TryGetValue(en, out t)) return t;
            return en;
        }

        public static readonly Dictionary<string, string> Ru = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Shared
            { "Save", "Сохранить" },
            { "Cancel", "Отмена" },
            { "OK", "ОК" },
            { "Rename", "Переименовать" },
            { "Rename...", "Переименовать…" },
            { "Remove", "Удалить" },
            { "Refresh", "Обновить" },
            { "Settings", "Настройки" },
            { "Restore", "Показать" },
            { "Exit", "Выход" },
            { "Close", "Закрыть" },
            { "Open", "Открыть" },
            { "Description", "Описание" },
            { "Description...", "Описание…" },
            { "Description text", "Текст описания" },
            { "Search", "Поиск" },
            { "Fuzzy accuracy (0-3):", "Точность fuzzy (0–3):" },
            { "Search in metadata (exe, product)", "Искать в метаданных (exe, продукт)" },
            { "Search in full paths", "Искать в полных путях" },
            { "Search in descriptions", "Искать в описаниях" },
            { "Search fonts:", "Шрифты поиска:" },
            { "box", "строк поиска" },
            { "results", "результаты" },
            { "Copy path", "Копировать путь" },

            // Main panel folder menu / native menu items
            { "Open in Mini Explorer", "Открыть в мини-проводнике" },
            { "Open containing folder", "Открыть содержащую папку" },
            { "Move out of folder", "Вынести из папки" },
            { "Size", "Размер" },
            { "Change Icon", "Сменить иконку" },
            { "Remove from Panel", "Убрать с панели" },
            { "Size-1", "1 x 1" },

            // Settings window
            { "Startup Size:", "Размер при запуске:" },
            { "Window Position:", "Позиция окна:" },
            { "Grid Transp. (0-255):", "Прозрачность сетки (0-255):" },
            { "Grid Columns:", "Колонки сетки:" },
            { "Grid Rows:", "Строки сетки:" },
            { "Extra rows below:", "Рядов ниже сетки:" },
            { "Def. Item Size:", "Размер элемента:" },
            { "Icon Scale (%):", "Масштаб иконок (%):" },
            { "Minimize instead of close", "Сворачивать в трей вместо закрытия" },
            { "Open folders in:", "Папки при открытии:" },
            { "Same window", "В этом же окне" },
            { "Popup window", "Во всплывающем окне" },
            { "sec idle", "сек простоя" },
            { "Return from a folder after this many seconds without activity (0 = off)", "Выходить из папки после стольких секунд без активности (0 = выкл)" },
            { "Light Theme", "Светлая тема" },
            { "Allow adding icons", "Разрешать добавлять значки" },
            { "Current window: ", "Текущее окно: " },
            { " at (", " в (" },
            { "Show window hotkey:", "Горячая клавиша показа:" },
            { "Tiles Font:", "Шрифт плиток:" },
            { "Tabs Font:", "Шрифт вкладок:" },
            { "UI Font:", "Шрифт интерфейса:" },
            { "File types:", "Типы файлов:" },
            { "Icons by type...", "Иконки по типам..." },
            { "Open with by type...", "Открытие по типам..." },
            { "Ctrl+Click a folder opens Mini Explorer", "Ctrl+ЛКМ по папке — мини-проводник" },
            { "Autostart & tray", "Автозапуск и трей" },
            { "Autostart with Windows", "Автозапуск с Windows" },
            { "After autostart - go to tray", "После автозапуска — сразу в трей" },
            { "Always keep tray icon", "Держать значок в трее" },
            { "Remember active tab", "Запоминать активную вкладку" },
            { "Language:", "Язык:" },
            { "Save backup", "Сохранить бекап" },
            { "Restore backup", "Восстановить бекап" },

            // Mini explorer - toolbar and console
            { "Edit", "Изменить" },
            { "Console", "Консоль" },
            { "New window", "Новое окно" },
            { "Restart", "Перезапуск" },
            { "Clear", "Очистить" },
            { "Run", "Выполнить" },
            { "+ Save", "+ Команда" },
            { "Ctrl+L or Edit - edit path · F5 - refresh · Backspace - up · Enter - open · double-click - open", "Ctrl+L или Изменить — путь · F5 — обновить · Backspace — наверх · Enter — открыть · двойной клик — открыть" },
            { "Show / hide bookmarks panel", "Показать / скрыть панель закладок" },
            { "Show / hide top bookmarks bar", "Показать / скрыть полосу закладок" },
            { "Drag to resize the console", "Потяните, чтобы изменить высоту консоли" },
            { "Ctrl+mouse wheel - console font size", "Ctrl+колесо мыши — размер шрифта консоли" },
            { "Search scope: click switches folder / everywhere", "Область поиска: клик переключает папка/везде" },
            { "BOOKMARKS", "ЗАКЛАДКИ" },

            // Mini explorer - menus
            { "Add current folder", "Добавить текущую папку" },
            { "Add command...", "Добавить команду..." },
            { "Add group...", "Добавить группу..." },
            { "Add to bookmarks", "Добавить в закладки" },
            { "Open in Explorer", "Открыть в Проводнике" },
            { "Show in Explorer", "Показать в Проводнике" },
            { "Copy folder path", "Копировать путь папки" },
            { "Add current folder to bookmarks", "Добавить текущую папку в закладки" },
            { "Open console window here", "Открыть окно консоли здесь" },
            { "(empty)", "(пусто)" },
            { "Edit command...", "Изменить команду..." },
            { "Edit command", "Изменить команду" },
            { "Move up", "Вверх" },
            { "Move down", "Вниз" },

            // Mini explorer - search
            { "Папка", "Папка" },
            { "Везде", "Везде" },

            // File type editors
            { "File Type Icons", "Иконки по типам файлов" },
            { "Open With by File Type", "Открытие по типам файлов" },
            { "Add type...", "Добавить тип..." },
            { "Change...", "Изменить..." },
            { "Remove icon", "Убрать иконку" },
            { "Open standard", "Стандартное открытие" },
            { "Import...", "Импорт..." },
            { "Export...", "Экспорт..." },

            // Tab menus / prompts (previously hard-coded English)
            { "Create Folder", "Создать папку" },
            { "Folder Name", "Имя папки" },
            { "Delete Tab", "Удалить вкладку" },
            { "Rename Tab", "Переименовать вкладку" },
            { "Toggle Layout (Free / Grid)", "Переключить раскладку (свободная / сетка)" },
            { "New Tab Name", "Новое имя вкладки" },
            { "Are you sure you want to delete this tab?", "Вы уверены, что хотите удалить эту вкладку?" },
            { "Cannot remove the last tab.", "Нельзя удалить последнюю вкладку." },
            { "Empty", "Пусто" },
            { "Mini Explorer", "Мини-проводник" },
            { "Rebuild icons & paths", "Пересобрать иконки и пути" },
            { "Checked: {0} · missing paths: {1} · cache files removed: {2}",
              "Проверено: {0} · потерянных путей: {1} · файлов кеша удалено: {2}" },
            { "Tile label: two rows", "Подпись плитки: две строки" },
            { "2 rows align:", "Выравнивание 2 строк:" },
            { "Left", "Слева" },
            { "Center", "По центру" },
            { "Right", "Справа" },
            { "Hide shortcut suffix ( - Shortcut)", "Скрывать суффикс ярлыка ( — ярлык)" },
            { "Hide file extension (.mp4)", "Скрывать расширение файла (.mp4)" },
            { "Extra tile rows under the visible grid, same cell size; scroll down to reach them (0 = off)",
              "Дополнительные ряды плиток под видимой сеткой, того же размера; добраться до них можно прокруткой вниз (0 = выкл)" },
            { "Past search", "Прошлый поиск" },
            { "Regular search", "Обычный поиск" },
            { "Command (%1 = current folder):", "Команда (%1 — текущая папка):" },
            { "in bookmarks %1 = current folder", "в закладках %1 — текущая папка" },
            { "New version v", "Новая версия v" },
            { " is published, but it is less than a day old. It will be offered after 24 hours - antivirus false positives on fresh builds usually settle within that time.",
              " уже опубликована, но ей меньше суток. Будет предложена через 24 часа — за это время обычно уходят ложные срабатывания антивирусов на свежих сборках." },
            { "Like Tilettes? Support the author:", "Понравились Плиточки? Поддержите автора:" },
            { "Opens the donate section on GitHub", "Открывает раздел доната на GitHub" },
            { "Virtual desktops - move the panel when shown", "Виртуальные рабочие столы — переставлять панель при показе" },
            { "When the desktop COM component is unavailable, the window is recreated on every show - this can occasionally cause a repaint glitch. Leave off unless you use several virtual desktops.", "Если COM-компонента рабочих столов недоступна, окно пересоздаётся при каждом показе — изредка это может давать баг перерисовки. Держите выключенным, если не пользуетесь несколькими виртуальными столами." },
            { "Bugs and rough edges are possible.", "Возможны баги и недоделки." },
            { "✅ - the corner checkmark enables adding and editing tiles", "✅ — галочка в углу панели включает добавление и редактирование плиток" },
        };

        // Applies dictionary translation to every static text in the control tree
        // (and to the context menus attached to the controls).
        public static void Walk(Control root)
        {
            if (IsEn || root == null) return; // English is the source of truth
            var table = Table();
            if (table == null) return;
            try
            {
                string t = root.Text;
                if (!string.IsNullOrEmpty(t))
                {
                    string tr;
                    if (table.TryGetValue(t, out tr)) root.Text = tr;
                }
                if (root.ContextMenu != null)
                {
                    foreach (MenuItem mi in root.ContextMenu.MenuItems) WalkMenu(mi, table);
                }
                foreach (Control c in root.Controls) Walk(c);
            }
            catch { }
        }

        public static void WalkMenu(MenuItem mi)
        {
            if (IsEn || mi == null) return;
            var table = Table();
            if (table != null) WalkMenu(mi, table);
        }

        private static void WalkMenu(MenuItem mi, Dictionary<string, string> table)
        {
            if (mi == null) return;
            try
            {
                string tr;
                if (!string.IsNullOrEmpty(mi.Text) && table.TryGetValue(mi.Text, out tr)) mi.Text = tr;
                foreach (MenuItem child in mi.MenuItems) WalkMenu(child, table);
            }
            catch { }
        }
    }
}
