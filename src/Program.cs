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
        private System.Windows.Forms.Timer folderIdleTimer;
        private DateTime lastActivityUtc = DateTime.UtcNow;
        private Button activeTabBtn;
        private Panel activeLayoutPanel;
        private TabData activeTabData;
        private readonly Dictionary<Button, TabData> tabDataByButton = new Dictionary<Button, TabData>();
        private TextBox panelSearchBox;
        private System.Windows.Forms.Timer panelSearchTimer;
        private Panel panelSearchOverlay;
        private Panel panelSearchRow;
        private ListBox panelSearchList;
        private Label panelSearchStatus;
        private bool panelSearchActive;
        private int panelSearchGen;
        // Set by ClearPanelSearch so the empty-box TextChanged (from clearing the
        // query) does not re-show the overlay as "Прошлый поиск" right after the
        // user closed the search by clicking a tab or pressing Esc.
        private bool suppressSearchOnEmpty;
        // Quick search settings: toggles to the right of the box, shown while the
        // box has text. State lives in Settings (persisted to settings.ini).
        private Panel panelSearchSettingsRow;
        private readonly Dictionary<Button, Action<int>> searchToggleRestylers = new Dictionary<Button, Action<int>>();
        private readonly Dictionary<Button, Func<int, string>> searchToggleLabels = new Dictionary<Button, Func<int, string>>();
        private readonly Dictionary<Button, int> searchToggleVariants = new Dictionary<Button, int>();
        private readonly Dictionary<Button, int> searchToggleDelay = new Dictionary<Button, int>();
        // Snapshot of the "Search" settings used by the background worker thread
        // (plain fields: the worker cannot touch the settings object safely).
        private static int fuzzyLevel = 2;
        private static bool useMeta = true, usePaths = true, useDesc = true;
        private static int descIndex = -1;
        private readonly List<ShortcutItem> panelSearchResults = new List<ShortcutItem>();
        private readonly Dictionary<string, Bitmap> panelSearchIcons = new Dictionary<string, Bitmap>();
        private List<string> panelSearchVariants = new List<string>(); // query variants for match highlighting
        private string lastSearchTip = null;
        private ToolTip itemTip;   // 0.3 s hover tooltip: descriptions + full search paths
        private string settingsPath = "settings.ini";
        private string recordsPath = "records.xml";
        private Panel topPanel;
        private Panel tabBar;
        private Panel rightPanel;
        private Panel contentPanel;
        private NotifyIcon trayIcon;
        private Icon appIcon;

        // "Update available" plate: set by UpdateChecker when a newer GitHub
        // release exists; LoadTabs builds the plate into the top-right corner.
        private string updateAvailableVersion;

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

        // Edit mode state: 0 = off, 1 = edit, 2 = multi-select mode (red button).
        private int editState = 0;
        private bool isEditMode { get { return editState >= 1; } }

        // Items picked in multi-select mode (red edit button); operations: delete,
        // move to another tab. Cleared when the tab or the mode changes.
        private readonly List<ShortcutItem> multiSelection = new List<ShortcutItem>();

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

        // Win-key capture ("panel instead of Start"): both physical Win keys.
        private const int HotkeyIdWinL = 0x5712;
        private const int HotkeyIdWinR = 0x5713;
        private bool hotkeyWinRegistered = false;

        // Active decorative skin (null/"None" id = classic behavior).
        private Skin skin = Skin.None;

        public MainForm()
        {
            settings = Settings.Load(settingsPath);
            records = Records.Load(recordsPath);
            CurrentSettings = settings;

            // UI language, autostart entry and the "start hidden" mode
            // (used when Windows starts the app with the --minimized argument).
            Loc.Lang = string.IsNullOrEmpty(settings.Language) ? "ru" : settings.Language;
            try { AutoStart.Apply(settings.AutoStart, settings.AutoStartMinimized); } catch { }
            bool startHidden = false;
            try
            {
                foreach (var a in Environment.GetCommandLineArgs())
                    if (string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase)) startHidden = true;
            }
            catch { }
            if (startHidden && settings.AutoStartMinimized)
                this.Shown += (s, e) => { try { this.Hide(); } catch { } };

            // Auto-test hook: WINPANEL_AUTOTEST=settings opens the settings dialog right
            // after startup so automated screenshots can capture it.
            try
            {
                if (string.Equals(Environment.GetEnvironmentVariable("WINPANEL_AUTOTEST"), "settings", StringComparison.OrdinalIgnoreCase))
                    this.Shown += (s, e) => OpenSettings();
            }
            catch { }

            // First start: the welcome window (thanks + beta note + language and
            // update-check questions + optional example tiles). The FirstRunDone
            // flag makes it strictly once per data folder; skipped for the
            // screenshot autotest and when autostart put us straight into the tray.
            bool autotestMode = false;
            try { autotestMode = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINPANEL_AUTOTEST")); } catch { }
            if (!settings.FirstRunDone && !autotestMode && !(startHidden && settings.AutoStartMinimized))
                this.Shown += (s, e) => RunFirstStartWelcome();

            // Copy user shortcuts/icons that live outside the panel folder into <exe>\ico
            // so they are not lost when the originals are moved or deleted.
            ConsolidateAllRecords();

            // File-type rules (icons and "open with" per extension).
            FileTypes.Load(FileTypes.DefaultFilePath);

            editState = settings.EditModeState;
            if (editState < 0 || editState > 2) editState = settings.EditMode ? 1 : 0;

            try { SearchHistoryStore.Load(); }
            catch (Exception ex) { AppLog.Write("Search history load", ex); }

            ApplyThemeColors();

            this.Text = Loc.S("Tilettes", "Плиточки");
            this.Width = settings.StartupWidth;
            this.Height = settings.StartupHeight;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(settings.WindowX, settings.WindowY);
            this.FormBorderStyle = FormBorderStyle.None;
            this.MinimumSize = new Size(300, 200);
            this.DoubleBuffered = true;
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

            // ---- Panel search: the box sits in the top strip, results overlay the tiles ----
            panelSearchTimer = new System.Windows.Forms.Timer();
            panelSearchTimer.Interval = 220;
            panelSearchTimer.Tick += (s, e) => { panelSearchTimer.Stop(); RunPanelSearch(); };

            panelSearchOverlay = new Panel { Visible = false, BackColor = bgColor };
            panelSearchList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = bgColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 26,
                IntegralHeight = false
            };
            panelSearchList.DrawItem += PanelSearchList_DrawItem;
            panelSearchList.MouseMove += PanelSearchList_MouseMove;
            panelSearchList.DoubleClick += (s, e) => OpenPanelSearchResult(panelSearchList.SelectedIndex);
            panelSearchList.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    int i = panelSearchList.SelectedIndex;
                    if (i < 0 && panelSearchList.Items.Count > 0) i = 0;
                    OpenPanelSearchResult(i);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape) { ClearPanelSearch(); e.SuppressKeyPress = true; }
            };
            panelSearchStatus = new Label { Dock = DockStyle.Top, Height = this.Font.Height + 8, BackColor = bgColor };
            panelSearchOverlay.Controls.Add(panelSearchList);
            panelSearchOverlay.Controls.Add(panelSearchStatus);
            this.Controls.Add(panelSearchOverlay);

            // Shared tooltip (0.05 s hover): item descriptions and full search paths.
            itemTip = new ToolTip { InitialDelay = 50, ReshowDelay = 50, AutoPopDelay = 8000, ShowAlways = true };
            ApplySearchListFont();

            // Type anywhere (except text inputs) to start the search; Esc clears it.
            this.KeyPreview = true;
            this.KeyPress += (s, e) =>
            {
                try
                {
                    if (char.IsControl(e.KeyChar)) return;
                    if ((Control.ModifierKeys & (Keys.Control | Keys.Alt)) != 0) return;
                    if (ActiveControl is TextBoxBase) return;
                    if (panelSearchBox == null || !panelSearchBox.Visible) return;
                    panelSearchBox.Focus();
                    panelSearchBox.Text = panelSearchBox.Text + e.KeyChar;
                    panelSearchBox.SelectionStart = panelSearchBox.Text.Length;
                    e.Handled = true;
                }
                catch { }
            };
            this.KeyDown += (s, e) =>
            {
                try
                {
                    if (e.KeyCode == Keys.Escape && panelSearchActive) { ClearPanelSearch(); e.SuppressKeyPress = true; return; }
                    if (e.Control && e.KeyCode == Keys.F)
                    {
                        if (panelSearchBox != null) { panelSearchBox.Focus(); panelSearchBox.SelectAll(); }
                        e.SuppressKeyPress = true;
                    }
                }
                catch { }
            };

            trayIcon = new NotifyIcon();
            trayIcon.Text = Loc.S("Tilettes", "Плиточки") + " v" + AppInfo.AppVersion;
            appIcon = CreateAppIcon();
            if (appIcon != null) trayIcon.Icon = appIcon;
            trayIcon.DoubleClick += (s, e) => RestoreWindow();

            var trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add(Loc.S("Restore"), (s, e) => RestoreWindow());
            trayMenu.MenuItems.Add(Loc.S("Settings"), (s, e) => OpenSettings());
            trayMenu.MenuItems.Add(Loc.S("Exit"), (s, e) => { Application.Exit(); });
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = settings.TrayIconAlways;

            // Folder auto-exit: when the user is inside a folder and no mouse/keyboard
            // activity happens for N seconds, the panel goes back (see FolderIdleTick).
            folderIdleTimer = new System.Windows.Forms.Timer();
            folderIdleTimer.Interval = 1000;
            folderIdleTimer.Tick += (s, e) => FolderIdleTick();
            folderIdleTimer.Start();
            Application.AddMessageFilter(new ActivityFilter(this));

            this.FormClosing += MainForm_FormClosing;
            this.FormClosed += (s, e) =>
            {
                if (hotkeyRegistered)
                {
                    UnregisterHotKey(this.Handle, HotkeyId);
                    hotkeyRegistered = false;
                }
                if (hotkeyWinRegistered)
                {
                    try { UnregisterHotKey(this.Handle, HotkeyIdWinL); } catch { }
                    try { UnregisterHotKey(this.Handle, HotkeyIdWinR); } catch { }
                    hotkeyWinRegistered = false;
                }
                if (winKeyHook != IntPtr.Zero)
                {
                    try { UnhookWindowsHookEx(winKeyHook); } catch { }
                    winKeyHook = IntPtr.Zero;
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

            // Start tab auto-layout: senior folders on the top row, their subfolders
            // copied as quick-access tiles (pure re-layout of the mirror data — no
            // path or content is touched). Safe to run on every start.
            try
            {
                if (StartMenuSync.RelayoutStartTab(records, settings.GridColumns, settings.GridRows))
                    records.Save(recordsPath);
            }
            catch (Exception ex) { AppLog.Write("Start tab layout", ex); }

            // Overflow protection: after the user shrank the grid, items that no
            // longer fit move into a last-resort folder (kept for later restore).
            try
            {
                bool changed = false;
                foreach (var tab in records.Tabs) changed |= EnsureTabFits(tab);
                if (changed) records.Save(recordsPath);
            }
            catch (Exception ex) { AppLog.Write("Overflow pass", ex); }

            // Test hook: WINPANEL_MOCK_UPDATE=0.6 renders the "Update" plate as
            // if a newer release existed — no network involved, lets the plate
            // be inspected before the first real release is published.
            try
            {
                string mock = Environment.GetEnvironmentVariable("WINPANEL_MOCK_UPDATE");
                if (!string.IsNullOrEmpty(mock)) updateAvailableVersion = mock;
            }
            catch { }

            LoadTabs();

            ApplyHotkey();

            AddEdgeGrips();

            // Scheduled maintenance: a full backup (3 minutes after launch when due)
            // and the Start Menu mirror sync (20 seconds after launch when due).
            try { BackupManager.ScheduleIfNeeded(this, settings); }
            catch (Exception ex) { AppLog.Write("Backup schedule", ex); }
            try { StartMenuSync.ScheduleIfNeeded(this, settings); }
            catch (Exception ex) { AppLog.Write("Sync schedule", ex); }

            // No update check is scheduled here on purpose: before the welcome
            // window the user has not consented yet, and the flag may still hold
            // the default. The check is scheduled after the welcome (only when
            // allowed) and after settings are saved.
        }

        // WS_EX_COMPOSITED: the window (with all children) paints double-buffered,
        // which removes the blink/re-render flash while the window is moved or resized.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        private bool sizing;
        private int lastRegionW = -1, lastRegionH = -1;

        private void UpdateWindowRegion()
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.Region = null;
                lastRegionW = -1;
                UpdateBorderOverlay();
                return;
            }
            // Recreate the rounded region only when the size actually changed, so a
            // plain window move never churns the region (another flicker source).
            if (Width == lastRegionW && Height == lastRegionH && this.Region != null) return;
            lastRegionW = Width;
            lastRegionH = Height;
            // GDI rounds the region off by the last pixel column/row (the form client
            // is Width x Height, but a W x H region stops at W-1 / H-1) — a 1px stripe
            // of whatever is behind the window shone through on the right/bottom edge.
            // Extending the region by 1px covers the full client area.
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 15, 15));
            UpdateBorderOverlay();
        }

        // Decorative skin frame: a thin colored line following the rounded window
        // contour. Lives on an overlay that is transparent to the mouse, so tiles,
        // grips and panels underneath keep working.
        private BorderOverlay borderOverlay;

        private void UpdateBorderOverlay()
        {
            try
            {
                if (!Skin.IsActive(skin) || skin.BorderWidth <= 0 || WindowState == FormWindowState.Maximized)
                {
                    if (borderOverlay != null) { borderOverlay.Dispose(); borderOverlay = null; }
                    return;
                }
                if (borderOverlay == null || borderOverlay.IsDisposed)
                {
                    borderOverlay = new BorderOverlay();
                    this.Controls.Add(borderOverlay);
                }
                borderOverlay.SkinBorder = skin.Border;
                borderOverlay.BorderSize = Math.Max(1, skin.BorderWidth);
                borderOverlay.Bounds = new Rectangle(0, 0, Width, Height);
                borderOverlay.BringToFront();
                borderOverlay.Invalidate();
            }
            catch (Exception ex) { AppLog.Write("Border overlay", ex); }
        }

        // Clicks and drag-resize must fall through the frame to the real controls.
        // The frame is drawn in OnPaint (no Control.Region: assigning a Region
        // before the handle exists blows up in Region.GetHrgn on some systems).
        private class BorderOverlay : Control
        {
            public Color SkinBorder = Color.Gray;
            public int BorderSize = 2;

            public BorderOverlay()
            {
                this.Enabled = false;
                this.TabStop = false;
                // No DoubleBuffered here: a buffered transparent control copies the
                // uninitialized buffer to the screen and renders as a black rect.
                SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            }
            protected override CreateParams CreateParams
            {
                get { var cp = base.CreateParams; cp.ExStyle |= 0x20; return cp; } // WS_EX_TRANSPARENT
            }
            protected override void OnPaintBackground(PaintEventArgs e) { }
            protected override void OnPaint(PaintEventArgs e)
            {
                try
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    int bw = Math.Max(1, BorderSize);
                    using (var pen = new Pen(SkinBorder, bw))
                        e.Graphics.DrawPath(pen, GetRoundedRectPath(new Rectangle(bw / 2, bw / 2, Math.Max(1, Width - bw - 1), Math.Max(1, Height - bw - 1)), 15));
                }
                catch { }
            }
            private System.Drawing.Drawing2D.GraphicsPath GetRoundedRectPath(Rectangle bounds, int radius)
            {
                var path = new System.Drawing.Drawing2D.GraphicsPath();
                int d = radius * 2;
                if (bounds.Width < d || bounds.Height < d) { path.AddRectangle(bounds); return path; }
                Rectangle arc = new Rectangle(bounds.Location, new Size(d, d));
                path.AddArc(arc, 180, 90);
                arc.X = bounds.Right - d; path.AddArc(arc, 270, 90);
                arc.Y = bounds.Bottom - d; path.AddArc(arc, 0, 90);
                arc.X = bounds.Left; path.AddArc(arc, 90, 90);
                path.CloseFigure();
                return path;
            }
            protected override void WndProc(ref Message m)
            {
                const int WM_NCHITTEST = 0x84;
                if (m.Msg == WM_NCHITTEST) { m.Result = (IntPtr)(-1); return; } // HTTRANSPARENT
                base.WndProc(ref m);
            }
        }

        // Invisible edge grips: the borderless window is resizable from any edge or
        // corner even over child controls (panels cover the client area, so the form
        // itself never receives the hit-test messages there).
        private readonly List<Panel> edgeGrips = new List<Panel>();

        private void AddEdgeGrip(int x, int y, int w, int h, Cursor cur, int hitTest, AnchorStyles anchor)
        {
            var p = new Panel { Size = new Size(w, h), Location = new Point(x, y), Cursor = cur, BackColor = bgColor, Anchor = anchor };
            p.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, hitTest, 0); // WM_NCLBUTTONDOWN with an edge hit-test code
                }
            };
            this.Controls.Add(p);
            p.BringToFront();
            edgeGrips.Add(p);
        }

        private void AddEdgeGrips()
        {
            AddEdgeGrip(0, 0, Width, 6, Cursors.SizeNS, 12, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            AddEdgeGrip(0, 0, 6, Height, Cursors.SizeWE, 10, AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom);
            AddEdgeGrip(Width - 6, 0, 6, Height, Cursors.SizeWE, 11, AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom);
            AddEdgeGrip(0, Height - 6, Width, 6, Cursors.SizeNS, 15, AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right);
            AddEdgeGrip(0, 0, 14, 14, Cursors.SizeNWSE, 13, AnchorStyles.Top | AnchorStyles.Left);
            AddEdgeGrip(Width - 14, 0, 14, 14, Cursors.SizeNESW, 14, AnchorStyles.Top | AnchorStyles.Right);
            AddEdgeGrip(0, Height - 14, 14, 14, Cursors.SizeNESW, 16, AnchorStyles.Bottom | AnchorStyles.Left);
            AddEdgeGrip(Width - 14, Height - 14, 14, 14, Cursors.SizeNWSE, 17, AnchorStyles.Bottom | AnchorStyles.Right);
        }

        // Edge resizing for the borderless window
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                RestoreWindow();
                return;
            }
            if (m.Msg == WM_HOTKEY)
            {
                int hkId = m.WParam.ToInt32();
                if (hkId == HotkeyIdWinL || hkId == HotkeyIdWinR)
                {
                    ToggleByWinKey();
                    return;
                }
            }
            if (m.Msg == SingleInstance.ShowMessage)
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
            const int WM_SIZING = 0x0214;
            const int WM_EXITSIZEMOVE = 0x0232;
            if (m.Msg == WM_SIZING)
            {
                // While the user drags a border: drop the rounded region so the whole
                // window paints with no clipping gaps (this removed the resize flicker
                // in the mini explorer; same fix here).
                if (!sizing)
                {
                    sizing = true;
                    try { this.Region = null; } catch { }
                    lastRegionW = -1;
                }
            }
            else if (m.Msg == WM_EXITSIZEMOVE)
            {
                if (sizing)
                {
                    sizing = false;
                    UpdateWindowRegion();
                }
            }
            base.WndProc(ref m);
        }

        // Program-drawn tray icon: rounded gradient square with a "W"
        // Tray icon drawn to match app.ico: the Tilettes 2x2 tile grid
        // (mint / amber / coral / blue rounded squares).
        private Icon CreateAppIcon()
        {
            try
            {
                using (var bmp = new Bitmap(32, 32))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);
                        int size = 32;
                        float m = size * 0.10f;
                        float gap = size * 0.09f;
                        float tile = (size - 2 * m - gap) / 2f;
                        float r = tile * 0.28f;
                        Color[] colors =
                        {
                            Color.FromArgb(46, 160, 110),
                            Color.FromArgb(245, 180, 60),
                            Color.FromArgb(235, 90, 70),
                            Color.FromArgb(70, 140, 230)
                        };
                        for (int i = 0; i < 4; i++)
                        {
                            int col = i % 2;
                            int row = i / 2;
                            float x = m + col * (tile + gap);
                            float y = m + row * (tile + gap);
                            using (var path = new GraphicsPath())
                            {
                                float d = r * 2;
                                path.AddArc(x, y, d, d, 180, 90);
                                path.AddArc(x + tile - d, y, d, d, 270, 90);
                                path.AddArc(x + tile - d, y + tile - d, d, d, 0, 90);
                                path.AddArc(x, y + tile - d, d, d, 90, 90);
                                path.CloseFigure();
                                using (var brush = new SolidBrush(colors[i]))
                                {
                                    g.FillPath(brush, path);
                                }
                            }
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
                    // A backup archive requested in the dialog is unpacked first;
                    // the reload below then picks the restored files up from disk,
                    // so the restore takes effect immediately (no restart, and the
                    // in-memory state can no longer overwrite it on save).
                    bool restored = false;
                    if (!string.IsNullOrEmpty(sf.RestoreZipPath))
                    {
                        int n;
                        string err = BackupManager.RestoreZip(sf.RestoreZipPath, out n);
                        if (err != null)
                            MessageBox.Show(this, Loc.S("Restore failed: ", "Восстановление не удалось: ") + err, Loc.S("Tilettes", "Плиточки"));
                        else
                        {
                            restored = true;
                            AppLog.Write("Backup restore applied: " + sf.RestoreZipPath + " (" + n + " files)");
                        }
                    }

                    this.settings = Settings.Load(settingsPath);
                    CurrentSettings = this.settings;
                    if (restored)
                    {
                        // The archive replaced the data files: drop everything the
                        // app cached in memory so the restored state is live.
                        records = Records.Load(recordsPath);
                        tabNavigations.Clear();
                        editState = settings.EditModeState;
                        if (editState < 0 || editState > 2) editState = settings.EditMode ? 1 : 0;
                        try { SearchHistoryStore.Load(); }
                        catch (Exception ex) { AppLog.Write("Search history load", ex); }
                        try { FileTypes.Load(FileTypes.DefaultFilePath); } catch { }
                    }
                    this.Width = settings.StartupWidth;
                    this.Height = settings.StartupHeight;
                    this.Location = new Point(settings.WindowX, settings.WindowY);
                    ApplyThemeColors();
                    mainFont = Settings.MakeFont(settings.FontUiName, settings.FontUiSize);
                    this.Font = mainFont;
                    ApplyHotkey();
                    Loc.Lang = string.IsNullOrEmpty(settings.Language) ? "ru" : settings.Language;
                    try { AutoStart.Apply(settings.AutoStart, settings.AutoStartMinimized); } catch { }
                    try { trayIcon.Visible = settings.TrayIconAlways; } catch { }
                    Loc.Walk(this);
                    try { if (panelSearchBox != null) panelSearchBox.Font = Settings.MakeFont(panelSearchBox.Font.FontFamily.Name, Math.Max(7, Math.Min(30, settings.SearchBoxFontSize))); } catch { }
                    ApplySearchListFont();
                    try { if (panelSearchStatus != null) panelSearchStatus.Height = this.Font.Height + 8; } catch { }

                    // A smaller grid may no longer hold every item: re-run overflow.
                    try
                    {
                        bool changed = false;
                        foreach (var tab in records.Tabs) changed |= EnsureTabFits(tab);
                        if (changed) records.Save(recordsPath);
                    }
                    catch (Exception ex) { AppLog.Write("Overflow pass", ex); }

                    LoadTabs();

                    // "Do it now" requests from the settings dialog.
                    if (sf.RunBackupNow) BackupManager.RunBackup(this, this.settings, true);
                    if (sf.RunSyncNow) StartMenuSync.Run(this, this.settings, true);
                    if (sf.RunWelcomeAgain) RunFirstStartWelcome();
                    if (sf.RunCheckNow)
                    {
                        // Explicit user action: ignores the auto-check flag and interval.
                        UpdateChecker.CheckNow(this, this.settings);
                    }

                    // Update settings may have changed: re-evaluate the schedule
                    // (a no-op when the check is disabled — hard rule).
                    try { UpdateChecker.ScheduleCheck(this, this.settings); }
                    catch (Exception ex) { AppLog.Write("Update schedule", ex); }
                }
            }
        }

        // ---- First start: welcome window and example tiles ----

        private void RunFirstStartWelcome()
        {
            try
            {
                bool wasFirstRun = !settings.FirstRunDone;
                bool createExamples = false;
                using (var wf = new WelcomeForm(settings))
                {
                    wf.ShowDialog(this);
                    createExamples = wf.CreateExamples;
                }
                // Standard settings land in settings.ini right on the first start,
                // together with the welcome answers (language, update check).
                settings.FirstRunDone = true;
                if (wasFirstRun)
                {
                    // The very first automatic check happens UpdateCheckDays days
                    // after the install, not immediately.
                    settings.LastUpdateCheck = DateTime.Now.ToString("yyyyMMddHHmmss");
                }
                settings.Save(settingsPath);

                Loc.Lang = string.IsNullOrEmpty(settings.Language) ? "ru" : settings.Language;
                ApplyThemeColors();
                mainFont = Settings.MakeFont(settings.FontUiName, settings.FontUiSize);
                this.Font = mainFont;
                try { trayIcon.Text = Loc.S("Tilettes", "Плиточки") + " v" + AppInfo.AppVersion; } catch { }

                if (createExamples) CreateExampleTiles();
                LoadTabs();

                // Only schedules a network check when the user allowed it in the
                // welcome window; the interval (and the "first check later" stamp
                // above) decides when it actually fires.
                try { UpdateChecker.ScheduleCheck(this, settings); }
                catch (Exception ex) { AppLog.Write("Update schedule", ex); }
            }
            catch (Exception ex) { AppLog.Write("First start welcome", ex); }
        }

        // A few ready tiles (standard Windows programs) so the panel is not empty
        // when the user chose "create examples" in the welcome window.
        private void CreateExampleTiles()
        {
            try
            {
                if (records.Tabs.Count == 0) return;
                var tab = records.Tabs[0];
                if (tab.Items.Count > 0) return; // content exists — add nothing
                string sys = Environment.SystemDirectory;
                string windir = Environment.GetEnvironmentVariable("windir") ?? "C:\\Windows";
                AddExampleTile(tab, Path.Combine(sys, "notepad.exe"), Loc.S("Notepad", "Блокнот"), Loc.S("Simple text editor", "Простой текстовый редактор"));
                AddExampleTile(tab, Path.Combine(sys, "calc.exe"), Loc.S("Calculator", "Калькулятор"), Loc.S("Windows calculator", "Калькулятор Windows"));
                AddExampleTile(tab, Path.Combine(windir, "mspaint.exe"), "Paint", null);
                AddExampleTile(tab, Path.Combine(windir, "explorer.exe"), Loc.S("Explorer", "Проводник"), Loc.S("File manager", "Файловый менеджер"));
                records.Save(recordsPath);
            }
            catch (Exception ex) { AppLog.Write("Example tiles", ex); }
        }

        private void AddExampleTile(TabData tab, string path, string name, string desc)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                var it = new ShortcutItem
                {
                    Path = path,
                    Name = name,
                    Size = Math.Max(2, settings.DefaultItemSize),
                    ShortDescription = desc
                };
                MainForm.PlaceIntoGridStatic(tab.Items, it, settings.GridColumns, settings.GridRows);
                tab.Items.Add(it);
            }
            catch { }
        }

        // ---- Update plate ("Обновить" next to the settings button) ----

        public void ShowUpdatePlate(string version)
        {
            try
            {
                updateAvailableVersion = version;
                ShowBalloon(Loc.S("New version available: ", "Доступна новая версия: ") + version);
                LoadTabs();
            }
            catch { }
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

            // Decorative skin (Android, Night, ...): its own palette replaces the
            // theme colors; the background becomes a gradient and a border line
            // frames the window. Empty id keeps the classic behavior untouched.
            try { skin = Skin.Find(settings != null ? settings.SkinName : null); } catch { skin = Skin.None; }
            if (Skin.IsActive(skin))
            {
                bgColor = skin.BgTop;
                panelColor = skin.Panel;
                hoverColor = skin.Hover;
                textColor = skin.Text;
            }

            this.BackColor = bgColor;
            this.ForeColor = textColor;
            if (topPanel != null) topPanel.BackColor = bgColor;
            if (tabBar != null) tabBar.BackColor = bgColor;
            if (rightPanel != null) rightPanel.BackColor = bgColor;
            if (contentPanel != null) contentPanel.BackColor = bgColor;
            foreach (var grip in edgeGrips) grip.BackColor = bgColor;

            // Custom UI text color overrides the theme color
            Color uiOverride = Settings.ParseColor(settings != null ? settings.FontUiColor : "", Color.Empty);
            if (uiOverride != Color.Empty)
            {
                textColor = uiOverride;
                this.ForeColor = textColor;
            }
            try { this.Invalidate(); } catch { }
            try { UpdateBorderOverlay(); } catch { }
        }

        // Skin background: vertical gradient between the two skin colors.
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            try
            {
                if (!Skin.IsActive(skin)) { base.OnPaintBackground(e); return; }
                using (var brush = new LinearGradientBrush(this.ClientRectangle, skin.BgTop, skin.BgBottom, LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(brush, this.ClientRectangle);
                }
            }
            catch { base.OnPaintBackground(e); }
        }

        // Skin border: a rounded outer line around the main window.
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            try
            {
                if (!Skin.IsActive(skin) || WindowState == FormWindowState.Maximized) return;
                int bw = Math.Max(1, skin.BorderWidth);
                using (var pen = new Pen(skin.Border, bw))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    int inset = bw / 2;
                    e.Graphics.DrawPath(pen, GetRoundedRectPath(new Rectangle(inset, inset, Width - bw - 1, Height - bw - 1), 15));
                }
            }
            catch { }
        }

        // ---- helpers used by the scheduled subsystems (backup / sync) ----

        public string SettingsFilePath { get { return settingsPath; } }
        public string RecordsFilePath { get { return recordsPath; } }
        public Records Records { get { return records; } }

        // Tray balloon from any thread-safe context.
        public void ShowBalloon(string text)
        {
            try
            {
                if (trayIcon == null) return;
                trayIcon.ShowBalloonTip(3000, Loc.S("Tilettes", "Плиточки"), text ?? "", ToolTipIcon.Info);
            }
            catch { }
        }

        // The records were replaced underneath the form (Start Menu sync): re-read
        // them, re-apply overflow protection and rebuild the UI.
        public void OnDataExternallyChanged()
        {
            try
            {
                records = Records.Load(recordsPath);
                foreach (var tab in records.Tabs)
                    if (!tabNavigations.ContainsKey(tab)) tabNavigations[tab] = new Stack<ShortcutItem>();
                foreach (var tab in records.Tabs) EnsureTabFits(tab);
                LoadTabs();
            }
            catch (Exception ex) { AppLog.Write("OnDataExternallyChanged", ex); }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (settings.MinimizeToTray)
                {
                    e.Cancel = true;
                    this.Hide();
                    try { trayIcon.Visible = true; } catch { }
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
            try { if (trayIcon != null) trayIcon.Visible = settings.TrayIconAlways; } catch { }
        }

        // ---- folder auto-exit on inactivity ----

        internal void TouchActivity()
        {
            lastActivityUtc = DateTime.UtcNow;
        }

        private void FolderIdleTick()
        {
            try
            {
                if (settings == null) return;
                int secs = settings.FolderAutoExitSeconds;
                if (secs <= 0) return;
                if (settings.OpenFoldersInPopup) return;
                if (activeTabData == null || activeLayoutPanel == null) return;
                var navStack = tabNavigations[activeTabData];
                if (navStack.Count == 0) return;
                if ((DateTime.UtcNow - lastActivityUtc).TotalSeconds < secs) return;
                navStack.Pop();
                RenderCurrentFolder(activeLayoutPanel, activeTabData);
                lastActivityUtc = DateTime.UtcNow;
            }
            catch { }
        }

        // Counts any mouse/keyboard message of our own windows as "activity".
        private class ActivityFilter : IMessageFilter
        {
            private MainForm form;
            public ActivityFilter(MainForm f) { form = f; }
            public bool PreFilterMessage(ref Message m)
            {
                int msg = m.Msg;
                if (msg == 0x0200 || msg == 0x0201 || msg == 0x0202 || msg == 0x0204 || msg == 0x0205 ||
                    msg == 0x0207 || msg == 0x0208 || msg == 0x020A ||
                    msg == 0x0100 || msg == 0x0101 || msg == 0x0102 || msg == 0x0104 || msg == 0x0105)
                {
                    form.TouchActivity();
                }
                return false;
            }
        }

        // ---- panel search ----

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessageStr(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private void RunPanelSearch()
        {
            try
            {
                if (panelSearchBox == null || panelSearchBox.IsDisposed) return;
                // The window may have been resized since the strip was built:
                // re-pick the caption shrink tier.
                StyleSearchToggles();
                string q = panelSearchBox.Text.Trim();
                if (q.Length == 0) { ShowPastSearch(); return; }
                if (activeTabData == null || activeLayoutPanel == null) return;
                var variants = SearchCore.Variants(q);
                if (variants.Count == 0) { HidePanelSearch(); return; }

                // Search spans every tab, so the mirrored Start Menu tab is reachable
                // from anywhere (its whole purpose is to feed the search) — unless the
                // user switched "Пуск" off in the quick search settings.
                var all = new List<ShortcutItem>();
                bool includeStart = settings.SearchInStart;
                foreach (var tab in records.Tabs)
                {
                    if (!includeStart && IsStartTab(tab)) continue;
                    CollectAllItems(tab.Items, all);
                }
                int gen = ++panelSearchGen;
                fuzzyLevel = Math.Max(0, Math.Min(3, settings.SearchFuzzyLevel));
                useMeta = settings.SearchInMeta; usePaths = settings.SearchInPaths; useDesc = settings.SearchInDesc;
                descIndex = PanelSearch.DescIndex;
                SearchCore.fuzzyLevelStatic = fuzzyLevel;

                panelSearchStatus.Text = Loc.IsRu ? "Поиск…" : "Searching…";
                panelSearchStatus.ForeColor = settings.IsLightTheme ? Color.FromArgb(110, 110, 115) : Color.FromArgb(165, 165, 170);
                panelSearchOverlay.Bounds = contentPanel.Bounds;
                panelSearchOverlay.Visible = true;
                panelSearchOverlay.BringToFront();
                panelSearchActive = true;

                var vv = variants;
                panelSearchVariants = vv;
                string qLower = q.ToLowerInvariant();
                System.Threading.ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    List<KeyValuePair<int, ShortcutItem>> found;
                    try { found = SearchItemsWorker(all, vv); }
                    catch { found = new List<KeyValuePair<int, ShortcutItem>>(); }
                    try
                    {
                        this.BeginInvoke((MethodInvoker)delegate()
                        {
                            if (this.IsDisposed || gen != panelSearchGen) return;
                            // History boost: queries that previously led to this exact
                            // item rank it far above fresh matches.
                            for (int i = 0; i < found.Count; i++)
                            {
                                int boost = 0;
                                try { boost = SearchHistoryStore.Boost(qLower, found[i].Value.Path) * 40; }
                                catch { }
                                if (boost != 0)
                                    found[i] = new KeyValuePair<int, ShortcutItem>(found[i].Key + boost, found[i].Value);
                            }
                            found.Sort(delegate(KeyValuePair<int, ShortcutItem> a, KeyValuePair<int, ShortcutItem> b)
                            {
                                if (b.Key != a.Key) return b.Key - a.Key;
                                return string.Compare(a.Value.Name, b.Value.Name, StringComparison.OrdinalIgnoreCase);
                            });
                            ApplySearchResults(found);
                        });
                    }
                    catch { }
                });
            }
            catch { }
        }

        // Empty search box: show what the user searched and opened before
        // ("Прошлый поиск"), best pairs first. Respects the history setting.
        private void ShowPastSearch()
        {
            try
            {
                if (!settings.SearchSaveHistory || activeTabData == null) { HidePanelSearch(); return; }
                var top = SearchHistoryStore.Top(15);
                if (top.Count == 0) { HidePanelSearch(); return; }
                panelSearchGen++;
                panelSearchResults.Clear();
                foreach (var e in top)
                {
                    var it = new ShortcutItem();
                    it.Name = (string.IsNullOrEmpty(e.Query) ? "" : e.Query + "  →  ") + e.Name;
                    it.Path = e.Path ?? "";
                    it.IsFolder = e.IsFolder;
                    panelSearchResults.Add(it);
                }
                panelSearchList.BeginUpdate();
                panelSearchList.Items.Clear();
                foreach (var it in panelSearchResults) panelSearchList.Items.Add(it.Name);
                panelSearchList.EndUpdate();
                panelSearchList.ClearSelected();
                panelSearchList.Invalidate();
                panelSearchVariants = new List<string>();
                panelSearchStatus.Text = Loc.IsRu
                    ? "Прошлый поиск · " + panelSearchResults.Count + " · Enter — открыть, Esc — закрыть"
                    : "Past search · " + panelSearchResults.Count + " · Enter to open, Esc to close";
                panelSearchOverlay.Bounds = contentPanel.Bounds;
                panelSearchOverlay.Visible = true;
                panelSearchOverlay.BringToFront();
                panelSearchActive = true;
            }
            catch (Exception ex) { AppLog.Write("Past search", ex); }
        }

        private static void CollectAllItems(List<ShortcutItem> items, List<ShortcutItem> outList)
        {
            foreach (var it in items)
            {
                outList.Add(it);
                if (it.Children != null && it.Children.Count > 0) CollectAllItems(it.Children, outList);
            }
        }

        // The mirrored Start Menu tab: by kind, or by its canonical names for tabs
        // created before the kind was stamped.
        private static bool IsStartTab(TabData tab)
        {
            if (tab == null) return false;
            if (tab.Kind == StartMenuSync.TabKind) return true;
            return tab.Name == Loc.S("Start", "Пуск") || tab.Name == Loc.S("Start Menu", "Пуск");
        }

        // ---------- quick search settings (right of the search box) ----------

        // Adds one toggle to the strip. `get` reports the state (raised vs dimmed),
        // `flip` switches it; both go through Settings so the state survives
        // restarts. `label` is re-evaluated on restyle (the fuzzy button shows the
        // current level).
        // `label(variant)` gives the caption per shrink step. `variants` is the
        // ladder length, `delay` orders the degradation: a toggle with a larger
        // delay keeps its full caption while others (smaller delay) shrink first.
        // Measurements use TextFormatFlags.NoPadding - the default padding double
        // counted here and shrank the strip even when the window had enough room.
        private void AddSearchToggle(Font font, int height, Func<int, string> label, int variants, int delay, Func<bool> get, Action flip, string tooltip)
        {
            var b = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = font,                  // same font as the search box
                Margin = new Padding(2, 0, 2, 0),
                BackColor = bgColor,
                ForeColor = textColor,
                Height = height               // matches the search box height
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = hoverColor;
            b.Text = label(0);
            b.Width = TextRenderer.MeasureText(b.Text, b.Font, new Size(int.MaxValue, int.MaxValue), System.Windows.Forms.TextFormatFlags.NoPadding).Width + 14;
            if (!string.IsNullOrEmpty(tooltip)) itemTip.SetToolTip(b, tooltip);
            b.Click += delegate
            {
                try { flip(); } catch { }
                try { settings.Save(settingsPath); } catch { }
                StyleSearchToggles();
                RerunPanelSearch();
            };
            panelSearchSettingsRow.Controls.Add(b);
            searchToggleLabels[b] = label;
            searchToggleVariants[b] = variants;
            searchToggleDelay[b] = delay;
            searchToggleRestylers[b] = delegate(int deg)
            {
                int vi = Math.Max(0, Math.Min(searchToggleVariants[b] - 1, deg - searchToggleDelay[b]));
                string text = label(vi);
                bool active;
                try { active = get(); } catch { active = false; }
                if (b.Text != text)
                {
                    b.Text = text;
                    b.Width = TextRenderer.MeasureText(text, b.Font, new Size(int.MaxValue, int.MaxValue), System.Windows.Forms.TextFormatFlags.NoPadding).Width + 14;
                }
                b.BackColor = active ? panelColor : bgColor;
                b.ForeColor = active ? textColor
                    : (settings.IsLightTheme ? Color.FromArgb(140, 140, 145) : Color.FromArgb(115, 115, 120));
            };
        }

        // Picks the degradation level that fits between the search box and the
        // window edge: level 0 = everything full, higher levels shrink toggles
        // in the order of their `delay` (metadata first, Start last).
        private int SearchToggleDeg()
        {
            var row = panelSearchSettingsRow != null ? panelSearchSettingsRow.Parent as Panel : null;
            if (row == null || panelSearchBox == null) return 0;
            int avail = row.ClientSize.Width - (panelSearchBox.Right + 10);
            int maxDeg = 0;
            foreach (var kv in searchToggleDelay)
                maxDeg = Math.Max(maxDeg, kv.Value + searchToggleVariants[kv.Key] - 1);
            for (int d = 0; d <= maxDeg; d++)
            {
                int total = 0;
                foreach (var kv in searchToggleLabels)
                {
                    int vi = Math.Max(0, Math.Min(searchToggleVariants[kv.Key] - 1, d - searchToggleDelay[kv.Key]));
                    total += TextRenderer.MeasureText(kv.Value(vi), kv.Key.Font, new Size(int.MaxValue, int.MaxValue), System.Windows.Forms.TextFormatFlags.NoPadding).Width + 18;
                }
                if (total <= avail) return d;
            }
            return maxDeg;
        }

        private void StyleSearchToggles()
        {
            if (panelSearchSettingsRow == null) return;
            int deg = SearchToggleDeg();
            foreach (var kv in searchToggleRestylers) kv.Value(deg);
        }

        // Re-runs the search right away so a toggle click updates the results.
        private void RerunPanelSearch()
        {
            try
            {
                if (panelSearchTimer != null) panelSearchTimer.Stop();
                RunPanelSearch();
            }
            catch { }
        }

        // Builds the strip of quick search toggles; called from LoadTabs.
        private void BuildSearchSettingsStrip(Panel searchRow, TextBox sBox)
        {
            panelSearchSettingsRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                BackColor = bgColor,
                // Align with the search box: same top, same height (DPI-safe).
                Location = new Point(sBox.Right + 10, sBox.Top),
                Visible = false
            };
            searchToggleRestylers.Clear();

            // Degradation ladders: metadata shrinks first, then descriptions,
            // then paths (folder icon), Start last (Windows-like glyph).
            Func<int, string> fuzzyLabel = delegate(int v)
            {
                int lvl = Math.Max(0, Math.Min(3, settings.SearchFuzzyLevel));
                return lvl == 0 ? Loc.S("Exact", "Точно") : "~ " + lvl;
            };
            Func<int, string> startLabel = delegate(int v)
            {
                return v == 0 ? Loc.S("Start", "Пуск") : "\u229E";
            };
            Func<int, string> pathsLabel = delegate(int v)
            {
                return v == 0 ? Loc.S("Paths", "Пути") : "C:\\";
            };
            Func<int, string> descLabel = delegate(int v)
            {
                return v == 0 ? Loc.S("Descriptions", "Описания") : (v == 1 ? Loc.S("Desc", "Опи") : Loc.S("D", "О"));
            };
            Func<int, string> metaLabel = delegate(int v)
            {
                return v == 0 ? Loc.S("Metadata", "Метаданные") : (v == 1 ? Loc.S("Meta", "Мета") : Loc.S("M", "М"));
            };
            // PreferredHeight is font-based (no handle needed): the toggles match the
            // search box even when a bigger search font grew it.
            AddSearchToggle(sBox.Font, sBox.PreferredHeight, fuzzyLabel, 1, 0,
                delegate { return settings.SearchFuzzyLevel > 0; },
                delegate { settings.SearchFuzzyLevel = (Math.Max(0, Math.Min(3, settings.SearchFuzzyLevel)) + 1) % 4; },
                Loc.S("Match precision: exact or fuzzy (0-3)", "Точность совпадения: точное или нечёткое (0-3)"));

            AddSearchToggle(sBox.Font, sBox.PreferredHeight, startLabel, 2, 5,
                delegate { return settings.SearchInStart; },
                delegate { settings.SearchInStart = !settings.SearchInStart; },
                Loc.S("Search in the Start Menu tab", "Искать во вкладке Пуск"));

            AddSearchToggle(sBox.Font, sBox.PreferredHeight, pathsLabel, 2, 4,
                delegate { return settings.SearchInPaths; },
                delegate { settings.SearchInPaths = !settings.SearchInPaths; },
                Loc.S("Search in full paths", "Искать в полных путях"));

            AddSearchToggle(sBox.Font, sBox.PreferredHeight, descLabel, 3, 2,
                delegate { return settings.SearchInDesc; },
                delegate { settings.SearchInDesc = !settings.SearchInDesc; },
                Loc.S("Search in item descriptions", "Искать в описаниях элементов"));

            AddSearchToggle(sBox.Font, sBox.PreferredHeight, metaLabel, 3, 0,
                delegate { return settings.SearchInMeta; },
                delegate { settings.SearchInMeta = !settings.SearchInMeta; },
                Loc.S("Search in exe name, product, company", "Искать в имени exe, продукте, компании"));

            StyleSearchToggles();
            searchRow.Controls.Add(panelSearchSettingsRow);
        }

        // Runs on a background thread. Uses the character-mask prefilter so that
        // only plausible candidates reach the (more expensive) scoring step.
        private static List<KeyValuePair<int, ShortcutItem>> SearchItemsWorker(List<ShortcutItem> items, List<string> variants)
        {
            var found = new List<KeyValuePair<int, ShortcutItem>>();
            int vn = variants.Count;
            var vA = new ulong[vn];
            var vB = new ulong[vn];
            var vAllow = new int[vn];
            for (int k = 0; k < vn; k++)
            {
                SearchCore.MakeMask(variants[k], out vA[k], out vB[k]);
                int lvl = fuzzyLevel;
                vAllow[k] = lvl <= 0 ? 0 : Math.Max(1, (int)Math.Round(variants[k].Length / (6.0 - lvl)));
                if (lvl >= 3) vAllow[k] = Math.Max(vAllow[k], 3);
            }

            foreach (var it in items)
            {
                ulong iA, iB;
                try { PanelSearch.GetMask(it, out iA, out iB); }
                catch { continue; }
                string[] metas;
                try { metas = PanelSearch.GetMetas(it); }
                catch { continue; }
                int best = -1;
                for (int k = 0; k < vn; k++)
                {
                    if (SearchCore.MissingBits(vA[k], iA, vB[k], iB) > vAllow[k]) continue;
                    for (int i = 0; i < metas.Length; i++)
                    {
                        if (i == 4 && !usePaths) continue;
                        if ((i == 2 || i == 3) && !usePaths) continue;
                        if (i == 1 && !useMeta) continue;
                        if (i == descIndex && !useDesc) continue;
                        if (i >= 6 && !useMeta) continue;
                        int s = SearchCore.ScoreVariant(metas[i], variants[k]);
                        if (s < 0) continue;
                        s += MetaBoost(i);
                        if (s > best) best = s;
                    }
                }
                if (best >= 0) found.Add(new KeyValuePair<int, ShortcutItem>(best, it));
            }
            return found;
        }

        // Ranking boost depending on which field matched: an item whose own name
        // (or description) matches ranks above one that matched only in its path.
        private static int MetaBoost(int metaIndex)
        {
            if (metaIndex == 0) return 140;                  // display name
            if (metaIndex == 1) return 80;                   // file name
            if (metaIndex == descIndex) return 60;           // user description
            if (metaIndex == 2 || metaIndex == 3) return 30; // parent folder names
            if (metaIndex == 4) return 10;                   // full path
            return 0;                                        // version info etc.
        }

        private void ApplySearchResults(List<KeyValuePair<int, ShortcutItem>> found)
        {
            try
            {
                panelSearchResults.Clear();
                int n = Math.Min(200, found.Count);
                for (int i = 0; i < n; i++) panelSearchResults.Add(found[i].Value);
                panelSearchList.BeginUpdate();
                panelSearchList.Items.Clear();
                foreach (var it in panelSearchResults) panelSearchList.Items.Add(it.Name);
                panelSearchList.EndUpdate();
                panelSearchList.ClearSelected();
                panelSearchList.Invalidate();
                panelSearchStatus.Text = (Loc.IsRu ? "Найдено: " : "Found: ") + panelSearchResults.Count +
                    (Loc.IsRu ? "   ·   Enter — открыть, Esc — закрыть" : "   ·   Enter to open, Esc to close");
            }
            catch { }
        }

        private void HidePanelSearch()
        {
            panelSearchGen++;
            if (panelSearchOverlay != null) panelSearchOverlay.Visible = false;
            panelSearchActive = false;
            if (panelSearchResults != null) panelSearchResults.Clear();
            panelSearchVariants = new List<string>();
            lastSearchTip = null;
        }

        private void ClearPanelSearch()
        {
            HidePanelSearch();
            try
            {
                if (panelSearchBox != null && panelSearchBox.Text.Length > 0)
                {
                    suppressSearchOnEmpty = true;
                    panelSearchBox.Clear();
                }
            }
            catch { }
        }

        private void OpenPanelSearchResult(int i)
        {
            if (i < 0 || i >= panelSearchResults.Count) return;
            var it = panelSearchResults[i];
            string q = panelSearchBox != null && panelSearchBox.Text != null ? panelSearchBox.Text.Trim() : "";
            bool fromPast = q.Length == 0; // clicked inside "Прошлый поиск"
            ClearPanelSearch();
            // Remember "query -> opened item" so the same pair ranks higher next time.
            if (settings.SearchSaveHistory && !fromPast && !string.IsNullOrEmpty(it.Path))
            {
                SearchHistoryStore.Record(q, it.Name, it.Path, it.IsFolder);
                SearchHistoryStore.Save();
            }
            if (it.IsFolder)
            {
                var nav = tabNavigations[activeTabData];
                nav.Push(it);
                RenderCurrentFolder(activeLayoutPanel, activeTabData);
            }
            else
            {
                LaunchItem(it.Path);
            }
        }

        private void PanelSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                ClearPanelSearch();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                try
                {
                    panelSearchList.Focus();
                    if (panelSearchList.Items.Count > 0 && panelSearchList.SelectedIndex < 0) panelSearchList.SelectedIndex = 0;
                }
                catch { }
                e.SuppressKeyPress = true;
            }
        }

        private void PanelSearchList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= panelSearchResults.Count) return;
            var it = panelSearchResults[e.Index];
            var g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var back = new SolidBrush(sel ? hoverColor : bgColor))
                g.FillRectangle(back, e.Bounds);
            try
            {
                string key = ((it.IsFolder ? "d:" : "f:") + (it.Path ?? "")).ToLowerInvariant();
                Bitmap ic;
                if (!panelSearchIcons.TryGetValue(key, out ic))
                {
                    Image big = null;
                    try { if (!string.IsNullOrEmpty(it.Path)) big = IconExtractor.GetIconAuto(it.Path, false); }
                    catch { }
                    ic = null;
                    if (big != null)
                    {
                        ic = new Bitmap(16, 16);
                        using (var gg = Graphics.FromImage(ic))
                        {
                            gg.Clear(Color.Transparent);
                            IconExtractor.DrawFit(gg, big, new Rectangle(0, 0, 16, 16));
                        }
                        big.Dispose();
                    }
                    panelSearchIcons[key] = ic;
                }
                int iconY = e.Bounds.Top + Math.Max(3, (panelSearchList.ItemHeight - 16) / 2);
                if (ic != null) g.DrawImage(ic, new Rectangle(e.Bounds.Left + 8, iconY, 16, 16));
            }
            catch { }

            Font f = this.Font;
            Color acc = settings.IsLightTheme ? Color.FromArgb(0, 102, 204) : Color.FromArgb(96, 180, 255);
            bool light = settings.IsLightTheme;
            Color subColor = settings.IsLightTheme ? Color.FromArgb(120, 120, 125) : Color.FromArgb(150, 150, 155);
            int textH = TextRenderer.MeasureText("Ag", f).Height;
            int ty = e.Bounds.Top + Math.Max(4, (panelSearchList.ItemHeight - textH) / 2);
            int left = e.Bounds.Left + 32;

            // Right column: for shortcuts both paths are shown (lnk -> target), the
            // query can match either of them; when it does not fit, only its tail.
            string path = it.Path ?? "";
            string targetPath = "";
            try { if (path.ToLowerInvariant().EndsWith(".lnk")) targetPath = PanelSearch.GetTarget(it); }
            catch { }
            string pathCombo = path;
            if (targetPath.Length > 0 && !string.Equals(targetPath, path, StringComparison.OrdinalIgnoreCase))
                pathCombo = path + "  →  " + targetPath;
            int maxPathW = (int)(e.Bounds.Width * 0.45);
            string pathDisplay = pathCombo.Length > 0 ? UiText.FitTail(pathCombo, f, maxPathW) : "";
            int pathW = pathDisplay.Length > 0 ? TextRenderer.MeasureText(pathDisplay, f).Width : 0;
            int pathX = e.Bounds.Right - 8 - pathW;

            // Middle column: the user description
            string desc = PanelSearch.GetDescription(it);
            bool hasDesc = !string.IsNullOrEmpty(desc);

            // Left column: name (the query match is highlighted)
            int hlStart, hlLen;
            bool nameHl = UiText.FindHighlight((it.Name ?? "").ToLowerInvariant(), panelSearchVariants, out hlStart, out hlLen);
            int maxNameW = (hasDesc
                ? e.Bounds.Left + (int)(e.Bounds.Width * 0.38) - 24
                : pathX - 16) - left;
            string nameDisplay = it.Name;
            if (TextRenderer.MeasureText(nameDisplay, f).Width > maxNameW && maxNameW > 30)
                nameDisplay = UiText.FitEnd(it.Name, f, maxNameW);
            UiText.ClampHighlight(nameDisplay, ref nameHl, ref hlStart, ref hlLen);
            UiText.DrawHighlighted(g, nameDisplay, nameHl ? hlStart : -1, nameHl ? hlLen : 0, f, new Point(left, ty), textColor, acc, light);

            if (hasDesc)
            {
                int descX = Math.Max(left + TextRenderer.MeasureText(nameDisplay, f).Width + 24,
                                     e.Bounds.Left + (int)(e.Bounds.Width * 0.38));
                int descSpace = pathX - 16 - descX;
                if (descSpace > 40)
                {
                    int dStart, dLen;
                    bool descHl = UiText.FindHighlight(desc.ToLowerInvariant(), panelSearchVariants, out dStart, out dLen);
                    string descDisplay = desc;
                    if (TextRenderer.MeasureText(desc, f).Width > descSpace)
                        descDisplay = UiText.FitEnd(desc, f, descSpace);
                    UiText.ClampHighlight(descDisplay, ref descHl, ref dStart, ref dLen);
                    UiText.DrawHighlighted(g, descDisplay, descHl ? dStart : -1, descHl ? dLen : 0, f, new Point(descX, ty), subColor, acc, light);
                }
            }

            if (pathDisplay.Length > 0)
            {
                int pStart, pLen;
                bool pathHl = UiText.FindHighlight(pathCombo.ToLowerInvariant(), panelSearchVariants, out pStart, out pLen);
                int cut = pathCombo.Length - (pathDisplay.Length - 1); // chars hidden by the leading "…"
                bool tailHl = pathHl && pStart >= cut;
                if (tailHl)
                    UiText.DrawHighlighted(g, pathDisplay, pStart - cut, pLen, f, new Point(pathX, ty), subColor, acc, light);
                else
                    TextRenderer.DrawText(g, pathDisplay, f, new Point(pathX, ty), subColor);
            }
        }

        // Applies the "search results" font from the settings and syncs the row height.
        private void ApplySearchListFont()
        {
            try
            {
                panelSearchList.Font = Settings.MakeFont(panelSearchList.Font.FontFamily.Name, Math.Max(7, Math.Min(30, settings.SearchResultsFontSize)));
                panelSearchList.ItemHeight = Math.Max(26, TextRenderer.MeasureText("Ag", panelSearchList.Font).Height + 10);
            }
            catch { }
        }
        // Hovering a result row shows a tooltip with the full path and description.
        private void PanelSearchList_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (itemTip == null) return;
                int i = panelSearchList.IndexFromPoint(e.Location);
                string t = "";
                if (i >= 0 && i < panelSearchResults.Count)
                {
                    var it = panelSearchResults[i];
                    t = it.Path ?? "";
                    try
                    {
                        if (t.ToLowerInvariant().EndsWith(".lnk"))
                        {
                            string tgt = PanelSearch.GetTarget(it);
                            if (!string.IsNullOrEmpty(tgt)) t = t + "  →  " + tgt;
                        }
                    }
                    catch { }
                    string d = PanelSearch.GetDescription(it);
                    if (!string.IsNullOrEmpty(d)) t = (t.Length > 0 ? t + "\n" : "") + d;
                }
                if (!string.Equals(t, lastSearchTip, StringComparison.Ordinal))
                {
                    lastSearchTip = t;
                    itemTip.SetToolTip(panelSearchList, t);
                }
            }
            catch { }
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
            if (!ParseHotkey(settings != null ? settings.HotkeyShow : null, out mods, out vk)) { ApplyWinKeyHotkey(); return; }
            hotkeyRegistered = RegisterHotKey(this.Handle, HotkeyId, mods | 0x4000 /* MOD_NOREPEAT */, vk);
            if (!hotkeyRegistered && trayIcon != null)
                trayIcon.ShowBalloonTip(2500, Loc.S("Tilettes", "Плиточки"), "Hotkey " + settings.HotkeyShow + " is already in use by another program.", ToolTipIcon.Warning);
            ApplyWinKeyHotkey();
        }

        // Captures (or releases) the physical Win keys so the panel appears instead
        // of the Start menu. RegisterHotKey alone shows the panel but the Start
        // menu still opens on some systems, so a WH_KEYBOARD_LL hook swallows the
        // bare Win press. Win+<key> chords are preserved: the first other key
        // pressed while Win is held re-injects the real Win modifier.
        private IntPtr winKeyHook = IntPtr.Zero;
        private LowLevelHookProc hookProcRef;
        private bool winHeld;      // our hook swallowed a Win press (still physically held)
        private bool winChord;     // another key was pressed while Win was held
        private bool winReinject;  // our own keybd_event Win re-injection, pass through

        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN_LL = 0x0100;
        private const int WM_KEYUP_LL = 0x0101;
        private const int WM_SYSKEYDOWN_LL = 0x0104;
        private const int WM_SYSKEYUP_LL = 0x0105;
        private const uint KEYEVENTF_KEYUP_LL = 0x0002;
        private const int VK_LWIN_LL = 0x5B;
        private const int VK_RWIN_LL = 0x5C;

        private IntPtr WinKeyHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    int msg = wParam.ToInt32();
                    var k = (KBDLLHOOKSTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    bool isWin = k.vkCode == VK_LWIN_LL || k.vkCode == VK_RWIN_LL;
                    if (winReinject && isWin)
                    {
                        winReinject = false;
                        return CallNextHookEx(winKeyHook, nCode, wParam, lParam);
                    }
                    if (isWin)
                    {
                        bool down = msg == WM_KEYDOWN_LL || msg == WM_SYSKEYDOWN_LL;
                        bool up = msg == WM_KEYUP_LL || msg == WM_SYSKEYUP_LL;
                        if (down)
                        {
                            if (winHeld) return (IntPtr)1; // autorepeat: swallow silently
                            winHeld = true;
                            winChord = false;
                            return (IntPtr)1; // swallow: the Start menu never sees it
                        }
                        if (up)
                        {
                            winHeld = false;
                            if (winChord)
                            {
                                // The modifier was re-injected for the chord; release it.
                                winReinject = true;
                                keybd_event((byte)VK_LWIN_LL, 0, KEYEVENTF_KEYUP_LL, System.UIntPtr.Zero);
                            }
                            else
                            {
                                // Solo tap (like the Start button): toggle on release,
                                // so Win+<key> chords never flash the panel.
                                try { BeginInvoke((MethodInvoker)delegate { ToggleByWinKey(); }); }
                                catch { }
                            }
                            return (IntPtr)1;
                        }
                    }
                    else if (winHeld && !winChord && (msg == WM_KEYDOWN_LL || msg == WM_SYSKEYDOWN_LL))
                    {
                        // Win+<key> chord: restore the real Win modifier so the
                        // combination still works, then let the key through.
                        winChord = true;
                        winReinject = true;
                        keybd_event((byte)VK_LWIN_LL, 0, 0, System.UIntPtr.Zero);
                    }
                }
            }
            catch { }
            return CallNextHookEx(winKeyHook, nCode, wParam, lParam);
        }

        private void ApplyWinKeyHotkey()
        {
            try
            {
                bool want = settings != null && settings.HotkeyWin;
                if (!want)
                {
                    if (winKeyHook != IntPtr.Zero)
                    {
                        try { UnhookWindowsHookEx(winKeyHook); } catch { }
                        winKeyHook = IntPtr.Zero;
                    }
                    if (hotkeyWinRegistered)
                    {
                        try { UnregisterHotKey(this.Handle, HotkeyIdWinL); } catch { }
                        try { UnregisterHotKey(this.Handle, HotkeyIdWinR); } catch { }
                        hotkeyWinRegistered = false;
                    }
                    return;
                }
                if (winKeyHook != IntPtr.Zero || hotkeyWinRegistered) return;

                hookProcRef = new LowLevelHookProc(WinKeyHookProc); // keep alive for the hook lifetime
                winKeyHook = SetWindowsHookEx(WH_KEYBOARD_LL, hookProcRef, GetModuleHandle(null), 0);
                if (winKeyHook == IntPtr.Zero)
                {
                    // Fallback: hotkey capture (the Start menu may still open).
                    AppLog.Write("Win key hook failed err=" + System.Runtime.InteropServices.Marshal.GetLastWin32Error() + ", falling back to hotkey");
                    const uint MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;
                    bool left = RegisterHotKey(this.Handle, HotkeyIdWinL, MOD_WIN | MOD_NOREPEAT, 0x5B);
                    bool right = RegisterHotKey(this.Handle, HotkeyIdWinR, MOD_WIN | MOD_NOREPEAT, 0x5C);
                    hotkeyWinRegistered = left || right;
                }
            }
            catch (Exception ex) { AppLog.Write("Win key hook", ex); }
        }

        // The captured Start button: show the panel (search focused) or hide it.
        private void ToggleByWinKey()
        {
            try
            {
                if (this.Visible && this.WindowState != FormWindowState.Minimized)
                {
                    this.Hide();
                }
                else
                {
                    RestoreWindow();
                    try
                    {
                        if (panelSearchBox != null && panelSearchBox.Visible)
                        {
                            panelSearchBox.Focus();
                            panelSearchBox.SelectAll();
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex) { AppLog.Write("Win key toggle", ex); }
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

        // Validation hook for the editable hotkey field in the settings dialog.
        internal static bool TryParseHotkey(string hotkey)
        {
            uint mods, vk;
            return ParseHotkey(hotkey, out mods, out vk);
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
            // Time-budgeted batch: drain as many icon tasks as fit in ~20 ms per tick
            // instead of exactly one. The UI stays responsive, but a panel with many
            // shortcuts no longer pays a fixed 15 ms pause per icon.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (iconQueue.Count > 0 && sw.ElapsedMilliseconds < 20)
            {
                try { iconQueue.Dequeue()(); } catch { }
            }
        }

        private static Image LoadIconForItem(ShortcutItem item)
        {
            try
            {
                // 1) direct icon of the item (Change Icon)
                if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath))
                    return IconExtractor.LoadAny(item.CustomIconPath);
                // 2) icon assigned to the file type
                string typeIcon = FileTypes.GetIconForPath(item.Path);
                if (!string.IsNullOrEmpty(typeIcon) && File.Exists(typeIcon))
                    return IconExtractor.LoadAny(typeIcon);
                // 3) standard shell icon (shell: paths = UWP apps)
                return IconExtractor.GetIconAuto(item.Path, true);
            }
            catch (Exception ex)
            {
                AppLog.Write("LoadIconForItem", ex);
                return null;
            }
        }

        private void LoadTileIcon(TileControl tile, ShortcutItem item)
        {
            if (tile.IsDisposed) return;
            Image img = null;
            try
            {
                img = LoadIconForItem(item);
            }
            catch (Exception ex)
            {
                AppLog.Write("LoadTileIcon: LoadIconForItem", ex);
            }
            if (img == null)
            {
                try { img = SystemIcons.Application.ToBitmap(); }
                catch { img = SystemIcons.Error.ToBitmap(); }
            }
            if (tile.IsDisposed) { if (img != null) img.Dispose(); return; }
            try
            {
                if (tile.IconImage != null) tile.IconImage.Dispose();
                tile.IconImage = img;
                tile.Invalidate();
            }
            catch (Exception ex)
            {
                AppLog.Write("LoadTileIcon: assign IconImage", ex);
                if (img != null) img.Dispose();
            }
        }

        // The closed-folder icon is fetched from the shell once and cloned per use:
        // folder children would otherwise repeat the (slow) shell lookup per tile.
        private static Image _closedFolderIcon;

        private static Image GetFolderIconImage()
        {
            if (_closedFolderIcon == null)
            {
                try
                {
                    var icon = ShellIcon.GetFolderIcon(ShellIcon.IconSize.Large, ShellIcon.FolderType.Closed);
                    _closedFolderIcon = icon != null ? icon.ToBitmap() : SystemIcons.WinLogo.ToBitmap();
                }
                catch (Exception ex)
                {
                    AppLog.Write("GetFolderIconImage", ex);
                    _closedFolderIcon = SystemIcons.WinLogo.ToBitmap();
                }
            }
            try
            {
                return (Image)_closedFolderIcon.Clone();
            }
            catch
            {
                return SystemIcons.WinLogo.ToBitmap();
            }
        }

        private void LoadFolderChildIcon(TileControl tile, ShortcutItem child, int index)
        {
            if (tile.IsDisposed || index >= tile.ChildIcons.Count) return;
            Image img = null;
            try
            {
                if (child.IsFolder)
                {
                    img = GetFolderIconImage();
                }
                else
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
                            img = IconExtractor.GetIconAuto(child.Path, true);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("LoadFolderChildIcon: icon extraction", ex);
            }
            if (img == null)
            {
                try { img = SystemIcons.Application.ToBitmap(); }
                catch { img = SystemIcons.Error.ToBitmap(); }
            }
            if (tile.IsDisposed) { if (img != null) img.Dispose(); return; }
            try
            {
                var old = tile.ChildIcons[index];
                if (old != null) old.Dispose();
                tile.ChildIcons[index] = img;
                tile.Invalidate();
            }
            catch (Exception ex)
            {
                AppLog.Write("LoadFolderChildIcon: assign ChildIcons", ex);
                if (img != null) img.Dispose();
            }
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
            catch (Exception ex) { AppLog.Write("LaunchItem: file-type rule", ex); }

            try { StartDetached(target, args); }
            catch (Exception ex) { AppLog.Write("LaunchItem: StartDetached", ex); ReportLaunchError("Error opening: " + path); }
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
                    AppLog.Write("StartDetached: Win32Exception", wex);
                    ReportLaunchError("Error opening file: " + wex.Message);
                }
                catch (Exception ex)
                {
                    AppLog.Write("StartDetached: Exception", ex);
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
            try { if (panelSearchActive) HidePanelSearch(); } catch { }
            if (panelSearchRow != null)
            {
                try { topPanel.Controls.Remove(panelSearchRow); panelSearchRow.Dispose(); } catch { }
                panelSearchRow = null;
                panelSearchBox = null;
            }
            DisposeControlTree(contentPanel);
            DisposeControlTree(tabBar);
            DisposeControlTree(rightPanel);
            tabBar.Controls.Clear();
            rightPanel.Controls.Clear();
            contentPanel.Controls.Clear();

            int tabRows = 1;
            foreach (var tab in records.Tabs) tabRows = Math.Max(tabRows, tab.Row + 1);
            int searchRowH = 31;
            try { searchRowH = Math.Max(31, Math.Max(7, Math.Min(30, settings.SearchBoxFontSize)) + 22); } catch { }
            topSearchRowH = searchRowH;
            // Tab size follows the tabs font: the caption must fit at any size.
            int tabH = Settings.MakeFont(settings.FontTabsName, settings.FontTabsSize).Height + 12;
            int tabPitch = tabH + 4;
            // Tab rows sit directly above the search row: no filler space between them.
            topPanel.Height = tabRows * tabPitch + searchRowH;

            // ---- Right stack: row 1 = min / max / close, row 2 = grid / edit / [update] / settings ----
            // The "Update" plate appears when UpdateChecker found a newer release;
            // both rows grow by the plate width so min/max/close stay in the corner.
            int plateW = 0;
            string plateCaption = "⟳ " + Loc.S("Update", "Обновить");
            if (updateAvailableVersion != null)
            {
                using (var pf = new Font("Segoe UI", 9f, FontStyle.Bold))
                    plateW = Math.Max(64, TextRenderer.MeasureText(plateCaption, pf).Width + 18);
                rightPanel.Width = 105 + plateW;
            }
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

            if (plateW > 0)
            {
                // Added last = docks first = leftmost: keeps min/max/close pinned
                // to the right corner while the rows carry the extra update plate.
                var winFiller = new Panel { Width = plateW, Height = 31, Dock = DockStyle.Left, BackColor = bgColor };
                winFiller.MouseDown += (s, e) => DragWindow(e);
                winRow.Controls.Add(winFiller);
            }

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

            // Added after settings and before edit/grid, so the visual order is
            // grid | edit | update-plate | settings — the plate sits right next
            // to the gear in the corner.
            if (plateW > 0 && updateAvailableVersion != null)
            {
                string v = updateAvailableVersion;
                var updateBtn = new Button
                {
                    Text = plateCaption,
                    Width = plateW,
                    Height = 31,
                    Dock = DockStyle.Left,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(39, 174, 96),
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                };
                updateBtn.FlatAppearance.BorderSize = 0;
                updateBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 204, 113);
                updateBtn.Click += (s, e) =>
                {
                    try { System.Diagnostics.Process.Start(AppInfo.ReleasesLatestUrl); }
                    catch { }
                };
                itemTip.SetToolTip(updateBtn, Loc.S("Version " + v + " is available - click to open the download page",
                    "Доступна версия " + v + " — нажмите, чтобы открыть страницу загрузок"));
                actionRow.Controls.Add(updateBtn);
            }

            var editBtn = new Button
            {
                Text = editState == 0 ? "⬜" : "✅",
                Width = 35,
                Height = 31,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = editState == 0 ? bgColor : (editState == 2 ? Color.FromArgb(192, 57, 43) : panelColor),
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 11f)
            };
            editBtn.FlatAppearance.BorderSize = 0;
            editBtn.FlatAppearance.MouseOverBackColor = hoverColor;
            editBtn.Click += (s, e) => {
                editState = (editState + 1) % 3; // off -> edit -> multi-select -> off
                settings.EditModeState = editState;
                settings.EditMode = editState >= 1;
                settings.Save(settingsPath);
                editBtn.Text = editState == 0 ? "⬜" : "✅";
                editBtn.BackColor = editState == 0 ? bgColor : (editState == 2 ? Color.FromArgb(192, 57, 43) : panelColor);
                ClearMultiSelection();
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
            // The window corner is rounded (r=15): a tab flush with (0,0) gets its
            // corner clipped into a step. Inset the top row so the tab starts
            // where the arc is only ~1px deep.
            rowX[0] = 12;

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

                Font tabFont = Settings.MakeFont(settings.FontTabsName, settings.FontTabsSize);
                var tabBtn = new Button
                {
                    Text = tabData.Name,
                    Width = Math.Max(100, TextRenderer.MeasureText(tabData.Name, tabFont).Width + 24),
                    Height = tabH,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = bgColor,
                    ForeColor = Settings.ParseColor(settings.FontTabsColor, textColor),
                    Cursor = Cursors.Hand,
                    Font = tabFont
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
                tabDataByButton[tabBtn] = tabData;
                layoutPanels[tabBtn] = layoutPanel;
                buttons.Add(tabBtn);

                tabBtn.Location = new Point(rowX[row], row * tabPitch);
                rowX[row] += tabBtn.Width;

                // The leftmost tab of the top row sits inside the rounded window
                // corner: round its own corner to match, otherwise the square
                // corner looks like a step against the window contour.
                if (row == 0 && tabBtn.Left == 12)
                {
                    using (var cornerPath = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        cornerPath.AddArc(0, 0, 15, 15, 180, 90);
                        cornerPath.AddLine(15, 0, tabBtn.Width, 0);
                        cornerPath.AddLine(tabBtn.Width, tabBtn.Height, 0, tabBtn.Height);
                        cornerPath.CloseFigure();
                        tabBtn.Region = new Region(cornerPath);
                    }
                }

                tabBar.Controls.Add(tabBtn);
                AttachTabDrag(tabBtn, tabByButton);
            }

            var addTabBtn = new Button
            {
                Text = "+",
                Width = 35,
                Height = tabH,
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
                string name = Prompt.ShowDialog(Loc.S("New Tab Name", "Имя новой вкладки"), Loc.S("Add Tab", "Добавить вкладку"));
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

            // ---- Search bar for the saved items (bottom row of the top strip) ----
            var searchRow = new Panel { Dock = DockStyle.Bottom, Height = 31, BackColor = bgColor };
            var sBox = new TextBox { Left = 8, Top = 5, Width = 280, BorderStyle = BorderStyle.FixedSingle, BackColor = panelColor, ForeColor = textColor };
            try
            {
                int bfs = Math.Max(7, Math.Min(30, settings.SearchBoxFontSize));
                if (bfs != 9) sBox.Font = new Font(sBox.Font.FontFamily, bfs);
                // The row always matches the font-dependent box height.
                searchRow.Height = Math.Max(31, bfs + 22);
                if (bfs > 13) sBox.Width = 380;
            }
            catch { }
            sBox.TextChanged += (s, e) =>
            {
                // Quick search settings appear while the box has text; the tier
                // is re-evaluated here because sBox.Right is only meaningful now.
                bool hasText = sBox.Text.Trim().Length > 0;
                if (panelSearchSettingsRow != null)
                {
                    panelSearchSettingsRow.Visible = hasText;
                    if (hasText) StyleSearchToggles();
                }
                if (panelSearchTimer == null) return;
                panelSearchTimer.Stop();
                if (hasText) { panelSearchTimer.Start(); return; }
                // Empty box: show "Прошлый поиск" as before — unless the box was
                // just cleared programmatically (tab click / Esc), which must close
                // the search for good instead of re-opening the overlay.
                if (suppressSearchOnEmpty) { suppressSearchOnEmpty = false; return; }
                panelSearchTimer.Start();
            };
            sBox.KeyDown += PanelSearchBox_KeyDown;
            searchRow.Controls.Add(sBox);
            BuildSearchSettingsStrip(searchRow, sBox);
            topPanel.Controls.Add(searchRow);
            // Docking is applied in reverse collection order: rightPanel must stay
            // last so it docks first at full height. Otherwise searchRow takes the
            // bottom strip across the whole width and the settings/edit row under
            // min/max/close is clipped to a 4px sliver.
            topPanel.Controls.SetChildIndex(rightPanel, topPanel.Controls.Count - 1);
            panelSearchRow = searchRow;
            panelSearchBox = sBox;
            try
            {
                var h = sBox.Handle;
                SendMessageStr(h, 0x1501, (IntPtr)1, Loc.S("Search...", "Поиск..."));
            }
            catch { }

            // Activate the remembered tab when it still exists, otherwise the first one.
            Button startBtn = null;
            if (settings.KeepActiveTab && !string.IsNullOrEmpty(settings.ActiveTab))
            {
                foreach (var b in buttons)
                {
                    TabData td;
                    if (tabByButton.TryGetValue(b, out td) && td.Name == settings.ActiveTab) { startBtn = b; break; }
                }
            }
            if (startBtn == null && buttons.Count > 0) startBtn = buttons[0];
            if (startBtn != null) ActivateTab(startBtn, layoutPanels, buttons);

            // Render tiles for every tab (kept in hidden panels)
            foreach (var tabData in records.Tabs)
            {
                Panel lp = null;
                foreach (var kv in layoutPanels) if (tabByButton[kv.Key] == tabData) { lp = kv.Value; break; }
                if (lp == null) continue;
                // Docking layout never sizes hidden panels (they keep the 200x100
                // default), so tiles rendered "in the dark" end up squashed in the
                // top-left corner of the tab. Make each panel briefly visible while
                // rendering so it lays out to its real bounds.
                // Only the active tab may stay visible afterwards — Control.Visible
                // GETTER reports the effective visibility (false while the form itself
                // is still hidden during startup), so it can NOT be used to remember
                // the previous state: doing so hid every panel, including the active
                // one, and the panel stayed empty until the next window move.
                bool keepVisible = ReferenceEquals(lp, activeLayoutPanel);
                lp.Visible = true;
                RenderCurrentFolder(lp, tabData);
                lp.Visible = keepVisible;
            }
        }

        private void ActivateTab(Button tabBtn, Dictionary<Button, Panel> layoutPanels, List<Button> buttons)
        {
            try { if (panelSearchActive) ClearPanelSearch(); } catch { }
            ClearMultiSelection();
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

            activeTabBtn = tabBtn;
            activeLayoutPanel = layoutPanels[tabBtn];
            TabData td;
            if (tabDataByButton.TryGetValue(tabBtn, out td))
            {
                activeTabData = td;
                if (settings.KeepActiveTab && !string.Equals(settings.ActiveTab, td.Name, StringComparison.Ordinal))
                {
                    settings.ActiveTab = td.Name;
                    try { settings.Save(settingsPath); } catch { }
                }
            }
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
                topPanel.Height = rowsUsed * 35 + topSearchRowH;
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
        private int topSearchRowH = 31; // height of the search row (kept for tab-drag relayout)

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
            ClearMultiSelection();
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

            // Synced folder children carry no grid coordinates (GridX/GridY = -1)
            // and their free-layout pixel fallback (item.X/Y) is 0 for all of them,
            // which used to collapse the whole folder view into the top-left cell.
            // Place every unpositioned item explicitly: reading order over the
            // grid, overflow beyond the visible rows (PlaceInGrid handles that).
            if (tabData.IsGridLayout && itemsToRender != null)
            {
                int cols = Math.Max(1, settings.GridColumns);
                int rows = Math.Max(1, settings.GridRows);
                bool placedAny = false;
                foreach (var it in itemsToRender)
                {
                    if (it.GridX >= 0 && it.GridY >= 0) continue;
                    PlaceInGrid(itemsToRender, it, Math.Max(0, it.GridX), Math.Max(0, it.GridY), cols, rows);
                    placedAny = true;
                }
                if (placedAny)
                {
                    try { records.Save(recordsPath); } catch { }
                }
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
            if (s <= 0 || s > 6) s = settings.DefaultItemSize;
            if (s <= 0 || s > 6) s = 1;
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
            if (cell.X < 0)
            {
                // Folder views can hold more items than the visible grid holds.
                // Without this fallback every overflowing item stayed unpositioned
                // (GridX=-1) and the whole pile rendered in the same spot; placing
                // beyond the fold instead keeps them reachable via AutoScroll.
                cell = FindFreeGridCell(items, item, s, cols, Math.Max(rows, s) * 3, -1, -1);
            }
            if (cell.X >= 0)
            {
                item.GridX = cell.X;
                item.GridY = cell.Y;
            }
        }

        // Static twin of PlaceInGrid for subsystems without settings access
        // (Start Menu sync): first free cell in reading order.
        internal static void PlaceIntoGridStatic(List<ShortcutItem> items, ShortcutItem item, int cols, int rows)
        {
            try
            {
                int s = item.Size;
                if (s < 1 || s > 6) s = 1;
                var occ = new bool[rows, cols];
                foreach (var it in items)
                {
                    if (ReferenceEquals(it, item)) continue;
                    int os = it.Size;
                    if (os < 1 || os > 6) os = 1;
                    if (os > cols || os > rows) os = Math.Max(1, Math.Min(cols, rows));
                    int gx = it.GridX, gy = it.GridY;
                    if (gx < 0 || gy < 0) continue;
                    gx = Math.Max(0, Math.Min(cols - os, gx));
                    gy = Math.Max(0, Math.Min(rows - os, gy));
                    for (int dy = 0; dy < os; dy++)
                        for (int dx = 0; dx < os; dx++)
                            occ[gy + dy, gx + dx] = true;
                }
                for (int y = 0; y + s <= rows; y++)
                {
                    for (int x = 0; x + s <= cols; x++)
                    {
                        bool free = true;
                        for (int dy = 0; dy < s && free; dy++)
                            for (int dx = 0; dx < s && free; dx++)
                                if (occ[y + dy, x + dx]) free = false;
                        if (free)
                        {
                            item.GridX = x;
                            item.GridY = y;
                            return;
                        }
                    }
                }
                // Nowhere free: the overflow pass will relocate the item.
                item.GridX = 0;
                item.GridY = 0;
            }
            catch (Exception ex) { AppLog.Write("PlaceIntoGridStatic", ex); }
        }

        // ---------- Grid overflow protection ----------
        // When the grid got smaller (manual settings change) or a sync added many
        // items, the entries that no longer fit move into a last-resort folder
        // ("Ещё"); a few rows always stay free. When the grid grows back, the
        // folder unpacks itself. Returns true when the layout changed.
        private const string OverflowSrcKey = "auto:overflow";
        private const int ReservedRows = 2;

        internal bool EnsureTabFits(TabData tab)
        {
            if (tab == null || !tab.IsGridLayout) return false;
            // The Start tab is laid out by StartMenuSync across the whole grid; the
            // generic overflow packing (with its reserved rows) would fight it.
            if (tab.Kind == StartMenuSync.TabKind) return false;
            int cols = Math.Max(1, settings.GridColumns);
            int rows = Math.Max(1, settings.GridRows);
            int usableRows = rows - ReservedRows;
            if (usableRows < 1) usableRows = rows;

            ShortcutItem overflow = null;
            foreach (var it in tab.Items)
                if (it.IsFolder && it.Src == OverflowSrcKey) { overflow = it; break; }

            bool changed = false;

            // 1) Unpack a previous overflow folder when everything fits again.
            if (overflow != null && overflow.Children != null && overflow.Children.Count > 0)
            {
                long need = 0;
                foreach (var it in tab.Items)
                    if (!ReferenceEquals(it, overflow)) need += CellCount(it, cols, rows);
                foreach (var ch in overflow.Children) need += CellCount(ch, cols, rows);
                if (need <= (long)cols * usableRows)
                {
                    foreach (var ch in overflow.Children)
                    {
                        PlaceInGrid(tab.Items, ch, Math.Max(0, ch.GridX), Math.Max(0, ch.GridY), cols, rows);
                        tab.Items.Add(ch);
                    }
                    overflow.Children.Clear();
                    tab.Items.Remove(overflow);
                    overflow = null;
                    changed = true;
                }
            }

            // 2) Evict bottom-most items until the rest fits into the usable rows.
            long total = 0;
            foreach (var it in tab.Items)
                if (!ReferenceEquals(it, overflow)) total += CellCount(it, cols, rows);

            if (total > (long)cols * usableRows)
            {
                if (overflow == null)
                {
                    overflow = new ShortcutItem();
                    overflow.Name = Loc.S("More", "Ещё");
                    overflow.IsFolder = true;
                    overflow.Src = OverflowSrcKey;
                    overflow.Size = 1;
                    tab.Items.Add(overflow);
                    changed = true;
                }
                if (overflow.Children == null) overflow.Children = new List<ShortcutItem>();

                var byBottom = new List<ShortcutItem>(tab.Items);
                byBottom.RemoveAll(delegate(ShortcutItem it) { return it.IsFolder && it.Src == OverflowSrcKey; });
                byBottom.Sort(delegate(ShortcutItem a, ShortcutItem b)
                {
                    int ba = ClampItemSize(a.Size) + Math.Max(0, a.GridY);
                    int bb = ClampItemSize(b.Size) + Math.Max(0, b.GridY);
                    if (bb != ba) return bb - ba;          // lowest on screen first
                    return b.GridX - a.GridX;               // then rightmost
                });

                int guard = byBottom.Count + 8;
                while (total > (long)cols * usableRows && guard-- > 0 && byBottom.Count > 0)
                {
                    ShortcutItem victim = byBottom[0];
                    byBottom.RemoveAt(0);
                    tab.Items.Remove(victim);
                    overflow.Children.Add(victim);
                    total -= CellCount(victim, cols, rows);
                    changed = true;
                }
                AppLog.Write("Overflow: " + overflow.Children.Count + " item(s) moved into '" + overflow.Name + "' (" + tab.Name + ")");
            }

            // 3) The overflow folder itself needs a visible cell (bottom rows first).
            if (overflow != null)
            {
                overflow.Size = 1;
                int beforeX = overflow.GridX, beforeY = overflow.GridY;
                PlaceInGrid(tab.Items, overflow, 0, rows - 1, cols, rows);
                if ((overflow.GridX != beforeX || overflow.GridY != beforeY) && beforeX >= 0) changed = true;
            }

            return changed;
        }

        private static long CellCount(ShortcutItem it, int cols, int rows)
        {
            int s = it.Size;
            if (s < 1 || s > 6) s = 1;
            if (s > cols || s > rows) s = Math.Max(1, Math.Min(cols, rows));
            return (long)s * s;
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

            // The grid stretches over the visible window area. The outer border lines
            // are pulled 8px inside: the last 6px of every window edge are covered by
            // the invisible resize grips (bg-colored strips), so a border drawn right
            // at the edge is hidden and "falls off" the visible area. Interior lines
            // stay on the tile-cell boundaries.
            const int BorderInset = 8;
            float cellWidth = panel.ClientSize.Width / (float)cols;
            float cellHeight = panel.ClientSize.Height / (float)rows;
            if (cellWidth <= 0 || cellHeight <= 0) return;

            var scroll = panel.AutoScrollPosition;

            e.Graphics.SetClip(panel.ClientRectangle);

            // The border rectangle in virtual (scroll-aware) coordinates: interior
            // lines span exactly between the border lines so the dashed grid ends
            // at the frame instead of running past it to the window edges.
            float left = BorderInset + scroll.X;
            float right = panel.ClientSize.Width - BorderInset + scroll.X;
            float top = BorderInset + scroll.Y;
            float bottom = panel.ClientSize.Height - BorderInset + scroll.Y;

            Color gridColor = settings.IsLightTheme
                ? Color.FromArgb(settings.GridTransparency, 0, 0, 0)
                : Color.FromArgb(settings.GridTransparency, 255, 255, 255);

            using (Pen gridPen = new Pen(gridColor))
            {
                gridPen.DashPattern = new float[] { 4, 8 };
                for (int i = 0; i <= cols; i++)
                {
                    float x = i * cellWidth + scroll.X;
                    if (i == 0) x = left;
                    else if (i == cols) x = right;
                    if (x >= 0 && x <= panel.ClientSize.Width)
                        e.Graphics.DrawLine(gridPen, x, top, x, bottom);
                }
                for (int i = 0; i <= rows; i++)
                {
                    float y = i * cellHeight + scroll.Y;
                    if (i == 0) y = top;
                    else if (i == rows) y = bottom;
                    if (y >= 0 && y <= panel.ClientSize.Height)
                        e.Graphics.DrawLine(gridPen, left, y, right, y);
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

                // Columns clamp to the visible grid (there is no horizontal scroll),
                // but rows must NOT: folder views hold more items than the visible
                // grid, and clamping the row piled every overflow tile onto the
                // last visible rows. Beyond-the-fold positions stay reachable
                // through AutoScroll.
                int col = Math.Max(0, Math.Min(cols - s, item.GridX));
                int row = Math.Max(0, item.GridY);

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

            // Description pops up as a tooltip after 0.3 s of hovering.
            if (itemTip != null && !string.IsNullOrEmpty(item.ShortDescription))
                itemTip.SetToolTip(tile, item.ShortDescription);

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
                    // Red multi-select mode: right-click opens the bulk menu instead.
                    if (editState == 2)
                    {
                        ShowMultiSelectMenu(tile, item, panel, tabData, e.Location);
                        return;
                    }
                    var pt = tile.PointToScreen(e.Location);
                    if (item.IsFolder)
                    {
                        bool canMini = !string.IsNullOrEmpty(item.Path) && Directory.Exists(item.Path);
                        if (!isEditMode && !canMini) return;
                        var fMenu = new ContextMenu();
                        if (canMini)
                            fMenu.MenuItems.Add(Loc.S("Open in Mini Explorer"), (s2, e2) => OpenMiniExplorer(item));
                        if (isEditMode)
                        {
                            if (canMini) fMenu.MenuItems.Add("-");
                            if (tabNavigations[tabData].Count > 0)
                                fMenu.MenuItems.Add(Loc.S("Move out of folder"), (s2, e2) => MoveItemOutOfFolder(panel, tabData, item));
                            var fSizeMenu = fMenu.MenuItems.Add(Loc.S("Size"));
                            fSizeMenu.MenuItems.Add("1 x 1", (s2, e2) => ChangeIconSize(item, tile, panel, tabData, 1));
                            fSizeMenu.MenuItems.Add("2 x 2", (s2, e2) => ChangeIconSize(item, tile, panel, tabData, 2));
                            fSizeMenu.MenuItems.Add("3 x 3", (s2, e2) => ChangeIconSize(item, tile, panel, tabData, 3));
                            fSizeMenu.MenuItems.Add("4 x 4", (s2, e2) => ChangeIconSize(item, tile, panel, tabData, 4));
                            fSizeMenu.MenuItems.Add("5 x 5", (s2, e2) => ChangeIconSize(item, tile, panel, tabData, 5));
                            fSizeMenu.MenuItems.Add("6 x 6", (s2, e2) => ChangeIconSize(item, tile, panel, tabData, 6));
                            fMenu.MenuItems.Add(Loc.S("Rename"), (s2, e2) => RenameItem(item, tile));
                            fMenu.MenuItems.Add(Loc.S("Description...", "Описание..."), (s2, e2) => EditItemDescription(item));
                            fMenu.MenuItems.Add(Loc.S("Change Icon"), (s2, e2) => ChangeItemIcon(item, tile));
                            fMenu.MenuItems.Add(Loc.S("Remove"), (s2, e2) => RemoveItem(panel, tile, item, tabData));
                            var fMoveMenu = fMenu.MenuItems.Add(Loc.S("Move to tab", "Переместить на вкладку"));
                            FillMoveToTabMenu(fMoveMenu, panel, tabData, new List<ShortcutItem> { item });
                        }
                        fMenu.Show(tile, e.Location);
                    }
                    else
                    {
                        // The Explorer menu is always available; our own items are shown
                        // before the Explorer items, edit actions only in edit mode.
                        NativeContextMenu.ShowContextMenu(item.Path, pt.X, pt.Y, this.Handle, isEditMode,
                            Loc.S("Description...", "Описание..."), (Action)delegate { EditItemDescription(item); },
                            tabNavigations[tabData].Count > 0,
                            () => MoveItemOutOfFolder(panel, tabData, item),
                            () => OpenContainingFolder(item),
                            (!string.IsNullOrEmpty(item.Path) && Directory.Exists(item.Path)) ? new Action(delegate() { OpenMiniExplorer(item); }) : null,
                            () => ChangeIconSize(item, tile, panel, tabData, 1),
                            () => ChangeIconSize(item, tile, panel, tabData, 2),
                            () => ChangeIconSize(item, tile, panel, tabData, 3),
                            () => ChangeIconSize(item, tile, panel, tabData, 4),
                            () => RemoveItem(panel, tile, item, tabData),
                            () => RenameItem(item, tile),
                            () => ChangeItemIcon(item, tile),
                            () => ChangeIconSize(item, tile, panel, tabData, 5),
                            () => ChangeIconSize(item, tile, panel, tabData, 6),
                            OtherTabNames(tabData),
                            i =>
                            {
                                var target = OtherTabByIndex(tabData, i);
                                if (target != null)
                                    MoveItemsToTab(new List<ShortcutItem> { item }, tabData, target);
                            });
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
                        bool ctrlClick = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                        // Red multi-select mode: a plain click toggles selection and
                        // never launches anything.
                        if (editState == 2)
                        {
                            ToggleMultiSelect(tile, item);
                            return;
                        }
                        bool isDir = false;
                        try { isDir = !string.IsNullOrEmpty(item.Path) && Directory.Exists(item.Path); }
                        catch (Exception ex) { AppLog.Write("TileClick: Directory.Exists", ex); }
                        if (ctrlClick && settings.MiniExplorerCtrlClick && isDir)
                        {
                            OpenMiniExplorer(item);
                        }
                        else if (item.IsFolder)
                        {
                            if (settings.OpenFoldersInPopup)
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
            string dir = null;
            try { dir = Path.GetDirectoryName(item.Path); }
            catch (Exception ex) { AppLog.Write("OpenContainingFolder: GetDirectoryName", ex); }
            if (!string.IsNullOrEmpty(dir))
            {
                bool dirExists = false;
                try { dirExists = Directory.Exists(dir); }
                catch (Exception ex) { AppLog.Write("OpenContainingFolder: Directory.Exists", ex); }
                if (dirExists)
                    StartDetached("explorer.exe", "/select,\"" + item.Path + "\"");
                else
                    StartDetached("explorer.exe", null);
            }
            else
            {
                StartDetached("explorer.exe", null);
            }
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
            bool isDir = false;
            try { isDir = Directory.Exists(item.Path); }
            catch (Exception ex) { AppLog.Write("OpenMiniExplorer: Directory.Exists", ex); }
            if (!isDir)
            {
                LaunchItem(item.Path);
                return;
            }
            try
            {
                if (miniExplorer == null || miniExplorer.IsDisposed)
                    miniExplorer = new MiniExplorerForm(item.Path, settings, settingsPath);
                else
                    miniExplorer.NavigateExternal(item.Path);
                // Shown WITHOUT owner: an owned window is pinned above its owner, which
                // made the mini explorer impossible to send behind the main panel.
                if (!miniExplorer.Visible) miniExplorer.Show();
                else miniExplorer.Activate();
            }
            catch (Exception ex)
            {
                AppLog.Write("OpenMiniExplorer: create/show", ex);
                ReportLaunchError("Mini Explorer error: " + ex.Message);
            }
        }

        // Lets the user attach a free-form description to any item; the text is
        // stored in records.xml, is searchable and shows as a hover tooltip.
        private void EditItemDescription(ShortcutItem item)
        {
            string d = DescriptionDialog.Show(this, item);
            if (d == null) return;
            item.ShortDescription = d;
            PanelSearch.Invalidate(item);
            records.Save(recordsPath);
            UpdateItemTooltip(item);
        }

        // Refreshes the hover tooltip of the tile that shows this item.
        private void UpdateItemTooltip(ShortcutItem item)
        {
            if (itemTip == null) return;
            foreach (Control c in contentPanel.Controls)
            {
                var tc = c as TileControl;
                if (tc != null && tc.Item == item)
                {
                    itemTip.SetToolTip(tc, string.IsNullOrEmpty(item.ShortDescription) ? null : item.ShortDescription);
                    return;
                }
            }
        }

        private void RenameItem(ShortcutItem item, TileControl tile)
        {
            string newName = Prompt.ShowDialog(Loc.S("New Name", "Новое имя"), Loc.S("Rename", "Переименовать"), item.Name);
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

        // Names of the other tabs (for the "Move to tab" submenu).
        internal string[] OtherTabNames(TabData skip)
        {
            try
            {
                var names = new List<string>();
                foreach (var t in records.Tabs)
                    if (!ReferenceEquals(t, skip)) names.Add(t.Name);
                return names.ToArray();
            }
            catch { return new string[0]; }
        }

        private TabData OtherTabByIndex(TabData skip, int index)
        {
            try
            {
                int i = 0;
                foreach (var t in records.Tabs)
                {
                    if (ReferenceEquals(t, skip)) continue;
                    if (i == index) return t;
                    i++;
                }
            }
            catch { }
            return null;
        }

        // Moves one or several items from the current visible list of `fromTab`
        // into `toTab` (grid placement, save, UI rebuild).
        internal void MoveItemsToTab(List<ShortcutItem> items, TabData fromTab, TabData toTab)
        {
            try
            {
                if (items == null || items.Count == 0 || toTab == null || ReferenceEquals(fromTab, toTab)) return;
                var fromList = GetCurrentItems(fromTab);
                int cols = Math.Max(1, settings.GridColumns);
                int rows = Math.Max(1, settings.GridRows);
                foreach (var it in items)
                {
                    if (it == null) continue;
                    fromList.Remove(it);
                    if (toTab.IsGridLayout)
                        PlaceInGrid(toTab.Items, it, 0, 0, cols, rows);
                    else
                    {
                        it.X = it.X >= 0 ? it.X : 30;
                        it.Y = it.Y >= 0 ? it.Y : 30;
                    }
                    toTab.Items.Add(it);
                }
                multiSelection.Clear();
                records.Save(recordsPath);
                LoadTabs();
            }
            catch (Exception ex) { AppLog.Write("MoveItemsToTab", ex); }
        }

        // Fills a WinForms submenu with the other tabs (for "Move to tab").
        private void FillMoveToTabMenu(MenuItem parent, Panel panel, TabData tabData, List<ShortcutItem> items)
        {
            try
            {
                foreach (var t in records.Tabs)
                {
                    if (ReferenceEquals(t, tabData)) continue;
                    var target = t;
                    var cap = t.Name;
                    parent.MenuItems.Add(cap, (s2, e2) => MoveItemsToTab(items, tabData, target));
                }
                if (parent.MenuItems.Count == 0)
                {
                    var none = parent.MenuItems.Add(Loc.S("(no other tabs)", "(нет других вкладок)"));
                    none.Enabled = false;
                }
            }
            catch { }
        }

        // ---------- multi-select mode (the red edit-button state) ----------

        private void ClearMultiSelection()
        {
            try
            {
                multiSelection.Clear();
                foreach (Control c in contentPanel.Controls)
                {
                    var lp = c as Panel;
                    if (lp == null) continue;
                    foreach (Control t in lp.Controls)
                    {
                        var tc = t as TileControl;
                        if (tc != null && tc.MultiSelected) { tc.MultiSelected = false; tc.Invalidate(); }
                    }
                }
            }
            catch (Exception ex) { AppLog.Write("ClearMultiSelection", ex); }
        }

        private void ToggleMultiSelect(TileControl tile, ShortcutItem item)
        {
            try
            {
                if (multiSelection.Remove(item)) tile.MultiSelected = false;
                else { multiSelection.Add(item); tile.MultiSelected = true; }
                tile.Invalidate();
            }
            catch (Exception ex) { AppLog.Write("ToggleMultiSelect", ex); }
        }

        // Context menu over a selected tile in multi-select mode: bulk delete /
        // move to tab / clear selection.
        private void ShowMultiSelectMenu(TileControl tile, ShortcutItem item, Panel panel, TabData tabData, Point location)
        {
            try
            {
                if (!multiSelection.Contains(item))
                {
                    multiSelection.Add(item);
                    tile.MultiSelected = true;
                    tile.Invalidate();
                }
                var m = new ContextMenu();
                string n = multiSelection.Count.ToString();
                m.MenuItems.Add(Loc.S("Remove selected (" + n + ")", "Удалить выбранные (" + n + ")"), (s2, e2) =>
                {
                    var doomed = new List<ShortcutItem>(multiSelection);
                    if (doomed.Count == 0) return;
                    string first = doomed[0].Name ?? "";
                    string caption = Loc.S("Remove selected", "Удалить выбранные");
                    string text = doomed.Count == 1
                        ? Loc.S("Remove", "Удалить") + " \"" + first + "\"?"
                        : Loc.S("Remove ", "Удалить ") + doomed.Count + Loc.S(" items?", " эл.") + "\n" + first + Loc.S(" and more...", " и др...");
                    if (MessageBox.Show(text, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    var currentList = GetCurrentItems(tabData);
                    foreach (var it in doomed) currentList.Remove(it);
                    ClearMultiSelection();
                    records.Save(recordsPath);
                    RenderCurrentFolder(panel, tabData);
                });
                var moveTo = m.MenuItems.Add(Loc.S("Move to tab", "Переместить на вкладку"));
                FillMoveToTabMenu(moveTo, panel, tabData, new List<ShortcutItem>(multiSelection));
                m.MenuItems.Add(Loc.S("Clear selection", "Снять выделение"), (s2, e2) => ClearMultiSelection());
                m.Show(tile, location);
            }
            catch (Exception ex) { AppLog.Write("MultiSelectMenu", ex); }
        }
    }

    // Popup window that opens a folder's children above everything else.
    // Nested folders are opened inside the popup itself (with a Back button),
    // so deep structures work in popup mode exactly like in the tab mode.
    public class FolderPopupForm : Form
    {
        private ShortcutItem folder;
        private readonly List<ShortcutItem> navStack = new List<ShortcutItem>();
        private ToolTip tip;
        private Settings settings;
        private bool editMode;
        private Action<ShortcutItem> onMoveOutOfFolder;
        private Action onChanged;
        private Color bgColor;
        private Color panelColor;
        private Color hoverColor;
        private Color textColor;
        private FlowLayoutPanel flow;
        private Label titleLbl;
        private Button backBtn;
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

            // Descriptions pop up as tooltips almost instantly (0.05 s).
            tip = new ToolTip { InitialDelay = 50, ReshowDelay = 50, AutoPopDelay = 8000, ShowAlways = true };

            openScreen = Screen.FromPoint(screenPos);
            var wa = openScreen.WorkingArea;

            var titleBar = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = panelColor };
            backBtn = new Button
            {
                Text = "‹",
                Width = 30,
                Height = 30,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                ForeColor = textColor,
                BackColor = panelColor,
                Visible = false,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold)
            };
            backBtn.FlatAppearance.BorderSize = 0;
            backBtn.Click += (s, e) => NavigateBack();
            titleBar.Controls.Add(backBtn);
            titleLbl = new Label
            {
                Text = folderItem.Name,
                ForeColor = textColor,
                AutoSize = true,
                Location = new Point(42, 7),
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
                ShortcutItem current = CurrentFolder();
                if (current.Children == null) current.Children = new List<ShortcutItem>();
                foreach (var f in files)
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (string.IsNullOrEmpty(name)) name = Path.GetFileName(f);
                    current.Children.Add(new ShortcutItem
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

        // The folder whose children are displayed right now (root when no navigation).
        private ShortcutItem CurrentFolder()
        {
            return navStack.Count > 0 ? navStack[navStack.Count - 1] : folder;
        }

        // Opens a nested folder inside the popup.
        private void NavigateInto(ShortcutItem sub)
        {
            try
            {
                if (sub == null || !sub.IsFolder) return;
                navStack.Add(sub);
                Rebuild();
            }
            catch { }
        }

        private void NavigateBack()
        {
            try
            {
                if (navStack.Count == 0) return;
                navStack.RemoveAt(navStack.Count - 1);
                Rebuild();
            }
            catch { }
        }

        // Sizes the popup so that every row shows all its tiles: the last column is no
        // longer cut off (4 items = 4 tiles in a row). Scrolling appears only when the
        // folder has more rows than fit on the screen.
        private void LayoutPopup()
        {
            if (this.IsDisposed || flow == null || flow.IsDisposed) return;
            var children = CurrentFolder().Children != null ? CurrentFolder().Children : new List<ShortcutItem>();
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
            if (titleLbl != null) titleLbl.Text = CurrentFolder().Name;
            if (backBtn != null) backBtn.Visible = navStack.Count > 0;
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

            var children = CurrentFolder().Children != null ? CurrentFolder().Children : new List<ShortcutItem>();
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
                if (!string.IsNullOrEmpty(child.ShortDescription))
                    tip.SetToolTip(tile, child.ShortDescription);
                flow.Controls.Add(tile);
            }
        }

        // Drag to swap places (edit mode) + right-click menu for each tile.
        // Left-click on a nested folder opens it inside the popup.
        private void AttachTileHandlers(PopupTile tile, ShortcutItem child)
        {
            bool dragging = false;
            Point down = Point.Empty;

            tile.Click += (s, e) =>
            {
                if (child.IsFolder) NavigateInto(child);
            };

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
            ShortcutItem current = CurrentFolder();
            if (current.Children == null || ReferenceEquals(a, b)) return;
            int ia = current.Children.IndexOf(a);
            int ib = current.Children.IndexOf(b);
            if (ia < 0 || ib < 0) return;
            current.Children[ia] = b;
            current.Children[ib] = a;
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
            if (editMode && navStack.Count == 0) // "move out" makes sense only for the root level
            {
                menu.MenuItems.Add("Move out of folder", (s2, e2) =>
                {
                    if (onMoveOutOfFolder != null) onMoveOutOfFolder(child);
                    Rebuild();
                });
            }
            if (editMode)
            {
                menu.MenuItems.Add("Remove from Panel", (s2, e2) =>
                {
                    suppressDeactivate = true;
                    bool ok;
                    try { ok = ConfirmDialog.Show(this, child); }
                    finally { suppressDeactivate = false; }
                    if (!ok) return;
                    CurrentFolder().Children.Remove(child);
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
                try { IconImage = IconExtractor.GetIconAuto(item.Path, true); } catch { }
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
            try
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
                    try
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
                    catch { }
                }

                Color tColor = this.ForeColor;
                Font labelFont = null;
                Settings cfg = MainForm.CurrentSettings;
                if (cfg != null)
                {
                    try { tColor = Settings.ParseColor(cfg.FontItemsColor, tColor); }
                    catch { }
                    try { labelFont = Settings.MakeFont(cfg.FontItemsName, cfg.FontItemsSize); }
                    catch { }
                }
                try
                {
                    using (var brush = new SolidBrush(tColor))
                    using (var font = labelFont != null ? labelFont : new Font("Segoe UI", 8f))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                        e.Graphics.DrawString(item.Name, font, brush, new Rectangle(2, this.Height - 18, this.Width - 4, 16), sf);
                    }
                }
                catch { }
                if (labelFont != null) { try { labelFont.Dispose(); } catch { } }
            }
            catch (Exception ex)
            {
                AppLog.Write("PopupTile.OnPaint", ex);
            }
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
        public bool MultiSelected { get; set; }
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
            try
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
                int radius = 15;
                var path = GetRoundRectangle(rect, radius);

                if (Item.IsFolder)
                {
                    try
                    {
                        using (var brush = new SolidBrush(Color.FromArgb(50, 128, 128, 128)))
                        {
                            e.Graphics.FillPath(brush, path);
                        }
                    }
                    catch { }

                    if (IsHovered)
                    {
                        try
                        {
                            using (var hoverBrush = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                                e.Graphics.FillPath(hoverBrush, path);
                        }
                        catch { }
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
                            try
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
                            catch { }
                        }
                    }
                }
                else
                {
                    // No solid tile fill: the icon sits directly on the panel background,
                    // with only a soft highlight when hovered.
                    if (IsHovered)
                    {
                        try
                        {
                            bool lightParent = this.Parent != null && this.Parent.BackColor.GetBrightness() > 0.5f;
                            using (var hoverBrush = new SolidBrush(lightParent ? Color.FromArgb(35, 0, 0, 0) : Color.FromArgb(45, 255, 255, 255)))
                            {
                                e.Graphics.FillPath(hoverBrush, path);
                            }
                        }
                        catch { }
                    }

                    if (IconImage != null)
                    {
                        try
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
                        catch { }
                    }
                }

                Color tColor = this.Parent != null ? this.Parent.ForeColor : Color.White;
                Font labelFont = null;
                Settings cfg = MainForm.CurrentSettings;
                if (cfg != null)
                {
                    try { tColor = Settings.ParseColor(cfg.FontItemsColor, tColor); }
                    catch { }
                    try { labelFont = Settings.MakeFont(cfg.FontItemsName, cfg.FontItemsSize); }
                    catch { }
                }
                int labelSpace = GetTextSpace();
                if (labelSpace > 0)
                {
                    try
                    {
                        using (var brush = new SolidBrush(tColor))
                        using (var font = labelFont != null ? labelFont : new Font("Segoe UI", 9f))
                        {
                            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                            Rectangle textRect = new Rectangle(4, this.Height - labelSpace - 1, this.Width - 8, labelSpace);
                            e.Graphics.DrawString(Item.Name, font, brush, textRect, sf);
                        }
                    }
                    catch { }
                }
                if (labelFont != null) { try { labelFont.Dispose(); } catch { } }

                // Multi-select highlight (red edit-button mode).
                if (MultiSelected)
                {
                    try
                    {
                        using (var pen = new Pen(Color.FromArgb(235, 60, 40), 2f))
                            e.Graphics.DrawPath(pen, path);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("TileControl.OnPaint", ex);
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
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
            if (MainForm.CurrentSettings != null)
                prompt.Font = Settings.MakeFont(MainForm.CurrentSettings.FontUiName, MainForm.CurrentSettings.FontUiSize);

            // Row positions follow the (possibly large) UI font.
            int fh = prompt.Font.Height;
            Label textLabel = new Label() { Left = 20, Top = 14, Text = text, Width = 350, Height = fh + 4, AutoSize = false };
            TextBox textBox = new TextBox() { Left = 20, Top = 14 + fh + 10, Width = 350, Text = defaultValue, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White };
            Button confirmation = new Button() { Text = Loc.S("OK", "ОК"), Left = 280, Top = textBox.Top + textBox.Height + 12, Width = 100, Height = fh + 12, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(62, 62, 66) };
            confirmation.FlatAppearance.BorderSize = 0;
            prompt.ClientSize = new Size(400, confirmation.Top + confirmation.Height + 14);

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
            if (st != null) this.Font = Settings.MakeFont(st.FontUiName, st.FontUiSize);

            // Row heights follow the (possibly large) UI font so the title and
            // buttons never get clipped.
            int fh = this.Font.Height;
            Font titleFont = Settings.MakeFont(st != null ? st.FontUiName : "Segoe UI", (st != null ? st.FontUiSize : 9) + 1, System.Drawing.FontStyle.Bold);
            int tfh = titleFont.Height;

            var titleLbl = new Label
            {
                Text = title,
                Left = 20,
                Top = 14,
                Width = 390,
                Height = tfh + 6,
                Font = titleFont
            };
            this.Controls.Add(titleLbl);

            var msgLbl = new Label
            {
                Text = message,
                Left = 20,
                Top = titleLbl.Bottom + 6,
                Width = 390,
                Height = System.Windows.Forms.TextRenderer.MeasureText(message, this.Font, new Size(390, 10000), System.Windows.Forms.TextFormatFlags.WordBreak).Height + 4
            };
            this.Controls.Add(msgLbl);

            int contentBottom = msgLbl.Bottom;
            if (danger != null)
            {
                var dangerLbl = new Label
                {
                    Text = danger,
                    Left = 20,
                    Top = contentBottom + 6,
                    Width = 390,
                    Height = System.Windows.Forms.TextRenderer.MeasureText(danger, this.Font, new Size(390, 10000), System.Windows.Forms.TextFormatFlags.WordBreak).Height + 4,
                    ForeColor = Color.FromArgb(235, 70, 70)
                };
                this.Controls.Add(dangerLbl);
                contentBottom = dangerLbl.Bottom;
            }

            int btnH = fh + 12;
            int okW = Math.Max(95, System.Windows.Forms.TextRenderer.MeasureText(Loc.S("Remove", "Удалить"), this.Font).Width + 26);
            int cancelW = Math.Max(95, System.Windows.Forms.TextRenderer.MeasureText(Loc.S("Cancel", "Отмена"), this.Font).Width + 26);
            int btnTop = contentBottom + 14;
            this.ClientSize = new Size(430, btnTop + btnH + 16);

            var cancelBtn = new Button { Text = Loc.S("Cancel", "Отмена"), Left = this.ClientSize.Width - 20 - okW - 10 - cancelW, Top = btnTop, Width = cancelW, Height = btnH, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = panel, ForeColor = txt };
            cancelBtn.FlatAppearance.BorderSize = 0;
            var okBtn = new Button { Text = Loc.S("Remove", "Удалить"), Left = this.ClientSize.Width - 20 - okW, Top = btnTop, Width = okW, Height = btnH, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(170, 48, 48), ForeColor = Color.White };
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

    // Themed editor for an item's description. The description feeds the panel
    // search and pops up as a hover tooltip — the dialog says so explicitly.
    public class DescriptionDialog : Form
    {
        private TextBox textBox;
        private bool accepted;

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        private DescriptionDialog(ShortcutItem item)
        {
            Settings st = MainForm.CurrentSettings;
            bool light = st != null && st.IsLightTheme;
            Color bg = light ? Color.FromArgb(232, 232, 234) : Color.FromArgb(24, 24, 28);
            Color panel = light ? Color.FromArgb(214, 214, 218) : Color.FromArgb(45, 45, 48);
            Color txt = light ? Color.Black : Color.White;
            Color dim = light ? Color.FromArgb(110, 110, 115) : Color.FromArgb(165, 165, 170);

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = bg;
            this.ForeColor = txt;
            if (st != null) this.Font = Settings.MakeFont(st.FontUiName, st.FontUiSize);

            // Row heights follow the (possibly large) UI font.
            int fh = this.Font.Height;
            Font titleFont = Settings.MakeFont(st != null ? st.FontUiName : "Segoe UI", (st != null ? st.FontUiSize : 9) + 1, System.Drawing.FontStyle.Bold);
            int tfh = titleFont.Height;

            var title = new Label
            {
                Text = Loc.S("Description", "Описание") + " — " + item.Name,
                Left = 20,
                Top = 14,
                Width = 430,
                Height = tfh + 6,
                Font = titleFont
            };
            this.Controls.Add(title);

            string infoText = Loc.S("The description is used by the search and pops up as a tooltip when hovering the element.",
                                    "Описание используется в поиске и всплывает подсказкой при наведении на элемент.");
            var info = new Label
            {
                Text = infoText,
                Left = 20,
                Top = title.Bottom + 6,
                Width = 430,
                Height = System.Windows.Forms.TextRenderer.MeasureText(infoText, this.Font, new Size(430, 10000), System.Windows.Forms.TextFormatFlags.WordBreak).Height + 4,
                ForeColor = dim
            };
            this.Controls.Add(info);

            textBox = new TextBox
            {
                Left = 20,
                Top = info.Bottom + 10,
                Width = 430,
                Height = fh * 4 + 14,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = panel,
                ForeColor = txt,
                BorderStyle = BorderStyle.FixedSingle,
                Text = item.ShortDescription == null ? "" : item.ShortDescription
            };
            this.Controls.Add(textBox);

            int btnH = fh + 12;
            int btnTop = textBox.Bottom + 14;
            this.ClientSize = new Size(470, btnTop + btnH + 16);
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));

            var cancel = new Button { Text = Loc.S("Cancel", "Отмена"), Left = 250, Top = btnTop, Width = 95, Height = btnH, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = panel, ForeColor = txt };
            cancel.FlatAppearance.BorderSize = 0;
            cancel.FlatAppearance.MouseOverBackColor = light ? Color.FromArgb(190, 190, 195) : Color.FromArgb(62, 62, 66);
            var ok = new Button { Text = Loc.S("OK", "ОК"), Left = 355, Top = btnTop, Width = 95, Height = btnH, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = panel, ForeColor = txt };
            ok.FlatAppearance.BorderSize = 0;
            ok.FlatAppearance.MouseOverBackColor = light ? Color.FromArgb(190, 190, 195) : Color.FromArgb(62, 62, 66);
            ok.Click += (s, e) => { accepted = true; };
            this.Controls.Add(cancel);
            this.Controls.Add(ok);
            this.AcceptButton = ok;
            this.CancelButton = cancel;
        }

        // Returns the entered text, or null when the dialog was cancelled.
        public static string Show(IWin32Window owner, ShortcutItem item)
        {
            using (var dlg = new DescriptionDialog(item))
            {
                dlg.ShowDialog(owner);
                return dlg.accepted ? dlg.textBox.Text.Trim() : null;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(120, this.ForeColor)))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
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

    // Text helpers shared by the panel search and the mini explorer search:
    // tail/end truncation and drawing a highlighted (matched) fragment.
    internal static class UiText
    {
        // Truncates the START of a string so the tail fits ("…\sub\file.exe").
        public static string FitTail(string s, Font f, int maxWidth)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (TextRenderer.MeasureText(s, f).Width <= maxWidth) return s;
            const string ell = "…";
            int lo = 0, hi = s.Length - 1, keep = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                string cand = ell + s.Substring(s.Length - mid);
                if (TextRenderer.MeasureText(cand, f).Width <= maxWidth) { keep = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return ell + s.Substring(s.Length - keep);
        }

        // Truncates the END of a string with an ellipsis.
        public static string FitEnd(string s, Font f, int maxWidth)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (TextRenderer.MeasureText(s, f).Width <= maxWidth) return s;
            const string ell = "…";
            int lo = 0, hi = s.Length, keep = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                string cand = s.Substring(0, mid) + ell;
                if (TextRenderer.MeasureText(cand, f).Width <= maxWidth) { keep = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return s.Substring(0, keep) + ell;
        }

        // Finds the earliest query variant occurrence inside a lowercase text.
        public static bool FindHighlight(string textLower, List<string> variants, out int start, out int len)
        {
            start = -1; len = 0;
            if (string.IsNullOrEmpty(textLower) || variants == null) return false;
            int bestIdx = int.MaxValue, bestLen = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                string v = variants[i];
                if (string.IsNullOrEmpty(v)) continue;
                int idx = textLower.IndexOf(v, StringComparison.Ordinal);
                if (idx < 0) continue;
                if (idx < bestIdx || (idx == bestIdx && v.Length > bestLen)) { bestIdx = idx; bestLen = v.Length; }
            }
            if (bestIdx == int.MaxValue) return false;
            start = bestIdx; len = bestLen;
            return true;
        }

        // Drops the highlight range when the truncated display string no longer contains it.
        public static void ClampHighlight(string display, ref bool enabled, ref int start, ref int len)
        {
            if (!enabled || start < 0) { enabled = false; start = -1; len = 0; return; }
            if (start >= display.Length) { enabled = false; start = -1; len = 0; return; }
            int maxLen = display.Length - start;
            if (display.EndsWith("…", StringComparison.Ordinal)) maxLen--;
            if (len > maxLen) len = maxLen;
            if (len <= 0) { enabled = false; start = -1; len = 0; }
        }

        // Draws "text" at pos with the [start, start+len) fragment highlighted:
        // same font (a bold font is wider and shifts the text around the match),
        // accent color plus a soft background bar.
        //
        // Measuring: TextRenderer adds a constant slack to EVERY call (a single
        // "G" measures ~FontHeight wide even with NoPadding), which padded the
        // highlight bar by several pixels on each side. The slack cancels in the
        // DIFFERENCE of two measurements of strings sharing a prefix, so every
        // width below is measured as "x"+text minus "x" — real glyph advances,
        // and the bar hugs the letters with no extra padding.
        public static void DrawHighlighted(Graphics g, string display, int start, int len,
            Font normal, Point pos, Color normalColor, Color highlightColor, bool lightTheme)
        {
            const TextFormatFlags flags = TextFormatFlags.NoPadding;
            if (len <= 0 || start < 0 || start + len > display.Length)
            {
                TextRenderer.DrawText(g, display, normal, pos, normalColor, flags);
                return;
            }
            string before = display.Substring(0, start);
            string mid = display.Substring(start, len);
            string after = display.Substring(start + len);
            int wX = TextRenderer.MeasureText("x", normal, Size.Empty, flags).Width;
            int wHead = TextRenderer.MeasureText("x" + before, normal, Size.Empty, flags).Width;
            int wHeadMid = TextRenderer.MeasureText("x" + before + mid, normal, Size.Empty, flags).Width;
            int beforeAdv = wHead - wX;
            int midAdv = wHeadMid - wHead;
            int barH = TextRenderer.MeasureText("x", normal, Size.Empty, flags).Height;
            TextRenderer.DrawText(g, before, normal, pos, normalColor, flags);
            int x = pos.X + beforeAdv;
            Color barColor = lightTheme ? Color.FromArgb(45, highlightColor) : Color.FromArgb(55, highlightColor);
            using (var brush = new SolidBrush(barColor))
                g.FillRectangle(brush, x, pos.Y, midAdv, barH);
            TextRenderer.DrawText(g, mid, normal, new Point(x, pos.Y), highlightColor, flags);
            TextRenderer.DrawText(g, after, normal, new Point(x + midAdv, pos.Y), normalColor, flags);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            AppLog.InstallGlobalHandlers();
            if (!SingleInstance.Start())
            {
                SingleInstance.NotifyExisting();
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                // Startup crash: log it and let the user see what happened instead of
                // a silent process exit.
                AppLog.Write("Fatal startup exception", ex);
                try { MessageBox.Show(Loc.S("Tilettes", "Плиточки") + ": " + ex.Message + "\n\n" + Loc.S("Details in log.txt", "Подробности в log.txt"), Loc.S("Tilettes", "Плиточки"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
                catch { }
            }
        }
    }
}
