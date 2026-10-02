using System;
using System.Collections.Generic;
using System.IO;

namespace WinPanel
{
    public class Settings
    {
        // The window always starts exactly with this size; resizing during a session
        // does not change it (only the position is remembered).
        public int StartupWidth { get; set; }
        public int StartupHeight { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool OpenFoldersInPopup { get; set; }
        public bool EditMode { get; set; }
        // Edit button state: 0 = off, 1 = edit, 2 = multi-select (red).
        public int EditModeState { get; set; }

        // Grid visibility: quick toggle button on the main panel
        public bool GridVisible { get; set; }
        // Hotkey to show the window, e.g. "Ctrl+J" ("None" = disabled)
        public string HotkeyShow { get; set; }
        // Ctrl+Click on a folder opens the mini explorer window
        public bool MiniExplorerCtrlClick { get; set; }

        // Mini explorer window state (0 = open at the default large size)
        public int MiniExplorerW { get; set; }
        public int MiniExplorerH { get; set; }
        public int MiniExplorerX { get; set; }
        public int MiniExplorerY { get; set; }
        // Bookmarks side panel visibility in the mini explorer
        public bool MiniExplorerBookmarks { get; set; }
        // Top bookmarks bar visibility; console height percent (default 40)
        public bool MiniExplorerTopBar { get; set; }
        public int MiniExplorerConsole { get; set; }
        // Mini explorer console font size, stored x10 (85 = 8.5pt); Ctrl+wheel zooms
        public int ConsoleFontSizeX10 { get; set; }

        // Folder auto-exit: while inside a folder (same-window mode), return back
        // after this many seconds without any mouse/keyboard activity (0 = off)
        public int FolderAutoExitSeconds { get; set; }

        // Program that opens directories instead of the system default (Explorer):
        // a path to a .exe (Total Commander etc.). Empty, "explorer.exe" or a
        // missing file keeps the system default. The folder is passed as the one
        // quoted argument.
        public string FolderOpenProgram { get; set; }

        // UI language: "ru" or "en"
        public string Language { get; set; }

        // Autostart / window behaviour
        public bool AutoStart { get; set; }
        public bool AutoStartMinimized { get; set; }
        public bool TrayIconAlways { get; set; }
        public bool KeepActiveTab { get; set; }
        public string ActiveTab { get; set; }

        // Panel search tuning
        public int SearchFuzzyLevel { get; set; }     // 0 = exact only, 1..3 = looser fuzzy
        public bool SearchInMeta { get; set; }        // exe name, product, company
        public bool SearchInPaths { get; set; }       // full path text
        public bool SearchInDesc { get; set; }        // FileDescription / shortcut description
        // Include the mirrored Start Menu tab ("Пуск") in panel search results.
        public bool SearchInStart { get; set; }
        public int SearchBoxFontSize { get; set; }
        public int SearchResultsFontSize { get; set; }

        public int GridTransparency { get; set; }
        public int GridColumns { get; set; }
        public int GridRows { get; set; }
        public int DefaultItemSize { get; set; }
        public bool IsLightTheme { get; set; }

        // Icon size inside a tile, percent of the default (100 = as designed)
        public int IconScale { get; set; }

        // Fonts: size, color (hex, empty = theme default) and family per group
        public int FontItemsSize { get; set; }
        public string FontItemsColor { get; set; }
        public string FontItemsName { get; set; }

        public int FontTabsSize { get; set; }
        public string FontTabsColor { get; set; }
        public string FontTabsName { get; set; }

        public int FontUiSize { get; set; }
        public string FontUiColor { get; set; }
        public string FontUiName { get; set; }

        // Full backup every N days (0 = off); stored into autoBackup\ as a zip.
        public int BackupDays { get; set; }
        public string LastBackupDate { get; set; }

        // Mirror the system Start Menu into the "Пуск" tab every N hours (0 = off).
        public int StartMenuSyncHours { get; set; }
        public string LastSyncDate { get; set; }

        // Remember what the user searched and opened (panel search history).
        public bool SearchSaveHistory { get; set; }

        // Capture the physical Win key (both) to show the panel instead of Start.
        public bool HotkeyWin { get; set; }

        // Decorative skin id ("" = disabled); see Skins.cs
        public string SkinName { get; set; }

        // First-start welcome window was shown and answered (the flag makes it
        // strictly once per data folder, also for users upgrading from older builds).
        public bool FirstRunDone { get; set; }

        // Update check (GitHub Releases; install is a stub — see UpdateChecker).
        public bool UpdateCheckEnabled { get; set; }
        public int UpdateCheckDays { get; set; }
        public bool UpdateAutoInstall { get; set; }
        public string LastUpdateCheck { get; set; }

        // Tile label display options — DISPLAY ONLY: the stored names
        // (records.xml), all paths and the search metadata are never touched,
        // unticking any of these brings the full label back.
        public bool LabelTwoRows { get; set; }      // tall tiles wrap the label onto two rows
        public bool LabelTrimShortcut { get; set; } // hide " - Shortcut" / " — ярлык" (dash variants, several languages)
        public bool LabelTrimExtension { get; set; } // hide the real extension of the item's path (.mp4 …)
        public int LabelAlign2Rows { get; set; }     // horizontal alignment of the two-row label: 0 left, 1 center, 2 right
        // Hold Ctrl -> tiles show their full (untruncated) name while the key is down.
        public bool LabelCtrlFullNames { get; set; }

        public Settings()
        {
            StartupWidth = 900;
            StartupHeight = 800;
            WindowX = 100;
            WindowY = 100;
            MinimizeToTray = true;
            OpenFoldersInPopup = false;
            // First-run defaults: grid visible and adding icons (edit mode) allowed.
            EditMode = true;
            EditModeState = 1;
            GridVisible = true;
            HotkeyShow = "Ctrl+Q";
            MiniExplorerCtrlClick = true;
            MiniExplorerBookmarks = true;
            MiniExplorerTopBar = true;
            MiniExplorerConsole = 40;
            ConsoleFontSizeX10 = 140;
            FolderAutoExitSeconds = 15;
            FolderOpenProgram = "";
            Language = "ru";
            AutoStart = false;
            AutoStartMinimized = false;
            TrayIconAlways = true;
            KeepActiveTab = true;
            ActiveTab = "";
            SearchFuzzyLevel = 2;
            SearchInMeta = true;
            SearchInPaths = true;
            SearchInDesc = true;
            SearchInStart = true;
            SearchBoxFontSize = 14;
            SearchResultsFontSize = 14;

            GridTransparency = 50;
            GridColumns = 16;
            GridRows = 16;
            DefaultItemSize = 2;
            // Mint is the factory look; IsLightTheme follows the skin (mint is
            // a light skin) so every dark/light branch agrees with it.
            IsLightTheme = true;

            IconScale = 100;

            FontItemsSize = 14;
            FontItemsColor = "";
            FontItemsName = "Segoe UI";

            FontTabsSize = 14;
            FontTabsColor = "";
            FontTabsName = "Segoe UI";

            FontUiSize = 14;
            FontUiColor = "";
            FontUiName = "Segoe UI";

            BackupDays = 7;
            LastBackupDate = "";
            StartMenuSyncHours = 24;
            LastSyncDate = "";
            SearchSaveHistory = true;
            // Opt-in (was true): the WH_KEYBOARD_LL global hook is a classic
            // heuristic trigger for security software, and a fresh sandbox run
            // used to install it immediately. Users who want the Win key to open
            // the panel enable it in Settings; existing settings.ini values are
            // untouched by this default.
            HotkeyWin = false;
            SkinName = "mint";
            FirstRunDone = false;
            UpdateCheckEnabled = true;
            // The first check happens UpdateCheckDays days after the very first
            // start (the welcome window stamps LastUpdateCheck), not instantly.
            UpdateCheckDays = 3;
            UpdateAutoInstall = false;
            LastUpdateCheck = "";

            LabelTwoRows = true;
            LabelTrimShortcut = true;
            LabelTrimExtension = true;
            LabelCtrlFullNames = true;
            LabelAlign2Rows = 1;
        }

        // First start on a small monitor: the factory 900x800 window at Y=100
        // hangs off the bottom of screens shorter than ~900px. Shrinks the
        // factory size to fit (about -15% on the borderline case, more on very
        // small laptops), keeps the aspect ratio and trims the grid rows AND
        // columns by the same factor so the tiles stay square. Runs once per
        // fresh install (FirstRunDone is still false, nothing is saved yet);
        // existing settings.ini files never hit this path.
        public static void FitFirstStartToScreen(Settings s, System.Drawing.Rectangle workArea)
        {
            try
            {
                if (workArea.Width <= 0 || workArea.Height <= 0) return;
                double scale = 1.0;
                // Small monitor: cut the factory size by ~15%...
                if (workArea.Height < 900) scale = 0.85;
                // ...and never hang over the bottom edge (taskbar/edge margin).
                int bottomLimit = workArea.Bottom - 24;
                double fitted = (bottomLimit - s.WindowY) / (double)Math.Max(1, s.StartupHeight);
                if (fitted < scale) scale = fitted;
                if (scale >= 1.0) return;

                int newH = Math.Max(560, (int)Math.Round(s.StartupHeight * scale));
                int newW = Math.Max(700, (int)Math.Round(s.StartupWidth * scale));
                int newRows = Math.Max(10, (int)Math.Round(s.GridRows * scale));
                int newCols = Math.Max(10, (int)Math.Round(s.GridColumns * scale));
                s.StartupHeight = newH;
                s.StartupWidth = newW;
                s.GridRows = newRows;
                s.GridColumns = newCols;

                // Keep the window itself inside the working area.
                if (s.WindowY + newH > bottomLimit)
                    s.WindowY = Math.Max(workArea.Top, bottomLimit - newH);
                if (s.WindowX + newW > workArea.Right - 8)
                    s.WindowX = Math.Max(workArea.Left, workArea.Right - 8 - newW);
            }
            catch
            {
                // A default-size tweak is never worth failing the startup for.
            }
        }

        public static System.Drawing.Color ParseColor(string hex, System.Drawing.Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try { return System.Drawing.ColorTranslator.FromHtml(hex); }
            catch { return fallback; }
        }

        // Font family lookup: System.Drawing.FontFamily.Families is expensive and was
        // previously enumerated on every MakeFont call (i.e. on every repaint).
        // The name->family map is built once per process.
        private static Dictionary<string, System.Drawing.FontFamily> fontFamilyCache;

        private static System.Drawing.FontFamily FindFontFamily(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (fontFamilyCache == null)
            {
                var map = new Dictionary<string, System.Drawing.FontFamily>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var f in System.Drawing.FontFamily.Families)
                    {
                        if (!map.ContainsKey(f.Name)) map[f.Name] = f;
                    }
                }
                catch { }
                fontFamilyCache = map;
            }
            System.Drawing.FontFamily fam;
            return fontFamilyCache.TryGetValue(name.Trim(), out fam) ? fam : null;
        }

        public static System.Drawing.Font MakeFont(string name, int size, System.Drawing.FontStyle style)
        {
            float s = size;
            if (s < 6) s = 6;
            if (s > 24) s = 24;
            var fam = FindFontFamily(name);
            if (fam != null)
            {
                try { return new System.Drawing.Font(fam, s, style); }
                catch { }
            }
            return new System.Drawing.Font("Segoe UI", s, style);
        }

        public static System.Drawing.Font MakeFont(string name, int size)
        {
            return MakeFont(name, size, System.Drawing.FontStyle.Regular);
        }

        public static Settings Load(string path)
        {
            var s = new Settings();
            if (!File.Exists(path))
            {
                return s;
            }

            try
            {
                var ini = new IniFile(path);
                int w, h, x, y, gt, gc, gr, dis, iscl, fis, fts, fus;
                bool m, lt, fp, em;
                if (int.TryParse(ini.Read("StartupWidth"), out w)) s.StartupWidth = w;
                if (int.TryParse(ini.Read("StartupHeight"), out h)) s.StartupHeight = h;
                if (int.TryParse(ini.Read("WindowX"), out x)) s.WindowX = x;
                if (int.TryParse(ini.Read("WindowY"), out y)) s.WindowY = y;
                if (bool.TryParse(ini.Read("MinimizeToTray"), out m)) s.MinimizeToTray = m;
                if (bool.TryParse(ini.Read("OpenFoldersInPopup"), out fp)) s.OpenFoldersInPopup = fp;
                if (bool.TryParse(ini.Read("EditMode"), out em)) s.EditMode = em;
                int ems;
                if (int.TryParse(ini.Read("EditModeState"), out ems) && ems >= 0 && ems <= 2) s.EditModeState = ems;

                bool gv;
                if (bool.TryParse(ini.Read("GridVisible"), out gv)) s.GridVisible = gv;
                string hk = ini.Read("HotkeyShow");
                if (!string.IsNullOrEmpty(hk)) s.HotkeyShow = hk;

                bool mx;
                if (bool.TryParse(ini.Read("MiniExplorerCtrlClick"), out mx)) s.MiniExplorerCtrlClick = mx;

                int mexw, mexh, mexx, mexy;
                if (int.TryParse(ini.Read("MiniExplorerW"), out mexw)) s.MiniExplorerW = mexw;
                if (int.TryParse(ini.Read("MiniExplorerH"), out mexh)) s.MiniExplorerH = mexh;
                if (int.TryParse(ini.Read("MiniExplorerX"), out mexx)) s.MiniExplorerX = mexx;
                if (int.TryParse(ini.Read("MiniExplorerY"), out mexy)) s.MiniExplorerY = mexy;
                bool mebm;
                if (bool.TryParse(ini.Read("MiniExplorerBookmarks"), out mebm)) s.MiniExplorerBookmarks = mebm;
                bool mtb;
                if (bool.TryParse(ini.Read("MiniExplorerTopBar"), out mtb)) s.MiniExplorerTopBar = mtb;
                int mcon;
                if (int.TryParse(ini.Read("MiniExplorerConsole"), out mcon)) s.MiniExplorerConsole = mcon;
                int cfs;
                if (int.TryParse(ini.Read("ConsoleFontSizeX10"), out cfs) && cfs >= 60 && cfs <= 280) s.ConsoleFontSizeX10 = cfs;

                int faеx;
                if (int.TryParse(ini.Read("FolderAutoExitSeconds"), out faеx)) s.FolderAutoExitSeconds = faеx;
                string fop = ini.Read("FolderOpenProgram");
                if (fop != null) s.FolderOpenProgram = fop;
                string lang = ini.Read("Language");
                if (Loc.IsSupported(lang)) s.Language = lang.ToLowerInvariant();
                bool astr, astrm, tray, ktab;
                if (bool.TryParse(ini.Read("AutoStart"), out astr)) s.AutoStart = astr;
                if (bool.TryParse(ini.Read("AutoStartMinimized"), out astrm)) s.AutoStartMinimized = astrm;
                if (bool.TryParse(ini.Read("TrayIconAlways"), out tray)) s.TrayIconAlways = tray;
                if (bool.TryParse(ini.Read("KeepActiveTab"), out ktab)) s.KeepActiveTab = ktab;
                string atab = ini.Read("ActiveTab");
                if (atab != null) s.ActiveTab = atab;

                int sfz;
                if (int.TryParse(ini.Read("SearchFuzzyLevel"), out sfz)) s.SearchFuzzyLevel = sfz;
                bool sim, sip, sid, sist;
                if (bool.TryParse(ini.Read("SearchInMeta"), out sim)) s.SearchInMeta = sim;
                if (bool.TryParse(ini.Read("SearchInPaths"), out sip)) s.SearchInPaths = sip;
                if (bool.TryParse(ini.Read("SearchInDesc"), out sid)) s.SearchInDesc = sid;
                if (bool.TryParse(ini.Read("SearchInStart"), out sist)) s.SearchInStart = sist;
                int sbfs, srfs;
                if (int.TryParse(ini.Read("SearchBoxFontSize"), out sbfs)) s.SearchBoxFontSize = sbfs;
                if (int.TryParse(ini.Read("SearchResultsFontSize"), out srfs)) s.SearchResultsFontSize = srfs;

                if (int.TryParse(ini.Read("GridTransparency"), out gt)) s.GridTransparency = gt;
                if (int.TryParse(ini.Read("GridColumns"), out gc)) s.GridColumns = gc;
                if (int.TryParse(ini.Read("GridRows"), out gr)) s.GridRows = gr;
                if (int.TryParse(ini.Read("DefaultItemSize"), out dis)) s.DefaultItemSize = dis;
                if (bool.TryParse(ini.Read("IsLightTheme"), out lt)) s.IsLightTheme = lt;

                if (int.TryParse(ini.Read("IconScale"), out iscl)) s.IconScale = iscl;

                string val = ini.Read("FontItemsColor"); if (val != null) s.FontItemsColor = val;
                val = ini.Read("FontItemsName"); if (val != null && val.Length > 0) s.FontItemsName = val;
                if (int.TryParse(ini.Read("FontItemsSize"), out fis)) s.FontItemsSize = fis;

                val = ini.Read("FontTabsColor"); if (val != null) s.FontTabsColor = val;
                val = ini.Read("FontTabsName"); if (val != null && val.Length > 0) s.FontTabsName = val;
                if (int.TryParse(ini.Read("FontTabsSize"), out fts)) s.FontTabsSize = fts;

                val = ini.Read("FontUiColor"); if (val != null) s.FontUiColor = val;
                val = ini.Read("FontUiName"); if (val != null && val.Length > 0) s.FontUiName = val;
                if (int.TryParse(ini.Read("FontUiSize"), out fus)) s.FontUiSize = fus;

                int bd;
                if (int.TryParse(ini.Read("BackupDays"), out bd)) s.BackupDays = Math.Max(0, Math.Min(365, bd));
                val = ini.Read("LastBackupDate"); if (val != null) s.LastBackupDate = val;
                int smh;
                if (int.TryParse(ini.Read("StartMenuSyncHours"), out smh)) s.StartMenuSyncHours = Math.Max(0, Math.Min(8760, smh));
                val = ini.Read("LastSyncDate"); if (val != null) s.LastSyncDate = val;
                bool ssh;
                if (bool.TryParse(ini.Read("SearchSaveHistory"), out ssh)) s.SearchSaveHistory = ssh;
                bool hkw;
                if (bool.TryParse(ini.Read("HotkeyWin"), out hkw)) s.HotkeyWin = hkw;
                val = ini.Read("SkinName"); if (val != null) s.SkinName = val;
                bool frd, uce, uai;
                if (bool.TryParse(ini.Read("FirstRunDone"), out frd)) s.FirstRunDone = frd;
                if (bool.TryParse(ini.Read("UpdateCheckEnabled"), out uce)) s.UpdateCheckEnabled = uce;
                if (bool.TryParse(ini.Read("UpdateAutoInstall"), out uai)) s.UpdateAutoInstall = uai;
                int ucd;
                if (int.TryParse(ini.Read("UpdateCheckDays"), out ucd)) s.UpdateCheckDays = Math.Max(1, Math.Min(365, ucd));
                val = ini.Read("LastUpdateCheck"); if (val != null) s.LastUpdateCheck = val;

                bool l2r, lts, lte;
                if (bool.TryParse(ini.Read("LabelTwoRows"), out l2r)) s.LabelTwoRows = l2r;
                if (bool.TryParse(ini.Read("LabelTrimShortcut"), out lts)) s.LabelTrimShortcut = lts;
                if (bool.TryParse(ini.Read("LabelTrimExtension"), out lte)) s.LabelTrimExtension = lte;
                bool lcf;
                if (bool.TryParse(ini.Read("LabelCtrlFullNames"), out lcf)) s.LabelCtrlFullNames = lcf;
                int la2;
                if (int.TryParse(ini.Read("LabelAlign2Rows"), out la2) && la2 >= 0 && la2 <= 2) s.LabelAlign2Rows = la2;

                // While a decorative skin is active, the light/dark flag is not
                // an independent choice: derive it from the skin's brightness so
                // the surfaces that only know IsLightTheme (settings dialog,
                // welcome, mini explorer, search accents) match the skin. This
                // also heals configs saved before this rule existed (e.g. mint
                // picked while the flag was still false = everything around the
                // panel stayed dark).
                var activeSkin = Skin.Find(s.SkinName);
                if (Skin.IsActive(activeSkin)) s.IsLightTheme = !activeSkin.IsDark;
            }
            catch
            {
                // fallback to defaults
            }
            return s;
        }

        public void Save(string path)
        {
            try
            {
                var ini = new IniFile(path);
                ini.Write("StartupWidth", StartupWidth.ToString());
                ini.Write("StartupHeight", StartupHeight.ToString());
                ini.Write("WindowX", WindowX.ToString());
                ini.Write("WindowY", WindowY.ToString());
                ini.Write("MinimizeToTray", MinimizeToTray.ToString());
                ini.Write("OpenFoldersInPopup", OpenFoldersInPopup.ToString());
                ini.Write("EditMode", EditMode.ToString());
                ini.Write("EditModeState", EditModeState.ToString());
                ini.Write("GridVisible", GridVisible.ToString());
                ini.Write("HotkeyShow", string.IsNullOrEmpty(HotkeyShow) ? "Ctrl+Q" : HotkeyShow);
                ini.Write("MiniExplorerCtrlClick", MiniExplorerCtrlClick.ToString());
                ini.Write("MiniExplorerW", MiniExplorerW.ToString());
                ini.Write("MiniExplorerH", MiniExplorerH.ToString());
                ini.Write("MiniExplorerX", MiniExplorerX.ToString());
                ini.Write("MiniExplorerY", MiniExplorerY.ToString());
                ini.Write("MiniExplorerBookmarks", MiniExplorerBookmarks.ToString());
                ini.Write("MiniExplorerTopBar", MiniExplorerTopBar.ToString());
                ini.Write("MiniExplorerConsole", MiniExplorerConsole.ToString());
                ini.Write("ConsoleFontSizeX10", ConsoleFontSizeX10.ToString());
                ini.Write("FolderAutoExitSeconds", FolderAutoExitSeconds.ToString());
                ini.Write("FolderOpenProgram", FolderOpenProgram ?? "");
                ini.Write("Language", string.IsNullOrEmpty(Language) ? "ru" : Language);
                ini.Write("AutoStart", AutoStart.ToString());
                ini.Write("AutoStartMinimized", AutoStartMinimized.ToString());
                ini.Write("TrayIconAlways", TrayIconAlways.ToString());
                ini.Write("KeepActiveTab", KeepActiveTab.ToString());
                ini.Write("ActiveTab", ActiveTab == null ? "" : ActiveTab);
                ini.Write("SearchFuzzyLevel", SearchFuzzyLevel.ToString());
                ini.Write("SearchInMeta", SearchInMeta.ToString());
                ini.Write("SearchInPaths", SearchInPaths.ToString());
                ini.Write("SearchInDesc", SearchInDesc.ToString());
                ini.Write("SearchInStart", SearchInStart.ToString());
                ini.Write("SearchBoxFontSize", SearchBoxFontSize.ToString());
                ini.Write("SearchResultsFontSize", SearchResultsFontSize.ToString());

                ini.Write("GridTransparency", GridTransparency.ToString());
                ini.Write("GridColumns", GridColumns.ToString());
                ini.Write("GridRows", GridRows.ToString());
                ini.Write("DefaultItemSize", DefaultItemSize.ToString());
                ini.Write("IsLightTheme", IsLightTheme.ToString());

                ini.Write("IconScale", IconScale.ToString());

                ini.Write("FontItemsSize", FontItemsSize.ToString());
                ini.Write("FontItemsColor", FontItemsColor == null ? "" : FontItemsColor);
                ini.Write("FontItemsName", FontItemsName == null ? "" : FontItemsName);

                ini.Write("FontTabsSize", FontTabsSize.ToString());
                ini.Write("FontTabsColor", FontTabsColor == null ? "" : FontTabsColor);
                ini.Write("FontTabsName", FontTabsName == null ? "" : FontTabsName);

                ini.Write("FontUiSize", FontUiSize.ToString());
                ini.Write("FontUiColor", FontUiColor == null ? "" : FontUiColor);
                ini.Write("FontUiName", FontUiName == null ? "" : FontUiName);

                ini.Write("BackupDays", BackupDays.ToString());
                ini.Write("LastBackupDate", LastBackupDate == null ? "" : LastBackupDate);
                ini.Write("StartMenuSyncHours", StartMenuSyncHours.ToString());
                ini.Write("LastSyncDate", LastSyncDate == null ? "" : LastSyncDate);
                ini.Write("SearchSaveHistory", SearchSaveHistory.ToString());
                ini.Write("HotkeyWin", HotkeyWin.ToString());
                ini.Write("SkinName", SkinName == null ? "" : SkinName);
                ini.Write("FirstRunDone", FirstRunDone.ToString());
                ini.Write("UpdateCheckEnabled", UpdateCheckEnabled.ToString());
                ini.Write("UpdateCheckDays", UpdateCheckDays.ToString());
                ini.Write("UpdateAutoInstall", UpdateAutoInstall.ToString());
                ini.Write("LastUpdateCheck", LastUpdateCheck == null ? "" : LastUpdateCheck);

                ini.Write("LabelTwoRows", LabelTwoRows.ToString());
                ini.Write("LabelTrimShortcut", LabelTrimShortcut.ToString());
                ini.Write("LabelTrimExtension", LabelTrimExtension.ToString());
                ini.Write("LabelCtrlFullNames", LabelCtrlFullNames.ToString());
                ini.Write("LabelAlign2Rows", LabelAlign2Rows.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving settings: " + ex.Message);
            }
        }
    }
}
