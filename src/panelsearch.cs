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
                    // Slot 6 is reserved for the user description so search can
                    // toggle it independently. Insert a placeholder when missing.
                    while (list.Count < 6) Add(list, "~~pad" + list.Count);
                    list.Insert(DescIndex, it.ShortDescription == null ? "" : it.ShortDescription.ToLowerInvariant());

                    string target = path;
                    if (path.ToLowerInvariant().EndsWith(".lnk"))
                    {
                        string tp = ResolveShortcut(path);
                        if (!string.IsNullOrEmpty(tp))
                        {
                            target = tp;
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
                    if (!string.IsNullOrEmpty(target) && File.Exists(target))
                    {
                        try
                        {
                            var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(target);
                            Add(list, vi.FileDescription);
                            Add(list, vi.ProductName);
                            Add(list, vi.CompanyName);
                            Add(list, vi.OriginalFilename);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            var entry = new MetaEntry();
            entry.Name = it.Name;
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

            lock (Gate) { cache[it] = entry; }
            return entry;
        }

        private static void Add(List<string> list, string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            s = s.Trim();
            if (s.Length < 2) return;
            list.Add(s.ToLowerInvariant());
        }

        // .lnk target via the WScript.Shell COM object (reflection, no dynamic).
        private static string ResolveShortcut(string lnkPath)
        {
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return null;
                object sh = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnkPath });
                object target = sc.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null);
                return target as string;
            }
            catch { return null; }
        }
    }
}
