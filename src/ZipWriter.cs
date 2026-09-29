using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace WinPanel
{
    // Minimal ZIP writer (deflate + CRC32) built only on what the stock
    // .NET Framework 4 compiler ships: DeflateStream from System.IO.Compression.
    // Produces a standard archive readable by Explorer / PowerShell / tar.
    public static class ZipWriter
    {
        private class Entry
        {
            public string Name;      // archive-relative name, '/' separators
            public long Offset;      // offset of the local header
            public uint Crc;
            public long Compressed;
            public long Uncompressed;
        }

        private static uint[] crcTable;

        private static uint Crc32(byte[] buf, int len)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                    crcTable[i] = c;
                }
            }
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < len; i++) crc = crcTable[(crc ^ buf[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        // files: pairs of (entry name inside the archive, source file path).
        // Missing sources are skipped; on any error the archive may end up
        // partial but never corrupted.
        public static bool Create(string zipPath, IList<KeyValuePair<string, string>> files)
        {
            var entries = new List<Entry>();
            try
            {
                string dir = Path.GetDirectoryName(zipPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
                {
                    foreach (var kv in files)
                    {
                        try
                        {
                            if (!File.Exists(kv.Value)) continue;
                            WriteEntry(fs, kv.Key, kv.Value, entries);
                        }
                        catch (Exception ex) { AppLog.Write("ZipWriter entry " + kv.Key, ex); }
                    }
                    WriteCentralDirectory(fs, entries);
                }
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Write("ZipWriter.Create " + zipPath, ex);
                try { File.Delete(zipPath); } catch { }
                return false;
            }
        }

        private static void WriteEntry(Stream fs, string entryName, string sourcePath, List<Entry> entries)
        {
            byte[] data = File.ReadAllBytes(sourcePath);
            uint crc = Crc32(data, data.Length);

            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(entryName.Replace('\\', '/'));
            long offset = fs.Position;

            var e = new Entry();
            e.Name = entryName.Replace('\\', '/');
            e.Offset = offset;
            e.Crc = crc;
            e.Uncompressed = data.Length;

            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionMode.Compress, true))
                {
                    ds.Write(data, 0, data.Length);
                }
                byte[] compressed = ms.ToArray();
                e.Compressed = compressed.Length;

                bool deflate = compressed.Length < data.Length;
                if (!deflate) e.Compressed = data.Length; // stored

                BinaryWriter w = new BinaryWriter(fs);
                w.Write(0x04034b50u);            // local file header signature
                w.Write((ushort)20);             // version needed
                w.Write((ushort)0x0800);         // flags: UTF-8 names
                w.Write((ushort)(deflate ? (ushort)8 : (ushort)0)); // method
                w.Write((ushort)0); w.Write((ushort)0);             // time, date
                w.Write(crc);
                w.Write((uint)e.Compressed);
                w.Write((uint)e.Uncompressed);
                w.Write((ushort)nameBytes.Length);
                w.Write((ushort)0);              // extra len
                w.Write(nameBytes);
                if (deflate) w.Write(compressed); else w.Write(data);
                w.Flush();
            }
            entries.Add(e);
        }

        private static void WriteCentralDirectory(Stream fs, List<Entry> entries)
        {
            long dirStart = fs.Position;
            BinaryWriter w = new BinaryWriter(fs);
            foreach (var e in entries)
            {
                byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(e.Name);
                w.Write(0x02014b50u);        // central directory header
                w.Write((ushort)20);         // version made by
                w.Write((ushort)20);         // version needed
                w.Write((ushort)0x0800);     // flags
                w.Write((ushort)(e.Compressed < e.Uncompressed ? (ushort)8 : (ushort)0));
                w.Write((ushort)0); w.Write((ushort)0);
                w.Write(e.Crc);
                w.Write((uint)e.Compressed);
                w.Write((uint)e.Uncompressed);
                w.Write((ushort)nameBytes.Length);
                w.Write((ushort)0);          // extra
                w.Write((ushort)0);          // comment
                w.Write((ushort)0);          // disk number
                w.Write((ushort)0);          // internal attrs
                w.Write(0u);                 // external attrs
                w.Write((uint)e.Offset);
                w.Write(nameBytes);
            }
            long dirSize = fs.Position - dirStart;
            w.Write(0x06054b50u);            // end of central directory
            w.Write((ushort)0); w.Write((ushort)0);
            w.Write((ushort)entries.Count);
            w.Write((ushort)entries.Count);
            w.Write((uint)dirSize);
            w.Write((uint)dirStart);
            w.Write((ushort)0);
            w.Flush();
        }
    }
}
