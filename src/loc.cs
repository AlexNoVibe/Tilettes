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
            { "Save", "РЎРѕС…СЂР°РЅРёС‚СЊ" },
            { "Cancel", "РћС‚РјРµРЅР°" },
            { "OK", "РћРљ" },
            { "Rename", "РџРµСЂРµРёРјРµРЅРѕРІР°С‚СЊ" },
            { "Rename...", "РџРµСЂРµРёРјРµРЅРѕРІР°С‚СЊвЂ¦" },
            { "Remove", "РЈРґР°Р»РёС‚СЊ" },
            { "Refresh", "РћР±РЅРѕРІРёС‚СЊ" },
            { "Settings", "РќР°СЃС‚СЂРѕР№РєРё" },
            { "Restore", "РџРѕРєР°Р·Р°С‚СЊ" },
            { "Exit", "Р’С‹С…РѕРґ" },
            { "Close", "Р—Р°РєСЂС‹С‚СЊ" },
            { "Open", "РћС‚РєСЂС‹С‚СЊ" },
            { "Description...", "РћРїРёСЃР°РЅРёРµвЂ¦" },
            { "Description text", "РўРµРєСЃС‚ РѕРїРёСЃР°РЅРёСЏ" },
            { "Search", "РџРѕРёСЃРє" },
            { "Fuzzy accuracy (0-3):", "Точность fuzzy (0–3):" },
            { "Search in metadata (exe, product)", "РСЃРєР°С‚СЊ РІ РјРµС‚Р°РґР°РЅРЅС‹С… (exe, РїСЂРѕРґСѓРєС‚)" },
            { "Search in full paths", "РСЃРєР°С‚СЊ РІ РїРѕР»РЅС‹С… РїСѓС‚СЏС…" },
            { "Search in descriptions", "РСЃРєР°С‚СЊ РІ РѕРїРёСЃР°РЅРёСЏС…" },
            { "Search fonts:", "РЁСЂРёС„С‚С‹ РїРѕРёСЃРєР°:" },
            { "box", "СЃС‚СЂРѕРє РїРѕРёСЃРєР°" },
            { "results", "СЂРµР·СѓР»СЊС‚Р°С‚С‹" },
            { "Copy path", "РљРѕРїРёСЂРѕРІР°С‚СЊ РїСѓС‚СЊ" },

            // Main panel folder menu / native menu items
            { "Open in Mini Explorer", "РћС‚РєСЂС‹С‚СЊ РІ РјРёРЅРё-РїСЂРѕРІРѕРґРЅРёРєРµ" },
            { "Open containing folder", "РћС‚РєСЂС‹С‚СЊ СЃРѕРґРµСЂР¶Р°С‰СѓСЋ РїР°РїРєСѓ" },
            { "Move out of folder", "Р’С‹РЅРµСЃС‚Рё РёР· РїР°РїРєРё" },
            { "Size", "Р Р°Р·РјРµСЂ" },
            { "Change Icon", "РЎРјРµРЅРёС‚СЊ РёРєРѕРЅРєСѓ" },
            { "Remove from Panel", "РЈР±СЂР°С‚СЊ СЃ РїР°РЅРµР»Рё" },
            { "Size-1", "1 x 1" },

            // Settings window
            { "Startup Size:", "Р Р°Р·РјРµСЂ РїСЂРё Р·Р°РїСѓСЃРєРµ:" },
            { "Window Position:", "РџРѕР·РёС†РёСЏ РѕРєРЅР°:" },
            { "Grid Transp. (0-255):", "РџСЂРѕР·СЂР°С‡РЅРѕСЃС‚СЊ СЃРµС‚РєРё (0-255):" },
            { "Grid Columns:", "РљРѕР»РѕРЅРєРё СЃРµС‚РєРё:" },
            { "Grid Rows:", "РЎС‚СЂРѕРєРё СЃРµС‚РєРё:" },
            { "Def. Item Size:", "Р Р°Р·РјРµСЂ СЌР»РµРјРµРЅС‚Р°:" },
            { "Icon Scale (%):", "РњР°СЃС€С‚Р°Р± РёРєРѕРЅРѕРє (%):" },
            { "Minimize instead of close", "РЎРІРѕСЂР°С‡РёРІР°С‚СЊ РІ С‚СЂРµР№ РІРјРµСЃС‚Рѕ Р·Р°РєСЂС‹С‚РёСЏ" },
            { "Open folders in:", "РџР°РїРєРё РїСЂРё РѕС‚РєСЂС‹С‚РёРё:" },
            { "Same window", "Р’ СЌС‚РѕРј Р¶Рµ РѕРєРЅРµ" },
            { "Popup window", "Р’Рѕ РІСЃРїР»С‹РІР°СЋС‰РµРј РѕРєРЅРµ" },
            { "sec idle", "СЃРµРє РїСЂРѕСЃС‚РѕСЏ" },
            { "Light Theme", "РЎРІРµС‚Р»Р°СЏ С‚РµРјР°" },
            { "Show window hotkey:", "Р“РѕСЂСЏС‡Р°СЏ РєР»Р°РІРёС€Р° РїРѕРєР°Р·Р°:" },
            { "Tiles Font:", "РЁСЂРёС„С‚ РїР»РёС‚РѕРє:" },
            { "Tabs Font:", "РЁСЂРёС„С‚ РІРєР»Р°РґРѕРє:" },
            { "UI Font:", "РЁСЂРёС„С‚ РёРЅС‚РµСЂС„РµР№СЃР°:" },
            { "File types:", "РўРёРїС‹ С„Р°Р№Р»РѕРІ:" },
            { "Icons by type...", "РРєРѕРЅРєРё РїРѕ С‚РёРїР°Рј..." },
            { "Open with by type...", "РћС‚РєСЂС‹С‚РёРµ РїРѕ С‚РёРїР°Рј..." },
            { "Ctrl+Click a folder opens Mini Explorer", "Ctrl+Р›РљРњ РїРѕ РїР°РїРєРµ вЂ” РјРёРЅРё-РїСЂРѕРІРѕРґРЅРёРє" },
            { "Autostart & window", "РђРІС‚РѕР·Р°РіСЂСѓР·РєР° Рё РѕРєРЅРѕ" },
            { "Autostart with Windows", "РђРІС‚РѕР·Р°РїСѓСЃРє СЃ Windows" },
            { "After autostart - go to tray", "РџРѕСЃР»Рµ Р°РІС‚РѕР·Р°РїСѓСЃРєР° вЂ” СЃСЂР°Р·Сѓ РІ С‚СЂРµР№" },
            { "Always keep tray icon", "Р”РµСЂР¶Р°С‚СЊ Р·РЅР°С‡РѕРє РІ С‚СЂРµРµ" },
            { "Remember active tab", "Р—Р°РїРѕРјРёРЅР°С‚СЊ Р°РєС‚РёРІРЅСѓСЋ РІРєР»Р°РґРєСѓ" },
            { "Language:", "РЇР·С‹Рє:" },
            { "Backup Settings", "РЎРѕС…СЂР°РЅРёС‚СЊ РЅР°СЃС‚СЂРѕР№РєРё" },
            { "Restore Settings", "Р’РѕСЃСЃС‚Р°РЅРѕРІРёС‚СЊ РЅР°СЃС‚СЂРѕР№РєРё" },

            // Mini explorer - toolbar and console
            { "Edit", "РР·РјРµРЅРёС‚СЊ" },
            { "Console", "РљРѕРЅСЃРѕР»СЊ" },
            { "New window", "РќРѕРІРѕРµ РѕРєРЅРѕ" },
            { "Restart", "РџРµСЂРµР·Р°РїСѓСЃРє" },
            { "Clear", "РћС‡РёСЃС‚РёС‚СЊ" },
            { "Run", "Р’С‹РїРѕР»РЅРёС‚СЊ" },
            { "+ Save", "+ РљРѕРјР°РЅРґР°" },
            { "Ctrl+L or Edit - edit path В· F5 - refresh В· Backspace - up В· Enter - open В· double-click - open", "Ctrl+L РёР»Рё РР·РјРµРЅРёС‚СЊ вЂ” РїСѓС‚СЊ В· F5 вЂ” РѕР±РЅРѕРІРёС‚СЊ В· Backspace вЂ” РЅР°РІРµСЂС… В· Enter вЂ” РѕС‚РєСЂС‹С‚СЊ В· РґРІРѕР№РЅРѕР№ РєР»РёРє вЂ” РѕС‚РєСЂС‹С‚СЊ" },
            { "Show / hide bookmarks panel", "РџРѕРєР°Р·Р°С‚СЊ / СЃРєСЂС‹С‚СЊ РїР°РЅРµР»СЊ Р·Р°РєР»Р°РґРѕРє" },
            { "Show / hide top bookmarks bar", "РџРѕРєР°Р·Р°С‚СЊ / СЃРєСЂС‹С‚СЊ РїРѕР»РѕСЃСѓ Р·Р°РєР»Р°РґРѕРє" },
            { "Drag to resize the console", "РџРѕС‚СЏРЅРёС‚Рµ, С‡С‚РѕР±С‹ РёР·РјРµРЅРёС‚СЊ РІС‹СЃРѕС‚Сѓ РєРѕРЅСЃРѕР»Рё" },
            { "BOOKMARKS", "Р—РђРљР›РђР”РљР" },

            // Mini explorer - menus
            { "Add current folder", "Р”РѕР±Р°РІРёС‚СЊ С‚РµРєСѓС‰СѓСЋ РїР°РїРєСѓ" },
            { "Add command...", "Р”РѕР±Р°РІРёС‚СЊ РєРѕРјР°РЅРґСѓ..." },
            { "Add group...", "Р”РѕР±Р°РІРёС‚СЊ РіСЂСѓРїРїСѓ..." },
            { "Add to bookmarks", "Р”РѕР±Р°РІРёС‚СЊ РІ Р·Р°РєР»Р°РґРєРё" },
            { "Open in Explorer", "РћС‚РєСЂС‹С‚СЊ РІ РџСЂРѕРІРѕРґРЅРёРєРµ" },
            { "Show in Explorer", "РџРѕРєР°Р·Р°С‚СЊ РІ РџСЂРѕРІРѕРґРЅРёРєРµ" },
            { "Copy folder path", "РљРѕРїРёСЂРѕРІР°С‚СЊ РїСѓС‚СЊ РїР°РїРєРё" },
            { "Add current folder to bookmarks", "Р”РѕР±Р°РІРёС‚СЊ С‚РµРєСѓС‰СѓСЋ РїР°РїРєСѓ РІ Р·Р°РєР»Р°РґРєРё" },
            { "Open console window here", "РћС‚РєСЂС‹С‚СЊ РѕРєРЅРѕ РєРѕРЅСЃРѕР»Рё Р·РґРµСЃСЊ" },
            { "(empty)", "(РїСѓСЃС‚Рѕ)" },

            // Mini explorer - search
            { "РџР°РїРєР°", "РџР°РїРєР°" },
            { "Р’РµР·РґРµ", "Р’РµР·РґРµ" },

            // File type editors
            { "File Type Icons", "РРєРѕРЅРєРё РїРѕ С‚РёРїР°Рј С„Р°Р№Р»РѕРІ" },
            { "Open With by File Type", "РћС‚РєСЂС‹С‚РёРµ РїРѕ С‚РёРїР°Рј С„Р°Р№Р»РѕРІ" },
            { "Add type...", "Р”РѕР±Р°РІРёС‚СЊ С‚РёРї..." },
            { "Change...", "РР·РјРµРЅРёС‚СЊ..." },
            { "Remove icon", "РЈР±СЂР°С‚СЊ РёРєРѕРЅРєСѓ" },
            { "Open standard", "РЎС‚Р°РЅРґР°СЂС‚РЅРѕРµ РѕС‚РєСЂС‹С‚РёРµ" },
            { "Import...", "РРјРїРѕСЂС‚..." },
            { "Export...", "Р­РєСЃРїРѕСЂС‚..." },
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
