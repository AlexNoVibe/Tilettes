using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace WinPanel
{
    // Scheduled full backups of everything the panel persists (settings,
    // shortcuts/records, bookmarks, file-type rules, search history and the exe
    // itself), compressed into autoBackup\backup_YYYY-MM-DD_HHMMSS.zip.
    // The schedule is checked at startup; a due backup is made 3 minutes after
    // launch, in the background, so the panel always starts instantly.
    public static class BackupManager
    {
        public static string BackupDir()
        {
            try { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autoBackup"); }
            catch { return "autoBackup"; }
        }

        private static bool IsDue(Settings s)
        {
            if (s == null || s.BackupDays <= 0) return false;
            DateTime last;
            if (string.IsNullOrEmpty(s.LastBackupDate) || !DateTime.TryParse(s.LastBackupDate, out last)) return true;
            return (DateTime.Now - last).TotalDays >= s.BackupDays;
        }

        private static bool scheduleGuard;

        // Called at startup: starts a one-shot 3-minute timer when a backup is due.
        public static void ScheduleIfNeeded(MainForm form, Settings s)
        {
            try
            {
                if (!IsDue(s) || scheduleGuard) return;
                scheduleGuard = true;
                AppLog.Write("Backup due (last: " + (s.LastBackupDate ?? "never") + "), will run in 3 minutes");
                var t = new System.Windows.Forms.Timer();
                t.Interval = 3 * 60 * 1000;
                t.Tick += delegate
                {
                    try
                    {
                        t.Stop();
                        t.Dispose();
                        System.Threading.ThreadPool.QueueUserWorkItem(delegate { RunBackup(form, s, true); });
                    }
                    catch (Exception ex) { AppLog.Write("Backup timer", ex); }
                };
                t.Start();
            }
            catch (Exception ex) { AppLog.Write("Backup schedule", ex); }
        }

        // Synchronous backup, safe to call from a settings button (files are small).
        // Returns the zip path, or null when the backup failed / was not needed.
        public static string RunBackup(MainForm form, Settings s, bool notify)
        {
            try
            {
                string dir = BackupDir();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var files = new List<KeyValuePair<string, string>>();
                TryAdd(files, "settings.ini", Path.Combine(baseDir, "settings.ini"));
                TryAdd(files, "records.xml", Path.Combine(baseDir, "records.xml"));
                TryAdd(files, "bookmarks.xml", Path.Combine(baseDir, "bookmarks.xml"));
                TryAdd(files, "filetypes.xml", Path.Combine(baseDir, "filetypes.xml"));
                TryAdd(files, "searchHistory.xml", Path.Combine(baseDir, "searchHistory.xml"));
                TryAdd(files, "WinPanel.exe", Path.Combine(baseDir, "WinPanel.exe"));

                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                string zipPath = Path.Combine(dir, "backup_" + stamp + ".zip");
                if (!ZipWriter.Create(zipPath, files))
                {
                    if (notify) Notify(form, Loc.S("Backup failed - see log.txt", "Бэкап не удался — подробности в log.txt"));
                    return null;
                }

                // Remember the date on the UI thread (settings belong to it).
                try
                {
                    s.LastBackupDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    if (form != null && form.IsHandleCreated)
                        form.BeginInvoke((MethodInvoker)delegate { try { s.Save(form.SettingsFilePath); } catch { } });
                }
                catch { }

                PruneOld(dir, 30);
                AppLog.Write("Backup created: " + zipPath + " (" + files.Count + " files)");
                if (notify) Notify(form, Loc.S("Backup created:", "Бэкап создан:") + " " + Path.GetFileName(zipPath));
                return zipPath;
            }
            catch (Exception ex)
            {
                AppLog.Write("Backup", ex);
                if (notify) Notify(form, Loc.S("Backup failed - see log.txt", "Бэкап не удался — подробности в log.txt"));
                return null;
            }
        }

        private static void TryAdd(List<KeyValuePair<string, string>> files, string entryName, string sourcePath)
        {
            try { if (File.Exists(sourcePath)) files.Add(new KeyValuePair<string, string>(entryName, sourcePath)); }
            catch { }
        }

        // Keeps only the newest `keep` archives.
        private static void PruneOld(string dir, int keep)
        {
            try
            {
                var zips = new List<FileInfo>();
                foreach (var f in Directory.GetFiles(dir, "*.zip"))
                {
                    try { zips.Add(new FileInfo(f)); } catch { }
                }
                zips.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
                for (int i = keep; i < zips.Count; i++)
                {
                    try { zips[i].Delete(); } catch { }
                }
            }
            catch { }
        }

        private static void Notify(MainForm form, string text)
        {
            try
            {
                if (form == null || form.IsDisposed || !form.IsHandleCreated) return;
                form.BeginInvoke((MethodInvoker)delegate { try { form.ShowBalloon(text); } catch { } });
            }
            catch { }
        }

        // The list of archive names, oldest first (for a future restore UI).
        public static List<string> ListBackups()
        {
            var res = new List<string>();
            try
            {
                string dir = BackupDir();
                if (Directory.Exists(dir))
                {
                    foreach (var f in Directory.GetFiles(dir, "*.zip")) res.Add(f);
                    res.Sort(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { }
            return res;
        }
    }
}
