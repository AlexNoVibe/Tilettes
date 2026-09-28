using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace WinPanel
{
    public class SearchItem
    {
        public string Name;
        public string Dir;
        public string FullPath;
        public bool IsDir;
        public long Size;
    }

    // Background file index and fuzzy search that also understands the wrong
    // keyboard layout (typing "руддщц" finds "hellow" and vice versa).
    public static class SearchCore
    {
        public const string AllKey = "*:all";

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, List<SearchItem>> Indexes = new Dictionary<string, List<SearchItem>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> Running = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> Scanned = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private const int CapPerRoot = 200000;

        private static string Normalize(string root)
        {
            if (string.IsNullOrEmpty(root)) return "";
            root = root.TrimEnd('\\');
            if (root.Length == 2 && root[1] == ':') root += "\\";
            return root.ToLowerInvariant();
        }

        // Returns the (possibly partially built) index for a root and starts the
        // background builder on first use. The returned list may grow over time.
        public static List<SearchItem> GetIndex(string root, out bool running, out int scanned)
        {
            string key = Normalize(root);
            lock (Gate)
            {
                List<SearchItem> list;
                if (!Indexes.TryGetValue(key, out list))
                {
                    list = new List<SearchItem>();
                    Indexes[key] = list;
                    Running[key] = true;
                    Scanned[key] = 0;
                    string[] roots = (key == Normalize(AllKey)) ? FixedRoots() : new string[] { root };
                    var t = new Thread(delegate() { Build(key, roots); });
                    t.IsBackground = true;
                    t.Priority = ThreadPriority.BelowNormal;
                    t.Start();
                }
                running = Running.ContainsKey(key) && Running[key];
                scanned = Scanned.ContainsKey(key) ? Scanned[key] : 0;
                return list;
            }
        }

        public static string[] FixedRoots()
        {
            var res = new List<string>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.DriveType == DriveType.Fixed && d.IsReady) res.Add(d.RootDirectory.FullName);
                    }
                    catch { }
                }
            }
            catch { }
            if (res.Count == 0) res.Add("C:\\");
            return res.ToArray();
        }

        private static void Build(string key, string[] roots)
        {
            int count = 0;
            bool done = false;
            try
            {
                var queue = new Queue<string>();
                foreach (var r in roots) queue.Enqueue(r);
                var buf = new List<SearchItem>(400);
                var badNames = new string[] { "$recycle.bin", "system volume information" };

                while (queue.Count > 0 && !done)
                {
                    string dir = queue.Dequeue();
                    try
                    {
                        foreach (var d in Directory.EnumerateDirectories(dir))
                        {
                            try
                            {
                                var di = new DirectoryInfo(d);
                                string nm = di.Name.ToLowerInvariant();
                                bool skip = false;
                                foreach (var b in badNames) if (nm == b) { skip = true; break; }
                                if (!skip)
                                {
                                    buf.Add(new SearchItem { Name = di.Name, Dir = dir, FullPath = d, IsDir = true });
                                    try
                                    {
                                        if ((di.Attributes & FileAttributes.ReparsePoint) == 0) queue.Enqueue(d);
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                            count++;
                            if (count % 400 == 0) Flush(key, buf, count);
                            if (count >= CapPerRoot) { done = true; break; }
                        }
                        if (done) break;
                        foreach (var f in Directory.EnumerateFiles(dir))
                        {
                            try
                            {
                                var fi = new FileInfo(f);
                                buf.Add(new SearchItem { Name = fi.Name, Dir = dir, FullPath = f, IsDir = false, Size = fi.Length });
                            }
                            catch { }
                            count++;
                            if (count % 400 == 0) Flush(key, buf, count);
                            if (count >= CapPerRoot) { done = true; break; }
                        }
                    }
                    catch { }
                }
                Flush(key, buf, count);
            }
            catch { }
            lock (Gate)
            {
                Running[key] = false;
                Scanned[key] = count;
            }
        }

        private static void Flush(string key, List<SearchItem> buf, int count)
        {
            if (buf.Count == 0) return;
            lock (Gate)
            {
                List<SearchItem> l;
                if (Indexes.TryGetValue(key, out l))
                {
                    if (l.Count < CapPerRoot) l.AddRange(buf);
                }
                Scanned[key] = count;
            }
            buf.Clear();
        }

        // ---------- fuzzy + keyboard layout matching ----------

        private static readonly string EnKeys = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`";
        private static readonly string RuKeys = "йцукенгшщзхъфывапролджэячсмитьбю.ё";

        public static string Translate(string s, bool toRu)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                char lo = char.ToLowerInvariant(c);
                int idx = toRu ? EnKeys.IndexOf(lo) : RuKeys.IndexOf(lo);
                if (idx < 0 || idx >= (toRu ? RuKeys.Length : EnKeys.Length))
                {
                    sb.Append(c);
                    continue;
                }
                char mapped = toRu ? RuKeys[idx] : EnKeys[idx];
                sb.Append(char.IsUpper(c) ? char.ToUpperInvariant(mapped) : mapped);
            }
            return sb.ToString();
        }

        private static int ScoreOne(string name, string v)
        {
            if (v.Length == 0) return -1;
            if (name.StartsWith(v, StringComparison.Ordinal)) return 1000 - name.Length;
            int idx = name.IndexOf(v, StringComparison.Ordinal);
            if (idx >= 0) return 700 - idx * 2 - name.Length;
            // Fuzzy subsequence
            int pi = 0, gaps = 0, last = -1;
            for (int i = 0; i < name.Length && pi < v.Length; i++)
            {
                if (name[i] == v[pi])
                {
                    if (last >= 0 && i - last > 1) gaps += i - last - 1;
                    last = i;
                    pi++;
                }
            }
            if (pi == v.Length) return 400 - gaps * 3 - Math.Max(0, name.Length - v.Length);
            return -1;
        }

        // Searches a snapshot of the given index list. Safe to call while the
        // background builder is still appending (the list is locked while read).
        public static List<SearchItem> Run(string query, List<SearchItem> source, int limit)
        {
            var res = new List<SearchItem>();
            if (source == null) return res;
            query = query.Trim();
            if (query.Length == 0) return res;

            string q = query.ToLowerInvariant();
            string v2 = Translate(q, true).ToLowerInvariant();
            string v3 = Translate(q, false).ToLowerInvariant();
            var variants = new List<string>();
            variants.Add(q);
            if (v2 != q) variants.Add(v2);
            if (v3 != q && v3 != v2) variants.Add(v3);

            var scored = new List<KeyValuePair<int, SearchItem>>();
            lock (Gate)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    var it = source[i];
                    string nm = (it.Name ?? "").ToLowerInvariant();
                    int best = -1;
                    for (int k = 0; k < variants.Count; k++)
                    {
                        int s = ScoreOne(nm, variants[k]);
                        if (s > best) best = s;
                    }
                    if (best >= 0) scored.Add(new KeyValuePair<int, SearchItem>(best, it));
                }
            }
            scored.Sort(delegate(KeyValuePair<int, SearchItem> a, KeyValuePair<int, SearchItem> b)
            {
                if (b.Key != a.Key) return b.Key - a.Key;
                return string.Compare(a.Value.Name, b.Value.Name, StringComparison.OrdinalIgnoreCase);
            });
            int n = Math.Min(limit, scored.Count);
            for (int i = 0; i < n; i++) res.Add(scored[i].Value);
            return res;
        }
    }
}
