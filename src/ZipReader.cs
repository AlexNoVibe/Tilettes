using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace WinPanel
{
    // Minimal ZIP reader paired with ZipWriter: parses the central directory,
    // inflates method-8 entries with DeflateStream and copies method-0 (stored)
    // entries byte for byte, verifying CRC32. Built only on what the stock
    // .NET Framework 4 compiler ships, same constraint as ZipWriter.
    // It reads standard archives, so Explorer-made zips of our backups work too.
    public static class ZipReader
    {
        public class Entry
        {
            public string Name;      // archive-relative, '/' separators
            public long Size;        // uncompressed size
            public uint Crc;
            internal ushort Method;
            internal long LocalHeaderOffset;
            internal long LocalCompSize;   // compressed size from the central dir
        }

        private static byte[] ReadAll(string zipPath)
        {
            // Backup archives are a few MB; reading them whole is simpler and
            // faster than seeking around.
            return File.ReadAllBytes(zipPath);
        }

        // Lists all entries; throws on a file that is not a zip at all.
        public static List<Entry> List(string zipPath)
        {
            byte[] buf = ReadAll(zipPath);
            long eocd = FindEocd(buf);
            if (eocd < 0) throw new IOException("Not a zip archive");

            ushort count = BitConverter.ToUInt16(buf, (int)eocd + 10);
            uint dirSize = BitConverter.ToUInt32(buf, (int)eocd + 12);
            uint dirStart = BitConverter.ToUInt32(buf, (int)eocd + 16);

            var res = new List<Entry>();
            long pos = dirStart;
            long end = dirStart + dirSize;
            for (int i = 0; i < count && pos + 46 <= end; i++)
            {
                if (BitConverter.ToUInt32(buf, (int)pos) != 0x02014b50) break;
                ushort flags = BitConverter.ToUInt16(buf, (int)pos + 8);
                ushort method = BitConverter.ToUInt16(buf, (int)pos + 10);
                uint crc = BitConverter.ToUInt32(buf, (int)pos + 16);
                uint compSize = BitConverter.ToUInt32(buf, (int)pos + 20);
                uint uncompSize = BitConverter.ToUInt32(buf, (int)pos + 24);
                ushort nameLen = BitConverter.ToUInt16(buf, (int)pos + 28);
                ushort extraLen = BitConverter.ToUInt16(buf, (int)pos + 30);
                ushort commentLen = BitConverter.ToUInt16(buf, (int)pos + 32);
                uint localOffset = BitConverter.ToUInt32(buf, (int)pos + 42);

                var e = new Entry();
                e.Name = DecodeName(buf, pos + 46, nameLen, flags);
                e.Size = uncompSize;
                e.Crc = crc;
                e.Method = method;
                e.LocalHeaderOffset = localOffset;
                e.LocalCompSize = compSize;
                res.Add(e);

                pos += 46 + nameLen + extraLen + commentLen;
            }
            return res;
        }

        // Decompresses one entry (from List()) to destPath, overwriting it.
        // Throws when the data is corrupt (CRC mismatch).
        public static void Extract(string zipPath, Entry entry, string destPath)
        {
            byte[] buf = ReadAll(zipPath);
            long p = entry.LocalHeaderOffset;
            if (p + 30 > buf.Length || BitConverter.ToUInt32(buf, (int)p) != 0x04034b50)
                throw new IOException("Broken zip entry: " + entry.Name);
            ushort nameLen = BitConverter.ToUInt16(buf, (int)p + 26);
            ushort extraLen = BitConverter.ToUInt16(buf, (int)p + 28);
            long dataStart = p + 30 + nameLen + extraLen;
            long compSize = entry.LocalCompSize;
            if (dataStart + compSize > buf.Length)
                throw new IOException("Truncated zip entry: " + entry.Name);

            byte[] data;
            if (entry.Method == 0)
            {
                data = new byte[compSize];
                Array.Copy(buf, (int)dataStart, data, 0, compSize);
            }
            else if (entry.Method == 8)
            {
                using (var src = new MemoryStream(buf, (int)dataStart, (int)compSize, false))
                using (var ds = new DeflateStream(src, CompressionMode.Decompress))
                using (var ms = new MemoryStream())
                {
                    ds.CopyTo(ms);
                    data = ms.ToArray();
                }
                if (data.Length != entry.Size)
                    throw new IOException("Bad inflate size: " + entry.Name);
            }
            else throw new IOException("Unsupported zip method " + entry.Method + ": " + entry.Name);

            if (ZipWriter.Crc32(data, data.Length) != entry.Crc)
                throw new IOException("CRC mismatch: " + entry.Name);

            string dir = Path.GetDirectoryName(Path.GetFullPath(destPath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(destPath, data);
        }

        internal static long FindEocd(byte[] buf)
        {
            // EOCD is at least 22 bytes; the comment can add up to 65535.
            long min = Math.Max(0, buf.Length - 22 - 65535);
            for (long i = buf.Length - 22; i >= min; i--)
                if (buf[i] == 0x50 && buf[i + 1] == 0x4b && buf[i + 2] == 0x05 && buf[i + 3] == 0x06)
                    return i;
            return -1;
        }

        private static string DecodeName(byte[] buf, long pos, int len, ushort flags)
        {
            if ((flags & 0x0800) != 0) return Encoding.UTF8.GetString(buf, (int)pos, len);
            // Names without the UTF-8 flag are codepage-437 by zip spec; our own
            // writer always sets the flag, this branch only covers foreign zips.
            try { return Encoding.GetEncoding(437).GetString(buf, (int)pos, len); }
            catch { return Encoding.UTF8.GetString(buf, (int)pos, len); }
        }
    }
}
