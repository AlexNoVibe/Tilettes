using System;
using System.Drawing;
using System.Windows.Forms;
using WinPanel;
class Test : Form {
    Image img;
    public Test() {
        img = IconExtractor.ToBitmapRobust(SystemIcons.Application);
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        e.Graphics.Clear(Color.Black);
        if (img != null) {
            e.Graphics.DrawImage(img, new Rectangle(50, 50, 64, 64));
            e.Graphics.DrawString("DRAWN: " + img.Width, new Font("Arial", 12), Brushes.White, 50, 120);
        }
    }
    static void Main() {
        Application.Run(new Test());
    }
}
