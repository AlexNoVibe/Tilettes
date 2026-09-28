using System;
using System.Drawing;
using WinPanel;
class Test {
    static void Main() {
        Image img = IconExtractor.GetIcon(@"C:\Windows\explorer.exe", true);
        Bitmap bmp = (Bitmap)img;
        bool hasColors = false;
        for (int i=0; i<bmp.Width; i+=10) {
            for (int j=0; j<bmp.Height; j+=10) {
                if (bmp.GetPixel(i, j).A > 0) hasColors = true;
            }
        }
        Console.WriteLine("Has visible pixels? " + hasColors);
    }
}
