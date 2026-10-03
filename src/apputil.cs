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

    // One running copy per machine: a second launch notifies the first copy and
    // exits, so launching from a shortcut never duplicates windows. BOTH name
    // generations are locked (pre-rename "WinPanel" and current "Tilettes") so an
    // old and a new exe can never run side by side and fight over the data files.
    public static class SingleInstance
    {
        public static readonly int ShowMessage = (int)RegisterWindowMessage("TilettesShow_9f2a41");
        public static readonly int LegacyShowMessage = (int)RegisterWindowMessage("WinPanelShow_9f2a41");

        // Two name generations of the product (WinPanel -> Tilettes).
        private static readonly string[] MutexNames = new string[]
        {
            "WinPanel_SingleInstance_9f2a41",
            "Tilettes_SingleInstance_9f2a41"
        };

        private static readonly System.Collections.Generic.List<Mutex> heldMutexes = new System.Collections.Generic.List<Mutex>();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static bool Start()
        {
            // Test hook: a second copy for screenshot automation must not be
            // rejected by the single-instance guard.
            try
            {
                if (string.Equals(Environment.GetEnvironmentVariable("WINPANEL_ALLOW_MULTI"), "1", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }
            try
            {
                foreach (var name in MutexNames)
                {
                    bool created;
                    var m = new Mutex(true, name, out created);
                    if (!created)
                    {
                        m.Dispose();
                        ReleaseHeld();
                        NotifyExisting();
                        return false;
                    }
                    heldMutexes.Add(m);
                }
                return true;
            }
            catch
            {
                return true; // fail open: a broken mutex must not block the app
            }
        }

        private static void ReleaseHeld()
        {
            foreach (var m in heldMutexes)
            {
                try { m.ReleaseMutex(); } catch { }
                try { m.Dispose(); } catch { }
            }
            heldMutexes.Clear();
        }

        public static void NotifyExisting()
        {
            try { PostMessage((IntPtr)0xFFFF, ShowMessage, (IntPtr)0x4242, IntPtr.Zero); } catch { }
            try { PostMessage((IntPtr)0xFFFF, LegacyShowMessage, (IntPtr)0x4242, IntPtr.Zero); } catch { }
        }
    }

    // Central app identity: bump AppVersion on every release tag (v0.5 = "0.5").
    public static class AppInfo
    {
        public const string AppVersion = "0.6.10";
        public const string RepoUrl = "https://github.com/AlexNoVibe/Tilettes";
        public const string IssuesUrl = RepoUrl + "/issues";
        public const string ReleasesUrl = RepoUrl + "/releases";
        public const string ReleasesLatestUrl = ReleasesUrl + "/latest";
        // Public REST endpoint used by UpdateChecker (no auth, needs a User-Agent).
        public const string ReleasesApiUrl = "https://api.github.com/repos/AlexNoVibe/Tilettes/releases/latest";
        public const string DonateUrl = RepoUrl + "#donate";
    }

    // Crypto donation wallets: one shared source for the welcome window, the
    // settings dialog popup and README.md ("Donate" section). EVM-compatible
    // networks share a single address, so they are grouped into one entry.
    public static class DonateWallets
    {
        public class Wallet
        {
            public string Label;    // full name ("Bitcoin (BTC)")
            public string Short;    // compact prefix for narrow rows ("BTC")
            public string Networks; // network list ("" when single-network)
            public string Address;
        }

        public static readonly Wallet[] All = new[]
        {
            new Wallet { Label = "EVM", Short = "EVM", Networks = "ETH · Polygon · Base · Monad · HyperEVM",
                         Address = "0xf84897FA0b74083c16865315A5b148f4d92e6C2a" },
            new Wallet { Label = "Bitcoin (BTC)", Short = "BTC", Networks = "",
                         Address = "bc1qu9cf5uqc5wxqwde8mk378xwdlnjatvmhxhvat5" },
            new Wallet { Label = "Solana (SOL)", Short = "SOL", Networks = "",
                         Address = "7ffCFnJBNVaF268FsZGKBPEWe3UNrWbasgt3aidiCw68" },
            new Wallet { Label = "Sui (SUI)", Short = "SUI", Networks = "",
                         Address = "0x3ca194b355bb00a1f5f646786407ebbcdaee361c6f56fb92f8df9abd73b0c3b1" }
        };

        // Row/menu caption: label, optional network list, mid-truncated address
        // (clicking always copies the full Address).
        public static string Display(Wallet w)
        {
            string addr = w.Address;
            if (addr.Length > 50)
                addr = addr.Substring(0, 8) + "…" + addr.Substring(addr.Length - 6);
            string net = string.IsNullOrEmpty(w.Networks) ? "" : " (" + w.Networks + ")";
            return w.Label + net + " — " + addr;
        }
    }
}
