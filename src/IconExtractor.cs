using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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

        // Session icon cache. Shell extraction is expensive (SHGetFileInfo + jumbo
        // image list + a full-pixel transparency scan) and every panel rebuild used
        // to ask for the same icons again. Results are stored by path and handed out
        // as clones: callers own and dispose their copy, the cached original is
        // never disposed. Stale icons are possible if a file's icon is changed
        // externally mid-session - an app restart refreshes them.
        private static readonly object CacheGate = new object();
        private static readonly Dictionary<string, Image> IconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private const int IconCacheCap = 1000;

        private static Image CacheGet(string key)
        {
            lock (CacheGate)
            {
                Image hit;
                if (!IconCache.TryGetValue(key, out hit)) return null;
                try { return (Image)hit.Clone(); }
                catch { IconCache.Remove(key); return null; }
            }
        }

        private static void CachePut(string key, Image img)
        {
            if (img == null) return;
            lock (CacheGate)
            {
                Image old;
                if (IconCache.TryGetValue(key, out old)) { try { old.Dispose(); } catch { } }
                if (IconCache.Count >= IconCacheCap) IconCache.Clear();
                IconCache[key] = img;
            }
        }

        // ---------- persistent disk cache ----------
        // Extracted icons are saved as PNG files in iconcache\ next to the exe.
        // The file name hashes the path, the size flag and the source file's mtime,
        // so changing an icon externally invalidates its entry automatically
        // (folder, shell: and NETWORK paths have no mtime component - a stat on a
        // share can block for the SMB timeout, so it is never done for them).
        private static readonly string IconCacheDir = BuildIconCacheDir();
        private static int diskCleanupDone;

        // UNC paths and network-mapped drives. Local-only checks (no I/O), safe to
        // call on the UI thread: callers send such paths to worker threads because
        // any shell/stat call on them can block for the network timeout.
        internal static bool IsNetworkPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return false;
                if (path.StartsWith(@"\\", StringComparison.Ordinal)) return true;
                if (path.Length >= 2 && path[1] == ':')
                    return new DriveInfo(path.Substring(0, 3)).DriveType == DriveType.Network;
            }
            catch { }
            return false;
        }

        private static string BuildIconCacheDir()
        {
            try { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "iconcache"); }
            catch { return null; }
        }

        private static string DiskCacheFile(string path, bool large)
        {
            if (IconCacheDir == null || string.IsNullOrEmpty(path)) return null;
            try
            {
                string src = path.ToLowerInvariant();
                if (!IsNetworkPath(path) && !src.StartsWith("shell:", StringComparison.Ordinal) && !Directory.Exists(path))
                    src += "|" + File.GetLastWriteTimeUtc(path).Ticks.ToString("x");
                string key = (large ? "L" : "S") + "|" + src;
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
                    var sb = new StringBuilder(bytes.Length * 2);
                    foreach (var b in bytes) sb.Append(b.ToString("x2"));
                    return Path.Combine(IconCacheDir, sb + ".png");
                }
            }
            catch { return null; }
        }

        private static Image DiskCacheGet(string file)
        {
            try
            {
                if (file == null || !File.Exists(file)) return null;
                // Decode from bytes: new Bitmap(file) would keep the file locked.
                using (var ms = new MemoryStream(File.ReadAllBytes(file)))
                    return new Bitmap(ms);
            }
            catch { return null; }
        }

        // Generic key/value access to the persistent cache folder (same cleanup
        // rules). Used by the media frame extractor: same folder, custom key that
        // already includes the source mtime.
        public static Image DiskCacheGetByKey(string key)
        {
            return DiskCacheGet(CacheFileForKey(key));
        }

        public static void DiskCachePutByKey(string key, Image img)
        {
            DiskCachePut(CacheFileForKey(key), img);
        }

        private static string CacheFileForKey(string key)
        {
            if (IconCacheDir == null || string.IsNullOrEmpty(key)) return null;
            try
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
                    var sb = new StringBuilder(bytes.Length * 2);
                    foreach (var b in bytes) sb.Append(b.ToString("x2"));
                    return Path.Combine(IconCacheDir, sb + ".png");
                }
            }
            catch { return null; }
        }

        private static void DiskCachePut(string file, Image img)
        {
            try
            {
                if (file == null || img == null) return;
                Directory.CreateDirectory(IconCacheDir);
                string tmp = file + "." + Guid.NewGuid().ToString("N").Substring(0, 6) + ".tmp";
                img.Save(tmp, System.Drawing.Imaging.ImageFormat.Png);
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
                if (System.Threading.Interlocked.Exchange(ref diskCleanupDone, 1) == 0)
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate { DiskCacheCleanup(); });
            }
            catch { }
        }

        // Keeps the cache folder bounded; past the caps the oldest half goes.
        private static void DiskCacheCleanup()
        {
            try
            {
                var dir = new DirectoryInfo(IconCacheDir);
                if (!dir.Exists) return;
                var files = dir.GetFiles("*.png");
                long total = 0;
                foreach (var f in files) total += f.Length;
                if (files.Length <= 3000 && total <= 64L * 1024 * 1024) return;
                Array.Sort(files, delegate(FileInfo a, FileInfo b) { return a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc); });
                int keep = files.Length / 2;
                for (int i = 0; i < files.Length - keep; i++) { try { files[i].Delete(); } catch { } }
            }
            catch { }
        }

        // Icon for any item path: shell: namespace paths (UWP apps) go through the
        // IShellItemImageFactory, everything else through the regular extraction.
        // Cached twice: in memory per session, and as a PNG next to the exe so an
        // icon is pulled from the shell exactly once per tile lifetime (at add
        // time) and every restart just decodes the file. See the disk cache note.
        public static Image GetIconAuto(string path, bool large)
        {
            string key = (large ? "L|" : "S|") + path;
            Image hit = CacheGet(key);
            if (hit != null) return hit;
            string diskFile = DiskCacheFile(path, large);
            Image img = DiskCacheGet(diskFile);
            if (img == null)
            {
                img = ExtractIconAuto(path, large);
                if (img != null) DiskCachePut(diskFile, img);
            }
            CachePut(key, img);
            return img != null ? (Image)img.Clone() : null;
        }

        // Cached-only probe of GetIconAuto: returns the icon when it is already
        // in the session memory cache or the persistent disk cache and NEVER
        // extracts (no shell call, no network). Panel rebuilds use it to assign
        // known icons synchronously - the panel then appears complete instead of
        // refilling tile by tile from the extraction queue (the "flickering
        // icons" on every move / tab switch). Not cached → false, the caller
        // falls back to the async path.
        public static bool TryGetCachedAuto(string path, bool large, out Image img)
        {
            img = null;
            try
            {
                if (string.IsNullOrEmpty(path)) return false;
                string key = (large ? "L|" : "S|") + path;
                Image hit = CacheGet(key);
                if (hit != null) { img = hit; return true; }
                if (IsNetworkPath(path)) return false; // never stat a share on the UI thread
                string diskFile = DiskCacheFile(path, large);
                img = DiskCacheGet(diskFile);
                if (img == null) return false;
                CachePut(key, img);
                img = (Image)img.Clone();
                return true;
            }
            catch { img = null; return false; }
        }

        // Cached-only probe of LoadAny: custom/type icons already decoded this
        // session (LoadAny caches before any file re-read).
        public static bool TryGetCachedAny(string path, out Image img)
        {
            img = null;
            try
            {
                if (string.IsNullOrEmpty(path)) return false;
                img = CacheGet("A|" + path);
                return img != null;
            }
            catch { img = null; return false; }
        }

        // Drops the whole icon cache (session dictionary + iconcache\ PNG files).
        // Used by the settings "rebuild icons" button so every tile re-extracts
        // fresh icons from the shell on the next render. Returns the number of
        // deleted cache files.
        public static int ClearAllCaches()
        {
            int removed = 0;
            lock (CacheGate) { IconCache.Clear(); }
            try
            {
                if (IconCacheDir != null)
                {
                    var dir = new DirectoryInfo(IconCacheDir);
                    if (dir.Exists)
                        foreach (var f in dir.GetFiles("*.png"))
                        {
                            try { f.Delete(); removed++; } catch { }
                        }
                }
            }
            catch { }
            System.Threading.Interlocked.Exchange(ref diskCleanupDone, 1); // nothing left to clean up
            return removed;
        }

        private static Image ExtractIconAuto(string path, bool large)
        {
            try
            {
                if (ShellItemApi.IsShellPath(path))
                    return ShellItemApi.GetShellIcon(path, large ? 96 : 48);
            }
            catch { }
            return GetIcon(path, large);
        }

public static Image GetIcon(string path, bool large)
        {            // SHGFI_SYSICONINDEX alone returns iIcon = 0 on some systems, which makes
            // every item render the generic blank-page icon. Requesting SHGFI_ICON as well makes
            // the shell resolve the real system image list index into iIcon.
            try
            {
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
            }
            catch (Exception ex)
            {
                AppLog.Write("IconExtractor.GetIcon (iml)", ex);
            }

            // Fallback
            try
            {
                SHFILEINFO shinfo = new SHFILEINFO();
                uint flags = SHGFI_ICON | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);
                IntPtr res = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                {
                    Icon icon = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
                    DestroyIcon(shinfo.hIcon);
                    var bmp = icon.ToBitmap();
                    icon.Dispose();
                    return TrimTransparent(bmp);
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("IconExtractor.GetIcon (fallback)", ex);
            }
            return null;
        }

        // Loads an icon (.ico/.exe) or an image file (.png/.jpg/.bmp) as an Image.
        // Cached like GetIconAuto: custom tile icons would otherwise re-read and
        // re-decode the file on every panel rebuild.
        public static Image LoadAny(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;
                string key = "A|" + path;
                Image hit = CacheGet(key);
                if (hit != null) return hit;
                Image img = null;
                string lower = path.ToLowerInvariant();
                if (lower.EndsWith(".exe") || lower.EndsWith(".ico"))
                    img = GetIconAuto(path, true);
                else
                    img = Image.FromFile(path);
                CachePut(key, img);
                return img != null ? (Image)img.Clone() : null;
            }
            catch (Exception ex)
            {
                AppLog.Write("IconExtractor.LoadAny", ex);
                return null;
            }
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
