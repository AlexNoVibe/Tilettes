using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WinPanel
{
    public class MainForm : Form
    {
        // Shared access for tile/popup rendering (fonts, colors, icon scale)
        public static Settings CurrentSettings;

        private Settings settings;
        private Records records;
        private MiniExplorerForm miniExplorer;
        private string settingsPath = "settings.ini";
        private string recordsPath = "records.xml";
        private Panel topPanel;
        private Panel tabBar;
        private Panel rightPanel;
        private Panel contentPanel;
        private NotifyIcon trayIcon;
        private Icon appIcon;

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

        // Deferred (non-blocking) icon loading
        private readonly Queue<Action> iconQueue = new Queue<Action>();
        private System.Windows.Forms.Timer iconTimer;

        // Double-launch protection (a double-click must not start an item twice)
        private static string lastLaunchKey;
        private static DateTime lastLaunchTime = DateTime.MinValue;

        // Tab drag state
        private Button dragTabBtn;
        private bool tabDragMoved;

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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Global hotkey ("show window") — the combination is configurable in settings.
        private const int HotkeyId = 0x5711;
        private const int WM_HOTKEY = 0x0312;
        private bool hotkeyRegistered = false;

        public MainForm()
        {
            settings = Settings.Load(settingsPath);
            records = Records.Load(recordsPath);
            CurrentSettings = settings;

            // Copy user shortcuts/icons that live outside the panel folder into <exe>\ico
            // so they are not lost when the originals are moved or deleted.
            ConsolidateAllRecords();

            // File-type rules (icons and "open with" per extension).
            FileTypes.Load(FileTypes.DefaultFilePath);

            isEditMode = settings.EditMode;

            ApplyThemeColors();

            this.Text = "WinPanel";
            this.Width = settings.StartupWidth;
            this.Height = settings.StartupHeight;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(settings.WindowX, settings.WindowY);
            this.FormBorderStyle = FormBorderStyle.None;
            this.MinimumSize = new Size(300, 200);
            UpdateWindowRegion();

            this.BackColor = bgColor;
            this.ForeColor = textColor;
            mainFont = Settings.MakeFont(settings.FontUiName, settings.FontUiSize);
            this.Font = mainFont;

            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = bgColor
            };

            tabBar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bgColor
            };

            rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 105,
                BackColor = bgColor
            };

            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bgColor
            };

            this.Controls.Add(contentPanel);
            this.Controls.Add(topPanel);
            topPanel.Controls.Add(tabBar);
            topPanel.Controls.Add(rightPanel);

            this.Resize += (s, e) => UpdateWindowRegion();

            trayIcon = new NotifyIcon();
            trayIcon.Text = "WinPanel";
            appIcon = CreateAppIcon();
            if (appIcon != null) trayIcon.Icon = appIcon;
            trayIcon.DoubleClick += (s, e) => RestoreWindow();

            var trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Restore", (s, e) => RestoreWindow());
            trayMenu.MenuItems.Add("Settings", (s, e) => OpenSettings());
            trayMenu.MenuItems.Add("Exit", (s, e) => { Application.Exit(); });
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;

            this.FormClosing += MainForm_FormClosing;
            this.FormClosed += (s, e) =>
            {
                if (hotkeyRegistered)
                {
                    UnregisterHotKey(this.Handle, HotkeyId);
                    hotkeyRegistered = false;
                }
                if (iconTimer != null)
                {
                    iconTimer.Stop();
                    iconTimer.Dispose();
                }
                trayIcon.Visible = false;
                trayIcon.Dispose();
                if (appIcon != null) appIcon.Dispose();
            };
            this.ResizeEnd += (s, e) => {
                if (settings != null) {
                    // Only the position is remembered; the window always starts with
                    // StartupWidth x StartupHeight from settings.
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

            ApplyHotkey();
        }

        private void UpdateWindowRegion()
        {
            if (this.WindowState == FormWindowState.Maximized)
                this.Region = null;
            else
                this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));
        }

        // Edge resizing for the borderless window
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                RestoreWindow();
                return;
            }
            const int WM_NCHITTEST = 0x84;
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if ((int)m.Result == 1) // HTCLIENT
                {
                    int x = (short)((long)m.LParam & 0xFFFF);
                    int y = (short)(((long)m.LParam >> 16) & 0xFFFF);
                    Point p = this.PointToClient(new Point(x, y));
                    const int grip = 8;
                    const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
                              HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
                    bool left = p.X < grip;
                    bool right = p.X > Width - grip;
                    bool top = p.Y < grip;
                    bool bottom = p.Y > Height - grip;
                    if (top && left) m.Result = (IntPtr)HTTOPLEFT;
                    else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
                    else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (left) m.Result = (IntPtr)HTLEFT;
                    else if (right) m.Result = (IntPtr)HTRIGHT;
                    else if (top) m.Result = (IntPtr)HTTOP;
                    else if (bottom) m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }
            base.WndProc(ref m);
        }

        // Program-drawn tray icon: rounded gradient square with a "W"
        private Icon CreateAppIcon()
        {
            try
            {
                using (var bmp = new Bitmap(32, 32))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        var rect = new Rectangle(1, 1, 30, 30);
                        using (var path = GetRoundedRectPath(rect, 8))
                        using (var brush = new LinearGradientBrush(rect, Color.FromArgb(0, 120, 215), Color.FromArgb(130, 80, 255), 45f))
                        {
                            g.FillPath(brush, path);
                        }
                        using (var font = new Font("Segoe UI", 15f, FontStyle.Bold))
                        using (var brush = new SolidBrush(Color.White))
                        {
                            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString("W", font, brush, new RectangleF(0, -1, 32, 32), sf);
                        }
                    }
                    IntPtr hIcon = bmp.GetHicon();
                    Icon owned = (Icon)Icon.FromHandle(hIcon).Clone();
                    DestroyIcon(hIcon);
                    return owned;
                }
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private GraphicsPath GetRoundedRectPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (bounds.Width < d || bounds.Height < d)
            {
                path.AddRectangle(bounds);
                return path;
            }
            Rectangle arc = new Rectangle(bounds.Location, new Size(d, d));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private void OpenSettings()
        {
            Rectangle liveRect = WindowState == FormWindowState.Normal ? new Rectangle(this.Location, this.Size) : this.RestoreBounds;
            using (var sf = new SettingsForm(settings, settingsPath, liveRect))
            {
                if (sf.ShowDialog() == DialogResult.OK)
                {
                    this.settings = Settings.Load(settingsPath);
                    CurrentSettings = this.settings;
                    this.Width = settings.StartupWidth;
                    this.Height = settings.StartupHeight;
                    this.Location = new Point(settings.WindowX, settings.WindowY);
                    ApplyThemeColors();
                    mainFont = Settings.MakeFont(settings.FontUiName, settings.FontUiSize);
                    this.Font = mainFont;
                    ApplyHotkey();
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
            if (topPanel != null) topPanel.BackColor = bgColor;
            if (tabBar != null) tabBar.BackColor = bgColor;
            if (rightPanel != null) rightPanel.BackColor = bgColor;
            if (contentPanel != null) contentPanel.BackColor = bgColor;

            // Custom UI text color overrides the theme color
            Color uiOverride = Settings.ParseColor(settings != null ? settings.FontUiColor : "", Color.Empty);
            if (uiOverride != Color.Empty)
            {
                textColor = uiOverride;
                this.ForeColor = textColor;
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

        // (Re)registers the global "show window" hotkey from settings.
        private void ApplyHotkey()
        {
            if (hotkeyRegistered)
            {
                UnregisterHotKey(this.Handle, HotkeyId);
                hotkeyRegistered = false;
            }
            uint mods, vk;
            if (!ParseHotkey(settings != null ? settings.HotkeyShow : null, out mods, out vk)) return;
            hotkeyRegistered = RegisterHotKey(this.Handle, HotkeyId, mods | 0x4000 /* MOD_NOREPEAT */, vk);
            if (!hotkeyRegistered && trayIcon != null)
                trayIcon.ShowBalloonTip(2500, "WinPanel", "Hotkey " + settings.HotkeyShow + " is already in use by another program.", ToolTipIcon.Warning);
        }

        private static bool ParseHotkey(string hotkey, out uint mods, out uint vk)
        {
            mods = 0;
            vk = 0;
            if (string.IsNullOrWhiteSpace(hotkey) || hotkey.Equals("None", StringComparison.OrdinalIgnoreCase)) return false;
            foreach (var part in hotkey.Split('+'))
            {
                string p = part.Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) mods |= 0x2;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) mods |= 0x1;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) mods |= 0x4;
                else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase)) mods |= 0x8;
                else if (p.Length == 1)
                {
                    char c = char.ToUpperInvariant(p[0]);
                    if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) vk = c;
                }
            }
            return vk != 0;
        }

        // ---------- Non-blocking icon loading ----------
        // Icons are extracted one-by-one between UI messages instead of blocking the
        // startup: the window appears immediately and the tiles fill in as it goes.
        private void EnqueueIconTask(Action task)
        {
            iconQueue.Enqueue(task);
            if (iconTimer == null)
            {
                iconTimer = new System.Windows.Forms.Timer();
                iconTimer.Interval = 15;
                iconTimer.Tick += (s, e) => ProcessNextIconTask();
            }
            if (!iconTimer.Enabled) iconTimer.Start();
        }

        private void ProcessNextIconTask()
        {
            if (this.IsDisposed)
            {
                iconQueue.Clear();
                iconTimer.Stop();
                return;
            }
            if (iconQueue.Count == 0)
            {
                iconTimer.Stop();
                return;
            }
            try { iconQueue.Dequeue()(); } catch { }
        }

        private static Image LoadIconForItem(ShortcutItem item)
        {
            // 1) direct icon of the item (Change Icon)
            if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath))
                return IconExtractor.LoadAny(item.CustomIconPath);
            // 2) icon assigned to the file type
            string typeIcon = FileTypes.GetIconForPath(item.Path);
            if (!string.IsNullOrEmpty(typeIcon) && File.Exists(typeIcon))
                return IconExtractor.LoadAny(typeIcon);
            // 3) standard shell icon
            return IconExtractor.GetIcon(item.Path, true);
        }

        private void LoadTileIcon(TileControl tile, ShortcutItem item)
        {
            if (tile.IsDisposed) return;
            Image img = null;
            try { img = LoadIconForItem(item); } catch { }
            if (img == null) img = SystemIcons.Application.ToBitmap();
            if (tile.IsDisposed) { img.Dispose(); return; }
            if (tile.IconImage != null) tile.IconImage.Dispose();
            tile.IconImage = img;
            tile.Invalidate();
        }

        private void LoadFolderChildIcon(TileControl tile, ShortcutItem child, int index)
        {
            if (tile.IsDisposed || index >= tile.ChildIcons.Count) return;
            Image img = null;
            if (child.IsFolder)
            {
                var folderIcon = ShellIcon.GetFolderIcon(ShellIcon.IconSize.Large, ShellIcon.FolderType.Closed);
                if (folderIcon != null) img = folderIcon.ToBitmap();
                else img = SystemIcons.WinLogo.ToBitmap();
            }
            else
            {
                try
                {
                    // 1) direct icon of the child, 2) file type icon, 3) standard icon
                    if (!string.IsNullOrEmpty(child.CustomIconPath) && File.Exists(child.CustomIconPath))
                        img = IconExtractor.LoadAny(child.CustomIconPath);
                    else
                    {
                        string typeIcon = FileTypes.GetIconForPath(child.Path);
                        if (!string.IsNullOrEmpty(typeIcon) && File.Exists(typeIcon))
                            img = IconExtractor.LoadAny(typeIcon);
                        else
                            img = IconExtractor.GetIcon(child.Path, true);
                    }
                }
                catch { }
            }
            if (tile.IsDisposed) { if (img != null) img.Dispose(); return; }
            var old = tile.ChildIcons[index];
            if (old != null) old.Dispose();
            tile.ChildIcons[index] = img;
            tile.Invalidate();
        }

        // ---------- Item launching ----------
        // Guards against launching the same item twice from a double-click and keeps
        // "cancel" in a UAC prompt silent instead of showing an error box.
        internal static bool SuppressDoubleLaunch(string key)
        {
            var now = DateTime.Now;
            if (key == lastLaunchKey && (now - lastLaunchTime).TotalMilliseconds < 800) return true;
            lastLaunchKey = key;
            lastLaunchTime = now;
            return false;
        }

        public static void LaunchItem(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (SuppressDoubleLaunch("item:" + path)) return;

            // File-type rule: open with the program assigned to this extension (if any).
            string target = path;
            string args = null;
            try
            {
                var rule = FileTypes.GetRuleForPath(path);
                if (rule != null && !string.IsNullOrEmpty(rule.OpenWith) && File.Exists(rule.OpenWith) && File.Exists(path))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext != ".lnk") // shortcuts keep their own target semantics
                    {
                        target = rule.OpenWith;
                        args = "\"" + path + "\"";
                        if (!string.IsNullOrEmpty(rule.OpenArgs)) args = rule.OpenArgs + " " + args;
                    }
                }
            }
            catch { }

            StartDetached(target, args);
        }

        // Launches on its own STA thread so the panel stays responsive even when the system
        // takes seconds to hand the launch over (cold start, antivirus inspection, UAC).
        private static void StartDetached(string fileName, string arguments)
        {
            var t = new Thread(delegate()
            {
                try
                {
                    if (arguments == null)
                        System.Diagnostics.Process.Start(fileName);
                    else
                        System.Diagnostics.Process.Start(fileName, arguments);
                }
                catch (System.ComponentModel.Win32Exception wex)
                {
                    if (wex.NativeErrorCode == 1223) return; // "No" in the UAC prompt
                    ReportLaunchError("Error opening file: " + wex.Message);
                }
                catch (Exception ex)
                {
                    ReportLaunchError("Error opening file: " + ex.Message);
                }
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private static void ReportLaunchError(string message)
        {
            try
            {
                foreach (Form f in Application.OpenForms)
                {
                    var mf = f as MainForm;
                    if (mf != null && !mf.IsDisposed && mf.IsHandleCreated)
                    {
                        mf.BeginInvoke((MethodInvoker)delegate { MessageBox.Show(message); });
                        return;
                    }
                }
            }
            catch { }
        }

        // ---------- Panel-local copies of shortcuts and icons ----------
        // .lnk / .ico files that are added to the panel are copied into <exe>\ico
        // so they are not lost when the originals are moved or deleted elsewhere.
        internal static string ConsolidateFilePath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return path;
                string lower = path.ToLowerInvariant();
                if (!lower.EndsWith(".lnk") && !lower.EndsWith(".ico")) return path;
                return CopyIntoIcoFolder(path);
            }
            catch
            {
                return path;
            }
        }

        // Icon image files picked for file-type rules are kept in the panel's ico folder too.
        internal static string ConsolidateIconFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return path;
                string lower = path.ToLowerInvariant();
                if (!lower.EndsWith(".ico") && !lower.EndsWith(".png") && !lower.EndsWith(".jpg") &&
                    !lower.EndsWith(".jpeg") && !lower.EndsWith(".bmp")) return path;
                return CopyIntoIcoFolder(path);
            }
            catch
            {
                return path;
            }
        }

        private static string CopyIntoIcoFolder(string path)
        {
            string appDir = Application.StartupPath.TrimEnd('\\');
            if (path.StartsWith(appDir + "\\", StringComparison.OrdinalIgnoreCase)) return path;
            string destDir = Path.Combine(appDir, "ico");
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
            string fileName = Path.GetFileName(path);
            string dest = Path.Combine(destDir, fileName);
            if (File.Exists(dest))
            {
                if (FilesEqual(path, dest)) return dest;
                string baseName = Path.GetFileNameWithoutExtension(fileName);
                string origExt = Path.GetExtension(fileName);
                for (int i = 2; i < 100; i++)
                {
                    string cand = Path.Combine(destDir, baseName + " (" + i + ")" + origExt);
                    if (!File.Exists(cand)) { dest = cand; break; }
                    if (FilesEqual(path, cand)) return cand;
                }
            }
            File.Copy(path, dest);
            return dest;
        }

        private static bool FilesEqual(string a, string b)
        {
            try
            {
                var fa = new FileInfo(a); var fb = new FileInfo(b);
                if (fa.Length != fb.Length) return false;
                using (var s1 = File.OpenRead(a))
                using (var s2 = File.OpenRead(b))
                {
                    int x1, x2;
                    do
                    {
                        x1 = s1.ReadByte(); x2 = s2.ReadByte();
                        if (x1 != x2) return false;
                    } while (x1 != -1);
                }
                return true;
            }
            catch { return false; }
        }

        private void ConsolidateAllRecords()
        {
            bool changed = false;
            foreach (var tab in records.Tabs)
                changed |= ConsolidateList(tab.Items);
            if (changed) records.Save(recordsPath);
        }

        private bool ConsolidateList(List<ShortcutItem> items)
        {
            if (items == null) return false;
            bool changed = false;
            foreach (var it in items)
            {
                if (!string.IsNullOrEmpty(it.Path))
                {
                    string np = ConsolidateFilePath(it.Path);
                    if (np != it.Path) { it.Path = np; changed = true; }
                }
                if (!string.IsNullOrEmpty(it.CustomIconPath))
                {
                    string np = ConsolidateFilePath(it.CustomIconPath);
                    if (np != it.CustomIconPath) { it.CustomIconPath = np; changed = true; }
                }
                if (it.Children != null && it.Children.Count > 0)
                    changed |= ConsolidateList(it.Children);
            }
            return changed;
        }

        private void DragWindow(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        private void LoadTabs()
        {
            DisposeControlTree(contentPanel);
            DisposeControlTree(tabBar);
            DisposeControlTree(rightPanel);
            tabBar.Controls.Clear();
            rightPanel.Controls.Clear();
            contentPanel.Controls.Clear();

            int tabRows = 1;
            foreach (var tab in records.Tabs) tabRows = Math.Max(tabRows, tab.Row + 1);
            topPanel.Height = Math.Max(tabRows * 35, 62);

            // ---- Right stack: row 1 = min / max / close, row 2 = settings / edit ----
            var winRow = new Panel { Location = new Point(0, 0), Size = new Size(rightPanel.Width, 31), BackColor = bgColor };
            var actionRow = new Panel { Location = new Point(0, 31), Size = new Size(rightPanel.Width, 31), BackColor = bgColor };
            winRow.MouseDown += (s, e) => DragWindow(e);
            actionRow.MouseDown += (s, e) => DragWindow(e);
            rightPanel.Controls.Add(winRow);
            rightPanel.Controls.Add(actionRow);

            // Added close-first: WinForms applies Dock.Left in reverse order, so the
            // visual result is min / max / close from left to right.
            var closeBtn = new Button { Text = "✕", Width = 35, Height = 31, Dock = DockStyle.Left, FlatStyle = FlatStyle.Flat, BackColor = bgColor, ForeColor = textColor, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10f) };
            closeBtn.FlatAppearance.BorderSize = 0; closeBtn.FlatAppearance.MouseOverBackColor = Color.Red;
            closeBtn.Click += (s, e) => this.Close();
            winRow.Controls.Add(closeBtn);

            var maxBtn = new Button { Text = "🗖", Width = 35, Height = 31, Dock = DockStyle.Left, FlatStyle = FlatStyle.Flat, BackColor = bgColor, ForeColor = textColor, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10f) };
            maxBtn.FlatAppearance.BorderSize = 0; maxBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            maxBtn.Click += (s, e) => this.WindowState = this.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            winRow.Controls.Add(maxBtn);

            var minBtn = new Button { Text = "🗕", Width = 35, Height = 31, Dock = DockStyle.Left, FlatStyle = FlatStyle.Flat, BackColor = bgColor, ForeColor = textColor, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10f) };
            minBtn.FlatAppearance.BorderSize = 0; minBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            minBtn.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            winRow.Controls.Add(minBtn);

            var settingsBtn = new Button
            {
                Text = "⚙️",
                Width = 35,
                Height = 31,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 11f)
            };
            settingsBtn.FlatAppearance.BorderSize = 0;
            settingsBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            settingsBtn.Click += (s, e) => OpenSettings();
            actionRow.Controls.Add(settingsBtn);

            var editBtn = new Button
            {
                Text = isEditMode ? "✅" : "⬜",
                Width = 35,
                Height = 31,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = isEditMode ? panelColor : bgColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 11f)
            };
            editBtn.FlatAppearance.BorderSize = 0;
            editBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            editBtn.Click += (s, e) => {
                isEditMode = !isEditMode;
                settings.EditMode = isEditMode;
                settings.Save(settingsPath);
                editBtn.Text = isEditMode ? "✅" : "⬜";
                editBtn.BackColor = isEditMode ? panelColor : bgColor;
            };
            actionRow.Controls.Add(editBtn);

            var gridBtn = new Button
            {
                Text = "▦",
                Width = 35,
                Height = 31,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = settings.GridVisible ? panelColor : bgColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 11f)
            };
            gridBtn.FlatAppearance.BorderSize = 0;
            gridBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            gridBtn.Click += (s, e) =>
            {
                settings.GridVisible = !settings.GridVisible;
                settings.Save(settingsPath);
                gridBtn.BackColor = settings.GridVisible ? panelColor : bgColor;
                foreach (Control c in contentPanel.Controls)
                {
                    var lp = c as Panel;
                    if (lp != null) lp.Invalidate();
                }
            };
            actionRow.Controls.Add(gridBtn);

            // ---- Tabs (manual rows) ----
            var layoutPanels = new Dictionary<Button, Panel>();
            var tabByButton = new Dictionary<Button, TabData>();
            var buttons = new List<Button>();
            int[] rowX = new int[tabRows];

            foreach (var tabData in records.Tabs)
            {
                if (!tabNavigations.ContainsKey(tabData)) tabNavigations[tabData] = new Stack<ShortcutItem>();
                if (tabData.Row < 0) tabData.Row = 0;
                if (tabData.Row >= tabRows) tabData.Row = tabRows - 1;

                int row = tabData.Row;

                var layoutPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    AllowDrop = true,
                    Tag = tabData,
                    BackColor = bgColor,
                    Visible = false
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

                var tabBtn = new Button
                {
                    Text = tabData.Name,
                    Width = 100,
                    Height = 35,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = bgColor,
                    ForeColor = Settings.ParseColor(settings.FontTabsColor, textColor),
                    Cursor = Cursors.Hand,
                    Font = Settings.MakeFont(settings.FontTabsName, settings.FontTabsSize)
                };
                tabBtn.FlatAppearance.BorderSize = 0;
                tabBtn.FlatAppearance.MouseOverBackColor = hoverColor;

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
                tabMenu.MenuItems.Add("Toggle Layout (Free / Grid)", (s, e) => {
                    tabData.IsGridLayout = !tabData.IsGridLayout;
                    records.Save(recordsPath);
                    RenderCurrentFolder(layoutPanel, tabData);
                });
                tabBtn.ContextMenu = tabMenu;

                tabByButton[tabBtn] = tabData;
                layoutPanels[tabBtn] = layoutPanel;
                buttons.Add(tabBtn);

                tabBtn.Location = new Point(rowX[row], row * 35);
                rowX[row] += tabBtn.Width;

                tabBar.Controls.Add(tabBtn);
                AttachTabDrag(tabBtn, tabByButton);
            }

            var addTabBtn = new Button
            {
                Text = "+",
                Width = 35,
                Height = 35,
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold)
            };
            addTabBtn.FlatAppearance.BorderSize = 0;
            addTabBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            addTabBtn.Location = new Point(rowX[0], 0);
            tabBar.Controls.Add(addTabBtn);
            addTabRef = addTabBtn;
            addTabBtn.Click += (s, e) => {
                string name = Prompt.ShowDialog("New Tab Name", "Add Tab");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var newTab = new TabData { Name = name, IsGridLayout = true, Row = 0 };
                    records.Tabs.Add(newTab);
                    tabNavigations[newTab] = new Stack<ShortcutItem>();
                    records.Save(recordsPath);
                    LoadTabs();
                }
            };

            tabBar.MouseDown += (s, e) => DragWindow(e);
            topPanel.MouseDown += (s, e) => DragWindow(e);
            rightPanel.MouseDown += (s, e) => DragWindow(e);

            // Activate the first tab
            if (buttons.Count > 0) ActivateTab(buttons[0], layoutPanels, buttons);

            // Render tiles for every tab (kept in hidden panels)
            foreach (var tabData in records.Tabs)
            {
                Panel lp = null;
                foreach (var kv in layoutPanels) if (tabByButton[kv.Key] == tabData) { lp = kv.Value; break; }
                if (lp != null) RenderCurrentFolder(lp, tabData);
            }
        }

        private void ActivateTab(Button tabBtn, Dictionary<Button, Panel> layoutPanels, List<Button> buttons)
        {
            foreach (var kv in layoutPanels) kv.Value.Visible = false;
            Font tabFont = Settings.MakeFont(settings.FontTabsName, settings.FontTabsSize);
            Font tabFontActive = Settings.MakeFont(settings.FontTabsName, settings.FontTabsSize, System.Drawing.FontStyle.Bold);
            Color tabColor = Settings.ParseColor(settings.FontTabsColor, textColor);
            foreach (var b in buttons)
            {
                b.BackColor = bgColor;
                b.ForeColor = tabColor;
                b.Font = tabFont;
                b.FlatAppearance.MouseOverBackColor = hoverColor;
            }
            tabBtn.BackColor = panelColor;
            tabBtn.ForeColor = tabColor;
            tabBtn.Font = tabFontActive;
            tabBtn.FlatAppearance.MouseOverBackColor = panelColor;
            layoutPanels[tabBtn].Visible = true;
        }

        // Drag a tab button to reorder it inside its row or move it to another row.
        private void AttachTabDrag(Button tabBtn, Dictionary<Button, TabData> tabByButton)
        {
            Point grabScreen = Point.Empty;
            int grabOffsetX = 0;


            tabBtn.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    dragTabBtn = tabBtn;
                    grabScreen = Cursor.Position;
                    grabOffsetX = e.X;
                    tabDragMoved = false;
                    tabBtn.BringToFront();
                }
            };

            tabBtn.MouseMove += (s, e) =>
            {
                if (dragTabBtn != tabBtn || e.Button != MouseButtons.Left) return;
                int dx = Cursor.Position.X - grabScreen.X;
                int dy = Cursor.Position.Y - grabScreen.Y;
                if (!tabDragMoved && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4)) tabDragMoved = true;
                if (!tabDragMoved) return;

                var parent = tabBar;
                Point local = parent.PointToClient(Cursor.Position);
                int maxRows = 1;
                foreach (Control c in parent.Controls)
                {
                    int r = c.Top / 35 + 1;
                    if (r > maxRows) maxRows = r;
                }
                int row = local.Y / 35;
                if (row < 0) row = 0;
                if (row > maxRows) row = maxRows - 1;

                int maxX = parent.Width - tabBtn.Width;
                int newX = Math.Max(0, Math.Min(maxX, local.X - grabOffsetX));
                tabBtn.Location = new Point(newX, row * 35);

                int rowsUsed = row + 1;
                foreach (Control c in parent.Controls)
                {
                    int r = c.Top / 35 + 1;
                    if (r > rowsUsed) rowsUsed = r;
                }
                topPanel.Height = Math.Max(rowsUsed * 35, 62);
            };

            tabBtn.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || dragTabBtn != tabBtn) return;
                dragTabBtn = null;
                if (!tabDragMoved)
                {
                    // plain click: activate
                    var root = this;
                    var layoutPanels = new Dictionary<Button, Panel>();
                    var buttons = new List<Button>();
                    foreach (Control c in tabBar.Controls)
                    {
                        var b = c as Button;
                        if (b != null && b != addTabRef && tabByButton.ContainsKey(b))
                        {
                            buttons.Add(b);
                        }
                    }
                    foreach (Control c in contentPanel.Controls)
                    {
                        var lp = c as Panel;
                        if (lp != null && lp.Tag is TabData)
                        {
                            foreach (var b in buttons)
                                if (tabByButton[b] == (TabData)lp.Tag) layoutPanels[b] = lp;
                        }
                    }
                    ActivateTab(tabBtn, layoutPanels, buttons);
                    return;
                }

                // Commit: order tabs by (row, x) and store the row index
                var sorted = new List<Button>();
                foreach (Control c in tabBar.Controls)
                {
                    var b = c as Button;
                    if (b != null && b != addTabRef && tabByButton.ContainsKey(b)) sorted.Add(b);
                }
                sorted.Sort((a, b) =>
                {
                    int ra = a.Top / 35, rb = b.Top / 35;
                    if (ra != rb) return ra.CompareTo(rb);
                    return a.Left.CompareTo(b.Left);
                });

                var orderedTabs = new List<TabData>();
                foreach (var b in sorted)
                {
                    var td = tabByButton[b];
                    td.Row = b.Top / 35;
                    orderedTabs.Add(td);
                }
                records.Tabs = orderedTabs;
                records.Save(recordsPath);
                LoadTabs();
            };
        }

        private Button addTabRef; // reference to the "+" button to skip it during tab drags

        private void DisposeControlTree(Control root)
        {
            foreach (Control c in root.Controls)
            {
                var tc = c as TileControl;
                if (tc != null)
                {
                    if (tc.IconImage != null) tc.IconImage.Dispose();
                    foreach (var img in tc.ChildIcons) if (img != null) img.Dispose();
                }
                DisposeControlTree(c);
                c.Dispose();
            }
        }

        private void RenderCurrentFolder(Panel layoutPanel, TabData tabData)
        {
            DisposeControlTree(layoutPanel);
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

        // ---- Grid occupancy helpers ----
        private int ClampItemSize(int size)
        {
            int s = size;
            if (s <= 0 || s > 4) s = settings.DefaultItemSize;
            if (s <= 0 || s > 4) s = 1;
            return s;
        }

        private bool CellFits(bool[,] occ, int cols, int rows, int x, int y, int size)
        {
            if (x < 0 || y < 0 || x + size > cols || y + size > rows) return false;
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    if (occ[y + dy, x + dx]) return false;
            return true;
        }

        // Finds the nearest free cell to (preferX, preferY) that fits a size x size item.
        // Returns the preferred cell when it is free.
        private Point FindFreeGridCell(List<ShortcutItem> items, ShortcutItem skip, int size, int cols, int rows, int preferX, int preferY)
        {
            var occ = new bool[rows, cols];
            foreach (var it in items)
            {
                if (ReferenceEquals(it, skip)) continue;
                int s = Math.Min(ClampItemSize(it.Size), Math.Min(cols, rows));
                int gx = Math.Max(0, Math.Min(cols - s, it.GridX));
                int gy = Math.Max(0, Math.Min(rows - s, it.GridY));
                for (int dy = 0; dy < s; dy++)
                    for (int dx = 0; dx < s; dx++)
                        if (gy + dy < rows && gx + dx < cols) occ[gy + dy, gx + dx] = true;
            }

            if (CellFits(occ, cols, rows, preferX, preferY, size)) return new Point(preferX, preferY);

            Point best = new Point(-1, -1);
            int bestDist = int.MaxValue;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    if (CellFits(occ, cols, rows, x, y, size))
                    {
                        int dist = Math.Abs(x - preferX) + Math.Abs(y - preferY);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = new Point(x, y);
                        }
                    }
                }
            }
            return best;
        }

        // Places the item into the grid, shifting to a free cell when the preferred one is taken.
        private void PlaceInGrid(List<ShortcutItem> items, ShortcutItem item, int preferX, int preferY, int cols, int rows)
        {
            int s = ClampItemSize(item.Size);
            Point cell = FindFreeGridCell(items, item, s, cols, rows, preferX, preferY);
            if (cell.X >= 0)
            {
                item.GridX = cell.X;
                item.GridY = cell.Y;
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
                    PlaceInGrid(targetList, folder, Math.Max(0, folder.X / Math.Max(1, layoutPanel.ClientSize.Width / cols)), Math.Max(0, folder.Y / Math.Max(1, layoutPanel.ClientSize.Height / rows)), cols, rows);
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

            int cols = Math.Max(1, settings.GridColumns);
            int rows = Math.Max(1, settings.GridRows);

            int offset = 0;
            foreach (var file in files)
            {
                var shortcut = new ShortcutItem
                {
                    Path = ConsolidateFilePath(file),
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
                        int cellWidth = Math.Max(1, layoutPanel.ClientSize.Width / cols);
                        int cellHeight = Math.Max(1, layoutPanel.ClientSize.Height / rows);
                        PlaceInGrid(targetList, shortcut, displayPt.X / cellWidth, displayPt.Y / cellHeight, cols, rows);
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
            if (tabData == null || !tabData.IsGridLayout || !settings.GridVisible) return;

            int cols = Math.Max(1, settings.GridColumns);
            int rows = Math.Max(1, settings.GridRows);

            // The grid always stretches with the visible window area and never paints
            // outside the panel's client rectangle.
            float cellWidth = panel.ClientSize.Width / (float)cols;
            float cellHeight = panel.ClientSize.Height / (float)rows;
            if (cellWidth <= 0 || cellHeight <= 0) return;

            var scroll = panel.AutoScrollPosition;

            e.Graphics.SetClip(panel.ClientRectangle);

            Color gridColor = settings.IsLightTheme
                ? Color.FromArgb(settings.GridTransparency, 0, 0, 0)
                : Color.FromArgb(settings.GridTransparency, 255, 255, 255);

            using (Pen gridPen = new Pen(gridColor))
            {
                gridPen.DashPattern = new float[] { 4, 8 };
                for (int i = 0; i <= cols; i++)
                {
                    float x = i * cellWidth + scroll.X;
                    if (x >= 0 && x <= panel.ClientSize.Width)
                        e.Graphics.DrawLine(gridPen, x, scroll.Y, x, panel.ClientSize.Height);
                }
                for (int i = 0; i <= rows; i++)
                {
                    float y = i * cellHeight + scroll.Y;
                    if (y >= 0 && y <= panel.ClientSize.Height)
                        e.Graphics.DrawLine(gridPen, scroll.X, y, panel.ClientSize.Width, y);
                }
            }
        }

        private void AddShortcutControl(Panel panel, ShortcutItem item, TabData tabData)
        {
            int cols = Math.Max(1, settings.GridColumns);
            int rows = Math.Max(1, settings.GridRows);
            int cellWidth = Math.Max(1, panel.ClientSize.Width / cols);
            int cellHeight = Math.Max(1, panel.ClientSize.Height / rows);

            int s = ClampItemSize(item.Size);

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

            if (item.IsFolder)
            {
                if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath))
                    EnqueueIconTask(() => LoadTileIcon(tile, item));

                int maxIcons = item.Children != null ? Math.Min(9, item.Children.Count) : 0;
                for (int i = 0; i < maxIcons; i++) tile.ChildIcons.Add(null);
                for (int i = 0; i < maxIcons; i++)
                {
                    var child = item.Children[i];
                    int idx = i;
                    EnqueueIconTask(() => LoadFolderChildIcon(tile, child, idx));
                }
            }
            else
            {
                EnqueueIconTask(() => LoadTileIcon(tile, item));
            }

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
                        fMenu.MenuItems.Add("Open in Mini Explorer", (s2, e2) => OpenMiniExplorer(item));
                        if (isEditMode)
                        {
                            fMenu.MenuItems.Add("-");
                            if (tabNavigations[tabData].Count > 0)
                                fMenu.MenuItems.Add("Move out of folder", (s2, e2) => MoveItemOutOfFolder(panel, tabData, item));
                            var fSizeMenu = fMenu.MenuItems.Add("Size");
                            fSizeMenu.MenuItems.Add("1 x 1", (s2, e2) => ChangeIconSize(item, tile, panel, tabData, 1));
                            fSizeMenu.MenuItems.Add("2 x 2", (s2, e2) => ChangeIconSize(item, tile, panel, tabData, 2));
                            fSizeMenu.MenuItems.Add("3 x 3", (s2, e2) => ChangeIconSize(item, tile, panel, tabData, 3));
                            fSizeMenu.MenuItems.Add("4 x 4", (s2, e2) => ChangeIconSize(item, tile, panel, tabData, 4));
                            fMenu.MenuItems.Add("Rename", (s2, e2) => RenameItem(item, tile));
                            fMenu.MenuItems.Add("Change Icon", (s2, e2) => ChangeItemIcon(item, tile));
                            fMenu.MenuItems.Add("Remove", (s2, e2) => RemoveItem(panel, tile, item, tabData));
                        }
                        fMenu.Show(tile, e.Location);
                    }
                    else
                    {
                        // The Explorer menu is always available; our own items are shown
                        // before the Explorer items, edit actions only in edit mode.
                        NativeContextMenu.ShowContextMenu(item.Path, pt.X, pt.Y, this.Handle, isEditMode,
                            tabNavigations[tabData].Count > 0,
                            () => MoveItemOutOfFolder(panel, tabData, item),
                            () => OpenContainingFolder(item),
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
                                int col = Math.Max(0, Math.Min(cols - s, (tile.Left + cellWidth / 2) / cellWidth));
                                int row = Math.Max(0, Math.Min(rows - s, (tile.Top + cellHeight / 2) / cellHeight));
                                PlaceInGrid(GetCurrentItems(tabData), item, col, row, cols, rows);
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
                            if ((Control.ModifierKeys & Keys.Control) == Keys.Control && settings.MiniExplorerCtrlClick && Directory.Exists(item.Path))
                            {
                                OpenMiniExplorer(item);
                            }
                            else if (settings.OpenFoldersInPopup)
                            {
                                OpenFolderPopup(item, tile, panel, tabData);
                            }
                            else
                            {
                                tabNavigations[tabData].Push(item);
                                RenderCurrentFolder(panel, tabData);
                            }
                        }
                        else
                        {
                            LaunchItem(item.Path);
                        }
                    }
                    draggingTile = null;
                    draggingItem = null;
                }
            };

            panel.Controls.Add(tile);
        }

        private List<ShortcutItem> GetCurrentItems(TabData tabData)
        {
            var navStack = tabNavigations[tabData];
            return navStack.Count > 0 ? navStack.Peek().Children : tabData.Items;
        }

        internal static void OpenContainingFolder(ShortcutItem item)
        {
            if (SuppressDoubleLaunch("select:" + item.Path)) return;
            string dir = Path.GetDirectoryName(item.Path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                StartDetached("explorer.exe", "/select,\"" + item.Path + "\"");
            else
                StartDetached("explorer.exe", null);
        }

        private void OpenFolderPopup(ShortcutItem folder, TileControl tile, Panel panel, TabData tabData)
        {
            var popup = new FolderPopupForm(folder, tile.PointToScreen(new Point(0, tile.Height)), settings, isEditMode,
                (child) =>
                {
                    if (folder.Children != null) folder.Children.Remove(child);
                    var targetList = GetCurrentItems(tabData);
                    if (tabData.IsGridLayout)
                    {
                        int cols = Math.Max(1, settings.GridColumns);
                        int rows = Math.Max(1, settings.GridRows);
                        PlaceInGrid(targetList, child, folder.GridX + 1, folder.GridY, cols, rows);
                    }
                    else
                    {
                        child.X = folder.X + 30;
                        child.Y = folder.Y + 30;
                    }
                    targetList.Add(child);
                    records.Save(recordsPath);
                    RenderCurrentFolder(panel, tabData);
                },
                () =>
                {
                    records.Save(recordsPath);
                    RenderCurrentFolder(panel, tabData);
                });
            popup.Show(this);
        }

        // Opens the mini explorer window at the folder; if it is already open it is
        // re-used and navigated to the folder instead of opening a second window.
        private void OpenMiniExplorer(ShortcutItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Path)) return;
            if (!Directory.Exists(item.Path))
            {
                LaunchItem(item.Path);
                return;
            }
            try
            {
                if (miniExplorer == null || miniExplorer.IsDisposed)
                    miniExplorer = new MiniExplorerForm(item.Path, settings);
                else
                    miniExplorer.NavigateExternal(item.Path);
                if (!miniExplorer.Visible) miniExplorer.Show(this);
                else miniExplorer.Activate();
            }
            catch (Exception ex)
            {
                ReportLaunchError("Mini Explorer error: " + ex.Message);
            }
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
                    // .ico files are copied into the panel's own ico\ folder so the
                    // custom icon is not lost if the original file is moved away.
                    item.CustomIconPath = ConsolidateFilePath(ofd.FileName);
                    records.Save(recordsPath);
                    EnqueueIconTask(() => LoadTileIcon(tile, item));
                }
            }
        }

        private void ChangeIconSize(ShortcutItem item, TileControl tile, Panel panel, TabData tabData, int newSize)
        {
            item.Size = newSize;
            if (tabData.IsGridLayout)
            {
                int cols = Math.Max(1, settings.GridColumns);
                int rows = Math.Max(1, settings.GridRows);
                // After resizing, keep the tile inside the grid and off other tiles.
                PlaceInGrid(GetCurrentItems(tabData), item, item.GridX, item.GridY, cols, rows);
            }
            records.Save(recordsPath);
            RenderCurrentFolder(panel, tabData);
        }

        private void RemoveItem(Panel panel, Control tile, ShortcutItem item, TabData tabData)
        {
            if (!ConfirmDialog.Show(this, item)) return;
            var navStack = tabNavigations[tabData];
            var targetList = navStack.Count > 0 ? navStack.Peek().Children : tabData.Items;
            targetList.Remove(item);
            panel.Controls.Remove(tile);
            tile.Dispose();
            records.Save(recordsPath);
        }

        // Moves an element out of the folder that is currently open inside the tab (one level up).
        private void MoveItemOutOfFolder(Panel panel, TabData tabData, ShortcutItem item)
        {
            var navStack = tabNavigations[tabData];
            if (navStack.Count == 0) return;
            var currentFolder = navStack.Peek();
            if (currentFolder.Children == null || !currentFolder.Children.Remove(item)) return;

            List<ShortcutItem> targetList;
            if (navStack.Count == 1)
            {
                targetList = tabData.Items;
            }
            else
            {
                var parentFolder = navStack.ToArray()[1];
                if (parentFolder.Children == null) parentFolder.Children = new List<ShortcutItem>();
                targetList = parentFolder.Children;
            }

            if (tabData.IsGridLayout)
            {
                int cols = Math.Max(1, settings.GridColumns);
                int rows = Math.Max(1, settings.GridRows);
                PlaceInGrid(targetList, item, currentFolder.GridX + 1, currentFolder.GridY, cols, rows);
            }
            else
            {
                item.X = currentFolder.X + 30;
                item.Y = currentFolder.Y + 30;
            }
            targetList.Add(item);
            records.Save(recordsPath);
            RenderCurrentFolder(panel, tabData);
        }
    }

    // Popup window that opens a folder's children above everything else.
    public class FolderPopupForm : Form
    {
        private ShortcutItem folder;
        private Settings settings;
        private bool editMode;
        private Action<ShortcutItem> onMoveOutOfFolder;
        private Action onChanged;
        private Color bgColor;
        private Color panelColor;
        private Color hoverColor;
        private Color textColor;
        private FlowLayoutPanel flow;
        private Screen openScreen;
        private bool suppressDeactivate;

        private const int TileSize = 84;
        private const int TileGap = 6;
        private const int Pad = 6;

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public FolderPopupForm(ShortcutItem folderItem, Point screenPos, Settings settings, bool editMode,
            Action<ShortcutItem> onMoveOutOfFolder, Action onChanged)
        {
            this.folder = folderItem;
            this.settings = settings;
            this.editMode = editMode;
            this.onMoveOutOfFolder = onMoveOutOfFolder;
            this.onChanged = onChanged;

            if (settings.IsLightTheme)
            {
                bgColor = Color.FromArgb(228, 228, 230);
                panelColor = Color.FromArgb(210, 210, 214);
                hoverColor = Color.FromArgb(190, 190, 195);
                textColor = Color.Black;
            }
            else
            {
                bgColor = Color.FromArgb(22, 22, 26);
                panelColor = Color.FromArgb(45, 45, 48);
                hoverColor = Color.FromArgb(62, 62, 66);
                textColor = Color.White;
            }

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = bgColor;
            this.ForeColor = textColor;
            this.Font = new Font("Segoe UI", 9f);

            openScreen = Screen.FromPoint(screenPos);
            var wa = openScreen.WorkingArea;

            var titleBar = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = panelColor };
            var titleLbl = new Label
            {
                Text = folderItem.Name,
                ForeColor = textColor,
                AutoSize = true,
                Location = new Point(10, 7),
                Font = Settings.MakeFont(settings.FontUiName, settings.FontUiSize)
            };
            titleBar.Controls.Add(titleLbl);
            titleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
            var closeBtn = new Button { Text = "X", Width = 30, Height = 30, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, ForeColor = textColor, BackColor = panelColor };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => this.Close();
            titleBar.Controls.Add(closeBtn);
            this.Controls.Add(titleBar);

            flow = new FlowLayoutPanel
            {
                Location = new Point(Pad, 32),
                BackColor = Color.Transparent,
                AllowDrop = true
            };
            flow.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            flow.DragDrop += (s, e) =>
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files == null || files.Length == 0) return;
                if (folder.Children == null) folder.Children = new List<ShortcutItem>();
                foreach (var f in files)
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (string.IsNullOrEmpty(name)) name = Path.GetFileName(f);
                    folder.Children.Add(new ShortcutItem
                    {
                        Path = MainForm.ConsolidateFilePath(f),
                        Name = name
                    });
                }
                if (onChanged != null) onChanged();
                Rebuild();
            };
            this.Controls.Add(flow);

            LayoutPopup();
            BuildTiles();

            int px = Math.Max(wa.Left, Math.Min(screenPos.X, wa.Right - this.Width));
            int py = Math.Max(wa.Top, Math.Min(screenPos.Y, wa.Bottom - this.Height));
            this.Location = new Point(px, py);

            this.Deactivate += (s, e) => { if (!suppressDeactivate) this.Close(); };
        }

        // Sizes the popup so that every row shows all its tiles: the last column is no
        // longer cut off (4 items = 4 tiles in a row). Scrolling appears only when the
        // folder has more rows than fit on the screen.
        private void LayoutPopup()
        {
            if (this.IsDisposed || flow == null || flow.IsDisposed) return;
            var children = folder.Children != null ? folder.Children : new List<ShortcutItem>();
            int count = children.Count;
            int cols = Math.Max(1, Math.Min(4, count));
            int rows = Math.Max(1, (int)Math.Ceiling(count / (double)cols));

            var wa = openScreen != null ? openScreen.WorkingArea : Screen.PrimaryScreen.WorkingArea;
            int maxRows = Math.Max(1, (wa.Height - 100) / (TileSize + TileGap));
            bool scroll = rows > maxRows;
            int visRows = Math.Min(rows, maxRows);

            int flowW = cols * (TileSize + TileGap);
            if (scroll) flowW += SystemInformation.VerticalScrollBarWidth;
            int flowH = visRows * (TileSize + TileGap);

            flow.Size = new Size(flowW, flowH);
            flow.AutoScroll = scroll;
            this.ClientSize = new Size(flowW + Pad * 2, 32 + flowH + Pad);
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            if (this.Region != null) this.Region.Dispose();
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));
        }

        private void Rebuild()
        {
            if (this.IsDisposed || flow == null || flow.IsDisposed) return;
            LayoutPopup();
            BuildTiles();
        }

        private void BuildTiles()
        {
            if (this.IsDisposed || flow == null || flow.IsDisposed) return;
            var old = new List<Control>();
            foreach (Control c in flow.Controls) old.Add(c);
            flow.Controls.Clear();
            foreach (var c in old) c.Dispose();

            var children = folder.Children != null ? folder.Children : new List<ShortcutItem>();
            if (children.Count == 0)
            {
                var emptyLbl = new Label
                {
                    Text = "Empty",
                    ForeColor = textColor,
                    AutoSize = false,
                    Width = TileSize,
                    Height = TileSize,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(TileGap / 2)
                };
                flow.Controls.Add(emptyLbl);
                return;
            }

            foreach (var child in children)
            {
                var tile = new PopupTile(child, panelColor, hoverColor, textColor);
                tile.Margin = new Padding(TileGap / 2);
                AttachTileHandlers(tile, child);
                flow.Controls.Add(tile);
            }
        }

        // Drag to swap places (edit mode) + right-click menu for each tile.
        private void AttachTileHandlers(PopupTile tile, ShortcutItem child)
        {
            bool dragging = false;
            Point down = Point.Empty;

            tile.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    dragging = false;
                    down = e.Location;
                }
            };
            tile.MouseMove += (s, e) =>
            {
                if (!editMode) return;
                if (e.Button == MouseButtons.Left && (Math.Abs(e.X - down.X) > 5 || Math.Abs(e.Y - down.Y) > 5))
                    dragging = true;
            };
            tile.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    ShowTileMenu(tile, child, e.Location);
                    return;
                }
                if (e.Button != MouseButtons.Left || !dragging) return;
                dragging = false;
                if (!editMode) return;

                tile.SuppressClick = true;
                var pt = flow.PointToClient(Cursor.Position);
                PopupTile target = null;
                foreach (Control c in flow.Controls)
                {
                    var t = c as PopupTile;
                    if (t != null && t != tile && t.Bounds.Contains(pt)) { target = t; break; }
                }
                if (target != null) SwapItems(child, target.Item);
            };
        }

        private void SwapItems(ShortcutItem a, ShortcutItem b)
        {
            if (folder.Children == null || ReferenceEquals(a, b)) return;
            int ia = folder.Children.IndexOf(a);
            int ib = folder.Children.IndexOf(b);
            if (ia < 0 || ib < 0) return;
            folder.Children[ia] = b;
            folder.Children[ib] = a;
            if (onChanged != null) onChanged();
            BuildTiles();
        }

        private void ShowTileMenu(PopupTile tile, ShortcutItem child, Point location)
        {
            var menu = new ContextMenu();
            if (!child.IsFolder && !string.IsNullOrEmpty(child.Path))
            {
                menu.MenuItems.Add("Open containing folder", (s2, e2) => MainForm.OpenContainingFolder(child));
            }
            if (editMode)
            {
                menu.MenuItems.Add("Move out of folder", (s2, e2) =>
                {
                    if (onMoveOutOfFolder != null) onMoveOutOfFolder(child);
                    Rebuild();
                });
                menu.MenuItems.Add("Remove from Panel", (s2, e2) =>
                {
                    suppressDeactivate = true;
                    bool ok;
                    try { ok = ConfirmDialog.Show(this, child); }
                    finally { suppressDeactivate = false; }
                    if (!ok) return;
                    if (folder.Children != null) folder.Children.Remove(child);
                    if (onChanged != null) onChanged();
                    Rebuild();
                });
            }
            if (menu.MenuItems.Count > 0)
                menu.Show(tile, location);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(120, this.ForeColor)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    // A single child tile inside the folder popup.
    public class PopupTile : Control
    {
        private ShortcutItem item;
        private Color tileColor;
        private Color hoverColor;
        private bool hovered;

        public ShortcutItem Item { get { return item; } }
        public bool SuppressClick;

        public PopupTile(ShortcutItem item, Color tileColor, Color hoverColor, Color textColor)
        {
            this.item = item;
            this.tileColor = tileColor;
            this.hoverColor = hoverColor;
            this.Width = 84;
            this.Height = 84;
            this.Cursor = Cursors.Hand;
            this.ForeColor = textColor;
            this.DoubleBuffered = true;

            if (!item.IsFolder)
            {
                try { IconImage = IconExtractor.GetIcon(item.Path, true); } catch { }
                if (IconImage == null) IconImage = SystemIcons.Application.ToBitmap();
            }
            else
            {
                var folderIcon = ShellIcon.GetFolderIcon(ShellIcon.IconSize.Large, ShellIcon.FolderType.Closed);
                IconImage = folderIcon != null ? folderIcon.ToBitmap() : SystemIcons.WinLogo.ToBitmap();
            }
        }

        public Image IconImage { get; private set; }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (SuppressClick)
            {
                SuppressClick = false;
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                if (item.IsFolder) return; // nested folders: open inside the panel for now
                MainForm.LaunchItem(item.Path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (var path = new GraphicsPath())
            {
                int d = 16;
                Rectangle arc = new Rectangle(rect.Location, new Size(d, d));
                path.AddArc(arc, 180, 90);
                arc.X = rect.Right - d; path.AddArc(arc, 270, 90);
                arc.Y = rect.Bottom - d; path.AddArc(arc, 0, 90);
                arc.X = rect.Left; path.AddArc(arc, 90, 90);
                path.CloseFigure();
                using (var brush = new SolidBrush(hovered ? hoverColor : tileColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }

            if (IconImage != null)
            {
                int vMargin = 3;
                int w = this.Width - 8;
                int h = this.Height - 20 - vMargin;

                Settings s = MainForm.CurrentSettings;
                if (s != null)
                {
                    double pct = Math.Max(0.25, Math.Min(4.0, s.IconScale / 100.0));
                    int sw = Math.Max(1, (int)Math.Round(w * pct));
                    int sh = Math.Max(1, (int)Math.Round(h * pct));
                    w = sw;
                    h = sh;
                }
                int ix = 4 + (this.Width - 8 - w) / 2;
                int iy = vMargin + (this.Height - 20 - vMargin - h) / 2;
                var iconRect = new Rectangle(ix, iy, w, h);
                double scale = Math.Min(iconRect.Width / (double)IconImage.Width, iconRect.Height / (double)IconImage.Height);
                int dw = Math.Max(1, (int)Math.Round(IconImage.Width * scale));
                int dh = Math.Max(1, (int)Math.Round(IconImage.Height * scale));
                e.Graphics.DrawImage(IconImage, new Rectangle(iconRect.X + (iconRect.Width - dw) / 2, iconRect.Y + (iconRect.Height - dh) / 2, dw, dh));
            }

            Color tColor = this.ForeColor;
            Font labelFont = null;
            Settings cfg = MainForm.CurrentSettings;
            if (cfg != null)
            {
                tColor = Settings.ParseColor(cfg.FontItemsColor, tColor);
                labelFont = Settings.MakeFont(cfg.FontItemsName, cfg.FontItemsSize);
            }
            using (var brush = new SolidBrush(tColor))
            using (var font = labelFont != null ? labelFont : new Font("Segoe UI", 8f))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                e.Graphics.DrawString(item.Name, font, brush, new Rectangle(2, this.Height - 18, this.Width - 4, 16), sf);
            }
            if (labelFont != null) labelFont.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && IconImage != null) IconImage.Dispose();
            base.Dispose(disposing);
        }
    }

    public class TileControl : Control
    {
        public ShortcutItem Item { get; set; }
        public TabData TabData { get; set; }
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

        // Height reserved for the label. Only reserve it when the tile fits a text line,
        // otherwise the label would overlap the icon on short tiles.
        private int GetTextSpace()
        {
            return this.Height >= 56 ? 20 : 0;
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
                    int textSpace = GetTextSpace();
                    int miniWidth = (this.Width - padding * 2) / cols;
                    int miniHeight = ((this.Height - textSpace) - padding * 2) / rows;
                    int miniSize = Math.Max(1, Math.Min(miniWidth, miniHeight) - 2);

                    for (int i = 0; i < maxIcons; i++)
                    {
                        var childImg = ChildIcons[i];
                        int c = i % cols;
                        int r = i / cols;
                        int cx = padding + c * miniWidth + (miniWidth - miniSize) / 2;
                        int cy = padding + r * miniHeight + (miniHeight - miniSize) / 2;

                        if (childImg != null)
                        {
                            IconExtractor.DrawFit(e.Graphics, childImg, new Rectangle(cx, cy, miniSize, miniSize));
                        }
                    }
                }
            }
            else
            {
                // No solid tile fill: the icon sits directly on the panel background,
                // with only a soft highlight when hovered.
                if (IsHovered)
                {
                    bool lightParent = this.Parent != null && this.Parent.BackColor.GetBrightness() > 0.5f;
                    using (var hoverBrush = new SolidBrush(lightParent ? Color.FromArgb(35, 0, 0, 0) : Color.FromArgb(45, 255, 255, 255)))
                    {
                        e.Graphics.FillPath(hoverBrush, path);
                    }
                }

                if (IconImage != null)
                {
                    int textSpace = GetTextSpace();
                    int marginTop = 4;
                    int marginX = Math.Max(3, this.Width / 20);
                    int w = this.Width - marginX * 2;
                    int h = this.Height - textSpace - marginTop - 2;

                    // Icon scale setting, percent of the default size
                    Settings s = MainForm.CurrentSettings;
                    if (s != null)
                    {
                        double pct = Math.Max(0.25, Math.Min(4.0, s.IconScale / 100.0));
                        int sw = Math.Max(1, (int)Math.Round(w * pct));
                        int sh = Math.Max(1, (int)Math.Round(h * pct));
                        marginX += (w - sw) / 2;
                        marginTop += (h - sh) / 2;
                        w = sw;
                        h = sh;
                    }

                    var iconRect = new Rectangle(marginX, marginTop, w, h);
                    IconExtractor.DrawFit(e.Graphics, IconImage, iconRect);
                }
            }

            Color tColor = this.Parent != null ? this.Parent.ForeColor : Color.White;
            Font labelFont = null;
            Settings cfg = MainForm.CurrentSettings;
            if (cfg != null)
            {
                tColor = Settings.ParseColor(cfg.FontItemsColor, tColor);
                labelFont = Settings.MakeFont(cfg.FontItemsName, cfg.FontItemsSize);
            }
            int labelSpace = GetTextSpace();
            if (labelSpace > 0)
            {
                using (var brush = new SolidBrush(tColor))
                using (var font = labelFont != null ? labelFont : new Font("Segoe UI", 9f))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    Rectangle textRect = new Rectangle(4, this.Height - labelSpace - 1, this.Width - 8, labelSpace);
                    e.Graphics.DrawString(Item.Name, font, brush, textRect, sf);
                }
            }
            if (labelFont != null) labelFont.Dispose();
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
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 350, Text = defaultValue, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White };
            Button confirmation = new Button() { Text = "Ok", Left = 270, Top = 80, Width = 100, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(62, 62, 66) };
            confirmation.FlatAppearance.BorderSize = 0;

            if (MainForm.CurrentSettings != null)
                prompt.Font = Settings.MakeFont(MainForm.CurrentSettings.FontUiName, MainForm.CurrentSettings.FontUiSize);

            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
    }

    // Themed confirmation dialog used when removing elements.
    // Removing a folder shows a red warning that everything inside will be deleted too.
    public class ConfirmDialog : Form
    {
        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        private ConfirmDialog(string title, string message, string danger)
        {
            Settings st = MainForm.CurrentSettings;
            bool light = st != null && st.IsLightTheme;
            Color bg = light ? Color.FromArgb(232, 232, 234) : Color.FromArgb(24, 24, 28);
            Color panel = light ? Color.FromArgb(214, 214, 218) : Color.FromArgb(45, 45, 48);
            Color txt = light ? Color.Black : Color.White;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = bg;
            this.ForeColor = txt;
            this.ClientSize = new Size(430, danger == null ? 152 : 192);
            if (st != null) this.Font = Settings.MakeFont(st.FontUiName, st.FontUiSize);

            var titleLbl = new Label
            {
                Text = title,
                Left = 20,
                Top = 16,
                Width = 390,
                Height = 22,
                Font = Settings.MakeFont(st != null ? st.FontUiName : "Segoe UI", (st != null ? st.FontUiSize : 9) + 1, System.Drawing.FontStyle.Bold)
            };
            this.Controls.Add(titleLbl);

            var msgLbl = new Label
            {
                Text = message,
                Left = 20,
                Top = 44,
                Width = 390,
                Height = 36
            };
            this.Controls.Add(msgLbl);

            if (danger != null)
            {
                var dangerLbl = new Label
                {
                    Text = danger,
                    Left = 20,
                    Top = 80,
                    Width = 390,
                    Height = 56,
                    ForeColor = Color.FromArgb(235, 70, 70)
                };
                this.Controls.Add(dangerLbl);
            }

            int btnTop = this.ClientSize.Height - 42;
            var cancelBtn = new Button { Text = "Cancel", Left = 210, Top = btnTop, Width = 95, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = panel, ForeColor = txt };
            cancelBtn.FlatAppearance.BorderSize = 0;
            var okBtn = new Button { Text = "Remove", Left = 315, Top = btnTop, Width = 95, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(170, 48, 48), ForeColor = Color.White };
            okBtn.FlatAppearance.BorderSize = 0;

            this.Controls.Add(cancelBtn);
            this.Controls.Add(okBtn);
            this.AcceptButton = cancelBtn;
            this.CancelButton = cancelBtn;

            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(120, this.ForeColor)))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        public static bool Show(IWin32Window owner, ShortcutItem item)
        {
            string title;
            string message;
            string danger = null;
            if (item.IsFolder)
            {
                int count = CountItemsRecursive(item);
                title = "Remove Folder";
                message = "Remove folder \"" + item.Name + "\" from the panel?";
                danger = "Warning: everything inside the folder will be deleted" +
                         (count > 0 ? " (" + count + " item" + (count == 1 ? "" : "s") + ")" : "") + "!";
            }
            else
            {
                title = "Remove Element";
                message = "Remove \"" + item.Name + "\" from the panel?";
            }

            using (var dlg = new ConfirmDialog(title, message, danger))
            {
                return dlg.ShowDialog(owner) == DialogResult.OK;
            }
        }

        private static int CountItemsRecursive(ShortcutItem folderItem)
        {
            if (folderItem.Children == null || folderItem.Children.Count == 0) return 0;
            int n = 0;
            foreach (var c in folderItem.Children)
            {
                n++;
                if (c.IsFolder) n += CountItemsRecursive(c);
            }
            return n;
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

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

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
                Icon icon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();
                DestroyIcon(shfi.hIcon);
                return icon;
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
