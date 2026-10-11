using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WinPanel
{
    // A plain system MessageBox with a bigger text font: the standard dialog
    // stays (the simplest, most compatible Windows help surface), a temporary
    // thread-local WH_CBT hook re-fonts its text and buttons on activation and
    // then re-fits the layout. The dialog measures itself with the standard
    // 9 pt font BEFORE HCBT_ACTIVATE fires, so an 18 pt WM_SETFONT alone only
    // clips the text - the text static, the buttons and the dialog itself are
    // resized here to the measured extents. The hook lives only for the
    // duration of the call and works because MessageBox.Show is modal on the
    // calling thread.
    internal static class MessageBoxFont
    {
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        private const int WH_CBT = 5;
        private const int HCBT_ACTIVATE = 5;
        private const uint WM_SETFONT = 0x0030;
        private const int DT_CALCRECT = 0x400, DT_WORDBREAK = 0x10, DT_NOPREFIX = 0x800, DT_SINGLELINE = 0x20;
        // winuser.h values - these two are easy to swap, and swapping them
        // turns every "grow in place" into "teleport to the client origin"
        // and every "shift" into "collapse to 0x0" (that was the broken help)
        private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;
        private const uint RDW_INVALIDATE = 0x1, RDW_ERASE = 0x4, RDW_ALLCHILDREN = 0x80, RDW_UPDATENOW = 0x100;

        private static IntPtr hookHandle;
        private static IntPtr fontHandle;
        private static HookProc hookProcRef; // keeps the delegate alive for the hook lifetime

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int L, T, R, B; }

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
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc proc, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")]
        private static extern int MapWindowPoints(IntPtr from, IntPtr to, ref Point pt, int count);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr dc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int DrawText(IntPtr dc, string text, int len, ref RECT rect, uint flags);
        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr region, uint flags);
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
                    // (text, icon pane) and Button the bigger font, remember
                    // which static carries the text (the icon pane is empty)
                    // and where the buttons sit relative to the dialog.
                    IntPtr textStatic = IntPtr.Zero;
                    var buttons = new List<IntPtr>();
                    var buttonRel = new List<Point>();
                    var buttonW = new List<int>();
                    var buttonRightMargin = new List<int>();
                    EnumChildWindows(wParam, delegate(IntPtr child, IntPtr l)
                    {
                        var sb = new StringBuilder(64);
                        if (GetClassName(child, sb, 64) > 0)
                        {
                            string cls = sb.ToString();
                            if (cls == "Static" || cls == "Button")
                            {
                                SendMessage(child, WM_SETFONT, fontHandle, (IntPtr)1);
                                if (cls == "Static" && textStatic == IntPtr.Zero && GetWindowTextLength(child) > 0)
                                    textStatic = child;
                            }
                            if (cls == "Button")
                            {
                                RECT br, dr;
                                if (GetWindowRect(child, out br) && GetWindowRect(wParam, out dr))
                                {
                                    buttons.Add(child);
                                    // exact CLIENT-relative offset of the
                                    // button's top-left (its own client (0,0)
                                    // mapped into the dialog's client space);
                                    // SetWindowPos on a child wants client
                                    // coords - a screen delta would double-
                                    // count caption+frame
                                    var rel = new Point(0, 0);
                                    MapWindowPoints(child, wParam, ref rel, 1);
                                    buttonRel.Add(rel);
                                    buttonW.Add(br.R - br.L);
                                    buttonRightMargin.Add(dr.R - br.R);
                                }
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                    if (textStatic != IntPtr.Zero)
                        RefitDialog(wParam, textStatic, buttons, buttonRel, buttonW, buttonRightMargin);
                }
            }
            catch { }
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        // The dialog sized itself for the standard font before activation.
        // Measure the text with the big font and grow the text static and the
        // dialog itself by the same delta, re-centering the dialog over its
        // old position. The width is capped by the work area: lines authored
        // with explicit breaks stay whole while they fit, only a too-long
        // line wraps.
        //
        // The buttons need special care: the dialog's own layout code garbles
        // them when the dialog is resized while hidden (it re-applies each
        // button's cached SCREEN position as client-relative - observed: the
        // OK button lands far below the dialog). So the buttons are never
        // moved directly before the resize; their captured offsets are
        // re-applied afterwards, with +dH/2 so the bottom margin survives the
        // symmetric growth.
        private static void RefitDialog(IntPtr dialog, IntPtr textStatic, List<IntPtr> buttons,
            List<Point> buttonRel, List<int> buttonW, List<int> buttonRightMargin)
        {
            RECT dlg, st;
            if (!GetWindowRect(dialog, out dlg) || !GetWindowRect(textStatic, out st)) return;
            int len = GetWindowTextLength(textStatic);
            if (len <= 0) return;
            var text = new StringBuilder(len + 2);
            GetWindowText(textStatic, text, text.Capacity);

            int maxW = (int)(SystemInformation.WorkingArea.Width * 0.6);
            int maxH = (int)(SystemInformation.WorkingArea.Height * 0.85);
            IntPtr dc = GetDC(textStatic);
            if (dc == IntPtr.Zero) return;
            int textW, textH;
            try
            {
                IntPtr old = SelectObject(dc, fontHandle);
                try
                {
                    // explicit lines measured one by one: the authored line
                    // breaks survive as long as every line fits under maxW
                    textW = 0; textH = 0;
                    string[] lines = text.ToString().Replace("\r\n", "\n").Split('\n');
                    foreach (string line in lines)
                    {
                        RECT lr = new RECT();
                        DrawText(dc, line.Length == 0 ? " " : line, line.Length == 0 ? 1 : line.Length,
                            ref lr, DT_CALCRECT | DT_NOPREFIX | DT_SINGLELINE);
                        if (lr.R > textW) textW = lr.R;
                        textH += lr.B;
                    }
                    if (textW > maxW)
                    {
                        // some line is too long: fall back to whole-text
                        // word-wrapping inside the work-area cap
                        RECT wr = new RECT { L = 0, T = 0, R = maxW, B = 0 };
                        DrawText(dc, text.ToString(), text.Length, ref wr, DT_CALCRECT | DT_WORDBREAK | DT_NOPREFIX);
                        textW = maxW;
                        textH = wr.B;
                    }
                }
                finally { SelectObject(dc, old); }
            }
            finally { ReleaseDC(textStatic, dc); }

            // the static keeps a few px of its own padding around the text
            int newW = Math.Min(textW + 16, maxW);
            int newH = Math.Min(textH + 16, maxH);
            int dW = Math.Max(0, newW - (st.R - st.L));
            int dH = Math.Max(0, newH - (st.B - st.T));
            if (dW == 0 && dH == 0) return;

            // grow the text static in place (it rides with the dialog move)
            SetWindowPos(textStatic, IntPtr.Zero, 0, 0,
                (st.R - st.L) + dW, (st.B - st.T) + dH, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
            // grow the dialog and re-center it over its old position; the
            // statics and the icon move with it, so all their margins are
            // preserved; the dialog's own layout garbles the buttons here
            SetWindowPos(dialog, IntPtr.Zero, dlg.L - dW / 2, dlg.T - dH / 2,
                (dlg.R - dlg.L) + dW, (dlg.B - dlg.T) + dH, SWP_NOZORDER | SWP_NOACTIVATE);
            // put the buttons back where they belong relative to the dialog.
            // Modern MessageBoxes right-align them: keep the RIGHT margin by
            // shifting by the full dW (a centered button - left margin not
            // smaller than the right one - is re-centered instead). The
            // bottom margin survives the symmetric growth with a full dH.
            int newDlgW = (dlg.R - dlg.L) + dW;
            for (int i = 0; i < buttons.Count; i++)
            {
                int newX = buttonRightMargin[i] <= buttonRel[i].X
                    ? buttonRel[i].X + dW
                    : (newDlgW - buttonW[i]) / 2;
                SetWindowPos(buttons[i], IntPtr.Zero, newX, buttonRel[i].Y + dH, 0, 0,
                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            }
            // the dialog's own layout pass leaves the old button slot painted:
            // force a full erase+redraw so no ghost of the pre-resize layout
            // remains on the grown dialog
            RedrawWindow(dialog, IntPtr.Zero, IntPtr.Zero,
                RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_UPDATENOW);
        }
    }
}
