using System;
using System.IO;
using System.Text.RegularExpressions;

namespace WinPanel
{
    // The app is portable: all data lives next to the exe and moving the whole
    // folder to another location must keep working. Most stored paths are
    // external (real programs) and are meant to be absolute, but the panel-local
    // copies under <exe>\ico are stored as absolute paths too — those break after
    // a move. The last known install dir is remembered in settings.ini (it
    // travels with the folder); when it differs from the current exe location,
    // every stored path that started with the old folder is rebased to the new
    // one. No absolute paths are invented: only previously stored ones are fixed.
    public static class FolderMigration
    {
        public static void RebaseIfNeeded()
        {
            try
            {
                string cur = AppDomain.CurrentDomain.BaseDirectory; // ends with '\'
                string iniPath = Path.Combine(cur, "settings.ini");
                string last;
                try { last = new IniFile(iniPath).Read("InstallDir"); }
                catch { return; }

                if (string.IsNullOrWhiteSpace(last))
                {
                    // First run with this mechanism: just remember the location.
                    try { new IniFile(iniPath).Write("InstallDir", cur); } catch { }
                    return;
                }
                if (string.Equals(last.TrimEnd('\\') + "\\", cur.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                    return; // same place, nothing to do

                string oldRoot = last.TrimEnd('\\') + "\\";
                int n = 0;
                n += RebaseFile(Path.Combine(cur, "records.xml"), oldRoot, cur);
                n += RebaseFile(Path.Combine(cur, "filetypes.xml"), oldRoot, cur);
                n += RebaseFile(Path.Combine(cur, "bookmarks.xml"), oldRoot, cur);
                try { new IniFile(iniPath).Write("InstallDir", cur); } catch { }
                AppLog.Write("Folder moved: " + last + " -> " + cur + "; rebased " + n + " stored path(s)");
            }
            catch (Exception ex)
            {
                AppLog.Write("FolderMigration", ex);
            }
        }

        // Replaces every occurrence of the old folder prefix with the new one.
        // Returns the number of replacements; the file is rewritten only when
        // something actually changed.
        private static int RebaseFile(string filePath, string oldRoot, string newRoot)
        {
            try
            {
                if (!File.Exists(filePath)) return 0;
                string text = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                var re = new Regex(Regex.Escape(oldRoot), RegexOptions.IgnoreCase);
                int count = re.Matches(text).Count;
                if (count == 0) return 0;
                string updated = re.Replace(text, newRoot.Replace("$", "$$"));
                File.WriteAllText(filePath, updated, new System.Text.UTF8Encoding(false));
                return count;
            }
            catch (Exception ex)
            {
                AppLog.Write("FolderMigration.RebaseFile " + filePath, ex);
                return 0;
            }
        }
    }
}
