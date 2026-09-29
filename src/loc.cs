using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WinPanel
{
    // Tiny localization helper. The UI is written in English; Loc.S("text")
    // returns the translation for the current language (default: Russian).
    // A dictionary is provided so the most visible strings can be translated
    // centrally (Loc.Walk applies it to a whole control tree).
    public static class Loc
    {
        public static string Lang = "ru";

        public static bool IsRu
        {
            get { return !string.Equals(Lang, "en", StringComparison.OrdinalIgnoreCase); }
        }

        public static string S(string en, string ru)
        {
            return IsRu ? ru : en;
        }

        public static string S(string en)
        {
            if (!IsRu) return en;
            string t;
            return Ru.TryGetValue(en, out t) ? t : en;
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
            { "Def. Item Size:", "Размер элемента:" },
            { "Icon Scale (%):", "Масштаб иконок (%):" },
            { "Minimize instead of close", "Сворачивать в трей вместо закрытия" },
            { "Open folders in:", "Папки при открытии:" },
            { "Same window", "В этом же окне" },
            { "Popup window", "Во всплывающем окне" },
            { "sec idle", "сек простоя" },
            { "Return from a folder after this many seconds without activity (0 = off)", "Выходить из папки после стольких секунд без активности (0 = выкл)" },
            { "Light Theme", "Светлая тема" },
            { "Show window hotkey:", "Горячая клавиша показа:" },
            { "Tiles Font:", "Шрифт плиток:" },
            { "Tabs Font:", "Шрифт вкладок:" },
            { "UI Font:", "Шрифт интерфейса:" },
            { "File types:", "Типы файлов:" },
            { "Icons by type...", "Иконки по типам..." },
            { "Open with by type...", "Открытие по типам..." },
            { "Ctrl+Click a folder opens Mini Explorer", "Ctrl+ЛКМ по папке — мини-проводник" },
            { "Autostart & window", "Автозагрузка и окно" },
            { "Autostart with Windows", "Автозапуск с Windows" },
            { "After autostart - go to tray", "После автозапуска — сразу в трей" },
            { "Always keep tray icon", "Держать значок в трее" },
            { "Remember active tab", "Запоминать активную вкладку" },
            { "Language:", "Язык:" },
            { "Backup Settings", "Сохранить настройки" },
            { "Restore Settings", "Восстановить настройки" },

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
        };

        // Applies dictionary translation to every static text in the control tree
        // (and to the context menus attached to the controls).
        public static void Walk(Control root)
        {
            if (!IsRu || root == null) return;
            try
            {
                string t = root.Text;
                if (!string.IsNullOrEmpty(t))
                {
                    string tr;
                    if (Ru.TryGetValue(t, out tr)) root.Text = tr;
                }
                if (root.ContextMenu != null)
                {
                    foreach (MenuItem mi in root.ContextMenu.MenuItems) WalkMenu(mi);
                }
                foreach (Control c in root.Controls) Walk(c);
            }
            catch { }
        }

        public static void WalkMenu(MenuItem mi)
        {
            if (mi == null) return;
            try
            {
                string tr;
                if (!string.IsNullOrEmpty(mi.Text) && Ru.TryGetValue(mi.Text, out tr)) mi.Text = tr;
                foreach (MenuItem child in mi.MenuItems) WalkMenu(child);
            }
            catch { }
        }
    }
}
