using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace WinPanel
{
    // Self-sufficient video frame extraction via Media Foundation (in-box since
    // Windows 7: mp4/m4v/mkv/mov/wmv/avi/3gp). Used when the shell thumbnail
    // cache has no preview yet - a freshly added video: IShellItemImageFactory
    // with THUMBNAILONLY fails until Explorer happens to generate the preview,
    // so the tile stayed generic. With this fallback the tile shows a frame
    // immediately. Fail-soft: any error returns null (caller keeps the icon).
    // The extracted frame is cached on disk (key includes the source mtime).
    public static class MediaFrame
    {
        private static bool mfStarted;
        private static readonly object Gate = new object();

        [DllImport("mfplat.dll")]
        private static extern int MFStartup(int version, int flags);

        [DllImport("mfplat.dll")]
        private static extern int MFCreateMediaType(out IMFMediaType mt);

        [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode)]
        private static extern int MFCreateSourceReaderFromURL(string url, IMFAttributes attrs, out IMFSourceReader reader);

        private const int MF_VERSION = 0x00020070;
        private const int MFSTARTUP_NOSOCKET = 0x1;

        // stream indexes (mfreadwrite.h)
        private const int MF_SOURCE_READER_ALL_STREAMS = unchecked((int)0xFFFFFFFE);
        private const int MF_SOURCE_READER_FIRST_VIDEO_STREAM = unchecked((int)0xFFFFFFFD);
        private const int MF_SOURCE_READERF_ENDOFSTREAM = 2;

        // attribute / media type GUIDs (mfapi.h)
        private static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        private static readonly Guid MF_MT_DEFAULT_STRIDE = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
        private static readonly Guid MF_MT_FRAME_SIZE = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
        private static readonly Guid MFMediaType_Video = new Guid("7364696d-0000-0010-8000-00aa00389b71");
        private static readonly Guid MFVideoFormat_RGB32 = new Guid("00000016-0000-0010-8000-00aa00389b71");

        // IID_IMFSourceReader — verbatim from the Windows SDK mfreadwrite.h
        // (70ae66f2-c809-4e4f-8915-bdcb406b7993; a wrong IID here fails QI with
        // E_NOINTERFACE and every extraction silently returns null).
        // Method ORDER must match the header's vtable exactly: GetCurrentMediaType
        // is declared BEFORE SetCurrentMediaType in mfreadwrite.h.
        [ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFSourceReader
        {
            [PreserveSig] int GetStreamSelection(int streamIndex, out int selected);
            [PreserveSig] int SetStreamSelection(int streamIndex, int selected);
            [PreserveSig] int GetNativeMediaType(int streamIndex, int mediaTypeIndex, out IMFMediaType type);
            [PreserveSig] int GetCurrentMediaType(int streamIndex, out IMFMediaType type);
            [PreserveSig] int SetCurrentMediaType(int streamIndex, IntPtr reserved, IMFMediaType type);
            [PreserveSig] int SetCurrentPosition(ref Guid majorType, IntPtr position);
            [PreserveSig] int ReadSample(int streamIndex, int controlFlags, out int actualStreamIndex, out int streamFlags, out long timestamp, out IMFSample sample);
            [PreserveSig] int Flush(int streamIndex);
            [PreserveSig] int GetServiceForStream(int streamIndex, ref Guid service, ref Guid riid, out IntPtr obj);
            [PreserveSig] int GetPresentationAttribute(int streamIndex, ref Guid key, IntPtr value);
        }

        // Interface IIDs — verbatim from the Windows SDK mfobjects.h (a wrong IID
        // fails QI with E_NOINTERFACE and every extraction silently returns null).
        // IMFAttributes: ALL methods in the exact mfobjects.h vtable order — a
        // ComImport interface maps slots by declaration order, so skipping or
        // reordering methods would silently call the wrong native function.
        // Only the marked ones are actually used; the rest are ABI stubs.
        [ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFAttributes
        {
            [PreserveSig] int GetItem(ref Guid key, IntPtr value);
            [PreserveSig] int GetItemType(ref Guid key, out int type);
            [PreserveSig] int CompareItem(ref Guid key, IntPtr value, out int result);
            [PreserveSig] int Compare(IMFAttributes theirs, int matchType, out int result);
            [PreserveSig] int GetUINT32(ref Guid key, out int value);       // used
            [PreserveSig] int GetUINT64(ref Guid key, out long value);      // used
            [PreserveSig] int GetDouble(ref Guid key, out double value);
            [PreserveSig] int GetGUID(ref Guid key, out Guid value);
            [PreserveSig] int GetStringLength(ref Guid key, out int value);
            [PreserveSig] int GetString(ref Guid key, IntPtr buf, int len, out int written);
            [PreserveSig] int GetAllocatedString(ref Guid key, out IntPtr buf, out int len);
            [PreserveSig] int GetBlobSize(ref Guid key, out int value);
            [PreserveSig] int GetBlob(ref Guid key, IntPtr buf, int size, out int read);
            [PreserveSig] int GetAllocatedBlob(ref Guid key, out IntPtr buf, out int size);
            [PreserveSig] int GetUnknown(ref Guid key, ref Guid riid, out IntPtr obj);
            [PreserveSig] int SetItem(ref Guid key, IntPtr value);
            [PreserveSig] int DeleteItem(ref Guid key);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid key, int value);
            [PreserveSig] int SetUINT64(ref Guid key, long value);
            [PreserveSig] int SetDouble(ref Guid key, double value);
            [PreserveSig] int SetGUID(ref Guid key, ref Guid value);        // used
            [PreserveSig] int SetString(ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
            [PreserveSig] int SetBlob(ref Guid key, IntPtr buf, int size);
            [PreserveSig] int SetUnknown(ref Guid key, [MarshalAs(UnmanagedType.IUnknown)] object obj);
            [PreserveSig] int LockStore();
            [PreserveSig] int UnlockStore();
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetItemByIndex(int index, ref Guid key, IntPtr value);
            [PreserveSig] int CopyAllItems(IMFAttributes dest);
        }

        [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFMediaType : IMFAttributes
        {
        }

        [ComImport, Guid("045FA593-8799-42b8-BC8D-8968C6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFMediaBuffer
        {
            [PreserveSig] int Lock(out IntPtr ppbBuffer, out int pcbMaxLength, out int pcbCurrentLength);
            [PreserveSig] int Unlock();
            [PreserveSig] int GetCurrentLength(out int length);
            [PreserveSig] int SetCurrentLength(int length);
            [PreserveSig] int GetMaxLength(out int length);
        }

        [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFSample : IMFMediaBuffer
        {
            [PreserveSig] int GetSampleTime(out long time);
            [PreserveSig] int SetSampleTime(long time);
            [PreserveSig] int GetSampleDuration(out long dur);
            [PreserveSig] int SetSampleDuration(long dur);
            [PreserveSig] int GetBufferCount(out int count);
            [PreserveSig] int GetBufferByIndex(int index, out IMFMediaBuffer buf);
            [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer buf);
        }

        // Extracts a representative frame, scaled to max `width` pixels.
        public static Bitmap ExtractFrame(string path, int width)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                string key = "mediaframe|" + path.ToLowerInvariant() + "|" +
                             File.GetLastWriteTimeUtc(path).Ticks + "|" + width;
                Bitmap cached = IconExtractor.DiskCacheGetByKey(key) as Bitmap;
                if (cached != null) return cached;

                Bitmap frame = ExtractFrameInternal(path);
                if (frame == null) return null;
                if (frame.Width > width)
                {
                    int h = Math.Max(1, (int)Math.Round(frame.Height * (width / (double)frame.Width)));
                    var scaled = new Bitmap(width, h);
                    using (var g = Graphics.FromImage(scaled))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(frame, 0, 0, width, h);
                    }
                    frame.Dispose();
                    frame = scaled;
                }
                IconExtractor.DiskCachePutByKey(key, frame);
                return frame;
            }
            catch (Exception ex)
            {
                AppLog.Write("MediaFrame.ExtractFrame " + path, ex);
                return null;
            }
        }

        private static Bitmap ExtractFrameInternal(string path)
        {
            lock (Gate)
            {
                if (!mfStarted)
                {
                    int hr = MFStartup(MF_VERSION, MFSTARTUP_NOSOCKET);
                    if (hr < 0) { AppLog.Write("MediaFrame: MFStartup hr=0x" + hr.ToString("X8")); return null; }
                    mfStarted = true;
                }
            }

            IMFSourceReader reader;
            int h = MFCreateSourceReaderFromURL(path, null, out reader);
            if (h < 0 || reader == null)
            {
                AppLog.Write("MediaFrame: reader hr=0x" + h.ToString("X8") + " " + Path.GetFileName(path));
                return null;
            }

            try
            {
                if (reader.SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS, 0) < 0) return null;
                if (reader.SetStreamSelection(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 1) < 0) return null;

                // Partial media type: major=Video + subtype only. NV12 is the
                // decoder's native output (works even without a color-converter
                // MFT); RGB32 is the fallback when a converter exists.
                bool nv12 = true;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    IMFMediaType partial;
                    if (MFCreateMediaType(out partial) < 0 || partial == null) return null;
                    partial.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
                    Guid sub = nv12 ? new Guid("3231564e-0000-0010-8000-00aa00389b71") /* 'NV12' */ : MFVideoFormat_RGB32;
                    partial.SetGUID(MF_MT_SUBTYPE, sub);
                    h = reader.SetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, IntPtr.Zero, partial);
                    Marshal.ReleaseComObject(partial);
                    if (h >= 0) break;
                    if (attempt == 1)
                    {
                        AppLog.Write("MediaFrame: SetCurrentMediaType hr=0x" + h.ToString("X8") + " (nv12+rgb32)");
                        return null;
                    }
                    nv12 = false; // retry with RGB32
                }

                // Output geometry: width and stride come from the actual output type.
                int width = 0, stride = 0;
                IMFMediaType cur;
                if (reader.GetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, out cur) >= 0 && cur != null)
                {
                    try
                    {
                        int s;
                        long packed;
                        if (cur.GetUINT32(MF_MT_DEFAULT_STRIDE, out s) >= 0) stride = s;
                        if (cur.GetUINT64(MF_MT_FRAME_SIZE, out packed) >= 0) width = (int)(packed >> 32);
                    }
                    finally { Marshal.ReleaseComObject(cur); }
                }
                if (stride == 0) stride = width * 4;
                int absStride = Math.Abs(stride);
                if (absStride < 4 || width <= 0) return null;
                bool bottomUp = stride > 0; // MF RGB32 defaults to bottom-up rows

                Bitmap firstFrame = null;
                Bitmap chosen = null;
                for (int i = 0; i < 60; i++)
                {
                    int flags, idx;
                    long ts;
                    IMFSample sample;
                    h = reader.ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, out idx, out flags, out ts, out sample);
                    if (h < 0) break;
                    if (sample == null)
                    {
                        if ((flags & MF_SOURCE_READERF_ENDOFSTREAM) != 0) break;
                        continue;
                    }
                    Bitmap bmp = null;
                    try
                    {
                        bmp = nv12 ? Nv12SampleToBitmap(sample, width) : SampleToBitmap(sample, absStride, width, bottomUp);
                        if (bmp != null)
                        {
                            if (firstFrame == null) firstFrame = bmp;
                            if (!IsMostlyBlack(bmp)) { chosen = bmp; break; }
                        }
                    }
                    finally { Marshal.ReleaseComObject(sample); }
                    if (bmp != null && !ReferenceEquals(bmp, firstFrame) && !ReferenceEquals(bmp, chosen)) bmp.Dispose();
                    if ((flags & MF_SOURCE_READERF_ENDOFSTREAM) != 0) break;
                }
                if (chosen != null && firstFrame != null && !ReferenceEquals(firstFrame, chosen)) firstFrame.Dispose();
                return chosen != null ? chosen : firstFrame; // all-black video: the first frame is still a preview
            }
            finally { Marshal.ReleaseComObject(reader); }
        }

        private static Bitmap SampleToBitmap(IMFSample sample, int absStride, int width, bool bottomUp)
        {
            IMFMediaBuffer buf;
            if (sample.ConvertToContiguousBuffer(out buf) < 0) return null;
            try
            {
                IntPtr data;
                int maxLen, curLen;
                if (buf.Lock(out data, out maxLen, out curLen) < 0) return null;
                try
                {
                    int rows = curLen / absStride;
                    if (rows <= 0) return null;
                    var bmp = new Bitmap(width, rows, PixelFormat.Format32bppArgb);
                    var rect = new Rectangle(0, 0, width, rows);
                    var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        int rowBytes = Math.Min(absStride, width * 4);
                        var rowBuf = new byte[rowBytes];
                        for (int y = 0; y < rows; y++)
                        {
                            long src = bottomUp ? (long)(rows - 1 - y) * absStride : (long)y * absStride;
                            Marshal.Copy(new IntPtr(data.ToInt64() + src), rowBuf, 0, rowBytes);
                            Marshal.Copy(rowBuf, 0, new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), rowBytes);
                        }
                    }
                    finally { bmp.UnlockBits(bd); }
                    return bmp;
                }
                finally { buf.Unlock(); }
            }
            finally { Marshal.ReleaseComObject(buf); }
        }

        // NV12 sample (Y plane + interleaved UV plane) -> BGRA bitmap.
        // BT.601 studio swing, integer math; alpha is opaque.
        private static Bitmap Nv12SampleToBitmap(IMFSample sample, int width)
        {
            IMFMediaBuffer buf;
            if (sample.ConvertToContiguousBuffer(out buf) < 0) return null;
            try
            {
                IntPtr data;
                int maxLen, curLen;
                if (buf.Lock(out data, out maxLen, out curLen) < 0) return null;
                try
                {
                    int h = (int)((long)curLen * 2 / (3L * width));
                    if (h <= 0) return null;
                    var nv = new byte[curLen];
                    Marshal.Copy(data, nv, 0, curLen);
                    var bgra = new byte[width * h * 4];
                    long uvBase = (long)width * h;
                    for (int y = 0; y < h; y++)
                    {
                        int yOff = y * width;
                        int uvOff = (int)uvBase + (y / 2) * width;
                        int dOff = y * width * 4;
                        for (int x = 0; x < width; x++)
                        {
                            int yy = nv[yOff + x] - 16;
                            int uv = uvOff + (x & ~1);
                            int u = nv[uv] - 128;
                            int v = nv[uv + 1] - 128;
                            int c = 1192 * yy;
                            int r = (c + 2066 * v) >> 10;
                            int g = (c - 400 * u - 833 * v) >> 10;
                            int b = (c + 1634 * u) >> 10;
                            int d = dOff + x * 4;
                            bgra[d] = (byte)(b < 0 ? 0 : b > 255 ? 255 : b);
                            bgra[d + 1] = (byte)(g < 0 ? 0 : g > 255 ? 255 : g);
                            bgra[d + 2] = (byte)(r < 0 ? 0 : r > 255 ? 255 : r);
                            bgra[d + 3] = 255;
                        }
                    }
                    var bmp = new Bitmap(width, h, PixelFormat.Format32bppArgb);
                    var rect = new Rectangle(0, 0, width, h);
                    var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    try { Marshal.Copy(bgra, 0, bd.Scan0, bgra.Length); }
                    finally { bmp.UnlockBits(bd); }
                    return bmp;
                }
                finally { buf.Unlock(); }
            }
            finally { Marshal.ReleaseComObject(buf); }
        }

        private static bool IsMostlyBlack(Bitmap bmp)
        {
            try
            {
                var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                var bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int step = Math.Max(4, bd.Stride / 40);
                    int hits = 0, total = 0;
                    for (int y = 0; y < bmp.Height; y += 4)
                    {
                        for (int x = 0; x < bd.Stride - 4; x += step)
                        {
                            IntPtr p = new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride + x);
                            byte b = Marshal.ReadByte(p, 0);
                            byte gr = Marshal.ReadByte(p, 1);
                            byte r = Marshal.ReadByte(p, 2);
                            total++;
                            if (b > 24 || gr > 24 || r > 24) hits++;
                        }
                    }
                    return total == 0 || hits < total / 10;
                }
                finally { bmp.UnlockBits(bd); }
            }
            catch { return false; }
        }
    }
}
