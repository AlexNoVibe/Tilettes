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

        public static Image GetIcon(string path, bool large)
        {
            // SHGFI_SYSICONINDEX alone returns iIcon = 0 on some systems, which makes every
            // item render the generic blank-page icon. Requesting SHGFI_ICON as well makes
            // the shell resolve the real system image list index into iIcon.
            SHFILEINFO shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SYSICONINDEX | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);
            IntPtr res = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (res != IntPtr.Zero)
            {
                if (shinfo.hIcon != IntPtr.Zero) DestroyIcon(shinfo.hIcon);
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
                    var bmp = icon.ToBitmap();
                    icon.Dispose();
                    return TrimTransparent(bmp);
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
            var bmp = icon.ToBitmap();
            icon.Dispose();
            return TrimTransparent(bmp);
        }
        return null;
    }

        // Shell icons often carry large fully transparent margins (some apps have no 256px
        // frame, leaving a tiny glyph in the corner of the jumbo canvas). Crop to the visible
        // glyph so tiles can scale it up instead of showing a small icon with dead space.
        public static Bitmap TrimTransparent(Bitmap src)
        {
            int w = src.Width, h = src.Height;
            int minX = w, minY = h, maxX = -1, maxY = -1;
            var bd = src.LockBits(new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int stride = bd.Stride;
                var bytes = new byte[stride * h];
                System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, bytes, 0, bytes.Length);
                for (int y = 0; y < h; y++)
                {
                    int rowStart = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        if (bytes[rowStart + x * 4 + 3] > 16)
                        {
                            if (x < minX) minX = x;
                            if (y < minY) minY = y;
                            if (x > maxX) maxX = x;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }
            finally
            {
                src.UnlockBits(bd);
            }

            if (maxX < 0) return src; // fully transparent, keep as is
            var outBmp = src.Clone(new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1), src.PixelFormat);
            src.Dispose();
            return outBmp;
        }

        // Draws the image centered inside dest, scaled to fit while preserving aspect ratio.
        public static void DrawFit(Graphics g, Image img, Rectangle dest)
        {
            if (img.Width <= 0 || img.Height <= 0)
            {
                g.DrawImage(img, dest);
                return;
            }
            double scale = Math.Min(dest.Width / (double)img.Width, dest.Height / (double)img.Height);
            int dw = Math.Max(1, (int)Math.Round(img.Width * scale));
            int dh = Math.Max(1, (int)Math.Round(img.Height * scale));
            int x = dest.X + (dest.Width - dw) / 2;
            int y = dest.Y + (dest.Height - dh) / 2;
            g.DrawImage(img, new Rectangle(x, y, dw, dh));
        }
    }
}
