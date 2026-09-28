using System;
using System.Drawing;
using System.Drawing.Imaging;

class Program {
    static void Main() {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp)) {
            g.Clear(Color.Transparent);
            var attrs = new ImageAttributes();
            attrs.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
            var destRect = new Rectangle(0, 0, 16, 16);
            Image img = SystemIcons.Warning.ToBitmap();
            g.DrawImage(img, destRect, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attrs);
        }
        Console.WriteLine("Success!");
    }
}
