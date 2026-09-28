using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace WinPanel
{
    // One rule per file type pattern: which icon to show for matching files on the panel
    // and which program to use when opening them.
    // Pattern forms: ".txt" (extension), "readme.*" / "report-*.xlsx" (name mask),
    // ".jpg / .jpeg / .png" (group of several patterns in one line).
    public class FileTypeRule
    {
        public string Extension { get; set; }
        public string IconPath { get; set; }
        public string OpenWith { get; set; }
        public string OpenArgs { get; set; }
    }

    // User-defined file type rules.
    // Icon priority on tiles: item icon (Change Icon) -> file type rule -> standard shell icon.
    // Rule priority: name masks (longest match wins) > exact extension.
    // Opening priority: custom program for the rule -> standard Windows opening.
    public static class FileTypes
    {
        public const string DefaultFilePath = "filetypes.xml";

        private static readonly List<FileTypeRule> rules = new List<FileTypeRule>();
        private static readonly Dictionary<string, Regex> wildcardCache = new Dictionary<string, Regex>();

        // ---------- pattern handling ----------

        public static List<string> SplitPatterns(string text)
        {
            var res = new List<string>();
            if (string.IsNullOrEmpty(text)) return res;
            foreach (var raw in text.Split(new char[] { '/', ';', ',' }))
            {
                string p = raw.Trim();
                if (p.Length > 0) res.Add(p);
            }
            return res;
        }

        // Normalizes one pattern. Returns null when it is not usable.
        public static string NormalizePatternPart(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return null;
            pattern = pattern.Trim().ToLowerInvariant();
            if (pattern.Length == 0 || pattern == ".") return null;
            if (pattern.IndexOf('\\') >= 0 || pattern.IndexOf(':') >= 0) return null;

            bool hasWildcard = pattern.IndexOf('*') >= 0 || pattern.IndexOf('?') >= 0;
            if (hasWildcard)
            {
                // "*.log" is exactly the same as the extension ".log"
                if (pattern.StartsWith("*.") && pattern.IndexOf('*', 1) < 0 && pattern.IndexOf('?') < 0 &&
                    pattern.LastIndexOf('.') == 1 && pattern.Length > 2)
                    pattern = pattern.Substring(1);
                else
                    return pattern; // name mask, e.g. "readme.*" or "report-*.xlsx"
            }
            if (!pattern.StartsWith(".")) pattern = "." + pattern;
            return pattern;
        }

        // Normalizes a whole entry (several patterns may be separated by "/").
        // Returns "part1 / part2 / ..." or null when nothing valid is left.
        public static string NormalizePatternText(string text)
        {
            var parts = new List<string>();
            foreach (var raw in SplitPatterns(text))
            {
                string p = NormalizePatternPart(raw);
                if (p == null) return null;
                if (!parts.Contains(p)) parts.Add(p);
            }
            if (parts.Count == 0) return null;
            return string.Join(" / ", parts.ToArray());
        }

        private static bool HasWildcard(string pattern)
        {
            return pattern.IndexOf('*') >= 0 || pattern.IndexOf('?') >= 0;
        }

        private static Regex GetWildcardRegex(string pattern)
        {
            Regex rx;
            if (wildcardCache.TryGetValue(pattern, out rx)) return rx;
            try
            {
                string expr = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                rx = new Regex(expr, RegexOptions.IgnoreCase);
            }
            catch
            {
                rx = null;
            }
            wildcardCache[pattern] = rx;
            return rx;
        }

        private static bool WildcardMatch(string name, string pattern)
        {
            var rx = GetWildcardRegex(pattern);
            if (rx == null) return false;
            try { return rx.IsMatch(name); } catch { return false; }
        }

        // ---------- rule lookup ----------
        // Name masks win over extensions; among masks the longest (most specific) wins.

        public static FileTypeRule GetRuleForPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;
                string fileName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(fileName)) return null;
                string ext = Path.GetExtension(path).ToLowerInvariant();

                FileTypeRule maskRule = null;
                int maskLen = -1;
                FileTypeRule extRule = null;
                foreach (var r in rules)
                {
                    foreach (var part in SplitPatterns(r.Extension))
                    {
                        if (HasWildcard(part))
                        {
                            if (part.Length > maskLen && WildcardMatch(fileName, part))
                            {
                                maskRule = r;
                                maskLen = part.Length;
                            }
                        }
                        else if (string.Equals(part, ext, StringComparison.OrdinalIgnoreCase))
                        {
                            extRule = r;
                        }
                    }
                }
                return maskRule != null ? maskRule : extRule;
            }
            catch { return null; }
        }

        // Returns the icon path assigned to the file type, or null.
        public static string GetIconForPath(string path)
        {
            var r = GetRuleForPath(path);
            if (r == null || string.IsNullOrEmpty(r.IconPath)) return null;
            return r.IconPath;
        }

        // ---------- list management ----------

        public static List<FileTypeRule> CloneRules()
        {
            var copy = new List<FileTypeRule>();
            foreach (var r in rules)
            {
                copy.Add(new FileTypeRule
                {
                    Extension = r.Extension,
                    IconPath = r.IconPath,
                    OpenWith = r.OpenWith,
                    OpenArgs = r.OpenArgs
                });
            }
            return copy;
        }

        public static void ReplaceAll(List<FileTypeRule> newRules)
        {
            rules.Clear();
            if (newRules == null) return;
            foreach (var r in newRules)
            {
                string norm = NormalizePatternText(r.Extension);
                if (norm == null) continue;
                r.Extension = norm;
                rules.Add(r);
            }
        }

        public static void Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var ser = new XmlSerializer(typeof(List<FileTypeRule>));
                using (var fs = File.OpenRead(path))
                {
                    var list = (List<FileTypeRule>)ser.Deserialize(fs);
                    ReplaceAll(list);
                }
            }
            catch
            {
                rules.Clear();
            }
        }

        public static void Save(string path)
        {
            try
            {
                var ser = new XmlSerializer(typeof(List<FileTypeRule>));
                using (var fs = File.Create(path))
                    ser.Serialize(fs, rules);
            }
            catch { }
        }

        // ---------- import / export ----------

        public static void ExportTo(string path, List<FileTypeRule> list)
        {
            try
            {
                var ser = new XmlSerializer(typeof(List<FileTypeRule>));
                using (var fs = File.Create(path))
                    ser.Serialize(fs, list);
            }
            catch { }
        }

        public static List<FileTypeRule> ImportFrom(string path)
        {
            try
            {
                var ser = new XmlSerializer(typeof(List<FileTypeRule>));
                using (var fs = File.OpenRead(path))
                {
                    var list = (List<FileTypeRule>)ser.Deserialize(fs);
                    if (list == null) return null;
                    var clean = new List<FileTypeRule>();
                    foreach (var r in list)
                    {
                        string norm = NormalizePatternText(r.Extension);
                        if (norm == null) continue;
                        r.Extension = norm;
                        clean.Add(r);
                    }
                    return clean;
                }
            }
            catch { return null; }
        }
    }
}
