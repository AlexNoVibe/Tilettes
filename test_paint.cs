using System;
using System.Drawing;
using System.Windows.Forms;
using WinPanel;
using System.IO;

class Test
{
    static void Main()
    {
        var tile = new TileControl();
        tile.Item = new ShortcutItem { Name = "Test", IsFolder = false, Path = "C:\\Windows\\System32\\cmd.exe" };
        tile.TabData = new TabData();
        tile.IconImage = IconExtractor.GetIcon("C:\\Windows\\System32\\cmd.exe", true);
        
        tile.Width = 100;
        tile.Height = 100;

        using (var bmp = new Bitmap(100, 100))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Red);
            var e = new PaintEventArgs(g, new Rectangle(0, 0, 100, 100));
            var mi = typeof(TileControl).GetMethod("OnPaint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            try {
                mi.Invoke(tile, new object[] { e });
                bmp.Save("test_out.png");
            } catch (Exception ex) {
                Console.WriteLine("Exception: " + ex);
            }
        }
    }
}
