using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace WinPanel
{
    // One remembered search: what the user typed and what they actually opened.
    // "Постоянно пишу games и жму games папку" - this pair gets remembered and
    // later ranks above everything else for that query.
    public class SearchHistoryEntry
    {
        public string Query { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsFolder { get; set; }
        public int Count { get; set; }
        public string LastUsed { get; set; }
    }

    // Load/save and ranking of the search history (searchHistory.xml next to the
    // exe). Recording can be disabled in the settings; the file is capped.
    public static class SearchHistoryStore
    {
        private static readonly object Gate = new object();
        private static List<SearchHistoryEntry> entries;
        private const int MaxEntries = 300;

        private static string FilePath()
        {
            try { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "searchHistory.xml"); }
            catch { return "searchHistory.xml"; }
        }

        public static void Load()
        {
            lock (Gate)
            {
                entries = new List<SearchHistoryEntry>();
                try
                {
                    string p = FilePath();
                    if (File.Exists(p))
                    {
                        var ser = new XmlSerializer(typeof(List<SearchHistoryEntry>));
                        using (var fs = new FileStream(p, FileMode.Open))
                        {
                            var list = ser.Deserialize(fs) as List<SearchHistoryEntry>;
                            if (list != null) entries = list;
                        }
                    }
                }
                catch (Exception ex) { AppLog.Write("SearchHistory.Load", ex); }
            }
        }

        public static void Save()
        {
            try
            {
                lock (Gate)
                {
                    var ser = new XmlSerializer(typeof(List<SearchHistoryEntry>));
                    using (var fs = new FileStream(FilePath(), FileMode.Create))
                    {
                        ser.Serialize(fs, entries);
                    }
                }
            }
            catch (Exception ex) { AppLog.Write("SearchHistory.Save", ex); }
        }

        // Records a "query -> opened item" pair (UI thread only).
        public static void Record(string query, string name, string path, bool isFolder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(path)) return;
                query = query.Trim();
                lock (Gate)
                {
                    if (entries == null) entries = new List<SearchHistoryEntry>();
                    SearchHistoryEntry hit = null;
                    foreach (var e in entries)
                    {
                        if (string.Equals(e.Query, query, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase)) { hit = e; break; }
                    }
                    if (hit == null)
                    {
                        hit = new SearchHistoryEntry();
                        hit.Query = query;
                        hit.Path = path;
                        hit.Name = name;
                        hit.IsFolder = isFolder;
                        hit.Count = 0;
                        entries.Add(hit);
                    }
                    hit.Count++;
                    hit.LastUsed = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    hit.Name = name;
                    if (entries.Count > MaxEntries) entries.RemoveRange(0, entries.Count - MaxEntries);
                }
            }
            catch (Exception ex) { AppLog.Write("SearchHistory.Record", ex); }
        }

        // Score used for the "past search" list: often and recently clicked first.
        public static List<SearchHistoryEntry> Top(int n)
        {
            var res = new List<SearchHistoryEntry>();
            lock (Gate)
            {
                if (entries == null) return res;
                var scored = new List<KeyValuePair<double, SearchHistoryEntry>>();
                foreach (var e in entries)
                {
                    double ageDays = 1;
                    DateTime t;
                    if (!string.IsNullOrEmpty(e.LastUsed) && DateTime.TryParse(e.LastUsed, out t))
                        ageDays = Math.Max(0.25, (DateTime.Now - t).TotalDays);
                    scored.Add(new KeyValuePair<double, SearchHistoryEntry>(e.Count / ageDays, e));
                }
                scored.Sort(delegate(KeyValuePair<double, SearchHistoryEntry> a, KeyValuePair<double, SearchHistoryEntry> b)
                {
                    return b.Key.CompareTo(a.Key);
                });
                for (int i = 0; i < scored.Count && i < n; i++) res.Add(scored[i].Value);
            }
            return res;
        }

        // Ranking boost for a live search: how often this exact query led to this
        // exact item. Thread-safe (called from the search worker).
        public static int Boost(string queryLower, string path)
        {
            try
            {
                if (string.IsNullOrEmpty(queryLower) || string.IsNullOrEmpty(path)) return 0;
                lock (Gate)
                {
                    if (entries == null) return 0;
                    foreach (var e in entries)
                    {
                        if (string.Equals(e.Query, queryLower, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase))
                            return Math.Min(e.Count, 5);
                    }
                }
            }
            catch { }
            return 0;
        }

        public static int Count()
        {
            lock (Gate) { return entries == null ? 0 : entries.Count; }
        }

        public static void Clear()
        {
            lock (Gate) { entries = new List<SearchHistoryEntry>(); }
            Save();
        }
    }
}
