using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace WinPanel
{
    public static class IconExtractor
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_SYSICONINDEX = 0x000040000;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", EntryPoint = "#727")]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        [ComImportAttribute()]
        [GuidAttribute("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IImageList
        {
            [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
            [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
            [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
            [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
            [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
            [PreserveSig] int Draw(ref IMAGELISTDRAWPARAMS pimldp);
            [PreserveSig] int Remove(int i);
            [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGELISTDRAWPARAMS
        {
            public int cbSize;
            public IntPtr himl;
            public int i;
            public IntPtr hdcDst;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int xBitmap;
            public int yBitmap;
            public int rgbBk;
            public int rgbFg;
            public int fStyle;
            public int dwRop;
            public int fState;
            public int Frame;
            public int crEffect;
        }

        private const int SHIL_JUMBO = 4;
        private const int SHIL_EXTRALARGE = 2;
        private const int ILD_TRANSPARENT = 1;

        public static Bitmap ToBitmapRobust(Icon icon)
        {
            if (icon == null) return null;
            Bitmap bmp = new Bitmap(icon.Width, icon.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.Clear(Color.Transparent);
                g.DrawIcon(icon, new Rectangle(0, 0, icon.Width, icon.Height));
            }
            return bmp;
        }

        public static Image GetIcon(string path, bool large)
        {
            SHFILEINFO shinfo = new SHFILEINFO();
            uint flags = SHGFI_SYSICONINDEX;
            IntPtr res = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (res != IntPtr.Zero)
            {
                Guid iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                IImageList iml;
                int hres = SHGetImageList(large ? SHIL_JUMBO : SHIL_EXTRALARGE, ref iidImageList, out iml);
                if (hres == 0 && iml != null)
                {
                    IntPtr hIcon = IntPtr.Zero;
                    iml.GetIcon(shinfo.iIcon, ILD_TRANSPARENT, ref hIcon);
                    if (hIcon != IntPtr.Zero)
                    {
                        Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
                        DestroyIcon(hIcon);
                        var bmp = ToBitmapRobust(icon);
                        icon.Dispose();
                        return bmp;
                    }
                }
            }
            
            // Fallback
            flags = SHGFI_ICON | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);
            res = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                Icon icon = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
                DestroyIcon(shinfo.hIcon);
                var bmp = ToBitmapRobust(icon);
                icon.Dispose();
                return bmp;
            }
            
            try 
            { 
                using (Icon fbIcon = System.Drawing.Icon.ExtractAssociatedIcon(path)) 
                {
                    return ToBitmapRobust(fbIcon);
                }
            } 
            catch { return null; }
        }
    }
}
