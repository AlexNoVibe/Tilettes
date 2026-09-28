using System;
using System.Drawing;
using System.Windows.Forms;
using WinPanel;
class Test {
    static void Main() {
        Image img = IconExtractor.ToBitmapRobust(SystemIcons.Application);
        Bitmap target = new Bitmap(200, 200);
        using (Graphics g = Graphics.FromImage(target)) {
            g.Clear(Color.Black);
            if (img != null) {
                e.Graphics.DrawImage(img, new Rectangle(50, 50, 64, 64));
            }
        }
    }
}
