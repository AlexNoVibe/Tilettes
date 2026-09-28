using System;
using System.Drawing;
using System.Runtime.InteropServices;
using WinPanel;

class Test2 {
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

    static void Main() {
        string path = @"C:\Windows\System32\cmd.exe";
        SHFILEINFO shinfo = new SHFILEINFO();
        uint flags = SHGFI_ICON | SHGFI_LARGEICON;
        IntPtr res = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
        if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
        {
            Icon icon = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
            DestroyIcon(shinfo.hIcon);
            
            Console.WriteLine("Icon Width: " + icon.Width + ", Height: " + icon.Height);
            
            Bitmap bmp = new Bitmap(icon.Width, icon.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.Clear(Color.Transparent);
                g.DrawIcon(icon, new Rectangle(0, 0, icon.Width, icon.Height));
            }
            
            bool hasAlpha = false;
            bool allTransparent = true;
            for (int y = 0; y < bmp.Height; y++) {
                for (int x = 0; x < bmp.Width; x++) {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A > 0 && c.A < 255) hasAlpha = true;
                    if (c.A != 0) allTransparent = false;
                }
            }
            Console.WriteLine(string.Format("hasAlpha: {0}, allTransparent: {1}", hasAlpha, allTransparent));
            icon.Dispose();
        }
        else {
            Console.WriteLine("SHGetFileInfo failed");
        }
    }
}
