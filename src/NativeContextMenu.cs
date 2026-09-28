using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WinPanel
{
    public static class NativeContextMenu
    {
        [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellFolder
        {
            void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
            void EnumObjects(IntPtr hwnd, int grfFlags, out IntPtr ppenumIDList);
            void BindToObject(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, out IntPtr ppv);
            void BindToStorage(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, out IntPtr ppv);
            void CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
            void CreateViewObject(IntPtr hwndOwner, [In] ref Guid riid, out IntPtr ppv);
            void GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] apidl, ref uint rgfInOut);
            void GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] apidl, [In] ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);
            void GetDisplayNameOf(IntPtr pidl, uint uFlags, out IntPtr pName);
            void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
        }

        [ComImport, Guid("000214E4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IContextMenu
        {
            [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
            [PreserveSig] int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax);
        }

        [ComImport, Guid("000214F4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IContextMenu2 : IContextMenu
        {
            [PreserveSig] new int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig] new int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
            [PreserveSig] new int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax);
            [PreserveSig] int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        }

        [ComImport, Guid("BCFCE0A0-6E7C-101B-BC65-08002B2CE9D6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IContextMenu3 : IContextMenu2
        {
            [PreserveSig] new int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig] new int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
            [PreserveSig] new int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax);
            [PreserveSig] new int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
            [PreserveSig] int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, ref IntPtr plResult);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CMINVOKECOMMANDINFO
        {
            public int cbSize;
            public int fMask;
            public IntPtr hwnd;
            public IntPtr lpVerb;
            public IntPtr lpParameters;
            public IntPtr lpDirectory;
            public int nShow;
            public int dwHotKey;
            public IntPtr hIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

        [DllImport("shell32.dll")]
        public static extern int SHBindToParent(IntPtr pidl, [In] ref Guid riid, out IShellFolder ppv, out IntPtr ppidlLast);

        [DllImport("ole32.dll")]
        public static extern void CoTaskMemFree(IntPtr pv);

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        public static extern uint TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool InsertMenuItem(IntPtr hMenu, uint uItem, bool fByPosition, ref MENUITEMINFO lpmii);

        [DllImport("user32.dll")]
        public static extern int GetMenuItemCount(IntPtr hMenu);

        [DllImport("user32.dll")]
        public static extern uint GetSysColor(int nIndex);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MENUITEMINFO
        {
            public uint cbSize;
            public uint fMask;
            public uint fType;
            public uint fState;
            public uint wID;
            public IntPtr hSubMenu;
            public IntPtr hbmpChecked;
            public IntPtr hbmpUnchecked;
            public IntPtr dwItemData;
            public string dwTypeData;
            public uint cch;
            public IntPtr hbmpItem;
        }

        public const uint MIIM_STRING = 0x00000040;
        public const uint MIIM_ID = 0x00000002;
        public const uint MIIM_FTYPE = 0x00000100;
        public const uint MIIM_SUBMENU = 0x00000004;
        public const uint MIIM_BITMAP = 0x00000080;
        public const uint MFT_SEPARATOR = 0x00000800;
        public const uint MFT_STRING = 0x00000000;

        public const uint TPM_RETURNCMD = 0x0100;
        public const uint CMF_NORMAL = 0x00000000;
        public const uint CMF_EXPLORE = 0x00000004;

        private class ContextMenuHook : NativeWindow
        {
            private IContextMenu2 cm2;
            private IContextMenu3 cm3;

            public ContextMenuHook(IntPtr hwnd, IContextMenu cm)
            {
                this.AssignHandle(hwnd);
                cm2 = cm as IContextMenu2;
                cm3 = cm as IContextMenu3;
            }

            public void Detach()
            {
                this.ReleaseHandle();
            }

            protected override void WndProc(ref Message m)
            {
                const int WM_INITMENUPOPUP = 0x0117;
                const int WM_DRAWITEM = 0x002B;
                const int WM_MEASUREITEM = 0x002C;
                const int WM_MENUCHAR = 0x0120;

                if (m.Msg == WM_INITMENUPOPUP || m.Msg == WM_DRAWITEM || m.Msg == WM_MEASUREITEM || m.Msg == WM_MENUCHAR)
                {
                    if (cm3 != null)
                    {
                        IntPtr lResult = IntPtr.Zero;
                        if (cm3.HandleMenuMsg2((uint)m.Msg, m.WParam, m.LParam, ref lResult) == 0) // S_OK
                        {
                            m.Result = lResult;
                            return;
                        }
                    }
                    else if (cm2 != null)
                    {
                        if (cm2.HandleMenuMsg((uint)m.Msg, m.WParam, m.LParam) == 0) // S_OK
                        {
                            if (m.Msg != WM_INITMENUPOPUP) m.Result = (IntPtr)1;
                            return;
                        }
                    }
                }
                
                base.WndProc(ref m);
            }
        }

        public static void ShowContextMenu(string path, int x, int y, IntPtr handle, bool editMode,
            bool canMoveOutOfFolder, Action onMoveOutOfFolder,
            Action onOpenContainingFolder,
            Action onSize1, Action onSize2, Action onSize3, Action onSize4,
            Action onRemove, Action onRename, Action onChangeIcon)
        {
            uint dummy;
            IntPtr pidl;
            if (SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out dummy) != 0) return;

            Guid riid = typeof(IShellFolder).GUID;
            IShellFolder parentFolder;
            IntPtr pidlChild;
            if (SHBindToParent(pidl, ref riid, out parentFolder, out pidlChild) == 0)
            {
                Guid iidContextMenu = typeof(IContextMenu).GUID;
                IntPtr pCtxMenu;
                parentFolder.GetUIObjectOf(handle, 1, new IntPtr[] { pidlChild }, ref iidContextMenu, IntPtr.Zero, out pCtxMenu);
                if (pCtxMenu != IntPtr.Zero)
                {
                    IContextMenu contextMenu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(pCtxMenu, typeof(IContextMenu));
                    IntPtr hMenu = CreatePopupMenu();

                    contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_NORMAL | CMF_EXPLORE);

                    uint customIdStart = 0x8000;
                    IntPtr marker = CreateMarkerBitmap();

                    // Our items go on top so they are shown before the Explorer items.
                    uint pos = 0;
                    InsertCustomItem(hMenu, pos++, customIdStart + 7, "Open containing folder", marker);
                    if (editMode && canMoveOutOfFolder)
                        InsertCustomItem(hMenu, pos++, customIdStart + 8, "Move out of folder", marker);
                    InsertSeparator(hMenu, pos++);

                    if (editMode)
                    {
                        // Edit actions stay at the bottom, below the Explorer items.
                        InsertSeparator(hMenu, (uint)GetMenuItemCount(hMenu));

                        IntPtr sizeMenu = CreatePopupMenu();
                        InsertCustomItem(sizeMenu, 0, customIdStart, "1 x 1", marker);
                        InsertCustomItem(sizeMenu, 1, customIdStart + 1, "2 x 2", marker);
                        InsertCustomItem(sizeMenu, 2, customIdStart + 2, "3 x 3", marker);
                        InsertCustomItem(sizeMenu, 3, customIdStart + 3, "4 x 4", marker);

                        MENUITEMINFO sizeItem = new MENUITEMINFO();
                        sizeItem.cbSize = (uint)Marshal.SizeOf(typeof(MENUITEMINFO));
                        sizeItem.fMask = MIIM_ID | MIIM_STRING | MIIM_FTYPE | MIIM_SUBMENU | MIIM_BITMAP;
                        sizeItem.fType = MFT_STRING;
                        sizeItem.wID = customIdStart + 10;
                        sizeItem.dwTypeData = "Size";
                        sizeItem.hSubMenu = sizeMenu;
                        sizeItem.hbmpItem = marker;
                        InsertMenuItem(hMenu, (uint)GetMenuItemCount(hMenu), true, ref sizeItem);

                        InsertCustomItem(hMenu, (uint)GetMenuItemCount(hMenu), customIdStart + 4, "Rename", marker);
                        InsertCustomItem(hMenu, (uint)GetMenuItemCount(hMenu), customIdStart + 5, "Change Icon", marker);
                        InsertCustomItem(hMenu, (uint)GetMenuItemCount(hMenu), customIdStart + 6, "Remove from Panel", marker);
                    }

                    ContextMenuHook hook = new ContextMenuHook(handle, contextMenu);
                    uint cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD, x, y, handle, IntPtr.Zero);
                    hook.Detach();

                    if (marker != IntPtr.Zero) DeleteObject(marker);

                    if (cmd >= 1 && cmd < customIdStart)
                    {
                        CMINVOKECOMMANDINFO ici = new CMINVOKECOMMANDINFO();
                        ici.cbSize = Marshal.SizeOf(ici);
                        ici.hwnd = handle;
                        ici.lpVerb = (IntPtr)(cmd - 1);
                        ici.nShow = 1; // SW_SHOWNORMAL
                        contextMenu.InvokeCommand(ref ici);
                    }
                    else if (cmd == customIdStart + 7) { if (onOpenContainingFolder != null) onOpenContainingFolder(); }
                    else if (cmd == customIdStart + 8) { if (onMoveOutOfFolder != null) onMoveOutOfFolder(); }
                    else if (cmd == customIdStart) { if (onSize1 != null) onSize1(); }
                    else if (cmd == customIdStart + 1) { if (onSize2 != null) onSize2(); }
                    else if (cmd == customIdStart + 2) { if (onSize3 != null) onSize3(); }
                    else if (cmd == customIdStart + 3) { if (onSize4 != null) onSize4(); }
                    else if (cmd == customIdStart + 4) { if (onRename != null) onRename(); }
                    else if (cmd == customIdStart + 5) { if (onChangeIcon != null) onChangeIcon(); }
                    else if (cmd == customIdStart + 6) { if (onRemove != null) onRemove(); }

                    DestroyMenu(hMenu);
                    Marshal.ReleaseComObject(contextMenu);
                }
                Marshal.ReleaseComObject(parentFolder);
            }
            CoTaskMemFree(pidl);
        }

        // Inserts one of our own items (with the round marker icon) at the given position.
        private static void InsertCustomItem(IntPtr hMenu, uint position, uint id, string text, IntPtr hbmp)
        {
            MENUITEMINFO mii = new MENUITEMINFO();
            mii.cbSize = (uint)Marshal.SizeOf(typeof(MENUITEMINFO));
            mii.fMask = MIIM_ID | MIIM_STRING | MIIM_FTYPE | MIIM_BITMAP;
            mii.fType = MFT_STRING;
            mii.wID = id;
            mii.dwTypeData = text;
            mii.hbmpItem = hbmp;
            InsertMenuItem(hMenu, position, true, ref mii);
        }

        private static void InsertSeparator(IntPtr hMenu, uint position)
        {
            MENUITEMINFO mii = new MENUITEMINFO();
            mii.cbSize = (uint)Marshal.SizeOf(typeof(MENUITEMINFO));
            mii.fMask = MIIM_FTYPE;
            mii.fType = MFT_SEPARATOR;
            InsertMenuItem(hMenu, position, true, ref mii);
        }

        // A small round blue dot drawn on the system menu background — used as the icon
        // of our own menu items so they are visually different from Explorer items.
        private static IntPtr CreateMarkerBitmap()
        {
            try
            {
                const int size = 16;
                Color menuBg;
                try { menuBg = ColorTranslator.FromWin32((int)GetSysColor(4)); } // COLOR_MENU
                catch { menuBg = SystemColors.Menu; }
                using (var bmp = new Bitmap(size, size))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(menuBg);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                            g.FillEllipse(brush, 3f, 3f, 10f, 10f);
                    }
                    return bmp.GetHbitmap();
                }
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }
}
