using System;
using System.Drawing;
using System.Windows.Forms;
using WinPanel;
class Test : Form {
    Image img;
    public Test() {
        img = IconExtractor.GetIcon(@"C:\Windows\explorer.exe", true);
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        e.Graphics.Clear(Color.Black);
        if (img != null) {
            int iconSize = 64;
            var destRect = new Rectangle(50, 50, iconSize, iconSize);
            using (var attrs = new System.Drawing.Imaging.ImageAttributes())
            {
                attrs.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                e.Graphics.DrawImage(img, destRect, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attrs);
            }
            e.Graphics.DrawString("DRAWN", new Font("Arial", 12), Brushes.White, 50, 120);
        }
    }
    static void Main() {
        Application.Run(new Test());
    }
}
