using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace WinPanel
{
    // UWP / Store apps (Alarm, Calculator, Settings, ...) do not live in the file
    // system: they are shell items in the virtual AppsFolder. This wrapper
    // enumerates that folder and pulls per-app icons through IShellItemImageFactory.
    // Everything is fail-soft: on any error the caller gets an empty result.
    public static class ShellItemApi
    {
        private static readonly Guid FOLDERID_AppsFolder = new Guid("1ac8c5a9-1443-4c9d-ab76-5bcc0ff99b6e");
        private static readonly Guid BHID_EnumItems = new Guid("94f60519-2850-4924-aa5a-d15e8486af39");
        private static readonly Guid IID_IShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
        private static readonly Guid IID_IShellItemImageFactory = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");

        private const uint SIGDN_NORMALDISPLAY = 0x80058000;
        private const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;
        private const uint SIIGBF_ICONONLY = 4;

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            [PreserveSig] int GetParent(out IntPtr ppsi);
            [PreserveSig] int GetDisplayName(uint sigdnName, out IntPtr ppszName);
            [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            [PreserveSig] int Compare(IntPtr psi, uint hint, out int piOrder);
        }

        [ComImport, Guid("70629033-E363-4A28-A567-0DB78006E6D7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IEnumShellItems
        {
            [PreserveSig] int Next(uint celt, out IShellItem rgelt, out uint pceltFetched);
            [PreserveSig] int Skip(uint celt);
            [PreserveSig] int Reset();
            [PreserveSig] int Clone(out IEnumShellItems ppenum);
        }

        [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(SIZE size, uint flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref BITMAP lpvObject);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, byte[] bits, ref BITMAPINFO bmi, uint usage);

        [DllImport("gdi32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType; public int bmWidth; public int bmHeight;
            public int bmWidthBytes; public ushort bmPlanes; public ushort bmBitsPixel; public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth; public int biHeight;
            public ushort biPlanes; public ushort biBitCount; public uint biCompression;
            public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter;
            public uint biClrUsed; public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors; // palette unused for 32bpp
        }

        public class UwpApp
        {
            public string Name;       // localized display name ("Калькулятор")
            public string ParsingName; // shell parse name, launchable via explorer/shell
        }

        // True when the path is a shell namespace path (UWP app) rather than a file.
        public static bool IsShellPath(string path)
        {
            return path != null && path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);
        }

        // Lists every app in shell:AppsFolder. May take a moment; call off the UI thread.
        public static List<UwpApp> EnumerateApps()
        {
            var res = new List<UwpApp>();
            try
            {
                // Preferred: resolve FOLDERID_AppsFolder. On locked-down systems this
                // can fail (0x80070002) while the plain "shell:AppsFolder" parse name
                // still binds, so try that as a fallback.
                string folderPath = null;
                IntPtr ptr;
                Guid knownFolder = FOLDERID_AppsFolder;
                if (SHGetKnownFolderPath(ref knownFolder, 0, IntPtr.Zero, out ptr) == 0)
                {
                    folderPath = Marshal.PtrToStringUni(ptr);
                    Marshal.FreeCoTaskMem(ptr);
                }
                if (string.IsNullOrEmpty(folderPath))
                {
                    object probe;
                    Guid iidProbe = IID_IShellItem;
                    SHCreateItemFromParsingName("shell:AppsFolder", IntPtr.Zero, ref iidProbe, out probe);
                    folderPath = "shell:AppsFolder";
                }
                if (string.IsNullOrEmpty(folderPath)) return res;

                object obj;
                Guid iidItem = IID_IShellItem;
                SHCreateItemFromParsingName(folderPath, IntPtr.Zero, ref iidItem, out obj);
                var folder = (IShellItem)obj;

                Guid bhid = BHID_EnumItems;
                Guid iidEnum = new Guid("70629033-E363-4A28-A567-0DB78006E6D7");
                IntPtr enumPtr;
                if (folder.BindToHandler(IntPtr.Zero, ref bhid, ref iidEnum, out enumPtr) != 0) return res;
                var enumItems = (IEnumShellItems)Marshal.GetObjectForIUnknown(enumPtr);
                Marshal.Release(enumPtr);

                for (; ; )
                {
                    IShellItem item;
                    uint fetched;
                    if (enumItems.Next(1, out item, out fetched) != 0 || fetched == 0) break;
                    try
                    {
                        IntPtr namePtr, parsePtr;
                        string name = null, parsing = null;
                        if (item.GetDisplayName(SIGDN_NORMALDISPLAY, out namePtr) == 0)
                        {
                            name = Marshal.PtrToStringUni(namePtr);
                            Marshal.FreeCoTaskMem(namePtr);
                        }
                        if (item.GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING, out parsePtr) == 0)
                        {
                            parsing = Marshal.PtrToStringUni(parsePtr);
                            Marshal.FreeCoTaskMem(parsePtr);
                        }
                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(parsing))
                        {
                            var app = new UwpApp();
                            app.Name = name;
                            // Turn "shell:::{...}\X!App" into a launchable "shell:AppsFolder\X!App".
                            int idx = parsing.LastIndexOf('\\');
                            app.ParsingName = "shell:AppsFolder\\" + (idx >= 0 ? parsing.Substring(idx + 1) : parsing);
                            res.Add(app);
                        }
                    }
                    catch { }
                    finally { Marshal.ReleaseComObject(item); }
                }
                Marshal.ReleaseComObject(enumItems);
            }
            catch (Exception ex) { AppLog.Write("EnumerateApps", ex); }
            return res;
        }

        // Icon of any shell item (works for UWP apps and normal paths alike).
        // Returns null on failure; the caller falls back to a generic icon.
        public static Bitmap GetShellIcon(string shellPath, int size)
        {
            try
            {
                if (string.IsNullOrEmpty(shellPath)) return null;
                object obj;
                Guid iid = IID_IShellItemImageFactory;
                SHCreateItemFromParsingName(shellPath, IntPtr.Zero, ref iid, out obj);
                var factory = (IShellItemImageFactory)obj;
                IntPtr hbm;
                SIZE sz; sz.cx = size; sz.cy = size;
                int hr = factory.GetImage(sz, SIIGBF_ICONONLY, out hbm);
                Marshal.ReleaseComObject(factory);
                if (hr != 0 || hbm == IntPtr.Zero) return null;
                Bitmap bmp = BitmapFromHbitmapWithAlpha(hbm);
                DeleteObject(hbm);
                if (bmp == null) return null;
                Bitmap trimmed = IconExtractor.TrimTransparent(bmp);
                if (!ReferenceEquals(trimmed, bmp)) bmp.Dispose();
                return trimmed;
            }
            catch (Exception ex)
            {
                AppLog.Write("GetShellIcon " + shellPath, ex);
                return null;
            }
        }

        // Copies an HBITMAP into a 32bpp ARGB Bitmap preserving the alpha channel
        // (Bitmap.FromHbitmap would lose it and render black corners).
        private static Bitmap BitmapFromHbitmapWithAlpha(IntPtr hbm)
        {
            try
            {
                var bm = new BITMAP();
                if (GetObject(hbm, System.Runtime.InteropServices.Marshal.SizeOf(typeof(BITMAP)), ref bm) == 0) return null;
                int w = bm.bmWidth, h = Math.Abs(bm.bmHeight);
                if (w <= 0 || h <= 0 || w > 2048 || h > 2048) return null;

                var bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = w;
                bmi.bmiHeader.biHeight = -h; // top-down
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0;

                byte[] bits = new byte[w * h * 4];
                IntPtr hdc = GetDC(IntPtr.Zero);
                try
                {
                    if (GetDIBits(hdc, hbm, 0, (uint)h, bits, ref bmi, 0) == 0) return null;
                }
                finally { ReleaseDC(IntPtr.Zero, hdc); }

                var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                var rect = new Rectangle(0, 0, w, h);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                Marshal.Copy(bits, 0, data.Scan0, bits.Length);
                bmp.UnlockBits(data);
                return bmp;
            }
            catch { return null; }
        }
    }
}
