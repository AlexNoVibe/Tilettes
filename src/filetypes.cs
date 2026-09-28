using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace WinPanel
{
    // One rule per file extension: which icon to show for such files on the panel
    // and which program to use when opening them.
    public class FileTypeRule
    {
        public string Extension { get; set; }
        public string IconPath { get; set; }
        public string OpenWith { get; set; }
        public string OpenArgs { get; set; }
    }

    // User-defined file type rules.
    // Icon priority on tiles: item icon (Change Icon) -> file type icon (here) -> standard shell icon.
    // Opening priority: custom program for the type (here) -> standard Windows opening.
    public static class FileTypes
    {
        public const string DefaultFilePath = "filetypes.xml";

        private static readonly List<FileTypeRule> rules = new List<FileTypeRule>();

        public static string NormalizeExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return null;
            ext = ext.Trim().ToLowerInvariant();
            if (ext.Length == 0) return null;
            if (!ext.StartsWith(".")) ext = "." + ext;
            return ext;
        }

        public static FileTypeRule GetRule(string extension)
        {
            string norm = NormalizeExtension(extension);
            if (norm == null) return null;
            foreach (var r in rules)
                if (string.Equals(r.Extension, norm, StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        public static FileTypeRule GetRuleForPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;
                string ext = Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext)) return null;
                return GetRule(ext);
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
                string norm = NormalizeExtension(r.Extension);
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
    }
}
