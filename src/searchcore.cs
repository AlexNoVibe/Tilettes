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
        // Precomputed for fast search: lowercase name and character bitmasks
        public string NameLower;
        public ulong MaskA; // a-z + 0-9
        public ulong MaskB; // cyrillic а-я + ё
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
                                    var si = new SearchItem { Name = di.Name, Dir = dir, FullPath = d, IsDir = true };
                                    Prepare(si);
                                    buf.Add(si);
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
                                var si = new SearchItem { Name = fi.Name, Dir = dir, FullPath = f, IsDir = false, Size = fi.Length };
                                Prepare(si);
                                buf.Add(si);
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

        // ---------- fast prefilter: character bitmasks ----------

        public static void MakeMask(string s, out ulong maskA, out ulong maskB)
        {
            maskA = 0; maskB = 0;
            if (string.IsNullOrEmpty(s)) return;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= 'a' && c <= 'z') maskA |= 1UL << (c - 'a');
                else if (c >= '0' && c <= '9') maskA |= 1UL << (26 + (c - '0'));
                else if (c >= 'а' && c <= 'я') maskB |= 1UL << (c - 'а');
                else if (c == 'ё') maskB |= 1UL << 32;
            }
        }

        // Characters of `need` missing in `have` (population count of the difference).
        public static int MissingBits(ulong needA, ulong haveA, ulong needB, ulong haveB)
        {
            ulong missA = needA & ~haveA;
            ulong missB = needB & ~haveB;
            int n = 0;
            while (missA != 0) { missA &= missA - 1; n++; }
            while (missB != 0) { missB &= missB - 1; n++; }
            return n;
        }

        private static void Prepare(SearchItem it)
        {
            it.NameLower = (it.Name ?? "").ToLowerInvariant();
            MakeMask(it.NameLower, out it.MaskA, out it.MaskB);
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

        public static List<string> Variants(string query)
        {
            var variants = new List<string>();
            string q = (query ?? "").Trim().ToLowerInvariant();
            if (q.Length == 0) return variants;
            string v2 = Translate(q, true).ToLowerInvariant();
            string v3 = Translate(q, false).ToLowerInvariant();
            variants.Add(q);
            if (v2 != q) variants.Add(v2);
            if (v3 != q && v3 != v2) variants.Add(v3);
            return variants;
        }

        public static int Score(string textLower, List<string> variants)
        {
            return Score(textLower, variants, 4);
        }

        public static int Score(string textLower, List<string> variants, int allowLevel)
        {
            if (string.IsNullOrEmpty(textLower) || variants == null) return -1;
            int best = -1;
            for (int i = 0; i < variants.Count; i++)
            {
                int s = ScoreOne(textLower, variants[i], allowLevel);
                if (s > best) best = s;
            }
            return best;
        }

        public static List<string> Variants(string query, int allowLevel)
        {
            var variants = new List<string>();
            string q = (query ?? "").Trim().ToLowerInvariant();
            if (q.Length == 0) return variants;
            string v2 = Translate(q, true).ToLowerInvariant();
            string v3 = Translate(q, false).ToLowerInvariant();
            variants.Add(q);
            if (v2 != q) variants.Add(v2);
            if (v3 != q && v3 != v2) variants.Add(v3);
            return variants;
        }

        public static int ScoreMeta(List<string> metas, List<string> variants, bool inMeta, bool inPaths, bool inDesc, int allowLevel)
        {
            if (metas == null || variants == null || variants.Count == 0) return -1;
            int best = -1;
            // Metas layout: [0]=name, [1]=fileName, [2]=parent folder, [3]=grandparent,
            // [4]=full path, [5]=extension, then shortcut target/version info entries.
            for (int k = 0; k < variants.Count; k++)
            {
                for (int i = 0; i < metas.Count; i++)
                {
                    if (i == 4 && !inPaths) continue;
                    if (i >= 6 && !inMeta) continue;
                    if (i == 1 && !inMeta) continue;
                    int s = ScoreOne(metas[i], variants[k], allowLevel);
                    if (s > best) best = s;
                }
            }
            return best;
        }

        private static int ScoreOne(string name, string v, int allowLevel)
        {
            if (v.Length == 0) return -1;
            if (name.StartsWith(v, StringComparison.Ordinal)) return 1000 - name.Length;
            int idx = name.IndexOf(v, StringComparison.Ordinal);
            if (idx >= 0) return 700 - idx * 2 - name.Length;

            // Relaxed fuzzy pass. allowLevel lowers sensitivity: it widens the
            // typo tolerance and the acceptable target length.
            int lenDiff = name.Length - v.Length;
            int maxLenDiff = 16 + allowLevel * 8;
            if (lenDiff < 0 || lenDiff > maxLenDiff) return -1;
            int allow = allowLevel <= 0 ? 0 : Math.Max(1, (int)Math.Round(v.Length / (6.0 - allowLevel)));
            if (allowLevel >= 3) allow = Math.Max(allow, 3);
            int cost = ApproxSubsequence(name, v, allow);
            if (cost < 0) return -1;
            return 300 - cost * 40 - lenDiff * 3;
        }

        public static int ScoreVariant(string textLower, string variant)
        {
            return ScoreOne(textLower, variant);
        }

        private static int ScoreOne(string name, string v)
        {
            if (v.Length == 0) return -1;
            if (name.StartsWith(v, StringComparison.Ordinal)) return 1000 - name.Length;
            int idx = name.IndexOf(v, StringComparison.Ordinal);
            if (idx >= 0) return 700 - idx * 2 - name.Length;

            // Relaxed fuzzy pass: typos (substitutions) and wide gaps are welcome;
            // only targets not much longer than the query participate (keeps paths out).
            int lenDiff = name.Length - v.Length;
            if (lenDiff < 0 || lenDiff > 16) return -1;
            int allow = Math.Max(1, v.Length / 4);
            int cost = ApproxSubsequence(name, v, allow);
            if (cost < 0) return -1;
            return 400 - cost * 40 - lenDiff * 3;
        }

        // Minimal substitutions needed to match v as a subsequence of name,
        // bounded by the allowance (-1 when it cannot fit).
        private static int ApproxSubsequence(string name, string v, int allow)
        {
            int m = v.Length;
            if (m == 0) return 0;
            const int INF = 1000;
            var prev = new int[m + 1];
            var cur = new int[m + 1];
            for (int j = 1; j <= m; j++) prev[j] = INF;
            prev[0] = 0;
            int best = INF;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                for (int j = 0; j <= m; j++) cur[j] = prev[j];
                int jmax = Math.Min(m, i + 1);
                for (int j = 1; j <= jmax; j++)
                {
                    int p = prev[j - 1];
                    if (p < INF)
                    {
                        int missCost = 0;
                        if (c != v[j - 1])
                        {
                            // Digits count double: codes like perf0250 vs perf0261 are
                            // different names, while letter typos stay cheap.
                            bool digit = (c >= '0' && c <= '9') || (v[j - 1] >= '0' && v[j - 1] <= '9');
                            missCost = digit ? 2 : 1;
                        }
                        int cand = p + missCost;
                        if (cand < cur[j]) cur[j] = cand;
                    }
                }
                var tmp = prev; prev = cur; cur = tmp;
                if (prev[m] < best) best = prev[m];
                if (best == 0) break;
            }
            return best <= allow ? best : -1;
        }

        // Searches a snapshot of the given index list. Safe to call while the
        // background builder is still appending (the list is locked while read).
        // A cheap character-mask prefilter skips most of the candidates first.
        public static List<SearchItem> Run(string query, List<SearchItem> source, int limit)
        {
            var res = new List<SearchItem>();
            if (source == null) return res;
            var variants = Variants(query);
            int vn = variants.Count;
            if (vn == 0) return res;
            var vA = new ulong[vn];
            var vB = new ulong[vn];
            var vAllow = new int[vn];
            for (int k = 0; k < vn; k++)
            {
                MakeMask(variants[k], out vA[k], out vB[k]);
                vAllow[k] = Math.Max(1, variants[k].Length / 4);
            }

            var scored = new List<KeyValuePair<int, SearchItem>>();
            lock (Gate)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    var it = source[i];
                    string nm = it.NameLower;
                    if (nm == null) nm = (it.Name ?? "").ToLowerInvariant();
                    ulong iA = it.MaskA, iB = it.MaskB;
                    if (iA == 0 && iB == 0 && nm.Length > 0) MakeMask(nm, out iA, out iB);
                    int best = -1;
                    for (int k = 0; k < vn; k++)
                    {
                        if (MissingBits(vA[k], iA, vB[k], iB) > vAllow[k]) continue;
                        int s = ScoreOne(nm, variants[k], 4);
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

namespace WinPanel
{
    public static class SearchCoreMini
    {
        // Removes results that matched only in fields the user turned off:
        // inMeta covers file names / exe names, inPaths covers folder-only hits.
        public static void ApplyMiniSearchToggles(List<SearchItem> res, bool inMeta, bool inPaths, bool inDesc)
        {
            if (res == null || res.Count == 0) return;
            if (inMeta && inPaths) return;
            var kept = new List<SearchItem>();
            foreach (var it in res)
            {
                string path = it.FullPath ?? "";
                string dir = "";
                try { dir = System.IO.Path.GetDirectoryName(path) ?? ""; } catch { }
                // A hit is "path-only" when the query is not contained in the item's
                // own name (so it matched via parent folders from the index).
                bool inName = (it.NameLower ?? "").Length > 0;
                bool pathOnly = !inName && dir.Length > 0;
                if (pathOnly && !inPaths) continue;
                if (!pathOnly && !inMeta) continue;
                kept.Add(it);
            }
            res.Clear();
            res.AddRange(kept);
        }
    }
}
