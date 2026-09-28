using System;
using System.Drawing;

class Test3 {
    static void Main() {
        var bmp = SystemIcons.WinLogo.ToBitmap();
        bool hasAlpha = false;
        bool allTransparent = true;
        for (int y = 0; y < bmp.Height; y++) {
            for (int x = 0; x < bmp.Width; x++) {
                Color c = bmp.GetPixel(x, y);
                if (c.A > 0 && c.A < 255) hasAlpha = true;
                if (c.A != 0) allTransparent = false;
            }
        }
        Console.WriteLine(string.Format("SystemIcons.WinLogo hasAlpha: {0}, allTransparent: {1}", hasAlpha, allTransparent));
        
        bmp = SystemIcons.Application.ToBitmap();
        hasAlpha = false;
        allTransparent = true;
        for (int y = 0; y < bmp.Height; y++) {
            for (int x = 0; x < bmp.Width; x++) {
                Color c = bmp.GetPixel(x, y);
                if (c.A > 0 && c.A < 255) hasAlpha = true;
                if (c.A != 0) allTransparent = false;
            }
        }
        Console.WriteLine(string.Format("SystemIcons.Application hasAlpha: {0}, allTransparent: {1}", hasAlpha, allTransparent));
    }
}
