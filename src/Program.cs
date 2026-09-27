using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WinPanel
{
    public class MainForm : Form
    {
        private Settings settings;
        private string settingsPath = "settings.xml";
        private TabControl tabControl;
        private NotifyIcon trayIcon;

        // Colors for modern dark theme
        private Color bgColor = Color.FromArgb(30, 30, 30); // #1E1E1E
        private Color panelColor = Color.FromArgb(45, 45, 48); // #2D2D30
        private Color hoverColor = Color.FromArgb(62, 62, 66);
        private Color textColor = Color.White;

        public MainForm()
        {
            settings = Settings.Load(settingsPath);

            this.Text = "WinPanel";
            this.Width = settings.WindowWidth;
            this.Height = settings.WindowHeight;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(settings.WindowX, settings.WindowY);
            
            // Apply Modern Flat Design to Form
            this.BackColor = bgColor;
            this.ForeColor = textColor;
            this.Font = new Font("Segoe UI", 9f);
            this.Padding = new Padding(5);

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                Padding = new Point(12, 8)
            };
            tabControl.DrawItem += TabControl_DrawItem;
            
            this.Controls.Add(tabControl);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "WinPanel";
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.DoubleClick += (s, e) => RestoreWindow();
            
            var trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Restore", (s, e) => RestoreWindow());
            trayMenu.MenuItems.Add("Settings", (s, e) => OpenSettings());
            trayMenu.MenuItems.Add("Exit", (s, e) => { Application.Exit(); });
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;

            this.FormClosing += MainForm_FormClosing;

            // Adding a context menu to the form itself to access Settings easily
            var formMenu = new ContextMenu();
            formMenu.MenuItems.Add("Settings", (s, e) => OpenSettings());
            this.ContextMenu = formMenu;

            LoadTabs();
        }

        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            var g = e.Graphics;
            var tabPage = tabControl.TabPages[e.Index];
            var tabBounds = tabControl.GetTabRect(e.Index);

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            
            using (var brush = new SolidBrush(isSelected ? panelColor : bgColor))
            {
                g.FillRectangle(brush, tabBounds);
            }

            TextRenderer.DrawText(g, tabPage.Text, e.Font, tabBounds, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void OpenSettings()
        {
            using (var sf = new SettingsForm(settings, settingsPath))
            {
                if (sf.ShowDialog() == DialogResult.OK)
                {
                    this.Width = settings.WindowWidth;
                    this.Height = settings.WindowHeight;
                    this.Location = new Point(settings.WindowX, settings.WindowY);
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (settings.MinimizeToTray)
                {
                    e.Cancel = true;
                    this.Hide();
                }
                else
                {
                    e.Cancel = true;
                    this.WindowState = FormWindowState.Minimized;
                }
            }
        }

        private void RestoreWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void LoadTabs()
        {
            tabControl.TabPages.Clear();
            foreach (var tabData in settings.Tabs)
            {
                var page = new TabPage(tabData.Name)
                {
                    BackColor = bgColor
                };
                
                var flowLayout = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    AllowDrop = true,
                    Tag = tabData,
                    BackColor = bgColor,
                    Padding = new Padding(10)
                };
                flowLayout.DragEnter += FlowLayout_DragEnter;
                flowLayout.DragDrop += FlowLayout_DragDrop;
                
                // Assign form menu to flow layout as well
                flowLayout.ContextMenu = this.ContextMenu;
                
                page.Controls.Add(flowLayout);
                tabControl.TabPages.Add(page);

                foreach (var item in tabData.Items)
                {
                    AddShortcutControl(flowLayout, item, tabData);
                }
            }
        }

        private void FlowLayout_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void FlowLayout_DragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var flowLayout = (FlowLayoutPanel)sender;
            var tabData = (TabData)flowLayout.Tag;

            foreach (var file in files)
            {
                var shortcut = new ShortcutItem
                {
                    Path = file,
                    Name = Path.GetFileNameWithoutExtension(file)
                };
                if (string.IsNullOrEmpty(shortcut.Name)) shortcut.Name = Path.GetFileName(file);
                
                tabData.Items.Add(shortcut);
                AddShortcutControl(flowLayout, shortcut, tabData);
            }
            settings.Save(settingsPath);
        }

        private void AddShortcutControl(FlowLayoutPanel panel, ShortcutItem item, TabData tabData)
        {
            int iconSize = settings.IconSize;
            int btnSize = iconSize + 30;

            var btn = new Button
            {
                Width = btnSize,
                Height = btnSize + 10,
                Text = item.Name,
                TextImageRelation = TextImageRelation.ImageAboveText,
                TextAlign = ContentAlignment.BottomCenter,
                Margin = new Padding(5),
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = hoverColor;
            btn.FlatAppearance.MouseDownBackColor = panelColor;

            try
            {
                var img = IconExtractor.GetIcon(item.Path, iconSize >= 32);
                if (img != null)
                {
                    if (img.Width != iconSize || img.Height != iconSize)
                        btn.Image = new Bitmap(img, new Size(iconSize, iconSize));
                    else
                        btn.Image = img;
                }
            }
            catch { }

            btn.Click += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(item.Path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error opening file: " + ex.Message);
                }
            };

            btn.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var pt = btn.PointToScreen(e.Location);
                    NativeContextMenu.ShowContextMenu(item.Path, pt.X, pt.Y, this.Handle,
                        () => ChangeIconSize(16),
                        () => ChangeIconSize(32),
                        () => ChangeIconSize(48),
                        () => RemoveItem(panel, btn, item, tabData));
                }
            };

            panel.Controls.Add(btn);
        }

        private void ChangeIconSize(int newSize)
        {
            settings.IconSize = newSize;
            settings.Save(settingsPath);
            LoadTabs();
        }

        private void RemoveItem(FlowLayoutPanel panel, Button btn, ShortcutItem item, TabData tabData)
        {
            tabData.Items.Remove(item);
            panel.Controls.Remove(btn);
            btn.Dispose();
            settings.Save(settingsPath);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
