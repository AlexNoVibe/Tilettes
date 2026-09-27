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
        private Panel tabBar;
        private Panel contentPanel;
        private NotifyIcon trayIcon;

        // Colors for modern dark theme
        private Color bgColor = Color.FromArgb(30, 30, 30); // #1E1E1E
        private Color panelColor = Color.FromArgb(45, 45, 48); // #2D2D30
        private Color hoverColor = Color.FromArgb(62, 62, 66);
        private Color textColor = Color.White;
        private Font mainFont = new Font("Segoe UI", 9f);

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
            this.Font = mainFont;

            tabBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = bgColor
            };
            
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bgColor
            };
            
            this.Controls.Add(contentPanel);
            this.Controls.Add(tabBar);

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

        private void OpenSettings()
        {
            using (var sf = new SettingsForm(settings, settingsPath))
            {
                if (sf.ShowDialog() == DialogResult.OK)
                {
                    this.Width = settings.WindowWidth;
                    this.Height = settings.WindowHeight;
                    this.Location = new Point(settings.WindowX, settings.WindowY);
                    LoadTabs(); // Reload to apply icon size changes if any
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
            tabBar.Controls.Clear();
            contentPanel.Controls.Clear();
            
            int xOffset = 0;
            bool isFirst = true;

            foreach (var tabData in settings.Tabs)
            {
                var flowLayout = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    AllowDrop = true,
                    Tag = tabData,
                    BackColor = bgColor,
                    Padding = new Padding(15),
                    Visible = isFirst
                };
                flowLayout.DragEnter += FlowLayout_DragEnter;
                flowLayout.DragDrop += FlowLayout_DragDrop;
                
                // Assign form menu to flow layout as well
                flowLayout.ContextMenu = this.ContextMenu;
                
                contentPanel.Controls.Add(flowLayout);

                foreach (var item in tabData.Items)
                {
                    AddShortcutControl(flowLayout, item, tabData);
                }

                // Custom Tab Button
                var tabBtn = new Button
                {
                    Text = tabData.Name,
                    Width = 100,
                    Height = 35,
                    Location = new Point(xOffset, 0),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = isFirst ? panelColor : bgColor,
                    ForeColor = textColor,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI", 9f, isFirst ? FontStyle.Bold : FontStyle.Regular)
                };
                tabBtn.FlatAppearance.BorderSize = 0;
                tabBtn.FlatAppearance.MouseOverBackColor = isFirst ? panelColor : hoverColor;
                tabBtn.FlatAppearance.MouseDownBackColor = panelColor;
                
                tabBtn.Click += (s, e) =>
                {
                    foreach (Control c in contentPanel.Controls) c.Visible = false;
                    foreach (Control c in tabBar.Controls)
                    {
                        c.BackColor = bgColor;
                        c.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                        Button b = c as Button;
                        if (b != null) b.FlatAppearance.MouseOverBackColor = hoverColor;
                    }
                    
                    flowLayout.Visible = true;
                    tabBtn.BackColor = panelColor;
                    tabBtn.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                    tabBtn.FlatAppearance.MouseOverBackColor = panelColor;
                };

                tabBar.Controls.Add(tabBtn);
                xOffset += tabBtn.Width;
                
                isFirst = false;
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
            int tileWidth = iconSize + 60;
            int tileHeight = iconSize + 40;

            var tile = new Panel
            {
                Width = tileWidth,
                Height = tileHeight,
                Margin = new Padding(8),
                BackColor = panelColor,
                Cursor = Cursors.Hand
            };

            var pic = new PictureBox
            {
                Width = iconSize,
                Height = iconSize,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Enabled = false,
                Location = new Point((tileWidth - iconSize) / 2, 10)
            };

            try
            {
                var img = IconExtractor.GetIcon(item.Path, iconSize >= 32);
                if (img != null)
                {
                    pic.Image = img;
                }
            }
            catch { }

            var lbl = new Label
            {
                Text = item.Name,
                ForeColor = textColor,
                AutoSize = false,
                TextAlign = ContentAlignment.TopCenter,
                Width = tileWidth - 10,
                Height = 25,
                Location = new Point(5, iconSize + 15),
                Enabled = false,
                AutoEllipsis = true
            };

            tile.Controls.Add(pic);
            tile.Controls.Add(lbl);

            // Hover effects
            tile.MouseEnter += (s, e) => tile.BackColor = hoverColor;
            tile.MouseLeave += (s, e) => tile.BackColor = panelColor;
            
            // Allow triggering mouse events even on children by passing them through, or disable children.
            // Since we disabled the children (Enabled=false), the Panel receives the clicks and mouse events!

            tile.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(item.Path);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error opening file: " + ex.Message);
                    }
                }
            };

            tile.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var pt = tile.PointToScreen(e.Location);
                    NativeContextMenu.ShowContextMenu(item.Path, pt.X, pt.Y, this.Handle,
                        () => ChangeIconSize(16),
                        () => ChangeIconSize(32),
                        () => ChangeIconSize(48),
                        () => RemoveItem(panel, tile, item, tabData));
                }
            };

            panel.Controls.Add(tile);
        }

        private void ChangeIconSize(int newSize)
        {
            settings.IconSize = newSize;
            settings.Save(settingsPath);
            LoadTabs();
        }

        private void RemoveItem(FlowLayoutPanel panel, Panel tile, ShortcutItem item, TabData tabData)
        {
            tabData.Items.Remove(item);
            panel.Controls.Remove(tile);
            tile.Dispose();
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
