using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace WinPanel
{
    // Windows autostart entry management (HKCU ...\Run).
    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Tilettes";
        // Registry value written by the builds before the rename.
        private const string LegacyValueName = "WinPanel";

        public static void Apply(bool enabled, bool minimized)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    // The old name would otherwise linger and autostart a second copy.
                    if (Array.IndexOf(key.GetValueNames(), LegacyValueName) >= 0)
                        key.DeleteValue(LegacyValueName, false);
                    if (enabled)
                    {
                        string exe = System.Windows.Forms.Application.ExecutablePath;
                        string val = "\"" + exe + "\"" + (minimized ? " --minimized" : "");
                        key.SetValue(ValueName, val);
                    }
                    else
                    {
                        if (Array.IndexOf(key.GetValueNames(), ValueName) >= 0)
                            key.DeleteValue(ValueName, false);
                    }
                }
            }
            catch { }
        }
    }

    // One running copy per machine: a second launch broadcasts a message to the
    // first copy and exits, so launching from a shortcut never duplicates windows.
    public static class SingleInstance
    {
        public static readonly int ShowMessage = (int)RegisterWindowMessage("TilettesShow_9f2a41");

        private static Mutex mutex;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static bool Start()
        {
            try
            {
                bool created;
                mutex = new Mutex(true, "Tilettes_SingleInstance_9f2a41", out created);
                return created;
            }
            catch
            {
                return true;
            }
        }

        public static void NotifyExisting()
        {
            try { PostMessage((IntPtr)0xFFFF, ShowMessage, (IntPtr)0x4242, IntPtr.Zero); }
            catch { }
        }
    }
}
