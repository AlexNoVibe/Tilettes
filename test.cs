using System;
using System.Drawing;
using WinPanel;

class Test {
    static void Main(string[] args) {
        var bmp = IconExtractor.GetIcon(@"C:\Windows\System32\cmd.exe", true) as Bitmap;
        if (bmp == null) {
            Console.WriteLine("Fail: bmp is null");
            return;
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
        Console.WriteLine(string.Format("hasAlpha: {0}, allTransparent: {1}, format: {2}", hasAlpha, allTransparent, bmp.PixelFormat));
    }
}
