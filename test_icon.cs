using System;
using System.Drawing;
using WinPanel;
class Test {
    static void Main() {
        Image img = IconExtractor.GetIcon(@"C:\Windows\explorer.exe", true);
        if (img == null) Console.WriteLine("IMG IS NULL");
        else Console.WriteLine(img.Width);
    }
}
