using System;
using System.Drawing;
using System.Windows.Forms;
using WinPanel;
class Test {
    static void Main() {
        Image img = IconExtractor.GetIcon(@"C:\Windows\explorer.exe", true);
        Bitmap target = new Bitmap(200, 200);
        using (Graphics g = Graphics.FromImage(target)) {
            g.Clear(Color.Black);
            if (img != null) {
                int iconSize = 64;
                var destRect = new Rectangle(50, 50, iconSize, iconSize);
                using (var attrs = new System.Drawing.Imaging.ImageAttributes())
                {
                    attrs.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    g.DrawImage(img, destRect, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attrs);
                }
            }
        }
        bool hasPixels = false;
        for (int i=50; i<114; i+=5) {
            for (int j=50; j<114; j+=5) {
                Color c = target.GetPixel(i, j);
                if (c.R > 0 || c.G > 0 || c.B > 0) hasPixels = true;
            }
        }
        Console.WriteLine("Drawn pixels? " + hasPixels);
    }
}
