using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace WinPanel
{
    // Collects searchable metadata for the items saved on the panel:
    // display name, file / exe name, folder names, full path, shortcut target
    // and version info of the target program (FileDescription, ProductName,
    // CompanyName, OriginalFilename). Everything the shell can give us cheaply.
    // Thread-safe: searches may run on a background thread.
    public static class PanelSearch
    {
        private class MetaEntry
        {
            public string Name;
            public string[] Metas;
            public ulong MaskA;
            public ulong MaskB;
            // Resolved target of a .lnk item ("" when not a shortcut).
            public string Target;
        }

        // Index of the description entry inside the metas array
        // (only present when the user filled one in).
        public const int DescIndex = 6;

        private static readonly object Gate = new object();
        private static readonly Dictionary<ShortcutItem, MetaEntry> cache = new Dictionary<ShortcutItem, MetaEntry>();

        public static void Invalidate(ShortcutItem item)
        {
            if (item == null) return;
            lock (Gate) { cache.Remove(item); }
        }

        // Short summary shown at the right side of a search result row.
        public static string GetSummary(ShortcutItem it)
        {
            try
            {
                if (!string.IsNullOrEmpty(it.Path))
                {
                    string fn = Path.GetFileName(it.Path);
                    string dir = Path.GetFileName(Path.GetDirectoryName(it.Path));
                    if (!string.IsNullOrEmpty(dir)) return dir + "\\" + fn;
                    return fn;
                }
            }
            catch { }
            return "";
        }

        public static string[] GetMetas(ShortcutItem it)
        {
            return GetEntry(it).Metas;
        }

        public static void GetMask(ShortcutItem it, out ulong maskA, out ulong maskB)
        {
            var e = GetEntry(it);
            maskA = e.MaskA;
            maskB = e.MaskB;
        }

        // Returns the user description of the item ("" when not set).
        public static string GetDescription(ShortcutItem it)
        {
            return it == null || it.ShortDescription == null ? "" : it.ShortDescription;
        }

        // Resolved target path of a .lnk item ("" when not a shortcut / unknown).
        public static string GetTarget(ShortcutItem it)
        {
            var e = GetEntry(it);
            return e.Target == null ? "" : e.Target;
        }

        // Target of a .lnk without building the full metadata entry: a cheap local
        // .lnk parse that never touches the network and never caches. Used by the
        // icon loading to decide whether extraction has to leave the UI thread.
        public static string ResolveTarget(string lnkPath)
        {
            return ResolveShortcut(lnkPath);
        }

        private static MetaEntry GetEntry(ShortcutItem it)
        {
            lock (Gate)
            {
                MetaEntry e;
                if (cache.TryGetValue(it, out e) && string.Equals(e.Name, it.Name, StringComparison.Ordinal))
                    return e;
            }

            // Build outside the lock: version info reads may touch the disk.
            var list = new List<string>();
            Add(list, it.Name);

            string path = it.Path;
            string resolvedTarget = "";
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string fileName = Path.GetFileName(path);
                    Add(list, fileName);
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Add(list, Path.GetFileName(dir));
                        string parent = Path.GetDirectoryName(dir);
                        if (!string.IsNullOrEmpty(parent)) Add(list, Path.GetFileName(parent));
                    }
                    Add(list, path);
                    Add(list, Path.GetExtension(path));

                    string target = path;
                    if (path.ToLowerInvariant().EndsWith(".lnk"))
                    {
                        string tp = ResolveShortcut(path);
                        if (!string.IsNullOrEmpty(tp))
                        {
                            target = tp;
                            resolvedTarget = tp;
                            Add(list, tp);
                            Add(list, Path.GetFileName(tp));
                            try
                            {
                                string tdir = Path.GetDirectoryName(tp);
                                if (!string.IsNullOrEmpty(tdir)) Add(list, Path.GetFileName(tdir));
                            }
                            catch { }
                        }
                    }
                    if (!string.IsNullOrEmpty(target))
                    {
                        string[] vi = VersionMetas(target);
                        for (int i = 0; i < vi.Length; i++) Add(list, vi[i]);
                    }
                }
                catch { }
            }

            // Slot 6 is reserved for the user description so search can toggle
            // it independently. Pad the list in case the path block was skipped.
            while (list.Count < 6) list.Add("~~pad" + list.Count);
            list.Insert(DescIndex, it.ShortDescription == null ? "" : it.ShortDescription.ToLowerInvariant());

            var entry = new MetaEntry();
            entry.Name = it.Name;
            entry.Target = resolvedTarget;
            entry.Metas = list.ToArray();
            ulong a = 0, b = 0;
            foreach (var m in entry.Metas)
            {
                ulong ma, mb;
                SearchCore.MakeMask(m, out ma, out mb);
                a |= ma;
                b |= mb;
            }
            entry.MaskA = a;
            entry.MaskB = b;

            // Soft cap: "Past search" rows are fresh objects, so their entries would
            // accumulate forever. 800 entries is far beyond any realistic panel.
            lock (Gate)
            {
                if (cache.Count > 800) cache.Clear();
                cache[it] = entry;
            }
            return entry;
        }

        private static void Add(List<string> list, string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            s = s.Trim();
            if (s.Length < 2) return;
            list.Add(s.ToLowerInvariant());
        }

        // Targets of resolved .lnk files this session (path -> target, "" when
        // unresolved). The resolution used to activate a fresh WScript.Shell COM
        // object for EVERY shortcut on EVERY call - milliseconds each, and the
        // first search (or the first icon pass over a .lnk-heavy tab) paid it
        // hundreds of times over. The binary parse is instant and covers the
        // normal local flavours; the UTF-16 scan catches the non-conformant
        // ones; the COM object is the last-resort fallback, one per thread.
        private static readonly object ResolveGate = new object();
        private static readonly Dictionary<string, string> ResolveCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [ThreadStatic]
        private static object WscriptShell;

        // Version-info strings per resolved target (FileDescription etc.). Panel
        // items commonly point at the same executable many times over (Start
        // menu mirrors, duplicated tiles); without this every cold meta entry
        // re-read the version resource from disk for each of them.
        private static readonly object ViGate = new object();
        private static readonly Dictionary<string, string[]> ViCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        private static string[] VersionMetas(string target)
        {
            lock (ViGate)
            {
                string[] hit;
                if (ViCache.TryGetValue(target, out hit)) return hit;
            }
            string[] metas = new string[0];
            try
            {
                var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(target);
                metas = new string[] { vi.FileDescription, vi.ProductName, vi.CompanyName, vi.OriginalFilename };
            }
            catch { }
            lock (ViGate)
            {
                if (ViCache.Count > 2000) ViCache.Clear();
                ViCache[target] = metas;
            }
            return metas;
        }

        // .lnk target via the WScript.Shell COM object (reflection, no dynamic).
        // One instance per thread: the COM activation itself costs milliseconds
        // and the automation object is reusable for any number of shortcuts.
        private static string ResolveShortcut(string lnkPath)
        {
            string key = lnkPath ?? "";
            lock (ResolveGate)
            {
                string hit;
                if (ResolveCache.TryGetValue(key, out hit)) return hit;
            }
            string target = null;
            try { target = MainForm.ParseLnkLocalBasePath(lnkPath); } catch { }
            if (string.IsNullOrEmpty(target))
            {
                try { target = MainForm.ScanUtf16AbsolutePath(File.ReadAllBytes(lnkPath)); }
                catch { }
            }
            if (string.IsNullOrEmpty(target))
            {
                try
                {
                    object sh = WscriptShell;
                    if (sh == null)
                    {
                        Type t = Type.GetTypeFromProgID("WScript.Shell");
                        if (t != null)
                        {
                            sh = Activator.CreateInstance(t);
                            WscriptShell = sh;
                        }
                    }
                    if (sh != null)
                    {
                        object sc = sh.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnkPath });
                        object tp = sc.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null);
                        target = tp as string;
                    }
                }
                catch { }
            }
            string resolved = target ?? "";
            lock (ResolveGate)
            {
                if (ResolveCache.Count > 4000) ResolveCache.Clear();
                ResolveCache[key] = resolved;
            }
            return resolved.Length == 0 ? null : resolved;
        }
    }
}
