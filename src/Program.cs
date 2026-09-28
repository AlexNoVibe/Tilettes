using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace WinPanel
{
    public class MainForm : Form
    {
        private Settings settings;
        private Records records;
        private string settingsPath = "settings.ini";
        private string recordsPath = "records.xml";
        private Panel tabBar;
        private Panel contentPanel;
        private NotifyIcon trayIcon;

        // Colors for modern dark theme
        private Color bgColor = Color.FromArgb(30, 30, 30); // #1E1E1E
        private Color panelColor = Color.FromArgb(45, 45, 48); // #2D2D30
        private Color hoverColor = Color.FromArgb(62, 62, 66);
        private Color textColor = Color.White;
        private Font mainFont = new Font("Segoe UI", 9f);

        // State for dragging
        private bool isDragging = false;
        private Point dragStartPoint;
        private Control draggingTile;
        private ShortcutItem draggingItem;

        // State for folder navigation
        private Dictionary<TabData, Stack<ShortcutItem>> tabNavigations = new Dictionary<TabData, Stack<ShortcutItem>>();

        public MainForm()
        {
            settings = Settings.Load(settingsPath);
            records = Records.Load(recordsPath);

            this.Text = "WinPanel";
            this.Width = settings.WindowWidth;
            this.Height = settings.WindowHeight;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(settings.WindowX, settings.WindowY);
            
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
            this.ResizeEnd += (s, e) => {
                if (settings != null) {
                    settings.WindowWidth = this.Width;
                    settings.WindowHeight = this.Height;
                    settings.WindowX = this.Location.X;
                    settings.WindowY = this.Location.Y;
                    settings.Save(settingsPath);
                }
                LoadTabs(); // Redraw grid if layout changed
            };

            var formMenu = new ContextMenu();
            formMenu.MenuItems.Add("Settings", (s, e) => OpenSettings());
            this.ContextMenu = formMenu;

            foreach (var tab in records.Tabs)
            {
                tabNavigations[tab] = new Stack<ShortcutItem>();
            }

            LoadTabs();
        }

        private void OpenSettings()
        {
            using (var sf = new SettingsForm(settings, settingsPath))
            {
                if (sf.ShowDialog() == DialogResult.OK)
                {
                    this.settings = Settings.Load(settingsPath);
                    this.Width = settings.WindowWidth;
                    this.Height = settings.WindowHeight;
                    this.Location = new Point(settings.WindowX, settings.WindowY);
                    LoadTabs();
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

            // Settings button
            var settingsBtn = new Button
            {
                Text = "⚙️",
                Width = 35,
                Height = 35,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 12f)
            };
            settingsBtn.FlatAppearance.BorderSize = 0;
            settingsBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            settingsBtn.Click += (s, e) => OpenSettings();
            tabBar.Controls.Add(settingsBtn);

            foreach (var tabData in records.Tabs)
            {
                if (!tabNavigations.ContainsKey(tabData)) tabNavigations[tabData] = new Stack<ShortcutItem>();

                var layoutPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    AllowDrop = true,
                    Tag = tabData,
                    BackColor = bgColor,
                    Visible = isFirst
                };
                layoutPanel.DragEnter += LayoutPanel_DragEnter;
                layoutPanel.DragDrop += LayoutPanel_DragDrop;

                var panelMenu = new ContextMenu();
                panelMenu.MenuItems.Add("Create Folder", (s, e) => CreateFolder(layoutPanel, tabData));
                panelMenu.MenuItems.Add("Settings", (s, e) => OpenSettings());
                layoutPanel.ContextMenu = panelMenu;
                
                contentPanel.Controls.Add(layoutPanel);

                RenderCurrentFolder(layoutPanel, tabData);

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
                
                var tabMenu = new ContextMenu();
                tabMenu.MenuItems.Add("Toggle Layout (Grid/Free)", (s, e) => {
                    tabData.IsGridLayout = !tabData.IsGridLayout;
                    records.Save(recordsPath);
                    RenderCurrentFolder(layoutPanel, tabData);
                });
                tabMenu.MenuItems.Add("Remove Tab", (s, e) => {
                    if (records.Tabs.Count > 1) {
                        records.Tabs.Remove(tabData);
                        tabNavigations.Remove(tabData);
                        records.Save(recordsPath);
                        LoadTabs();
                    } else {
                        MessageBox.Show("Cannot remove the last tab.");
                    }
                });
                tabBtn.ContextMenu = tabMenu;

                tabBtn.Click += (s, e) =>
                {
                    foreach (Control c in contentPanel.Controls) c.Visible = false;
                    foreach (Control c in tabBar.Controls)
                    {
                        if (c == settingsBtn) continue;
                        c.BackColor = bgColor;
                        c.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                        Button b = c as Button;
                        if (b != null) b.FlatAppearance.MouseOverBackColor = hoverColor;
                    }
                    
                    layoutPanel.Visible = true;
                    tabBtn.BackColor = panelColor;
                    tabBtn.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                    tabBtn.FlatAppearance.MouseOverBackColor = panelColor;
                };

                tabBar.Controls.Add(tabBtn);
                xOffset += tabBtn.Width;
                
                isFirst = false;
            }

            var addTabBtn = new Button
            {
                Text = "+",
                Width = 35,
                Height = 35,
                Location = new Point(xOffset, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold)
            };
            addTabBtn.FlatAppearance.BorderSize = 0;
            addTabBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            addTabBtn.FlatAppearance.MouseDownBackColor = panelColor;
            addTabBtn.Click += (s, e) => {
                string name = Prompt.ShowDialog("New Tab Name", "Add Tab");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var newTab = new TabData { Name = name, IsGridLayout = true };
                    records.Tabs.Add(newTab);
                    tabNavigations[newTab] = new Stack<ShortcutItem>();
                    records.Save(recordsPath);
                    LoadTabs();
                }
            };
            tabBar.Controls.Add(addTabBtn);
        }

        private void RenderCurrentFolder(Panel layoutPanel, TabData tabData)
        {
            layoutPanel.Controls.Clear();
            var navStack = tabNavigations[tabData];
            List<ShortcutItem> itemsToRender = navStack.Count > 0 ? navStack.Peek().Children : tabData.Items;

            if (navStack.Count > 0)
            {
                var backBtn = new Button
                {
                    Text = "<- Back",
                    Location = new Point(10, 10),
                    Width = 80,
                    Height = 30,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = panelColor,
                    ForeColor = textColor,
                    Cursor = Cursors.Hand
                };
                backBtn.FlatAppearance.BorderSize = 0;
                backBtn.Click += (s, e) =>
                {
                    navStack.Pop();
                    RenderCurrentFolder(layoutPanel, tabData);
                };
                layoutPanel.Controls.Add(backBtn);
            }

            // Layout button overlay inside the panel
            var layoutBtn = new Button
            {
                Text = tabData.IsGridLayout ? "Layout: Grid (16x20)" : "Layout: Free",
                Location = new Point(layoutPanel.Width - 150, 10),
                Width = 130,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = Color.LightGray,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            layoutBtn.FlatAppearance.BorderSize = 1;
            layoutBtn.FlatAppearance.BorderColor = Color.Gray;
            layoutBtn.Click += (s, e) => {
                tabData.IsGridLayout = !tabData.IsGridLayout;
                records.Save(recordsPath);
                RenderCurrentFolder(layoutPanel, tabData);
            };
            layoutPanel.Controls.Add(layoutBtn);

            foreach (var item in itemsToRender)
            {
                AddShortcutControl(layoutPanel, item, tabData);
            }
        }

        private void CreateFolder(Panel layoutPanel, TabData tabData)
        {
            string name = Prompt.ShowDialog("Folder Name", "Create Folder");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var navStack = tabNavigations[tabData];
                var targetList = navStack.Count > 0 ? navStack.Peek().Children : tabData.Items;
                
                var pt = layoutPanel.PointToClient(Cursor.Position);
                pt.X -= layoutPanel.DisplayRectangle.X;
                pt.Y -= layoutPanel.DisplayRectangle.Y;
                
                var folder = new ShortcutItem
                {
                    Name = name,
                    IsFolder = true,
                    X = pt.X,
                    Y = pt.Y,
                    Size = 2
                };
                targetList.Add(folder);
                records.Save(recordsPath);
                RenderCurrentFolder(layoutPanel, tabData);
            }
        }

        private void LayoutPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void LayoutPanel_DragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var layoutPanel = (Panel)sender;
            var tabData = (TabData)layoutPanel.Tag;

            var pt = layoutPanel.PointToClient(new Point(e.X, e.Y));
            pt.X -= layoutPanel.DisplayRectangle.X;
            pt.Y -= layoutPanel.DisplayRectangle.Y;
            var navStack = tabNavigations[tabData];
            var targetList = navStack.Count > 0 ? navStack.Peek().Children : tabData.Items;

            int offset = 0;
            foreach (var file in files)
            {
                var shortcut = new ShortcutItem
                {
                    Path = file,
                    Name = Path.GetFileNameWithoutExtension(file),
                    X = pt.X + offset,
                    Y = pt.Y + offset,
                    Size = 2
                };
                if (string.IsNullOrEmpty(shortcut.Name)) shortcut.Name = Path.GetFileName(file);
                
                targetList.Add(shortcut);
                AddShortcutControl(layoutPanel, shortcut, tabData);
                offset += 20; // stagger drops
            }
            records.Save(recordsPath);
            RenderCurrentFolder(layoutPanel, tabData); // Re-render to apply snapping
        }

        private void AddShortcutControl(Panel panel, ShortcutItem item, TabData tabData)
        {
            int cellWidth = panel.Width / 16;
            int cellHeight = panel.Height / 20;
            if (cellWidth < 10) cellWidth = 20;
            if (cellHeight < 10) cellHeight = 20;

            int s = item.Size;
            if (s <= 0 || s > 4) s = 2; // Default 2x2

            int tileWidth, tileHeight, xPos, yPos;
            
            if (tabData.IsGridLayout)
            {
                tileWidth = s * cellWidth;
                tileHeight = s * cellHeight;
                
                int col = Math.Max(0, Math.Min(16 - s, item.X / cellWidth));
                int row = Math.Max(0, Math.Min(20 - s, item.Y / cellHeight));
                
                xPos = col * cellWidth;
                yPos = row * cellHeight;
            }
            else
            {
                // Free layout: base size on 40x40 unit
                tileWidth = s * 40;
                tileHeight = s * 40;
                xPos = item.X;
                yPos = item.Y;
            }

            var tile = new TileControl
            {
                Item = item,
                TabData = tabData,
                Width = tileWidth,
                Height = tileHeight,
                Location = new Point(xPos, yPos)
            };

            Image iconImg = null;
            if (item.IsFolder)
            {
                try
                {
                    if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath))
                    {
                        if (item.CustomIconPath.ToLower().EndsWith(".exe") || item.CustomIconPath.ToLower().EndsWith(".ico"))
                            iconImg = IconExtractor.GetIcon(item.CustomIconPath, true);
                        else
                            iconImg = Image.FromFile(item.CustomIconPath);
                    }
                }
                catch { }
            }
            else
            {
                try
                {
                    if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath))
                    {
                        if (item.CustomIconPath.ToLower().EndsWith(".exe") || item.CustomIconPath.ToLower().EndsWith(".ico"))
                            iconImg = IconExtractor.GetIcon(item.CustomIconPath, true);
                        else
                            iconImg = Image.FromFile(item.CustomIconPath);
                    }
                    else
                    {
                        iconImg = IconExtractor.GetIcon(item.Path, true);
                    }
                }
                catch { }
                if (iconImg == null) iconImg = SystemIcons.Application.ToBitmap();
            }
            tile.IconImage = iconImg;

            bool dragFired = false;
            tile.MouseDown += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDragging = true;
                    dragFired = false;
                    dragStartPoint = e.Location;
                    draggingTile = tile;
                    draggingItem = item;
                    tile.BringToFront();
                }
                else if (e.Button == MouseButtons.Right)
                {
                    var pt = tile.PointToScreen(e.Location);
                    if (item.IsFolder)
                    {
                        var fMenu = new ContextMenu();
                        fMenu.MenuItems.Add("Rename", (s2, e2) => RenameItem(item, tile));
                        fMenu.MenuItems.Add("Change Icon", (s2, e2) => ChangeItemIcon(item, tile));
                        fMenu.MenuItems.Add("Remove", (s2, e2) => RemoveItem(panel, tile, item, tabData));
                        fMenu.Show(tile, e.Location);
                    }
                    else
                    {
                        NativeContextMenu.ShowContextMenu(item.Path, pt.X, pt.Y, this.Handle,
                            () => ChangeIconSize(item, tile, panel, tabData, 1),
                            () => ChangeIconSize(item, tile, panel, tabData, 2),
                            () => ChangeIconSize(item, tile, panel, tabData, 3),
                            () => ChangeIconSize(item, tile, panel, tabData, 4),
                            () => RemoveItem(panel, tile, item, tabData),
                            () => RenameItem(item, tile),
                            () => ChangeItemIcon(item, tile));
                    }
                }
            };

            tile.MouseMove += (sender, e) =>
            {
                if (isDragging && draggingTile == tile)
                {
                    if (Math.Abs(e.X - dragStartPoint.X) > 3 || Math.Abs(e.Y - dragStartPoint.Y) > 3)
                    {
                        dragFired = true;
                    }
                    if (dragFired)
                    {
                        tile.Left = tile.Left + e.X - dragStartPoint.X;
                        tile.Top = tile.Top + e.Y - dragStartPoint.Y;
                    }
                }
            };

            tile.MouseUp += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left && isDragging && draggingTile == tile)
                {
                    isDragging = false;
                    if (dragFired)
                    {
                        if (tabData.IsGridLayout)
                        {
                            int col = Math.Max(0, Math.Min(16 - s, (tile.Left + cellWidth/2) / cellWidth));
                            int row = Math.Max(0, Math.Min(20 - s, (tile.Top + cellHeight/2) / cellHeight));
                            item.X = col * cellWidth;
                            item.Y = row * cellHeight;
                        }
                        else
                        {
                            item.X = tile.Left;
                            item.Y = tile.Top;
                        }
                        records.Save(recordsPath);
                        RenderCurrentFolder(panel, tabData);
                    }
                    else
                    {
                        if (item.IsFolder)
                        {
                            tabNavigations[tabData].Push(item);
                            RenderCurrentFolder(panel, tabData);
                        }
                        else
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
                    }
                    draggingTile = null;
                    draggingItem = null;
                }
            };

            panel.Controls.Add(tile);
        }

        private void RenameItem(ShortcutItem item, TileControl tile)
        {
            string newName = Prompt.ShowDialog("New Name", "Rename", item.Name);
            if (!string.IsNullOrWhiteSpace(newName))
            {
                item.Name = newName;
                records.Save(recordsPath);
                tile.Invalidate();
            }
        }

        private void ChangeItemIcon(ShortcutItem item, TileControl tile)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Icon Files (*.ico;*.exe)|*.ico;*.exe|All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    item.CustomIconPath = ofd.FileName;
                    records.Save(recordsPath);
                    if (item.CustomIconPath.ToLower().EndsWith(".exe") || item.CustomIconPath.ToLower().EndsWith(".ico"))
                        tile.IconImage = IconExtractor.GetIcon(item.CustomIconPath, true);
                    else
                    {
                        try { tile.IconImage = Image.FromFile(item.CustomIconPath); }
                        catch { }
                    }
                    tile.Invalidate();
                }
            }
        }

        private void ChangeIconSize(ShortcutItem item, TileControl tile, Panel panel, TabData tabData, int newSize)
        {
            item.Size = newSize;
            records.Save(recordsPath);
            RenderCurrentFolder(panel, tabData);
        }

        private void RemoveItem(Panel panel, Control tile, ShortcutItem item, TabData tabData)
        {
            var navStack = tabNavigations[tabData];
            var targetList = navStack.Count > 0 ? navStack.Peek().Children : tabData.Items;
            targetList.Remove(item);
            panel.Controls.Remove(tile);
            tile.Dispose();
            records.Save(recordsPath);
        }
    }

    public class TileControl : Control
    {
        public ShortcutItem Item { get; set; }
        public TabData TabData { get; set; }
        private Color bgColor = Color.FromArgb(45, 45, 48);
        private Color hoverColor = Color.FromArgb(62, 62, 66);
        public bool IsHovered { get; set; }
        public Image IconImage { get; set; }

        public TileControl()
        {
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            IsHovered = true;
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            IsHovered = false;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            int radius = 15;
            var path = GetRoundRectangle(rect, radius);

            Color currentBg = IsHovered ? hoverColor : bgColor;

            if (Item.IsFolder)
            {
                using (var brush = new SolidBrush(Color.FromArgb(128, currentBg)))
                {
                    e.Graphics.FillPath(brush, path);
                }

                if (Item.Children != null && Item.Children.Count > 0)
                {
                    int maxIcons = Math.Min(9, Item.Children.Count);
                    int cols = maxIcons > 4 ? 3 : 2;
                    int rows = (int)Math.Ceiling(maxIcons / (float)cols);
                    int padding = 10;
                    int miniWidth = (this.Width - padding * 2) / cols;
                    int miniHeight = ((this.Height - 25) - padding * 2) / rows;
                    int miniSize = Math.Min(miniWidth, miniHeight) - 4;

                    for (int i = 0; i < maxIcons; i++)
                    {
                        var child = Item.Children[i];
                        int c = i % cols;
                        int r = i / cols;
                        int cx = padding + c * miniWidth + (miniWidth - miniSize)/2;
                        int cy = padding + r * miniHeight + (miniHeight - miniSize)/2;

                        Image childImg = null;
                        if (child.IsFolder)
                        {
                            var folderIcon = ShellIcon.GetFolderIcon(ShellIcon.IconSize.Large, ShellIcon.FolderType.Closed);
                            if (folderIcon != null) childImg = folderIcon.ToBitmap();
                            else childImg = SystemIcons.WinLogo.ToBitmap();
                        }
                        else childImg = IconExtractor.GetIcon(child.Path, true);
                        
                        if (childImg != null)
                        {
                            e.Graphics.DrawImage(childImg, new Rectangle(cx, cy, miniSize, miniSize));
                        }
                    }
                }
            }
            else
            {
                using (var brush = new SolidBrush(currentBg))
                {
                    e.Graphics.FillPath(brush, path);
                }

                if (IconImage != null)
                {
                    int iconSize = Math.Min(this.Width, this.Height - 25) - 20;
                    if (iconSize > 0)
                    {
                        int ix = (this.Width - iconSize) / 2;
                        int iy = (this.Height - 25 - iconSize) / 2;
                        e.Graphics.DrawImage(IconImage, new Rectangle(ix, iy, iconSize, iconSize));
                    }
                }
            }

            using (var brush = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", 9f))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                Rectangle textRect = new Rectangle(5, this.Height - 25, this.Width - 10, 20);
                e.Graphics.DrawString(Item.Name, font, brush, textRect, sf);
            }
        }

        private GraphicsPath GetRoundRectangle(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            if (bounds.Width < diameter || bounds.Height < diameter)
            {
                path.AddRectangle(bounds);
                return path;
            }
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public static class Prompt
    {
        public static string ShowDialog(string text, string caption, string defaultValue = "")
        {
            Form prompt = new Form()
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
            Label textLabel = new Label() { Left = 20, Top = 20, Text = text, Width = 350 };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 350, Text = defaultValue, BackColor = Color.FromArgb(30,30,30), ForeColor = Color.White };
            Button confirmation = new Button() { Text = "Ok", Left = 270, Top = 80, Width = 100, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(62, 62, 66) };
            confirmation.FlatAppearance.BorderSize = 0;
            
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
    }

    public static class ShellIcon
    {
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        public const uint SHGFI_OPENICON = 0x000000002;
        public const uint SHGFI_SMALLICON = 0x000000001;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        public enum IconSize { Large = 0, Small = 1 }
        public enum FolderType { Closed = 0, Open = 1 }

        public static Icon GetFolderIcon(IconSize size, FolderType folderType)
        {
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
            if (folderType == FolderType.Open) flags |= SHGFI_OPENICON;
            if (size == IconSize.Small) flags |= SHGFI_SMALLICON;
            else flags |= SHGFI_LARGEICON;

            SHFILEINFO shfi = new SHFILEINFO();
            SHGetFileInfo("dummy", FILE_ATTRIBUTE_DIRECTORY, ref shfi, (uint)System.Runtime.InteropServices.Marshal.SizeOf(shfi), flags);

            if (shfi.hIcon != IntPtr.Zero)
            {
                return (Icon)Icon.FromHandle(shfi.hIcon).Clone();
            }
            return null;
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
