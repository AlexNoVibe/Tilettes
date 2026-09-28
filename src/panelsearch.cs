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
    public static class PanelSearch
    {
        private static readonly Dictionary<ShortcutItem, string[]> cache = new Dictionary<ShortcutItem, string[]>();
        private static readonly Dictionary<ShortcutItem, string> cacheName = new Dictionary<ShortcutItem, string>();

        public static void Invalidate(ShortcutItem item)
        {
            if (item == null) return;
            cache.Remove(item);
            cacheName.Remove(item);
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
            string[] cached;
            string cachedNm;
            if (cache.TryGetValue(it, out cached) && cacheName.TryGetValue(it, out cachedNm) &&
                string.Equals(cachedNm, it.Name, StringComparison.Ordinal))
                return cached;

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

            string[] res = list.ToArray();
            cache[it] = res;
            cacheName[it] = it.Name;
            return res;
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
