using System;
using System.Collections.Generic;
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

        // State for dragging
        private bool isDragging = false;
        private Point dragStartPoint;
        private Panel draggingTile;
        private ShortcutItem draggingItem;

        // State for folder navigation
        // Map TabData to its current navigation stack
        private Dictionary<TabData, Stack<ShortcutItem>> tabNavigations = new Dictionary<TabData, Stack<ShortcutItem>>();

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

            foreach (var tab in settings.Tabs)
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
                    
                    // Synchronize tabNavigations
                    var toRemove = new List<TabData>();
                    foreach (var k in tabNavigations.Keys) if (!settings.Tabs.Contains(k)) toRemove.Add(k);
                    foreach (var k in toRemove) tabNavigations.Remove(k);
                    foreach (var tab in settings.Tabs) if (!tabNavigations.ContainsKey(tab)) tabNavigations[tab] = new Stack<ShortcutItem>();

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

            foreach (var tabData in settings.Tabs)
            {
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
                
                // Tab right-click menu
                var tabMenu = new ContextMenu();
                tabMenu.MenuItems.Add("Remove Tab", (s, e) => {
                    if (settings.Tabs.Count > 1) {
                        settings.Tabs.Remove(tabData);
                        tabNavigations.Remove(tabData);
                        settings.Save(settingsPath);
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
                    var newTab = new TabData { Name = name };
                    settings.Tabs.Add(newTab);
                    tabNavigations[newTab] = new Stack<ShortcutItem>();
                    settings.Save(settingsPath);
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
                    Y = pt.Y
                };
                targetList.Add(folder);
                settings.Save(settingsPath);
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
                    Y = pt.Y + offset
                };
                if (string.IsNullOrEmpty(shortcut.Name)) shortcut.Name = Path.GetFileName(file);
                
                targetList.Add(shortcut);
                AddShortcutControl(layoutPanel, shortcut, tabData);
                offset += 20; // stagger drops
            }
            settings.Save(settingsPath);
        }

        private void AddShortcutControl(Panel panel, ShortcutItem item, TabData tabData)
        {
            int iconSize = settings.IconSize;
            int tileWidth = iconSize + 60;
            int tileHeight = iconSize + 40;

            var tile = new Panel
            {
                Width = tileWidth,
                Height = tileHeight,
                BackColor = panelColor,
                Cursor = Cursors.Hand,
                Location = new Point(item.X, item.Y)
            };

            var pic = new PictureBox
            {
                Width = iconSize,
                Height = iconSize,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Enabled = false,
                Location = new Point((tileWidth - iconSize) / 2, 10)
            };

            if (item.IsFolder)
            {
                try
                {
                    if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath))
                    {
                        if (item.CustomIconPath.ToLower().EndsWith(".exe") || item.CustomIconPath.ToLower().EndsWith(".ico"))
                            pic.Image = IconExtractor.GetIcon(item.CustomIconPath, iconSize >= 32);
                        else
                            pic.Image = Image.FromFile(item.CustomIconPath);
                    }
                    else
                    {
                        // Use default folder icon, fallback to a standard icon if unavailable.
                        Icon folderIcon = ShellIcon.GetFolderIcon(ShellIcon.IconSize.Large, ShellIcon.FolderType.Closed);
                        if (folderIcon != null)
                            pic.Image = folderIcon.ToBitmap();
                        else
                            pic.Image = SystemIcons.WinLogo.ToBitmap();
                    }
                }
                catch { pic.Image = SystemIcons.WinLogo.ToBitmap(); }
            }
            else
            {
                try
                {
                    if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath))
                    {
                        if (item.CustomIconPath.ToLower().EndsWith(".exe") || item.CustomIconPath.ToLower().EndsWith(".ico"))
                            pic.Image = IconExtractor.GetIcon(item.CustomIconPath, iconSize >= 32);
                        else
                            pic.Image = Image.FromFile(item.CustomIconPath);
                    }
                    else
                    {
                        var img = IconExtractor.GetIcon(item.Path, iconSize >= 32);
                        if (img != null) pic.Image = img;
                    }
                }
                catch { }
                if (pic.Image == null) pic.Image = SystemIcons.Application.ToBitmap();
            }

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

            // Drag and drop / click logic
            bool dragFired = false;
            
            tile.MouseDown += (s, e) =>
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
                        fMenu.MenuItems.Add("Rename", (s2, e2) => RenameItem(item, lbl));
                        fMenu.MenuItems.Add("Change Icon", (s2, e2) => ChangeItemIcon(item, pic));
                        fMenu.MenuItems.Add("Remove", (s2, e2) => RemoveItem(panel, tile, item, tabData));
                        fMenu.Show(tile, e.Location);
                    }
                    else
                    {
                        NativeContextMenu.ShowContextMenu(item.Path, pt.X, pt.Y, this.Handle,
                            () => ChangeIconSize(16),
                            () => ChangeIconSize(32),
                            () => ChangeIconSize(48),
                            () => RemoveItem(panel, tile, item, tabData),
                            () => RenameItem(item, lbl),
                            () => ChangeItemIcon(item, pic));
                    }
                }
            };

            tile.MouseMove += (s, e) =>
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

            tile.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && isDragging && draggingTile == tile)
                {
                    isDragging = false;
                    if (dragFired)
                    {
                        draggingItem.X = tile.Left;
                        draggingItem.Y = tile.Top;
                        settings.Save(settingsPath);
                    }
                    else
                    {
                        // Clicked
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

        private void RenameItem(ShortcutItem item, Label lbl)
        {
            string newName = Prompt.ShowDialog("New Name", "Rename", item.Name);
            if (!string.IsNullOrWhiteSpace(newName))
            {
                item.Name = newName;
                lbl.Text = newName;
                settings.Save(settingsPath);
            }
        }

        private void ChangeItemIcon(ShortcutItem item, PictureBox pic)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Icon Files (*.ico;*.exe)|*.ico;*.exe|All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    item.CustomIconPath = ofd.FileName;
                    settings.Save(settingsPath);
                    if (item.CustomIconPath.ToLower().EndsWith(".exe") || item.CustomIconPath.ToLower().EndsWith(".ico"))
                        pic.Image = IconExtractor.GetIcon(item.CustomIconPath, settings.IconSize >= 32);
                    else
                    {
                        try { pic.Image = Image.FromFile(item.CustomIconPath); }
                        catch { }
                    }
                }
            }
        }

        private void ChangeIconSize(int newSize)
        {
            settings.IconSize = newSize;
            settings.Save(settingsPath);
            LoadTabs();
        }

        private void RemoveItem(Panel panel, Panel tile, ShortcutItem item, TabData tabData)
        {
            var navStack = tabNavigations[tabData];
            var targetList = navStack.Count > 0 ? navStack.Peek().Children : tabData.Items;
            targetList.Remove(item);
            panel.Controls.Remove(tile);
            tile.Dispose();
            settings.Save(settingsPath);
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

    // Helper for shell icons since standard C# doesn't provide easy folder icons
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
