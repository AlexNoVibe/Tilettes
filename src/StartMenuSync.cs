using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace WinPanel
{
    // Mirrors the system Start Menu into a dedicated panel tab so everything is
    // reachable through the panel search. Three sources are merged:
    //   - the user's Start Menu   (%APPDATA%\Microsoft\Windows\Start Menu)
    //   - the local profile copy  (%LOCALAPPDATA%\Microsoft\Windows\Start Menu, if any)
    //   - the all-users Start Menu (C:\ProgramData\Microsoft\Windows\Start Menu)
    //   - UWP / system apps (Alarm, Calculator, Settings, ...) from shell:AppsFolder
    // The sync is differential: existing items keep their place, only new entries
    // are added and entries whose source (or shortcut target) disappeared are
    // removed. A due sync runs in the background shortly after startup and then
    // every StartMenuSyncHours (0 = off).
    public static class StartMenuSync
    {
        public const string TabKind = "startmenu";
        private const string KeyUser = "sm:user";
        private const string KeyCommon = "sm:common";
        private const string KeyLocal = "sm:local";
        private const string KeyUwp = "sm:uwp";

        private class Node
        {
            public string Name;
            public string Src;          // stable key stored in ShortcutItem.Src
            public bool IsFolder;
            public string Path;         // launchable path (file/dir) or shell:AppsFolder\...
            public bool IsUwp;
            public List<Node> Children = new List<Node>();
        }

        public static bool IsDue(Settings s)
        {
            if (s == null || s.StartMenuSyncHours <= 0) return false;
            DateTime last;
            if (string.IsNullOrEmpty(s.LastSyncDate) || !DateTime.TryParse(s.LastSyncDate, out last)) return true;
            return (DateTime.Now - last).TotalHours >= s.StartMenuSyncHours;
        }

        // Startup check: sync in the background shortly after the window is up.
        public static void ScheduleIfNeeded(MainForm form, Settings s)
        {
            try
            {
                if (!IsDue(s)) return;
                var t = new System.Windows.Forms.Timer();
                t.Interval = 20 * 1000; // let the panel finish its own startup first
                t.Tick += delegate
                {
                    try
                    {
                        t.Stop();
                        t.Dispose();
                        Run(form, s, false);
                    }
                    catch (Exception ex) { AppLog.Write("Sync timer", ex); }
                };
                t.Start();
            }
            catch (Exception ex) { AppLog.Write("Sync schedule", ex); }
        }

        // Public entry: builds the tree on a background thread, merges on the UI thread.
        public static void Run(MainForm form, Settings s, bool force)
        {
            try
            {
                if (!force && !IsDue(s)) return;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    List<Node> roots = BuildTree();
                    try
                    {
                        if (form != null && form.IsHandleCreated && !form.IsDisposed)
                            form.BeginInvoke((MethodInvoker)delegate { ApplyMerge(form, s, roots, force); });
                    }
                    catch (Exception ex) { AppLog.Write("Sync dispatch", ex); }
                });
            }
            catch (Exception ex) { AppLog.Write("Sync run", ex); }
        }

        // ---------- source enumeration (background thread) ----------

        private static List<Node> BuildTree()
        {
            var roots = new List<Node>();

            string userMenu = SafeFolder(Environment.SpecialFolder.StartMenu);
            if (userMenu != null) roots.Add(MirrorFolder(userMenu, KeyUser, Loc.S("Start Menu (user)", "Пуск (пользователь)")));

            string localMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) ?? "",
                "Microsoft", "Windows", "Start Menu");
            if (!string.IsNullOrEmpty(localMenu) && Directory.Exists(localMenu) &&
                (userMenu == null || !string.Equals(localMenu, userMenu, StringComparison.OrdinalIgnoreCase)))
                roots.Add(MirrorFolder(localMenu, KeyLocal, Loc.S("Start Menu (local)", "Пуск (локальный)")));

            string commonMenu = SafeFolder(Environment.SpecialFolder.CommonStartMenu);
            if (commonMenu != null) roots.Add(MirrorFolder(commonMenu, KeyCommon, Loc.S("Start Menu (all users)", "Пуск (все пользователи)")));

            roots.Add(BuildUwpFolder());
            return roots;
        }

        private static string SafeFolder(Environment.SpecialFolder folder)
        {
            try
            {
                string p = Environment.GetFolderPath(folder);
                return (!string.IsNullOrEmpty(p) && Directory.Exists(p)) ? p : null;
            }
            catch { return null; }
        }

        private static Node MirrorFolder(string dir, string key, string displayName)
        {
            var node = new Node();
            node.Name = displayName;
            node.Src = key;
            node.IsFolder = true;
            try { FillChildren(node, dir); }
            catch (Exception ex) { AppLog.Write("MirrorFolder " + dir, ex); }
            return node;
        }

        private static void FillChildren(Node parent, string dir)
        {
            // Subdirectories
            try
            {
                string[] subDirs = null;
                try { subDirs = Directory.GetDirectories(dir); }
                catch (Exception ex) { AppLog.Write("FillChildren: GetDirectories " + dir, ex); }
                if (subDirs != null)
                {
                    foreach (string sub in subDirs)
                    {
                        try
                        {
                            string name = Path.GetFileName(sub.TrimEnd('\\'));
                            if (string.IsNullOrEmpty(name) || name.StartsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                            var n = new Node();
                            n.Name = name;
                            n.Src = "dir:" + sub.ToLowerInvariant();
                            n.IsFolder = true;
                            n.Path = sub;
                            FillChildren(n, sub);
                            parent.Children.Add(n);
                        }
                        catch (Exception ex) { AppLog.Write("FillChildren: subdir " + sub, ex); }
                    }
                }
            }
            catch (Exception ex) { AppLog.Write("FillChildren: directories " + dir, ex); }

            // Files (shortcuts)
            try
            {
                string[] files = null;
                try { files = Directory.GetFiles(dir); }
                catch (Exception ex) { AppLog.Write("FillChildren: GetFiles " + dir, ex); }
                if (files != null)
                {
                    foreach (string file in files)
                    {
                        try
                        {
                            string name = Path.GetFileName(file);
                            if (name.StartsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                            string target = ResolveShortcutTarget(file);
                            // Dead link: the .lnk names a target that no longer exists.
                            if (target != null && !File.Exists(target) && !Directory.Exists(target)) continue;
                            var n = new Node();
                            n.Name = Path.GetFileNameWithoutExtension(file);
                            n.Src = "file:" + file.ToLowerInvariant();
                            n.Path = file;
                            parent.Children.Add(n);
                        }
                        catch (Exception ex) { AppLog.Write("FillChildren: file " + file, ex); }
                    }
                }
            }
            catch (Exception ex) { AppLog.Write("FillChildren: files " + dir, ex); }
        }

        private static Node BuildUwpFolder()
        {
            var node = new Node();
            node.Name = Loc.S("Apps (system)", "Приложения (система)");
            node.Src = KeyUwp;
            node.IsFolder = true;
            try
            {
                foreach (var app in ShellItemApi.EnumerateApps())
                {
                    try
                    {
                        var n = new Node();
                        n.Name = app.Name;
                        n.Src = "uwp:" + app.ParsingName.ToLowerInvariant();
                        n.Path = app.ParsingName;
                        n.IsUwp = true;
                        node.Children.Add(n);
                    }
                    catch { }
                }
            }
            catch (Exception ex) { AppLog.Write("UWP enumeration", ex); }
            node.Children.Sort(delegate(Node a, Node b) { return string.Compare(a.Name, b.Name, true); });
            return node;
        }

        // .lnk target via WScript.Shell COM (reflection, no dynamic). Returns null
        // when the path is not a shortcut or the target cannot be determined
        // (advertised shortcuts stay: launching them still works).
        private static string ResolveShortcutTarget(string path)
        {
            try
            {
                if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return "";
                var t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return "";
                object sh = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { path });
                object target = sc.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null);
                return target as string;
            }
            catch { return ""; }
        }

        // ---------- merge into the records (UI thread) ----------

        private static void ApplyMerge(MainForm form, Settings s, List<Node> roots, bool notify)
        {
            try
            {
                Records records = form.Records;
                TabData tab = null;
                foreach (var t in records.Tabs)
                {
                    if (t.Kind == TabKind || t.Name == Loc.S("Start Menu", "Пуск") || t.Name == Loc.S("Start", "Пуск")) { tab = t; break; }
                }
                if (tab == null)
                {
                    tab = new TabData();
                    tab.Kind = TabKind;
                    tab.IsGridLayout = true;
                    records.Tabs.Add(tab);
                    AppLog.Write("Start Menu sync: tab created");
                }
                // The sync owns this tab and keeps its name canonical.
                tab.Name = Loc.S("Start", "Пуск");

                int cols = Math.Max(1, s.GridColumns);
                int rows = Math.Max(1, s.GridRows);

                MergeList(tab.Items, roots, cols, rows);

                // Start tab auto-layout: senior folders on the top row, their
                // subfolders copied as quick-access tiles across the field.
                LayoutStartTab(tab, cols, rows);

                // The sync is a bulk add event: collect search metadata for the
                // (re)merged items once, on a worker thread.
                form.WarmAllSearchMeta(true);

                // The layout uses the whole grid; the generic overflow packing
                // (EnsureTabFits) is skipped for this tab and would fight it.

                s.LastSyncDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                records.Save(form.RecordsFilePath);
                s.Save(form.SettingsFilePath);
                form.OnDataExternallyChanged();
                AppLog.Write("Start Menu sync finished: " + tab.Items.Count + " top-level items");
                if (notify)
                    form.ShowBalloon(Loc.S("Start Menu synced:", "Пуск синхронизирован:") + " " + tab.Items.Count);
            }
            catch (Exception ex)
            {
                AppLog.Write("Sync merge", ex);
                if (notify) form.ShowBalloon(Loc.S("Start Menu sync failed - see log.txt", "Синхронизация Пуска не удалась — подробности в log.txt"));
            }
        }

        // Differential merge of one list level.
        private static void MergeList(List<ShortcutItem> items, List<Node> nodes, int cols, int rows)
        {
            // Index the existing items by their sync key.
            var bySrc = new Dictionary<string, ShortcutItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in items)
            {
                if (!string.IsNullOrEmpty(it.Src) && !bySrc.ContainsKey(it.Src)) bySrc[it.Src] = it;
            }

            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in nodes)
            {
                keep.Add(n.Src);
                ShortcutItem existing;
                if (bySrc.TryGetValue(n.Src, out existing))
                {
                    if (existing.IsFolder && n.IsFolder) MergeList(existing.Children, n.Children, cols, rows);
                    // Files are kept as-is (user renames, sizes and positions survive).
                }
                else
                {
                    var it = new ShortcutItem();
                    it.Name = n.Name;
                    it.Src = n.Src;
                    it.IsFolder = n.IsFolder;
                    it.Path = n.Path ?? "";
                    it.IsUwp = n.IsUwp;
                    it.Size = 2; // 1x1 synced tiles proved too small to read
                    if (n.IsFolder) MergeChildren(it.Children, n.Children);
                    // Find a free cell: the spiral search starts at (0,0), so new
                    // items pack into the first available corner of the grid.
                    MainForm.PlaceIntoGridStatic(items, it, cols, rows);
                    items.Add(it);
                }
            }

            // Remove synced items whose source disappeared (dead links included).
            for (int i = items.Count - 1; i >= 0; i--)
            {
                string src = items[i].Src;
                if (IsSyncKey(src) && !keep.Contains(src)) items.RemoveAt(i);
            }
        }

        private static void MergeChildren(List<ShortcutItem> items, List<Node> nodes)
        {
            // Folder children are a flat flow: same diff logic, no grid placement.
            var bySrc = new Dictionary<string, ShortcutItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in items)
                if (!string.IsNullOrEmpty(it.Src) && !bySrc.ContainsKey(it.Src)) bySrc[it.Src] = it;

            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in nodes)
            {
                keep.Add(n.Src);
                ShortcutItem existing;
                if (!bySrc.TryGetValue(n.Src, out existing))
                {
                    var it = new ShortcutItem();
                    it.Name = n.Name;
                    it.Src = n.Src;
                    it.IsFolder = n.IsFolder;
                    it.Path = n.Path ?? "";
                    it.IsUwp = n.IsUwp;
                    it.Size = 2; // 1x1 synced tiles proved too small to read
                    if (n.IsFolder) MergeChildren(it.Children, n.Children);
                    items.Add(it);
                }
                else if (existing.IsFolder && n.IsFolder)
                {
                    MergeChildren(existing.Children, n.Children);
                }
            }
            for (int i = items.Count - 1; i >= 0; i--)
            {
                string src = items[i].Src;
                if (IsSyncKey(src) && !keep.Contains(src)) items.RemoveAt(i);
            }
        }

        private static bool IsSyncKey(string src)
        {
            if (string.IsNullOrEmpty(src)) return false;
            return src.StartsWith("sm:", StringComparison.OrdinalIgnoreCase) ||
                   src.StartsWith("dir:", StringComparison.OrdinalIgnoreCase) ||
                   src.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                   src.StartsWith("uwp:", StringComparison.OrdinalIgnoreCase);
        }

        // ---------- Start tab auto-layout ----------

        // Src prefix of the quick-access copies; they are rebuilt from scratch on
        // every layout pass, which makes the pass idempotent.
        private const string AutoKey = "sm:auto:";

        // Total number of descendants of an item (the whole subtree).
        private static int CountDeep(ShortcutItem it)
        {
            int n = 0;
            if (it.Children != null)
            {
                n += it.Children.Count;
                foreach (var c in it.Children) n += CountDeep(c);
            }
            return n;
        }

        // Tile size of a quick-access copy by its content: more items -> bigger,
        // never 1x1 (too small to be readable). Empty folders are not placed at all.
        private static int TileSizeFor(int count)
        {
            if (count >= 80) return 6;
            if (count >= 40) return 5;
            if (count >= 15) return 4;
            if (count >= 6) return 3;
            return 2;
        }

        // Re-lays out the Start tab over the whole grid. Returns true when anything
        // changed. A pure layout pass on the existing mirror data: nothing is moved
        // between folders, every path and every child stays where it was.
        // Public entry for the startup pass (works without a sync).
        internal static bool RelayoutStartTab(Records records, int cols, int rows)
        {
            try
            {
                TabData tab = null;
                foreach (var t in records.Tabs)
                    if (t.Kind == TabKind) { tab = t; break; }
                if (tab == null) return false;
                return LayoutStartTab(tab, cols, rows);
            }
            catch (Exception ex) { AppLog.Write("Start tab relayout", ex); return false; }
        }

        // The layout itself (simplified on request, 30.09):
        //   ONLY the senior folders (the sync roots) are placed on the tab, each
        //   at the maximum possible size (6x6, or a uniform smaller size when
        //   the grid is too narrow), packed edge to edge in the top row.
        //   Nothing else is put on the field: the subfolder quick-access copies
        //   of the old algorithm are dropped and stay inside their seniors.
        //   The full old filling algorithm (content-based senior sizes, phase B
        //   columns, phase C overflow with the 1-cell gap rule) is kept below in
        //   a commented block so it can be restored later.
        private static bool LayoutStartTab(TabData tab, int cols, int rows)
        {
            bool changed = false;

            // Drop the previous auto-copies — they are rebuilt from scratch.
            for (int i = tab.Items.Count - 1; i >= 0; i--)
            {
                string src = tab.Items[i].Src;
                if (src != null && src.StartsWith(AutoKey, StringComparison.OrdinalIgnoreCase))
                {
                    tab.Items.RemoveAt(i);
                    changed = true;
                }
            }

            var seniors = new List<ShortcutItem>();
            foreach (var it in tab.Items)
                if (it.IsFolder && IsSyncKey(it.Src)) seniors.Add(it);
            if (seniors.Count == 0) return changed;

            var counts = new Dictionary<ShortcutItem, int>();
            foreach (var s in seniors) counts[s] = CountDeep(s);

            // Richest senior first: it takes the leftmost spot of the top row.
            seniors.Sort(delegate(ShortcutItem a, ShortcutItem b)
            {
                int d = counts[b] - counts[a];
                if (d != 0) return d;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            // owner: 0 = free, idx+1 = cells belonging to senior #idx,
            // int.MaxValue = user-placed items (never overlapped).
            int[,] owner = new int[rows, cols];

            // ---- OLD senior sizing (by content) — replaced 30.09, kept for restore ----
            //foreach (var s in seniors)
            //{
            //    int c = counts[s];
            //    s.Size = c == 0 ? 1 : (c >= 60 ? 6 : 5);
            //}
            //int total = 0;
            //foreach (var s in seniors) total += s.Size;
            //while (total > cols)
            //{
            //    ShortcutItem biggest = null;
            //    foreach (var s in seniors)
            //        if (counts[s] > 0 && s.Size > 2 && (biggest == null || s.Size > biggest.Size)) biggest = s;
            //    if (biggest == null) break;
            //    biggest.Size--;
            //    total--;
            //}

            // NEW: every senior at the maximum possible size — 6x6 each while the
            // row fits the grid width, then one uniform smaller size, with the
            // spare cells of the last row handed to the leftmost seniors.
            int baseSize = Math.Max(1, Math.Min(6, cols / Math.Max(1, seniors.Count)));
            int spare = cols - baseSize * seniors.Count;
            foreach (var s in seniors)
            {
                int sz = baseSize + (spare > 0 ? 1 : 0);
                if (sz > 6) sz = 6;
                if (spare > 0) spare--;
                if (s.Size != sz) { s.Size = sz; changed = true; }
            }

            // User-placed items keep their cells (and block them).
            foreach (var it in tab.Items)
            {
                if (it.IsFolder && IsSyncKey(it.Src)) continue;
                int sz = Math.Max(1, it.Size);
                if (it.GridX >= 0 && it.GridY >= 0)
                    MarkRect(owner, it.GridX, it.GridY, sz, sz, int.MaxValue);
            }

            // Top row: seniors packed edge to edge.
            int x = 0;
            for (int idx = 0; idx < seniors.Count; idx++)
            {
                var s = seniors[idx];
                int gx = Math.Max(0, Math.Min(cols - s.Size, x));
                if (s.GridX != gx || s.GridY != 0) changed = true;
                s.GridX = gx;
                s.GridY = 0;
                MarkRect(owner, gx, 0, s.Size, s.Size, idx + 1);
                x += s.Size;
            }

            // ---- OLD filling algorithm (30.09: commented out on request) ----
            // Phase B: every senior fills its own column (directly below it).
            // Phase C: the rest goes to the first free spot on the field, keeping
            //          a 1-cell gap above foreign tiles.
            //var overflow = new List<KeyValuePair<int, ShortcutItem>>();
            //for (int idx = 0; idx < seniors.Count; idx++)
            //{
            //    var senior = seniors[idx];
            //    if (senior.Children == null || senior.Children.Count == 0) continue;
            //    int cy = senior.Size;     // the column starts right below the senior
            //    int cx = senior.GridX;
            //    var subs = new List<ShortcutItem>();
            //    foreach (var c in senior.Children)
            //        if (c.IsFolder && CountDeep(c) > 0) subs.Add(c);
            //    subs.Sort(delegate(ShortcutItem a, ShortcutItem b)
            //    {
            //        return CountDeep(b).CompareTo(CountDeep(a));
            //    });
            //    foreach (var sub in subs)
            //    {
            //        int k = TileSizeFor(CountDeep(sub));
            //        if (k > senior.Size) k = senior.Size;
            //        if (cy + k <= rows && CanPlace(owner, idx + 1, cx, cy, k))
            //        {
            //            PlaceCopy(tab, owner, idx, sub, k, cx, cy);
            //            changed = true;
            //            cy += k;
            //        }
            //        else
            //        {
            //            overflow.Add(new KeyValuePair<int, ShortcutItem>(idx, sub));
            //        }
            //    }
            //}
            //foreach (var kv in overflow)
            //{
            //    var senior = seniors[kv.Key];
            //    int k = TileSizeFor(CountDeep(kv.Value));
            //    if (k > senior.Size) k = senior.Size;
            //    Point p = FindSpot(owner, kv.Key + 1, k, cols, rows);
            //    if (p.X < 0) continue;   // the field is full: the rest stays inside the senior
            //    PlaceCopy(tab, owner, kv.Key, kv.Value, k, p.X, p.Y);
            //    changed = true;
            //}
            return changed;
        }

        // Used only by the commented-out filling algorithm above (kept so the
        // old behaviour can be restored verbatim).
        private static void PlaceCopy(TabData tab, int[,] owner, int seniorIdx, ShortcutItem sub, int size, int gx, int gy)
        {
            var copy = new ShortcutItem();
            copy.Name = sub.Name;
            copy.Src = AutoKey + sub.Src;
            copy.IsFolder = true;
            copy.Path = sub.Path ?? "";
            copy.IsUwp = sub.IsUwp;
            copy.Size = size;
            // The children list is SHARED with the original folder: the copy is a
            // second door to the same live content, the original loses nothing.
            copy.Children = sub.Children;
            copy.GridX = gx;
            copy.GridY = gy;
            tab.Items.Add(copy);
            MarkRect(owner, gx, gy, size, size, seniorIdx + 1);
        }

        private static void MarkRect(int[,] owner, int x, int y, int w, int h, int value)
        {
            int rows = owner.GetLength(0), cols = owner.GetLength(1);
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    if (y + dy < rows && x + dx < cols) owner[y + dy, x + dx] = value;
        }

        // Free k x k rect; a tile may touch a foreign senior's tiles SIDE-BY-SIDE,
        // but must keep a 1-cell gap when it would sit directly below one.
        private static bool CanPlace(int[,] owner, int value, int x, int y, int k)
        {
            int rows = owner.GetLength(0), cols = owner.GetLength(1);
            if (x < 0 || y < 0 || x + k > cols || y + k > rows) return false;
            for (int dy = 0; dy < k; dy++)
                for (int dx = 0; dx < k; dx++)
                    if (owner[y + dy, x + dx] != 0) return false;
            if (y > 0)
            {
                for (int dx = 0; dx < k; dx++)
                {
                    int o = owner[y - 1, x + dx];
                    if (o != 0 && o != value) return false;
                }
            }
            return true;
        }

        // First free position in reading order.
        private static Point FindSpot(int[,] owner, int value, int k, int cols, int rows)
        {
            for (int y = 0; y + k <= rows; y++)
                for (int x = 0; x + k <= cols; x++)
                    if (CanPlace(owner, value, x, y, k)) return new Point(x, y);
            return new Point(-1, -1);
        }
    }
}
