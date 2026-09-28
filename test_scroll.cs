using System;
using System.Drawing;
using System.Windows.Forms;

public class TestForm : Form {
    public TestForm() {
        Panel p = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        this.Controls.Add(p);
        Button b1 = new Button { Location = new Point(0, 1000), Text = "B1" };
        p.Controls.Add(b1);
        this.Load += (s, e) => {
            p.AutoScrollPosition = new Point(0, 500);
            p.Controls.Clear();
            Console.WriteLine(string.Format("After clear. AutoScrollPos: {0}, DisplayRect: {1}", p.AutoScrollPosition, p.DisplayRectangle));
            Application.Exit();
        };
    }
    [STAThread]
    static void Main() { Application.Run(new TestForm()); }
}
