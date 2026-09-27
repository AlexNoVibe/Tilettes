using System;
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
            [PreserveSig]
            int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig]
            int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
            [PreserveSig]
            int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax);
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

        [DllImport("shell32.dll")]
        public static extern int SHGetDesktopFolder(out IShellFolder ppshf);

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
        public const uint MFT_SEPARATOR = 0x00000800;
        public const uint MFT_STRING = 0x00000000;

        public const uint TPM_RETURNCMD = 0x0100;
        public const uint CMF_NORMAL = 0x00000000;

        public static void ShowContextMenu(string path, int x, int y, IntPtr handle, Action onSmall, Action onMedium, Action onLarge, Action onRemove)
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
                    
                    contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_NORMAL);
                    
                    uint customIdStart = 0x8000;

                    // Add Separator
                    MENUITEMINFO sep = new MENUITEMINFO { cbSize = (uint)Marshal.SizeOf(typeof(MENUITEMINFO)), fMask = MIIM_FTYPE, fType = MFT_SEPARATOR };
                    InsertMenuItem(hMenu, (uint)GetMenuItemCount(hMenu), true, ref sep);

                    // Add custom items
                    AddMenuItem(hMenu, customIdStart, "Size: Small");
                    AddMenuItem(hMenu, customIdStart + 1, "Size: Medium");
                    AddMenuItem(hMenu, customIdStart + 2, "Size: Large");
                    AddMenuItem(hMenu, customIdStart + 3, "Remove from Panel");

                    uint cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD, x, y, handle, IntPtr.Zero);
                    if (cmd >= 1 && cmd < customIdStart)
                    {
                        CMINVOKECOMMANDINFO ici = new CMINVOKECOMMANDINFO();
                        ici.cbSize = Marshal.SizeOf(ici);
                        ici.hwnd = handle;
                        ici.lpVerb = (IntPtr)(cmd - 1);
                        ici.nShow = 1; // SW_SHOWNORMAL
                        contextMenu.InvokeCommand(ref ici);
                    }
                    else if (cmd == customIdStart) { if (onSmall != null) onSmall(); }
                    else if (cmd == customIdStart + 1) { if (onMedium != null) onMedium(); }
                    else if (cmd == customIdStart + 2) { if (onLarge != null) onLarge(); }
                    else if (cmd == customIdStart + 3) { if (onRemove != null) onRemove(); }

                    DestroyMenu(hMenu);
                    Marshal.ReleaseComObject(contextMenu);
                }
                Marshal.ReleaseComObject(parentFolder);
            }
            CoTaskMemFree(pidl);
        }

        [DllImport("user32.dll")]
        public static extern int GetMenuItemCount(IntPtr hMenu);

        private static void AddMenuItem(IntPtr hMenu, uint id, string text)
        {
            MENUITEMINFO mii = new MENUITEMINFO();
            mii.cbSize = (uint)Marshal.SizeOf(typeof(MENUITEMINFO));
            mii.fMask = MIIM_ID | MIIM_STRING | MIIM_FTYPE;
            mii.fType = MFT_STRING;
            mii.wID = id;
            mii.dwTypeData = text;
            InsertMenuItem(hMenu, (uint)GetMenuItemCount(hMenu), true, ref mii);
        }
    }
}
