using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WinPanel
{
    // A plain system MessageBox with a bigger text font: the standard dialog
    // stays (the simplest, most compatible Windows help surface), a temporary
    // WH_CBT hook just re-fonts its text and buttons while the dialog window
    // activates. The hook lives only for the duration of the call and works
    // because MessageBox.Show is modal on the calling thread.
    internal static class MessageBoxFont
    {
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        private const int WH_CBT = 5;
        private const int HCBT_ACTIVATE = 5;
        private const uint WM_SETFONT = 0x0030;

        private static IntPtr hookHandle;
        private static IntPtr fontHandle;
        private static HookProc hookProcRef; // keeps the delegate alive for the hook lifetime

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int maxCount);
        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc proc, IntPtr lParam);
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);

        public static void Show(IWin32Window owner, string text, string caption, float fontSize)
        {
            Font font = null;
            try
            {
                font = new Font("Segoe UI", fontSize);
                fontHandle = font.ToHfont();
                hookProcRef = new HookProc(CbtHookProc);
                // Thread-local hook (NULL module + this thread's id): dwThreadId
                // 0 means ALL desktop threads, i.e. a global hook - those need a
                // real DLL module, so with the exe's module handle the call kept
                // failing with error 1428 and the box silently stayed in the
                // standard 9 pt font. Checked and logged, never silent again.
                hookHandle = SetWindowsHookEx(WH_CBT, hookProcRef, IntPtr.Zero, GetCurrentThreadId());
                if (hookHandle == IntPtr.Zero)
                    AppLog.Write(string.Format("Help font hook failed: error {0}",
                        Marshal.GetLastWin32Error()));
                MessageBox.Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                if (hookHandle != IntPtr.Zero) { try { UnhookWindowsHookEx(hookHandle); } catch { } hookHandle = IntPtr.Zero; }
                if (fontHandle != IntPtr.Zero) { try { DeleteObject(fontHandle); } catch { } fontHandle = IntPtr.Zero; }
                if (font != null) font.Dispose();
            }
        }

        private static IntPtr CbtHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode == HCBT_ACTIVATE && fontHandle != IntPtr.Zero)
                {
                    // wParam = the dialog being activated: give every Static
                    // (text, icon pane) and Button the bigger font.
                    EnumChildWindows(wParam, delegate(IntPtr child, IntPtr l)
                    {
                        var sb = new StringBuilder(64);
                        if (GetClassName(child, sb, 64) > 0)
                        {
                            string cls = sb.ToString();
                            if (cls == "Static" || cls == "Button")
                                SendMessage(child, WM_SETFONT, fontHandle, (IntPtr)1);
                        }
                        return true;
                    }, IntPtr.Zero);
                }
            }
            catch { }
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }
    }
}
