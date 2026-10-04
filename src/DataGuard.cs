using System;
using System.IO;
using System.Windows.Forms;

namespace WinPanel
{
    // Corrupt internal data files (records.xml and friends) must neither crash
    // the app nor lose data silently: the damaged file is renamed aside so it
    // stays recoverable by hand, a default is built in its place, and the user
    // is told once per run - briefly. The technical details go to log.txt.
    internal static class DataGuard
    {
        private static string pendingNotice;

        // The caller wraps the read of an internal data file with this and, on
        // false, builds its default in memory. Best effort: if even the rename
        // fails (locked disk, permissions), the file stays where it is and the
        // failure is still logged.
        public static bool OnReadFailure(string path, string tag, Exception ex)
        {
            string saved = null;
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    saved = path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    File.Move(path, saved);
                }
            }
            catch (Exception mex) { AppLog.Write(tag + ": the damaged file could not be set aside", mex); }

            AppLog.Write(tag + ": " + (ex == null ? "?" : ex.GetType().Name + ": " + ex.Message)
                + (saved == null ? "" : "\nDamaged file kept as " + saved));

            string name = FileName(path);
            Notice(name + Loc.S(" was damaged and has been recreated. Details in log.txt.",
                " повреждён — создан заново. Подробности в log.txt."));
            return false;
        }

        // Called once the main form is shown: shows a notice parked before any
        // UI existed (data files load in the constructor).
        public static void FlushPending(MainForm mf)
        {
            if (string.IsNullOrEmpty(pendingNotice) || mf == null || mf.IsDisposed) return;
            string text = pendingNotice;
            pendingNotice = null;
            ShowNotice(mf, text);
        }

        private static void Notice(string text)
        {
            try
            {
                foreach (Form f in Application.OpenForms)
                {
                    var mf = f as MainForm;
                    if (mf != null && !mf.IsDisposed && mf.IsHandleCreated)
                    {
                        mf.BeginInvoke((MethodInvoker)delegate { ShowNotice(mf, text); });
                        return;
                    }
                }
            }
            catch { }
            pendingNotice = text; // no UI yet - shown after the form appears
        }

        private static void ShowNotice(MainForm mf, string text)
        {
            // A balloon when the tray icon is around, a small dialog otherwise;
            // one per run either way.
            try
            {
                if (mf.ShowBalloon(text)) return;
            }
            catch { }
            try { ConfirmDialog.ShowInfo(mf, text); }
            catch { }
        }

        private static string FileName(string path)
        {
            try { return Path.GetFileName(path); }
            catch { return path; }
        }
    }
}
