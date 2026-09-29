using System;
using System.Collections.Generic;
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
            try
            {
                foreach (string sub in Directory.GetDirectories(dir))
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
                    catch { }
                }
            }
            catch { }
            try
            {
                foreach (string file in Directory.GetFiles(dir))
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
                    catch { }
                }
            }
            catch { }
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
                    if (t.Kind == TabKind || t.Name == Loc.S("Start Menu", "Пуск")) { tab = t; break; }
                }
                if (tab == null)
                {
                    tab = new TabData();
                    tab.Name = Loc.S("Start Menu", "Пуск");
                    tab.Kind = TabKind;
                    tab.IsGridLayout = true;
                    records.Tabs.Add(tab);
                    AppLog.Write("Start Menu sync: tab created");
                }

                int cols = Math.Max(1, s.GridColumns);
                int rows = Math.Max(1, s.GridRows);

                MergeList(tab.Items, roots, cols, rows);

                // Overflow protection: too many top-level cells -> into the "Ещё" folder.
                form.EnsureTabFits(tab);

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
                    it.Size = 1; // synced items start at the minimal 1x1 size
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
                    it.Size = 1;
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
    }
}
