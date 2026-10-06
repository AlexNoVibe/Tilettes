using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WinPanel
{
    public class IniFile
    {
        public string Path { get; private set; }

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern bool WritePrivateProfileSection(string Section, string Data, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        public IniFile(string path)
        {
            Path = new FileInfo(path).FullName;
        }

        public string Read(string Key, string Section = "Settings", string Default = "")
        {
            var RetVal = new StringBuilder(255);
            GetPrivateProfileString(Section, Key, Default, RetVal, 255, Path);
            return RetVal.ToString();
        }

        public void Write(string Key, string Value, string Section = "Settings")
        {
            WritePrivateProfileString(Section, Key, Value, Path);
        }

        // Replaces a whole section in a single file write. The per-key Write
        // calls each rewrite the file from scratch, and a Save built of dozens
        // of them stalls the UI thread on machines with a slow file filter
        // (antivirus): every edit-mode toggle froze for hundreds of ms.
        public void WriteSection(string Section, string[] KeyValueLines)
        {
            var sb = new StringBuilder();
            foreach (var line in KeyValueLines)
                sb.Append(line).Append('\0');
            WritePrivateProfileSection(Section, sb.ToString(), Path);
        }
    }
}
