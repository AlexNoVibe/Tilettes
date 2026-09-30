using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WinPanel
{
    // Tiny append-only log next to the exe. Every subsystem writes failures here
    // instead of staying silent, so problems can be diagnosed after the fact.
    // All operations are best-effort: logging must never crash the app.
    public static class AppLog
    {
        private static readonly object Gate = new object();
        private static string path;
        private static bool tried;

        public static string FilePath
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    try { path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt"); }
                    catch { path = null; }
                }
                return path;
            }
        }

        public static void Write(string message)
        {
            try
            {
                string p = FilePath;
                if (string.IsNullOrEmpty(p)) return;
                lock (Gate)
                {
                    // Keep the log bounded: start a fresh file past ~512 KB.
                    try
                    {
                        var fi = new FileInfo(p);
                        if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
                    }
                    catch { }
                    File.AppendAllText(p, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + (message ?? "") + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }

        public static void Write(string tag, Exception ex)
        {
            Write(tag + ": " + (ex == null ? "?" : ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace));
        }

        // Installs the global safety nets. Any exception that escapes a UI or
        // thread-pool handler is logged and shown once; the app keeps running.
        public static void InstallGlobalHandlers()
        {
            try
            {
                Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
                {
                    Write("UI exception", e.Exception);
                    try { MessageBox.Show(Loc.S("An error occurred but Tilettes keeps running.", "Произошла ошибка, но Плиточки продолжают работать.") + "\n\n" + e.Exception.Message, Loc.S("Tilettes", "Плиточки"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                    catch { }
                };
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            }
            catch { }
            try
            {
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    var ex = e.ExceptionObject as Exception;
                    Write("Unhandled exception", ex);
                };
            }
            catch { }
        }
    }
}
