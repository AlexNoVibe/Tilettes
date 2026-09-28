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
        private Color bgColor;
        private Color panelColor;
        private Color hoverColor;
        private Color textColor;
        private Font mainFont = new Font("Segoe UI", 9f);

        // State for dragging
        private bool isDragging = false;
        private Point dragStartPoint;
        private Control draggingTile;
        private ShortcutItem draggingItem;

        // Edit mode state
        private bool isEditMode = false;

        // State for folder navigation
        private Dictionary<TabData, Stack<ShortcutItem>> tabNavigations = new Dictionary<TabData, Stack<ShortcutItem>>();

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse
        );

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public MainForm()
        {
            settings = Settings.Load(settingsPath);
            records = Records.Load(recordsPath);
            
            ApplyThemeColors();

            this.Text = "WinPanel";
            this.Width = settings.WindowWidth;
            this.Height = settings.WindowHeight;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(settings.WindowX, settings.WindowY);
            this.FormBorderStyle = FormBorderStyle.None;
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));
            
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
                    ApplyThemeColors();
                    LoadTabs();
                }
            }
        }

        private void ApplyThemeColors()
        {
            if (settings.IsLightTheme)
            {
                bgColor = Color.FromArgb(240, 240, 240);
                panelColor = Color.FromArgb(220, 220, 220);
                hoverColor = Color.FromArgb(200, 200, 200);
                textColor = Color.Black;
            }
            else
            {
                bgColor = Color.FromArgb(30, 30, 30);
                panelColor = Color.FromArgb(45, 45, 48);
                hoverColor = Color.FromArgb(62, 62, 66);
                textColor = Color.White;
            }
            this.BackColor = bgColor;
            this.ForeColor = textColor;
            if (tabBar != null) tabBar.BackColor = bgColor;
            if (contentPanel != null) contentPanel.BackColor = bgColor;
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
            tabBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
            foreach (Control c in contentPanel.Controls)
            {
                var pnl = c as Panel;
                if (pnl != null)
                {
                    foreach (Control pc in pnl.Controls)
                    {
                        var tc = pc as TileControl;
                        if (tc != null)
                        {
                            if (tc.IconImage != null) tc.IconImage.Dispose();
                            foreach (var img in tc.ChildIcons) if (img != null) img.Dispose();
                        }
                        pc.Dispose();
                    }
                }
                c.Dispose();
            }
            foreach (Control c in tabBar.Controls) c.Dispose();
            tabBar.Controls.Clear();
            contentPanel.Controls.Clear();
            
            int xOffset = 0;
            bool isFirst = true;

            // Window Buttons
            var closeBtn = new Button { Text = "✕", Width = 35, Height = 35, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, BackColor = bgColor, ForeColor = textColor, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10f) };
            closeBtn.FlatAppearance.BorderSize = 0; closeBtn.FlatAppearance.MouseOverBackColor = Color.Red;
            closeBtn.Click += (s, e) => this.Close();
            tabBar.Controls.Add(closeBtn);

            var maxBtn = new Button { Text = "🗖", Width = 35, Height = 35, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, BackColor = bgColor, ForeColor = textColor, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10f) };
            maxBtn.FlatAppearance.BorderSize = 0; maxBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            maxBtn.Click += (s, e) => this.WindowState = this.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            tabBar.Controls.Add(maxBtn);

            var minBtn = new Button { Text = "🗕", Width = 35, Height = 35, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, BackColor = bgColor, ForeColor = textColor, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10f) };
            minBtn.FlatAppearance.BorderSize = 0; minBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            minBtn.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            tabBar.Controls.Add(minBtn);

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

            var editBtn = new Button
            {
                Text = "✅",
                Width = 35,
                Height = 35,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = isEditMode ? panelColor : bgColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 12f)
            };
            editBtn.FlatAppearance.BorderSize = 0;
            editBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            editBtn.Click += (s, e) => {
                isEditMode = !isEditMode;
                editBtn.BackColor = isEditMode ? panelColor : bgColor;
            };
            tabBar.Controls.Add(editBtn);

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
                layoutPanel.Paint += LayoutPanel_Paint;
                layoutPanel.Resize += (s, e) => ((Panel)s).Invalidate();

                var panelMenu = new ContextMenu();
                panelMenu.Popup += (s, e) =>
                {
                    panelMenu.MenuItems[0].Enabled = isEditMode;
                };
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
                tabMenu.MenuItems.Add("Delete Tab", (s, e) => {
                    if (records.Tabs.Count > 1) {
                        var res = MessageBox.Show("Are you sure you want to delete this tab?", "Delete Tab", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (res == DialogResult.Yes) {
                            records.Tabs.Remove(tabData);
                            tabNavigations.Remove(tabData);
                            records.Save(recordsPath);
                            LoadTabs();
                        }
                    } else {
                        MessageBox.Show("Cannot remove the last tab.");
                    }
                });
                tabMenu.MenuItems.Add("Rename Tab", (s, e) => {
                    string newName = Prompt.ShowDialog("New Tab Name", "Rename Tab", tabData.Name);
                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        tabData.Name = newName;
                        records.Save(recordsPath);
                        tabBtn.Text = newName;
                    }
                });
                tabMenu.MenuItems.Add("Toggle Layout (Free / Grid 16x16)", (s, e) => {
                    tabData.IsGridLayout = !tabData.IsGridLayout;
                    records.Save(recordsPath);
                    RenderCurrentFolder(layoutPanel, tabData);
                });
                tabBtn.ContextMenu = tabMenu;

                tabBtn.Click += (s, e) =>
                {
                    foreach (Control c in contentPanel.Controls) c.Visible = false;
                    foreach (Control c in tabBar.Controls)
                    {
                        if (c == settingsBtn || c == editBtn) continue;
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
            foreach (Control c in layoutPanel.Controls)
            {
                var tc = c as TileControl;
                if (tc != null)
                {
                    if (tc.IconImage != null) tc.IconImage.Dispose();
                    foreach (var img in tc.ChildIcons) if (img != null) img.Dispose();
                }
                c.Dispose();
            }
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
                    Size = settings.DefaultItemSize
                };
                if (tabData.IsGridLayout)
                {
                    int cols = Math.Max(1, settings.GridColumns);
                    int rows = Math.Max(1, settings.GridRows);
                    int cellWidth = layoutPanel.Width / cols;
                    int cellHeight = layoutPanel.Height / rows;
                    if (cellWidth < 10) cellWidth = 20;
                    if (cellHeight < 10) cellHeight = 20;
                    folder.GridX = Math.Max(0, Math.Min(cols - folder.Size, folder.X / cellWidth));
                    folder.GridY = Math.Max(0, Math.Min(rows - folder.Size, folder.Y / cellHeight));
                }
                targetList.Add(folder);
                records.Save(recordsPath);
                RenderCurrentFolder(layoutPanel, tabData);
            }
        }

        private void LayoutPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (!isEditMode)
            {
                e.Effect = DragDropEffects.None;
                return;
            }
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
            var displayPt = new Point(pt.X - layoutPanel.DisplayRectangle.X, pt.Y - layoutPanel.DisplayRectangle.Y);
            
            var navStack = tabNavigations[tabData];
            var currentFolder = navStack.Count > 0 ? navStack.Peek() : null;
            var targetList = currentFolder != null ? currentFolder.Children : tabData.Items;

            ShortcutItem targetFolder = null;
            foreach (Control c in layoutPanel.Controls)
            {
                var tc = c as TileControl;
                if (tc != null && tc.Item.IsFolder)
                {
                    if (tc.Bounds.Contains(pt))
                    {
                        targetFolder = tc.Item;
                        break;
                    }
                }
            }

            int offset = 0;
            foreach (var file in files)
            {
                var shortcut = new ShortcutItem
                {
                    Path = file,
                    Name = Path.GetFileNameWithoutExtension(file),
                    X = displayPt.X + offset,
                    Y = displayPt.Y + offset,
                    Size = settings.DefaultItemSize
                };
                if (string.IsNullOrEmpty(shortcut.Name)) shortcut.Name = Path.GetFileName(file);
                
                if (targetFolder != null)
                {
                    if (targetFolder.Children == null) targetFolder.Children = new List<ShortcutItem>();
                    targetFolder.Children.Add(shortcut);
                }
                else
                {
                    if (tabData.IsGridLayout)
                    {
                        int cols = Math.Max(1, settings.GridColumns);
                        int rows = Math.Max(1, settings.GridRows);
                        int cellWidth = layoutPanel.Width / cols;
                        int cellHeight = layoutPanel.Height / rows;
                        if (cellWidth < 10) cellWidth = 20;
                        if (cellHeight < 10) cellHeight = 20;
                        shortcut.GridX = Math.Max(0, Math.Min(cols - shortcut.Size, shortcut.X / cellWidth));
                        shortcut.GridY = Math.Max(0, Math.Min(rows - shortcut.Size, shortcut.Y / cellHeight));
                    }
                    targetList.Add(shortcut);
                }
                offset += 20; // stagger drops
            }
            records.Save(recordsPath);
            RenderCurrentFolder(layoutPanel, tabData);
        }

        private void LayoutPanel_Paint(object sender, PaintEventArgs e)
        {
            var panel = sender as Panel;
            if (panel == null) return;
            var tabData = panel.Tag as TabData;
            if (tabData == null || !tabData.IsGridLayout) return;

            int cols = Math.Max(1, settings.GridColumns);
            int rows = Math.Max(1, settings.GridRows);

            float cellWidth = panel.Width / (float)cols;
            float cellHeight = panel.Height / (float)rows;

            Color gridColor = settings.IsLightTheme 
                ? Color.FromArgb(settings.GridTransparency, 0, 0, 0)
                : Color.FromArgb(settings.GridTransparency, 255, 255, 255);

            using (Pen gridPen = new Pen(gridColor))
            {
                gridPen.DashPattern = new float[] { 4, 8 };
                for (int i = 0; i <= cols; i++)
                {
                    e.Graphics.DrawLine(gridPen, i * cellWidth, 0, i * cellWidth, panel.Height);
                }
                for (int i = 0; i <= rows; i++)
                {
                    e.Graphics.DrawLine(gridPen, 0, i * cellHeight, panel.Width, i * cellHeight);
                }
            }
        }

        private void AddShortcutControl(Panel panel, ShortcutItem item, TabData tabData)
        {
            int cols = Math.Max(1, settings.GridColumns);
            int rows = Math.Max(1, settings.GridRows);
            int cellWidth = panel.Width / cols;
            int cellHeight = panel.Height / rows;
            if (cellWidth < 10) cellWidth = 20;
            if (cellHeight < 10) cellHeight = 20;

            int s = item.Size;
            if (s <= 0 || s > 4) s = settings.DefaultItemSize;

            int tileWidth, tileHeight, xPos, yPos;
            
            if (tabData.IsGridLayout)
            {
                tileWidth = s * cellWidth;
                tileHeight = s * cellHeight;
                
                if (item.GridX == -1) item.GridX = Math.Max(0, Math.Min(cols - s, item.X / cellWidth));
                if (item.GridY == -1) item.GridY = Math.Max(0, Math.Min(rows - s, item.Y / cellHeight));

                int col = Math.Max(0, Math.Min(cols - s, item.GridX));
                int row = Math.Max(0, Math.Min(rows - s, item.GridY));
                
                xPos = col * cellWidth;
                yPos = row * cellHeight;
            }
            else
            {
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

                if (item.Children != null)
                {
                    int maxIcons = Math.Min(9, item.Children.Count);
                    for (int i = 0; i < maxIcons; i++)
                    {
                        var child = item.Children[i];
                        Image childImg = null;
                        if (child.IsFolder)
                        {
                            var folderIcon = ShellIcon.GetFolderIcon(ShellIcon.IconSize.Large, ShellIcon.FolderType.Closed);
                            if (folderIcon != null) childImg = folderIcon.ToBitmap();
                            else childImg = SystemIcons.WinLogo.ToBitmap();
                        }
                        else
                        {
                            try { childImg = IconExtractor.GetIcon(child.Path, true); } catch { }
                        }
                        tile.ChildIcons.Add(childImg);
                    }
                }
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
                    if (!isEditMode) return;
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
                    if (!isEditMode) return;
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
                        if (!isEditMode) return;

                        var ptClient = panel.PointToClient(Cursor.Position);
                        ShortcutItem targetFolder = null;
                        foreach (Control c in panel.Controls)
                        {
                            var otherTile = c as TileControl;
                            if (otherTile != null && otherTile != tile && otherTile.Item.IsFolder)
                            {
                                if (otherTile.Bounds.Contains(ptClient))
                                {
                                    targetFolder = otherTile.Item;
                                    break;
                                }
                            }
                        }

                        if (targetFolder != null)
                        {
                            var navStack = tabNavigations[tabData];
                            var currentList = navStack.Count > 0 ? navStack.Peek().Children : tabData.Items;
                            currentList.Remove(item);
                            if (targetFolder.Children == null) targetFolder.Children = new List<ShortcutItem>();
                            targetFolder.Children.Add(item);
                        }
                        else
                        {
                            if (tabData.IsGridLayout)
                            {
                                int col = Math.Max(0, Math.Min(cols - s, (tile.Left + cellWidth/2) / cellWidth));
                                int row = Math.Max(0, Math.Min(rows - s, (tile.Top + cellHeight/2) / cellHeight));
                                item.GridX = col;
                                item.GridY = row;
                            }
                            else
                            {
                                item.X = tile.Left - panel.DisplayRectangle.X;
                                item.Y = tile.Top - panel.DisplayRectangle.Y;
                            }
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
        public List<Image> ChildIcons { get; set; }

        public TileControl()
        {
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Hand;
            ChildIcons = new List<Image>();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (IconImage != null) IconImage.Dispose();
                if (ChildIcons != null)
                {
                    foreach (var img in ChildIcons) if (img != null) img.Dispose();
                    ChildIcons.Clear();
                }
            }
            base.Dispose(disposing);
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

            if (Item.IsFolder)
            {
                using (var brush = new SolidBrush(Color.FromArgb(50, 128, 128, 128)))
                {
                    e.Graphics.FillPath(brush, path);
                }

                if (IsHovered)
                {
                    using (var hoverBrush = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                        e.Graphics.FillPath(hoverBrush, path);
                }

                if (ChildIcons != null && ChildIcons.Count > 0)
                {
                    int maxIcons = Math.Min(9, ChildIcons.Count);
                    int cols = maxIcons > 4 ? 3 : 2;
                    int rows = (int)Math.Ceiling(maxIcons / (float)cols);
                    int padding = 5;
                    int textSpace = this.Height > 40 ? 30 : 0;
                    int miniWidth = (this.Width - padding * 2) / cols;
                    int miniHeight = ((this.Height - textSpace) - padding * 2) / rows;
                    int miniSize = Math.Max(1, Math.Min(miniWidth, miniHeight) - 2);

                    for (int i = 0; i < maxIcons; i++)
                    {
                        var childImg = ChildIcons[i];
                        int c = i % cols;
                        int r = i / cols;
                        int cx = padding + c * miniWidth + (miniWidth - miniSize)/2;
                        int cy = padding + r * miniHeight + (miniHeight - miniSize)/2;

                        if (childImg != null)
                        {
                            var destRect = new Rectangle(cx, cy, miniSize, miniSize);
                            e.Graphics.DrawImage(childImg, destRect, 0, 0, childImg.Width, childImg.Height, GraphicsUnit.Pixel);
                        }
                    }
                }
            }
            else
            {
                if (IsHovered)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                }

                if (IconImage != null)
                {
                    int textSpace = this.Height > 40 ? 30 : 0;
                    int iconSize = Math.Max(1, Math.Min(this.Width, this.Height - textSpace) - 10);
                    
                    int ix = (this.Width - iconSize) / 2;
                    int iy = (this.Height - textSpace - iconSize) / 2;
                    var destRect = new Rectangle(ix, iy, iconSize, iconSize);
                    e.Graphics.DrawImage(IconImage, destRect, 0, 0, IconImage.Width, IconImage.Height, GraphicsUnit.Pixel);
                }
            }

            // Get text color from parent form's ForeColor if possible, or fallback to White/Black based on theme.
            // Since we can't easily access settings here, we just use Parent's ForeColor.
            Color tColor = this.Parent != null ? this.Parent.ForeColor : Color.White;
            using (var brush = new SolidBrush(tColor))
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
