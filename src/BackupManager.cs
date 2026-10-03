using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace WinPanel
{
    // What to tell the user about a finished backup: a manual "Backup now"
    // confirms success and failure, the scheduled backup stays silent on
    // success (the log.txt line is enough) and only warns when it failed.
    public enum BackupNotify { None, FailureOnly, Everything }

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
                        System.Threading.ThreadPool.QueueUserWorkItem(delegate { RunBackup(form, s, BackupNotify.FailureOnly); });
                    }
                    catch (Exception ex) { AppLog.Write("Backup timer", ex); }
                };
                t.Start();
            }
            catch (Exception ex) { AppLog.Write("Backup schedule", ex); }
        }

        // Synchronous backup, safe to call from a settings button (files are small).
        // Returns the zip path, or null when the backup failed / was not needed.
        public static string RunBackup(MainForm form, Settings s, BackupNotify notify)
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
                // The exe (current name first, whatever it is now: WinPanel/Tilettes/...).
                try
                {
                    string exePath = System.Windows.Forms.Application.ExecutablePath;
                    TryAdd(files, Path.GetFileName(exePath), exePath);
                }
                catch { }
                // Custom icons and panel-local shortcut copies: without them a
                // restored records.xml would point at missing icon files.
                TryAddFolder(files, "ico", Path.Combine(baseDir, "ico"));

                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                string zipPath = Path.Combine(dir, "backup_" + stamp + ".zip");
                if (!ZipWriter.Create(zipPath, files))
                {
                    if (notify != BackupNotify.None) Notify(form, Loc.S("Backup failed - see log.txt", "Бэкап не удался — подробности в log.txt"));
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
                if (notify == BackupNotify.Everything) Notify(form, Loc.S("Backup created:", "Бэкап создан:") + " " + Path.GetFileName(zipPath));
                return zipPath;
            }
            catch (Exception ex)
            {
                AppLog.Write("Backup", ex);
                if (notify != BackupNotify.None) Notify(form, Loc.S("Backup failed - see log.txt", "Бэкап не удался — подробности в log.txt"));
                return null;
            }
        }

        private static void TryAdd(List<KeyValuePair<string, string>> files, string entryName, string sourcePath)
        {
            try { if (File.Exists(sourcePath)) files.Add(new KeyValuePair<string, string>(entryName, sourcePath)); }
            catch { }
        }

        // Adds every file under dir as "<entryPrefix>/<relative path>" entries.
        private static void TryAddFolder(List<KeyValuePair<string, string>> files, string entryPrefix, string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                string full = new DirectoryInfo(dir).FullName;
                foreach (string f in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string rel = f.Substring(full.Length).TrimStart('\\', '/').Replace('\\', '/');
                        if (rel.Length == 0) continue;
                        files.Add(new KeyValuePair<string, string>(entryPrefix + "/" + rel, f));
                    }
                    catch { }
                }
            }
            catch (Exception ex) { AppLog.Write("Backup collect " + entryPrefix, ex); }
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

        // Returns null when zipPath looks like a backup made by RunBackup
        // (it must carry settings.ini and/or records.xml), otherwise a
        // user-readable reason why it was rejected.
        public static string ValidateBackupZip(string zipPath)
        {
            try
            {
                var entries = ZipReader.List(zipPath);
                bool hasSettings = false, hasRecords = false;
                foreach (var e in entries)
                {
                    string n = e.Name.Replace('\\', '/');
                    if (n.Equals("settings.ini", StringComparison.OrdinalIgnoreCase)) hasSettings = true;
                    if (n.Equals("records.xml", StringComparison.OrdinalIgnoreCase)) hasRecords = true;
                }
                if (!hasSettings && !hasRecords)
                    return Loc.S("This archive has no settings.ini / records.xml - it is not a Tilettes backup.",
                                 "В архиве нет settings.ini / records.xml — это не бэкап Tilettes.");
                return null;
            }
            catch (Exception ex)
            {
                AppLog.Write("Backup validate " + zipPath, ex);
                return Loc.S("Cannot read the archive: ", "Не удалось прочитать архив: ") + ex.Message;
            }
        }

        // Restores a backup zip created by RunBackup into the working directory
        // (the exe folder): settings, records, bookmarks, file types, search
        // history and the whole ico folder. The exe is never replaced —
        // a running instance cannot overwrite itself anyway, and an old exe
        // inside the archive must not clobber the installed one.
        // Returns null on success, otherwise a user-readable error.
        public static string RestoreZip(string zipPath, out int restoredCount)
        {
            restoredCount = 0;
            try
            {
                string reject = ValidateBackupZip(zipPath);
                if (reject != null) return reject;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var entries = ZipReader.List(zipPath);
                foreach (var e in entries)
                {
                    try
                    {
                        string name = e.Name.Replace('\\', '/');
                        if (name.EndsWith("/") || name.Length == 0) continue;              // folder entry
                        // Old archives carry WinPanel.exe, current ones Tilettes.exe: never replace the exe.
                        if (name.Equals("Tilettes.exe", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("WinPanel.exe", StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.IndexOf("..") >= 0 || Path.IsPathRooted(name)) continue;  // stay inside the working dir
                        string dest = Path.Combine(baseDir, name.Replace('/', Path.DirectorySeparatorChar));
                        ZipReader.Extract(zipPath, e, dest);
                        restoredCount++;
                    }
                    catch (Exception ex) { AppLog.Write("Restore entry " + e.Name, ex); }
                }
                if (restoredCount == 0)
                    return Loc.S("Nothing was restored - see log.txt", "Ничего не восстановлено — подробности в log.txt");
                AppLog.Write("Backup restored: " + zipPath + " (" + restoredCount + " files, exe skipped)");
                return null;
            }
            catch (Exception ex)
            {
                AppLog.Write("Backup restore " + zipPath, ex);
                return Loc.S("Restore failed: ", "Восстановление не удалось: ") + ex.Message;
            }
        }
    }
}
