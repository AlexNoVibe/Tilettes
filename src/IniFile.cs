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
    }
}
