using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WinPanel
{
    // Mini file explorer: a quick folder window opened with Ctrl+Click on a folder.
    // Browser-style bookmarks panel (folders + saved console commands), a Windows
    // Explorer style breadcrumb path bar and a small embedded console.
    public class MiniExplorerForm : Form
    {
        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private Settings settings;
        private string settingsPath;
        private Color bgColor, panelColor, hoverColor, textColor, dimColor, accentColor, listColor, consoleBg;

        // navigation state
        private string currentPath = "";
        // The ACTIVE tab's back/forward history (mirrors activeTab.History).
        private List<string> history = new List<string>();
        private int historyIndex = -1;
        // Explorer tabs: each tab keeps its own path and back/forward history;
        // the file list, breadcrumbs, bookmarks and the console are shared.
        private class ExplorerTab
        {
            public string Path = "";
            public List<string> History = new List<string>();
            public int HistoryIndex = -1;
        }
        private readonly List<ExplorerTab> tabs = new List<ExplorerTab>();
        private ExplorerTab activeTab;
        private Panel tabStripHost;
        private FlowLayoutPanel tabFlow;
        private Button btnTabNew;
        private readonly Dictionary<Button, ExplorerTab> tabByButton = new Dictionary<Button, ExplorerTab>();

        // controls
        private Label titleLbl, statusLbl;
        private Label lblConsole, hintLbl, promptLbl, bmHeader;
        private Button btnBack, btnFwd, btnUp, btnRefresh, btnEditPath, btnBmAdd, btnToggleBm, btnTopBar;
        private Button btnConsoleWin, btnConsoleRestart, btnConsoleClear, btnSaveCmd, btnRunCmd, btnConsoleStop;
        private Panel topBarHost, splitter, bmSplitter;
        private FlowLayoutPanel topBarFlow;
        private int splitGrabDy;
        private bool topBarVisible = true;
        private bool splitterDragging;
        private bool bmSplitterDragging;
        private int bmGrabDx;
        // Bookmarks panel width, fraction of the window width (settings key MiniExplorerBm).
        private double bmFrac = 0.18;
        private bool sizing;
        private double consoleFrac = 0.40;
        private Panel crumbHost;
        private Panel titleBarPanel; // height follows the UI font
        private FlowLayoutPanel crumbFlow;
        private TextBox pathEdit, consoleIn;
        private ListBox bookmarksList, fileList;
        private RichTextBox consoleOut;
        private TextBox searchBox;
        private Button btnScope;
        private System.Windows.Forms.Timer searchTimer;
        // Mini explorer search is disabled for now: each query indexed whole drives
        // (SearchCore.GetIndex, up to 200k entries per root) and re-scanned the index
        // on the UI thread every 700 ms while it was still building. All the search
        // code stays in place for a possible re-enable - only the controls are hidden
        // and the entry points short-circuit. The panel search does not use this path.
        internal static readonly bool SearchEnabled = false;
        private List<SearchItem> searchItems;
        private List<string> searchVariants = new List<string>();
        private bool searchMode;
        private string searchScope = "folder";
        private int topBarChipH = 24; // chip height follows the UI font

        // file list data
        private class DirEntry { public string Name; public string FullPath; public bool IsDir; public long Size; }
        private readonly List<DirEntry> entries = new List<DirEntry>();
        private readonly Dictionary<string, Bitmap> iconCache = new Dictionary<string, Bitmap>();
        // Keys whose (network) icon extraction is in flight - no duplicate workers.
        private readonly HashSet<string> pendingIcons = new HashSet<string>();
        private int hoverFile = -1, hoverBm = -1;
        private Font boldFont;
        private ToolTip tip;

        // bookmarks
        private List<ExplorerBookmark> bookmarks;
        private string bookmarksPath;
        private bool bookmarksVisible = true;
        private readonly HashSet<string> expandedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private class Row { public ExplorerBookmark Bm; public int Depth; }
        private readonly List<Row> bmRows = new List<Row>();
        // Manual drag-and-drop reordering of bookmark rows (no OLE DnD).
        private int bmDragIndex = -1;   // source row
        private int bmDragTarget = -1;  // row the insertion line is drawn on (-1 = none)
        private bool bmDragDropAbove;   // insert above (true) / below the target row
        private bool bmDragging;
        private Point bmDragStart;
        private bool bmSuppressClick;   // a drag release must not act as a click

        // console
        private Process shell;
        private StreamWriter shellIn;
        private readonly List<string> cmdHistory = new List<string>();
        private int cmdHistoryPos = 0;
        private const string Sentinel = "__WPMARK__";
        private readonly Queue<string> sentEcho = new Queue<string>();
        private volatile bool skipBanner = true;

        public MiniExplorerForm(string startPath, Settings st, string stPath)
        {
            settings = st != null ? st : new Settings();
            settingsPath = stPath != null ? stPath : "settings.ini";
            bookmarksVisible = settings.MiniExplorerBookmarks;
            topBarVisible = settings.MiniExplorerTopBar;
            if (settings.MiniExplorerConsole >= 15 && settings.MiniExplorerConsole <= 85)
                consoleFrac = settings.MiniExplorerConsole / 100.0;
            if (settings.MiniExplorerBm >= 12 && settings.MiniExplorerBm <= 45)
                bmFrac = settings.MiniExplorerBm / 100.0;
            // UiPalette: follows the active skin, falls back to the classic
            // light/dark theme (previously the window ignored decorative skins).
            bool light = UiPalette.IsLight;
            bgColor = UiPalette.Bg;
            panelColor = UiPalette.Panel;
            hoverColor = UiPalette.Hover;
            textColor = UiPalette.Text;
            dimColor = UiPalette.Dim;
            accentColor = UiPalette.Accent;
            listColor = UiPalette.ListBg;
            consoleBg = UiPalette.ConsoleBg;

            this.Text = Loc.S("Mini Explorer", "Мини-проводник");
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = true;
            this.KeyPreview = true;

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int winW, winH, winX, winY;
            if (settings.MiniExplorerW >= 760 && settings.MiniExplorerH >= 520)
            {
                winW = Math.Min(settings.MiniExplorerW, wa.Width - 20);
                winH = Math.Min(settings.MiniExplorerH, wa.Height - 20);
                winX = settings.MiniExplorerX;
                winY = settings.MiniExplorerY;
                if (winX < wa.Left - 50 || winX > wa.Right - 200) winX = wa.Left + (wa.Width - winW) / 2;
                if (winY < wa.Top - 10 || winY > wa.Bottom - 120) winY = wa.Top + (wa.Height - winH) / 2;
            }
            else
            {
                winW = (int)(wa.Width * 0.78);
                winH = (int)(wa.Height * 0.78);
                if (winW < 980) winW = Math.Min(980, wa.Width - 20);
                if (winH < 660) winH = Math.Min(660, wa.Height - 20);
                if (winW > 1600) winW = 1600;
                if (winH > 1000) winH = 1000;
                if (winW > wa.Width - 20) winW = wa.Width - 20;
                if (winH > wa.Height - 20) winH = wa.Height - 20;
                winX = wa.Left + (wa.Width - winW) / 2;
                winY = wa.Top + (wa.Height - winH) / 2;
            }
            this.ClientSize = new Size(winW, winH);
            this.Location = new Point(winX, winY);
            this.MinimumSize = new Size(760, 520);
            this.BackColor = bgColor;
            this.ForeColor = textColor;
            this.Font = Settings.MakeFont(settings.FontUiName, settings.FontUiSize);
            this.DoubleBuffered = true;
            this.Paint += (s, e) =>
            {
                try
                {
                    Color bc = light ? Color.FromArgb(185, 185, 190) : Color.FromArgb(78, 78, 84);
                    using (var pen = new Pen(bc))
                        e.Graphics.DrawRectangle(pen, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
                }
                catch { }
            };
            this.KeyDown += Mini_KeyDown;
            boldFont = new Font(this.Font, FontStyle.Bold);
            tip = new ToolTip();

            // ---------- title bar ----------
            var titleBar = new EdgeTitlePanel(this) { Dock = DockStyle.Top, Height = 30, BackColor = panelColor };
            titleBarPanel = titleBar;
            titleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
            titleLbl = new Label { Text = Loc.S("Mini Explorer", "Мини-проводник"), ForeColor = textColor, AutoSize = true, Location = new Point(10, 7) };
            titleBar.Controls.Add(titleLbl);
            var closeBtn = new Button { Text = "X", Width = 30, Height = 30, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, ForeColor = textColor, BackColor = panelColor };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => this.Close();
            titleBar.Controls.Add(closeBtn);
            this.Controls.Add(titleBar);

            // ---------- tab strip (one tab per browsed folder) ----------
            tabStripHost = new Panel { Left = 6, Top = 34, Width = 900, Height = 30, BackColor = bgColor, AutoScroll = true };
            tabFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = bgColor
            };
            tabStripHost.Controls.Add(tabFlow);
            btnTabNew = FlatButton("+", 0, 0, 26, 24);
            btnTabNew.Margin = new Padding(2, 2, 2, 2);
            btnTabNew.Click += (s, e) => NewTab(currentPath);
            tip.SetToolTip(btnTabNew, Loc.S("New tab", "Новая вкладка"));
            this.Controls.Add(tabStripHost);

            // ---------- toolbar: navigation + breadcrumb + status ----------
            btnToggleBm = NavButton("≡", 8);
            btnToggleBm.Click += (s, e) =>
            {
                bookmarksVisible = !bookmarksVisible;
                settings.MiniExplorerBookmarks = bookmarksVisible;
                LayoutAll();
                SaveWindowState();
                try { fileList.Focus(); } catch { }
            };
            tip.SetToolTip(btnToggleBm, Loc.S("Show / hide bookmarks panel"));
            btnTopBar = NavButton("\u2630", 44);
            btnTopBar.Click += (s, e) =>
            {
                topBarVisible = !topBarVisible;
                settings.MiniExplorerTopBar = topBarVisible;
                LayoutAll();
                SaveWindowState();
                try { fileList.Focus(); } catch { }
            };
            tip.SetToolTip(btnTopBar, Loc.S("Show / hide top bookmarks bar"));
            btnBack = NavButton("←", 82);
            btnFwd = NavButton("→", 120);
            btnUp = NavButton("↑", 158);
            btnRefresh = NavButton("↻", 196);
            btnBack.Click += (s, e) => GoBack();
            btnFwd.Click += (s, e) => GoForward();
            btnUp.Click += (s, e) => GoUp();
            btnRefresh.Click += (s, e) => LoadDir();
            this.Controls.Add(btnToggleBm);
            this.Controls.Add(btnTopBar);
            this.Controls.Add(btnBack);
            this.Controls.Add(btnFwd);
            this.Controls.Add(btnUp);
            this.Controls.Add(btnRefresh);

            crumbHost = new Panel { Left = 158, Top = 31, Width = 598, Height = 30, BackColor = bgColor, AutoScroll = true };
            crumbHost.MouseClick += (s, e) => BeginPathEdit();
            crumbFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = bgColor,
                Location = new Point(2, 1),
                Height = 26
            };
            crumbFlow.MouseClick += (s, e) => BeginPathEdit();
            crumbHost.Controls.Add(crumbFlow);
            this.Controls.Add(crumbHost);

            pathEdit = new TextBox { Left = 158, Top = 32, Width = 598, Height = 24, Visible = false, BorderStyle = BorderStyle.FixedSingle, BackColor = listColor, ForeColor = textColor };
            pathEdit.KeyDown += PathEdit_KeyDown;
            this.Controls.Add(pathEdit);

            btnEditPath = FlatButton(Loc.S("Edit"), 762, 32, 44, 26);
            btnEditPath.Click += (s, e) => BeginPathEdit();
            this.Controls.Add(btnEditPath);

            // Search: results replace the file list; one toggle button shows and
            // switches the scope (current folder / everywhere).
            btnScope = FlatButton(Loc.S("Folder", "Папка"), 540, 32, 64, 26);
            btnScope.Click += (s, e) => SetSearchScope(searchScope == "all" ? "folder" : "all");
            this.Controls.Add(btnScope);
            tip.SetToolTip(btnScope, Loc.S("Search scope: click switches folder / everywhere", "Область поиска: клик переключает папка/везде"));

            searchBox = new TextBox { Left = 470, Top = 33, Width = 120, Height = 24, BorderStyle = BorderStyle.FixedSingle, BackColor = listColor, ForeColor = textColor };
            try
            {
                int sfs = Math.Max(7, Math.Min(30, settings.SearchBoxFontSize));
                if (sfs != 9) searchBox.Font = new Font(searchBox.Font.FontFamily, sfs);
            }
            catch { }
            searchBox.TextChanged += (s, e) =>
            {
                if (searchBox.TextLength > 0)
                {
                    searchTimer.Stop();
                    searchTimer.Interval = 250;
                    searchTimer.Start();
                }
                else
                {
                    ExitSearch();
                }
            };
            searchBox.KeyDown += SearchBox_KeyDown;
            this.Controls.Add(searchBox);
            tip.SetToolTip(searchBox, Loc.S("Search files (fuzzy, any keyboard layout)", "Поиск файлов (fuzzy, любая раскладка)"));

            searchTimer = new System.Windows.Forms.Timer();
            searchTimer.Interval = 250;
            searchTimer.Tick += (s, e) => { searchTimer.Stop(); RunSearch(false); };
            UpdateScopeButtons();
            if (!SearchEnabled)
            {
                btnScope.Visible = false;
                searchBox.Visible = false;
            }

            statusLbl = new Label { Left = 812, Top = 37, Width = 120, Height = 18, ForeColor = dimColor, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true };
            this.Controls.Add(statusLbl);

            // ---------- top bookmarks bar (same list as the left panel) ----------
            topBarHost = new Panel { Left = 8, Top = 64, Width = 100, Height = 26, BackColor = bgColor, AutoScroll = true };
            topBarHost.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) ShowTopBarMenu(); };
            topBarFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = bgColor,
                Location = new Point(2, 2),
                Height = 24
            };
            topBarFlow.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) ShowTopBarMenu(); };
            topBarHost.Controls.Add(topBarFlow);
            this.Controls.Add(topBarHost);

            // ---------- bookmarks panel ----------
            bmHeader = new Label { Text = "BOOKMARKS", Left = 10, Top = 70, Width = 150, Height = 16, ForeColor = dimColor };
            this.Controls.Add(bmHeader);
            btnBmAdd = FlatButton("+", 184, 66, 24, 20);
            btnBmAdd.Click += (s, e) => ShowBookmarksAddMenu();
            this.Controls.Add(btnBmAdd);

            bookmarksList = new ListBox
            {
                Left = 8,
                Top = 88,
                Width = 200,
                Height = 340,
                BackColor = listColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22,
                IntegralHeight = false
            };
            bookmarksList.DrawItem += BookmarksList_DrawItem;
            bookmarksList.Click += BookmarksList_Click;
            bookmarksList.MouseDown += BookmarksList_MouseDown;
            bookmarksList.MouseMove += BookmarksList_MouseMove;
            bookmarksList.MouseUp += BookmarksList_MouseUp;
            bookmarksList.MouseLeave += (s, e) => { if (hoverBm != -1) { hoverBm = -1; bookmarksList.Invalidate(); } };
            this.Controls.Add(bookmarksList);

            // ---------- bookmarks width splitter (drag to resize the left panel) ----------
            bmSplitter = new Panel { Left = 210, Top = 88, Width = 6, Height = 340, BackColor = panelColor, Cursor = Cursors.SizeWE };
            bmSplitter.MouseDown += BmSplitter_MouseDown;
            bmSplitter.MouseMove += BmSplitter_MouseMove;
            bmSplitter.MouseUp += BmSplitter_MouseUp;
            bmSplitter.Paint += BmSplitter_Paint;
            tip.SetToolTip(bmSplitter, Loc.S("Drag to resize the bookmarks panel", "Потяните, чтобы изменить ширину панели закладок"));
            this.Controls.Add(bmSplitter);

            // ---------- file list ----------
            fileList = new ListBox
            {
                Left = 216,
                Top = 88,
                Width = 716,
                Height = 340,
                BackColor = listColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = Math.Max(16, Math.Min(60, settings.SearchResultsFontSize + 15)),
                IntegralHeight = false
            };
            fileList.DrawItem += FileList_DrawItem;
            fileList.DoubleClick += (s, e) => OpenSelectedEntry();
            fileList.KeyDown += FileList_KeyDown;
            fileList.KeyPress += FileList_KeyPress;
            fileList.MouseDown += FileList_MouseDown;
            fileList.MouseMove += FileList_MouseMove;
            fileList.MouseLeave += (s, e) => { if (hoverFile != -1) { hoverFile = -1; fileList.Invalidate(); } };
            this.Controls.Add(fileList);

            // ---------- splitter (drag to resize the console) ----------
            splitter = new Panel { Left = 8, Top = 400, Width = 100, Height = 8, BackColor = panelColor, Cursor = Cursors.SizeNS };
            splitter.MouseDown += Splitter_MouseDown;
            splitter.MouseMove += Splitter_MouseMove;
            splitter.MouseUp += Splitter_MouseUp;
            splitter.Paint += Splitter_Paint;
            tip.SetToolTip(splitter, Loc.S("Drag to resize the console"));
            this.Controls.Add(splitter);

            // ---------- console ----------
            lblConsole = new Label { Text = "Console", Left = 10, Top = 440, Width = 120, ForeColor = textColor };
            this.Controls.Add(lblConsole);
            btnConsoleWin = FlatButton("New window", 832, 437, 100, 22);
            btnConsoleWin.Click += (s, e) => OpenRealConsole();
            btnConsoleRestart = FlatButton("Restart", 752, 437, 74, 22);
            btnConsoleRestart.Click += (s, e) => StartShell();
            btnConsoleClear = FlatButton("Clear", 682, 437, 64, 22);
            btnConsoleClear.Click += (s, e) => { try { consoleOut.Clear(); } catch { } };
            btnConsoleStop = FlatButton(Loc.S("Stop", "Стоп"), 610, 437, 62, 22);
            btnConsoleStop.Click += (s, e) => StopConsoleCommand();
            tip.SetToolTip(btnConsoleStop, Loc.S("Stop the running command", "Остановить выполняющуюся команду"));
            this.Controls.Add(btnConsoleWin);
            this.Controls.Add(btnConsoleRestart);
            this.Controls.Add(btnConsoleClear);
            this.Controls.Add(btnConsoleStop);

            consoleOut = new RichTextBox
            {
                Left = 8,
                Top = 464,
                Width = 924,
                Height = 120,
                BackColor = consoleBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                DetectUrls = false,
                Font = ConsoleFont()
            };
            consoleOut.MouseWheel += ConsoleOut_MouseWheel;
            tip.SetToolTip(consoleOut, Loc.S("Ctrl+mouse wheel - console font size", "Ctrl+колесо мыши — размер шрифта консоли"));
            this.Controls.Add(consoleOut);

            promptLbl = new Label { Text = "›", Left = 8, Top = 594, Width = 16, ForeColor = accentColor };
            this.Controls.Add(promptLbl);
            consoleIn = new TextBox { Left = 26, Top = 590, Width = 772, Height = 24, BackColor = listColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            consoleIn.KeyDown += ConsoleIn_KeyDown;
            this.Controls.Add(consoleIn);
            btnSaveCmd = FlatButton("+ Save", 806, 590, 64, 24);
            btnSaveCmd.Click += (s, e) => SaveCommandBookmark(consoleIn.Text);
            this.Controls.Add(btnSaveCmd);
            btnRunCmd = FlatButton(Loc.S("Run", "Выполнить"), 876, 590, 56, 24);
            btnRunCmd.Click += (s, e) => RunConsoleInput();
            this.Controls.Add(btnRunCmd);

            hintLbl = new EdgeHintLabel(this)
            {
                Text = "Ctrl+L or Edit - edit path · F5 - refresh · Backspace - up · Enter - open · double-click - open · "
                     + Loc.S("in bookmarks %1 = current folder", "в закладках %1 — текущая папка"),
                Left = 8,
                Top = 618,
                Width = 924,
                Height = 16,
                ForeColor = dimColor
            };
            this.Controls.Add(hintLbl);

            // ---------- bookmarks data ----------
            bookmarksPath = Path.Combine(Application.StartupPath, "bookmarks.xml");
            bookmarks = BookmarksStore.Load(bookmarksPath);
            foreach (var b in bookmarks) ExpandGroupRecursive(b);
            RebuildBookmarks();

            this.Load += (s, e) => { StartShell(); };
            this.Shown += (s, e) => { try { fileList.Focus(); } catch { } };
            this.FormClosed += (s, e) =>
            {
                StopShell();
                try { boldFont.Dispose(); } catch { }
                foreach (var b in iconCache.Values)
                {
                    try { if (b != null) b.Dispose(); } catch { }
                }
                iconCache.Clear();
            };
            this.FormClosing += (s, e) => { SaveWindowState(); };
            Loc.Walk(this);
            LayoutAll();
            UpdateRegion();

            // ---------- tabs: the initial tab holds the startup folder ----------
            activeTab = new ExplorerTab();
            tabs.Add(activeTab);
            history = activeTab.History;
            historyIndex = activeTab.HistoryIndex;
            RebuildTabStrip();

            Navigate(startPath);
            if (currentPath.Length == 0)
            {
                try { Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); } catch { }
            }
            if (currentPath.Length == 0) Navigate("C:\\");
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!this.IsHandleCreated) return;
            if (sizing)
            {
                LayoutAll();
                return;
            }
            UpdateRegion();
            LayoutAll();
        }

        // Shared edge calculation for the form itself and the edge-aware child controls.
        internal void ApplyEdgeHit(ref Message m)
        {
            int lp = m.LParam.ToInt32();
            int sx = (short)(lp & 0xFFFF);
            int sy = (short)((lp >> 16) & 0xFFFF);
            Point p = PointToClient(new Point(sx, sy));
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            const int grip = 12;
            bool left = p.X < grip;
            bool right = p.X >= w - grip;
            bool top = p.Y < grip;
            bool bottom = p.Y >= h - grip;
            if (top || left || right || bottom)
            {
                if (top && left) m.Result = (IntPtr)13;
                else if (top && right) m.Result = (IntPtr)14;
                else if (bottom && left) m.Result = (IntPtr)16;
                else if (bottom && right) m.Result = (IntPtr)17;
                else if (left) m.Result = (IntPtr)10;
                else if (right) m.Result = (IntPtr)11;
                else if (top) m.Result = (IntPtr)12;
                else if (bottom) m.Result = (IntPtr)15;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int WM_SIZING = 0x0214;
            const int WM_EXITSIZEMOVE = 0x0232;
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                ApplyEdgeHit(ref m);
                return;
            }
            if (m.Msg == WM_SIZING)
            {
                // While the user drags a border: drop the rounded region so the whole
                // window paints with no clipping gaps (this removed the resize flicker).
                if (!sizing)
                {
                    sizing = true;
                    try { this.Region = null; } catch { }
                }
            }
            else if (m.Msg == WM_EXITSIZEMOVE)
            {
                if (sizing)
                {
                    sizing = false;
                    base.WndProc(ref m);
                    UpdateRegion();
                    LayoutAll();
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void UpdateRegion()
        {
            try
            {
                if (this.Region != null) this.Region.Dispose();
                this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 12, 12));
            }
            catch { }
        }

        private void SaveWindowState()
        {
            try
            {
                if (settings == null) return;
                settings.MiniExplorerW = this.Width;
                settings.MiniExplorerH = this.Height;
                settings.MiniExplorerX = this.Location.X;
                settings.MiniExplorerY = this.Location.Y;
                settings.MiniExplorerBookmarks = bookmarksVisible;
                settings.MiniExplorerTopBar = topBarVisible;
                settings.MiniExplorerConsole = (int)Math.Round(consoleFrac * 100);
                settings.MiniExplorerBm = (int)Math.Round(bmFrac * 100);
                settings.Save(settingsPath);
            }
            catch { }
        }

        // Layout is proportional so the window can be resized: the console takes
        // about 40% of the height, the file list fills the rest.
        private void LayoutAll()
        {
            if (this.IsDisposed || btnBack == null || fileList == null) return;
            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;

            // Every row height derives from the UI font so nothing clips at
            // bigger font sizes.
            int fh = this.Font.Height;
            int btnH = fh + 8;   // buttons: nav, scope, edit path, console, input row
            int lblH = fh + 4;   // small labels: status, hint, bookmarks header
            int titleH = fh + 14;
            if (titleBarPanel != null) titleBarPanel.Height = titleH;
            // Tab strip below the title bar; everything else moves down with it.
            // The row height follows the font so tab captions do not clip.
            int tabBtnH = Math.Max(22, TextRenderer.MeasureText("Ag", this.Font).Height + 8);
            int tabH = tabBtnH + 8;
            if (tabStripHost != null)
            {
                tabStripHost.SetBounds(6, titleH + 2, W - 12, tabH);
            }
            int top = titleH + tabH + 2;
            int rightEdge = W - 8;

            int navY = top + 4;
            btnToggleBm.SetBounds(8, navY, 28, btnH);
            btnTopBar.SetBounds(44, navY, 28, btnH);
            btnBack.SetBounds(82, navY, 28, btnH);
            btnFwd.SetBounds(120, navY, 28, btnH);
            btnUp.SetBounds(158, navY, 28, btnH);
            btnRefresh.SetBounds(196, navY, 28, btnH);

            // Top-right is reserved for search: [scope toggle][search box][status].
            statusLbl.SetBounds(rightEdge - 150, navY + (btnH - lblH) / 2, 146, lblH);
            int searchW = Math.Min(280, Math.Max(150, W / 5));
            int searchX = rightEdge - 150 - 6 - searchW;
            searchBox.SetBounds(searchX, navY + (btnH - searchBox.Height) / 2, searchW, searchBox.Height);
            int scopeW = TextRenderer.MeasureText(btnScope.Text, this.Font).Width + 20;
            btnScope.SetBounds(searchX - 6 - scopeW, navY, scopeW, btnH);

            // The breadcrumb path bar sits on its own row below the nav row.
            int crumbTop = navY + btnH + 8;
            int editW = TextRenderer.MeasureText(btnEditPath.Text, this.Font).Width + 20;
            btnEditPath.SetBounds(rightEdge - editW, crumbTop, editW, btnH);
            int crumbH = btnH + 6;
            int crumbW = Math.Max(80, rightEdge - editW - 6 - 8);
            crumbHost.SetBounds(8, crumbTop, crumbW, crumbH);
            pathEdit.SetBounds(8, crumbTop + 2, crumbW, 24);
            int crumbBottom = crumbTop + crumbH + 2;

            // Top bookmarks bar height follows the font so chip labels are not cut.
            topBarHost.Visible = topBarVisible;
            topBarHost.SetBounds(8, crumbBottom + 2, W - 16, topBarChipH + 6);

            int listTop = crumbBottom + 6 + (topBarVisible ? topBarChipH + 8 : 0);
            int contentH = H - top;
            int consArea = (int)(contentH * consoleFrac);
            if (consArea < 120) consArea = Math.Min(120, contentH / 2);
            int consTop = H - consArea;
            if (consTop < listTop + 90) consTop = listTop + 90;
            int splitH = 8;
            int listH = consTop - 10 - 4 - listTop;
            if (listH < 60) listH = 60;

            // Bookmarks panel: a fraction of the window width, adjustable by the
            // vertical splitter next to it.
            int bmW = 200;
            if (bookmarksVisible)
            {
                bmW = (int)Math.Round(W * bmFrac);
                if (bmW < 130) bmW = 130;
                if (bmW > W - 210) bmW = Math.Max(130, W - 210); // keep the file list usable
            }
            bmHeader.SetBounds(10, listTop - lblH - 4, Math.Max(80, bmW - 20), lblH);
            btnBmAdd.SetBounds(8 + bmW - 28, listTop - btnH - 2, 24, btnH);
            bookmarksList.SetBounds(8, listTop, bmW, listH);
            int bmItemH = Math.Max(20, TextRenderer.MeasureText("Ag", this.Font).Height + 8);
            if (bookmarksList.ItemHeight != bmItemH) bookmarksList.ItemHeight = bmItemH;
            bmHeader.Visible = bookmarksVisible;
            btnBmAdd.Visible = bookmarksVisible;
            bookmarksList.Visible = bookmarksVisible;
            bmSplitter.Visible = bookmarksVisible;
            bmSplitter.SetBounds(8 + bmW + 1, listTop, 6, listH);

            int fx = bookmarksVisible ? 8 + bmW + 9 : 8;
            fileList.SetBounds(fx, listTop, W - fx - 8, listH);

            splitter.SetBounds(8, listTop + listH + 2, W - 16, splitH);

            lblConsole.SetBounds(10, consTop + (btnH - lblH) / 2, 120, lblH);
            int conBtnY = consTop + 2;
            int wWin = TextRenderer.MeasureText(btnConsoleWin.Text, this.Font).Width + 20;
            int wRestart = TextRenderer.MeasureText(btnConsoleRestart.Text, this.Font).Width + 16;
            int wClear = TextRenderer.MeasureText(btnConsoleClear.Text, this.Font).Width + 16;
            int wStop = TextRenderer.MeasureText(btnConsoleStop.Text, this.Font).Width + 16;
            btnConsoleWin.SetBounds(rightEdge - wWin, conBtnY, wWin, btnH);
            btnConsoleRestart.SetBounds(rightEdge - wWin - 6 - wRestart, conBtnY, wRestart, btnH);
            btnConsoleClear.SetBounds(rightEdge - wWin - 6 - wRestart - 6 - wClear, conBtnY, wClear, btnH);
            btnConsoleStop.SetBounds(rightEdge - wWin - 6 - wRestart - 6 - wClear - 6 - wStop, conBtnY, wStop, btnH);

            // Bottom: input row + hint line, both scaled from the font.
            int ipTop = H - (btnH + lblH + 16);
            int outTop = consTop + btnH + 10;
            int outH = ipTop - 6 - outTop;
            if (outH < 40) outH = 40;
            consoleOut.SetBounds(8, outTop, W - 16, outH);

            promptLbl.SetBounds(8, ipTop + (btnH - lblH) / 2, 16, lblH);
            int wRun = TextRenderer.MeasureText(btnRunCmd.Text, this.Font).Width + 20;
            int wSave = TextRenderer.MeasureText(btnSaveCmd.Text, this.Font).Width + 16;
            int runX = rightEdge - wRun;
            int saveX = runX - 6 - wSave;
            consoleIn.SetBounds(26, ipTop, Math.Max(80, saveX - 6 - 26), btnH);
            btnSaveCmd.SetBounds(saveX, ipTop, wSave, btnH);
            btnRunCmd.SetBounds(runX, ipTop, wRun, btnH);

            hintLbl.SetBounds(8, ipTop + btnH + 4, W - 16, lblH);
        }

        // ---------- top bookmarks bar ----------

        private void RebuildTopBar()
        {
            if (topBarFlow == null || topBarFlow.IsDisposed) return;
            topBarFlow.SuspendLayout();
            var old = new List<Control>();
            foreach (Control c in topBarFlow.Controls) old.Add(c);
            topBarFlow.Controls.Clear();
            foreach (var c in old) c.Dispose();
            topBarChipH = Math.Max(22, TextRenderer.MeasureText("Ag", this.Font).Height + 9);
            topBarFlow.Height = topBarChipH + 4;
            foreach (var b in bookmarks) topBarFlow.Controls.Add(MakeChip(b));
            topBarFlow.ResumeLayout();
            topBarHost.Visible = topBarVisible;
        }

        private Button MakeChip(ExplorerBookmark b)
        {
            string text;
            if (b.Kind == "group") text = b.Name + " \u25BE";
            else if (b.Kind == "folder") text = b.Name;
            else text = "\u203A " + (!string.IsNullOrEmpty(b.Name) ? b.Name : b.Value);
            var chip = new Button
            {
                Text = text,
                AutoSize = true,
                Height = topBarChipH, // follows the font so labels are not cut off
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(2, 2, 2, 1)
            };
            chip.FlatAppearance.BorderSize = 0;
            chip.FlatAppearance.MouseOverBackColor = hoverColor;
            // The command/path of a chip is one hover away, its label stays short.
            tip.SetToolTip(chip, b.Kind == "group" ? b.Name : b.Value);
            var cap = b;
            chip.Click += (s, e) => OnChipClick(cap, chip);
            chip.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) ShowTopBarItemMenu(cap, chip, e.Location); };
            return chip;
        }

        private void OnChipClick(ExplorerBookmark b, Button chip)
        {
            if (b.Kind == "group") { ShowGroupMenu(b, chip); return; }
            if (b.Kind == "folder") { Navigate(b.Value); return; }
            RunInConsole(ExpandBookmarkCommand(b.Value));
        }

        private void ShowGroupMenu(ExplorerBookmark g, Control anchor)
        {
            var m = new ContextMenu();
            var kids = g.Children != null ? g.Children : new List<ExplorerBookmark>();
            if (kids.Count == 0)
            {
                var dead = m.MenuItems.Add(Loc.S("(empty)"));
                dead.Enabled = false;
            }
            foreach (var c in kids)
            {
                var cap = c;
                string label = c.Kind == "cmd"
                    ? (!string.IsNullOrEmpty(c.Name) ? c.Name : c.Value)
                    : (c.Kind == "group" ? c.Name + " \u203A" : c.Name);
                m.MenuItems.Add(label, (s2, e2) =>
                {
                    if (cap.Kind == "cmd") RunInConsole(ExpandBookmarkCommand(cap.Value));
                    else if (cap.Kind == "folder") Navigate(cap.Value);
                    else if (cap.Kind == "group") ShowGroupMenu(cap, anchor);
                });
            }
            m.MenuItems.Add("-");
            m.MenuItems.Add(Loc.S("Rename..."), (s2, e2) => RenameBookmark(g));
            m.MenuItems.Add(Loc.S("Move up", "Вверх"), (s2, e2) => MoveBookmark(g, -1));
            m.MenuItems.Add(Loc.S("Move down", "Вниз"), (s2, e2) => MoveBookmark(g, 1));
            m.MenuItems.Add(Loc.S("Remove"), (s2, e2) => RemoveBookmark(g));
            m.Show(anchor, new Point(0, anchor.Height));
        }

        private void ShowTopBarMenu()
        {
            var m = new ContextMenu();
            m.MenuItems.Add(Loc.S("Add current folder"), (s2, e2) => AddFolderBookmark(currentPath, FolderNameOf(currentPath)));
            m.MenuItems.Add(Loc.S("Add command..."), (s2, e2) => SaveCommandBookmark(""));
            m.MenuItems.Add(Loc.S("Add group..."), (s2, e2) => AddGroup());
            m.Show(topBarHost, new Point(8, topBarHost.Height));
        }

        private void ShowTopBarItemMenu(ExplorerBookmark b, Control anchor, Point loc)
        {
            var m = new ContextMenu();
            if (b.Kind == "cmd")
                m.MenuItems.Add(Loc.S("Edit command...", "Изменить команду..."), (s2, e2) => EditBookmarkCommand(b));
            m.MenuItems.Add(Loc.S("Rename..."), (s2, e2) => RenameBookmark(b));
            m.MenuItems.Add(Loc.S("Move up", "Вверх"), (s2, e2) => MoveBookmark(b, -1));
            m.MenuItems.Add(Loc.S("Move down", "Вниз"), (s2, e2) => MoveBookmark(b, 1));
            m.MenuItems.Add(Loc.S("Remove"), (s2, e2) => RemoveBookmark(b));
            m.MenuItems.Add("-");
            m.MenuItems.Add(Loc.S("Add current folder"), (s2, e2) => AddFolderBookmark(currentPath, FolderNameOf(currentPath)));
            m.MenuItems.Add(Loc.S("Add command..."), (s2, e2) => SaveCommandBookmark(""));
            m.MenuItems.Add(Loc.S("Add group..."), (s2, e2) => AddGroup());
            m.Show(anchor, loc);
        }

        // ---------- splitter ----------

        private void Splitter_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                splitterDragging = true;
                splitGrabDy = e.Y;
                try { splitter.Capture = true; } catch { }
            }
        }

        private void Splitter_MouseMove(object sender, MouseEventArgs e)
        {
            if (!splitterDragging) return;
            Point p = this.PointToClient(splitter.PointToScreen(e.Location));
            int contentH = this.ClientSize.Height - 30;
            if (contentH < 100) return;
            int desiredTop = p.Y - splitGrabDy;
            int consArea = this.ClientSize.Height - (desiredTop + 12);
            double f = consArea / (double)contentH;
            if (f < 0.15) f = 0.15;
            if (f > 0.85) f = 0.85;
            if (Math.Abs(f - consoleFrac) > 0.004)
            {
                consoleFrac = f;
                LayoutAll();
            }
        }

        private void Splitter_MouseUp(object sender, MouseEventArgs e)
        {
            if (splitterDragging)
            {
                splitterDragging = false;
                try { splitter.Capture = false; } catch { }
                SaveWindowState();
            }
        }

        private void Splitter_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                var r = new Rectangle((splitter.Width - 40) / 2, (splitter.Height - 2) / 2, 40, 2);
                using (var b = new SolidBrush(dimColor)) e.Graphics.FillRectangle(b, r);
            }
            catch { }
        }

        // ---------- bookmarks panel width splitter ----------

        private void BmSplitter_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                bmSplitterDragging = true;
                bmGrabDx = e.X;
                try { bmSplitter.Capture = true; } catch { }
            }
        }

        private void BmSplitter_MouseMove(object sender, MouseEventArgs e)
        {
            if (!bmSplitterDragging) return;
            Point p = this.PointToClient(bmSplitter.PointToScreen(e.Location));
            int availW = this.ClientSize.Width - 16;
            if (availW < 300) return;
            int desiredBmW = p.X - bmGrabDx - 8;
            double f = desiredBmW / (double)availW;
            if (f < 0.12) f = 0.12;
            if (f > 0.45) f = 0.45;
            if (Math.Abs(f - bmFrac) > 0.003)
            {
                bmFrac = f;
                LayoutAll();
            }
        }

        private void BmSplitter_MouseUp(object sender, MouseEventArgs e)
        {
            if (bmSplitterDragging)
            {
                bmSplitterDragging = false;
                try { bmSplitter.Capture = false; } catch { }
                SaveWindowState();
            }
        }

        private void BmSplitter_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                var r = new Rectangle((bmSplitter.Width - 2) / 2, (bmSplitter.Height - 40) / 2, 2, 40);
                using (var b = new SolidBrush(dimColor)) e.Graphics.FillRectangle(b, r);
            }
            catch { }
        }

        private Font ConsoleFont()
        {
            float size = 8.5f;
            try { size = Math.Max(6f, Math.Min(28f, settings.ConsoleFontSizeX10 / 10f)); } catch { }
            try { return new Font("Consolas", size); }
            catch { }
            try { return new Font("Courier New", size); }
            catch { }
            return new Font(FontFamily.GenericMonospace, size);
        }

        // Ctrl+mouse wheel over the console changes its font size; the size is
        // remembered in the settings (the built-in RichTextBox zoom is suppressed).
        private void ConsoleOut_MouseWheel(object sender, MouseEventArgs e)
        {
            try
            {
                if ((Control.ModifierKeys & Keys.Control) == 0) return;
                var he = e as HandledMouseEventArgs;
                if (he != null) he.Handled = true;
                float cur = Math.Max(6f, Math.Min(28f, settings.ConsoleFontSizeX10 / 10f));
                float ns = Math.Max(6f, Math.Min(28f, cur + (e.Delta > 0 ? 1f : -1f)));
                if (Math.Abs(ns - cur) < 0.05f) return;
                settings.ConsoleFontSizeX10 = (int)Math.Round(ns * 10);
                consoleOut.Font = ConsoleFont();
                SaveWindowState();
            }
            catch { }
        }

        private Button FlatButton(string text, int x, int y, int w, int h)
        {
            var b = new Button
            {
                Text = text,
                Left = x,
                Top = y,
                Width = w,
                Height = h,
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = hoverColor;
            b.FlatAppearance.MouseDownBackColor = bgColor;
            return b;
        }

        private Button NavButton(string text, int x)
        {
            var b = new Button
            {
                Text = text,
                Left = x,
                Top = 32,
                Width = 28,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = hoverColor;
            b.FlatAppearance.MouseDownBackColor = bgColor;
            return b;
        }

        // ---------- navigation ----------

        // Opens `path` in a new tab of this explorer window and activates the
        // window. The very first open reuses the constructor's initial tab when
        // it still shows exactly that folder (one tile click = one tab); every
        // later open adds a tab instead of overwriting the current view.
        public void OpenInTab(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (this.InvokeRequired)
            {
                try { this.BeginInvoke((Action)delegate { OpenInTab(path); }); } catch { }
                return;
            }
            try { if (!Directory.Exists(path)) return; } catch { return; }
            bool firstOpen = tabs.Count == 1 && activeTab != null && activeTab.History != null
                             && activeTab.History.Count <= 1 && Eq(activeTab.Path, path);
            if (firstOpen) Navigate(path);
            else NewTab(path);
            if (!this.Visible) this.Show();
            else this.Activate();
        }

        // OpenInTab + select a child entry — used by the panel-search context
        // menu ("open the original folder" for a search result).
        public void OpenInTabSelect(string folder, string selectName)
        {
            OpenInTab(folder);
            SelectEntryByName(selectName);
        }

        private void SaveActiveTabState()
        {
            if (activeTab == null) return;
            activeTab.Path = currentPath;
            activeTab.History = history;
            activeTab.HistoryIndex = historyIndex;
        }

        // Creates a tab and navigates it to `path`; the new tab becomes active.
        private void NewTab(string path)
        {
            SaveActiveTabState();
            var t = new ExplorerTab();
            tabs.Add(t);
            activeTab = t;
            history = t.History;
            historyIndex = t.HistoryIndex;
            currentPath = "";
            RebuildTabStrip();
            if (!string.IsNullOrEmpty(path)) Navigate(path);
            else { UpdateTitle(); UpdateCrumbs(); LoadDir(); UpdateNavButtons(); }
            UpdateTabButtons();
        }

        private void ActivateTab(ExplorerTab t)
        {
            if (t == null || ReferenceEquals(t, activeTab)) return;
            SaveActiveTabState();
            activeTab = t;
            history = t.History;
            historyIndex = t.HistoryIndex;
            if (historyIndex >= 0 && historyIndex < history.Count) GoToPath(history[historyIndex]);
            else if (!string.IsNullOrEmpty(t.Path)) GoToPath(t.Path);
            else { currentPath = ""; UpdateTitle(); UpdateCrumbs(); LoadDir(); UpdateNavButtons(); }
            UpdateTabButtons();
        }

        private void CloseTab(ExplorerTab t)
        {
            if (t == null) return;
            int i = tabs.IndexOf(t);
            if (i < 0) return;
            tabs.RemoveAt(i);
            if (tabs.Count == 0)
            {
                this.Close(); // the last tab closes the window
                return;
            }
            if (ReferenceEquals(t, activeTab))
            {
                activeTab = null;
                ActivateTab(tabs[Math.Min(i, tabs.Count - 1)]);
            }
            RebuildTabStrip();
        }

        private void RebuildTabStrip()
        {
            if (tabFlow == null) return;
            tabFlow.SuspendLayout();
            var keep = btnTabNew;
            var old = new List<Control>();
            foreach (Control c in tabFlow.Controls) old.Add(c);
            tabFlow.Controls.Clear();
            foreach (var c in old)
            {
                if (ReferenceEquals(c, keep)) continue;
                var b = c as Button;
                if (b != null) tabByButton.Remove(b);
                c.Dispose();
            }
            int btnH = Math.Max(22, TextRenderer.MeasureText("Ag", this.Font).Height + 8);
            if (keep != null) keep.Height = btnH;
            foreach (var t in tabs)
            {
                string cap = TabCaption(t);
                int w = Math.Min(170, Math.Max(60, TextRenderer.MeasureText(cap, this.Font).Width + 26));
                var btn = new Button
                {
                    Text = cap,
                    Width = w,
                    Height = btnH,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ReferenceEquals(t, activeTab) ? hoverColor : panelColor,
                    ForeColor = textColor,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(2, 1, 2, 1),
                    Font = ReferenceEquals(t, activeTab) ? boldFont : this.Font,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = hoverColor;
                tabByButton[btn] = t;
                var tabRef = t;
                btn.Click += (s, e) => ActivateTab(tabRef);
                btn.MouseUp += (s, e) =>
                {
                    if (e.Button == MouseButtons.Middle) CloseTab(tabRef);
                    else if (e.Button == MouseButtons.Right) ShowTabMenu(tabRef, btn, e.Location);
                };
                tip.SetToolTip(btn, t.Path);
                tabFlow.Controls.Add(btn);
            }
            if (keep != null) tabFlow.Controls.Add(keep);
            tabFlow.ResumeLayout();
        }

        // Lightweight refresh of tab captions/colors (no rebuild) — on navigation.
        private void UpdateTabButtons()
        {
            if (tabFlow == null) return;
            foreach (var kv in tabByButton)
            {
                var btn = kv.Key;
                var t = kv.Value;
                if (btn == null || btn.IsDisposed) continue;
                string cap = TabCaption(t);
                if (btn.Text != cap) btn.Text = cap;
                bool act = ReferenceEquals(t, activeTab);
                btn.BackColor = act ? hoverColor : panelColor;
                btn.Font = act ? boldFont : this.Font;
                tip.SetToolTip(btn, t.Path);
            }
        }

        private string TabCaption(ExplorerTab t)
        {
            string p = t != null ? t.Path : "";
            if (string.IsNullOrEmpty(p)) return Loc.S("New tab", "Новая вкладка");
            string name;
            try { name = Path.GetFileName(p.TrimEnd('\\')); } catch { name = p; }
            if (string.IsNullOrEmpty(name)) name = p;
            if (name.Length > 24) name = name.Substring(0, 23) + "…";
            return name;
        }

        private void ShowTabMenu(ExplorerTab t, Control anchor, Point loc)
        {
            var m = new ContextMenu();
            m.MenuItems.Add(Loc.S("Close tab", "Закрыть вкладку"), (s2, e2) => CloseTab(t));
            m.Show(anchor, loc);
        }

        private void Navigate(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string full;
            try { full = Path.GetFullPath(path); } catch { return; }
            if (full.Length > 3 && full.EndsWith("\\")) full = full.Substring(0, full.Length - 1);
            if (!Directory.Exists(full)) return;
            if (historyIndex < 0 || !Eq(history[historyIndex], full))
            {
                if (historyIndex < history.Count - 1)
                    history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
                history.Add(full);
                historyIndex = history.Count - 1;
            }
            GoToPath(full);
        }

        private void GoToPath(string path)
        {
            currentPath = path;
            if (activeTab != null) activeTab.Path = path;
            UpdateTitle();
            UpdateTabButtons();
            UpdateCrumbs();
            LoadDir();
            UpdateNavButtons();
            SyncShellDir();
            HidePathEdit();
        }

        private void GoBack()
        {
            if (historyIndex > 0)
            {
                historyIndex--;
                GoToPath(history[historyIndex]);
            }
        }

        private void GoForward()
        {
            if (historyIndex >= 0 && historyIndex < history.Count - 1)
            {
                historyIndex++;
                GoToPath(history[historyIndex]);
            }
        }

        private void GoUp()
        {
            if (currentPath.Length <= 3) return;
            string parent;
            try { parent = Path.GetDirectoryName(currentPath.TrimEnd('\\')); } catch { return; }
            if (string.IsNullOrEmpty(parent)) return;
            Navigate(parent);
        }

        private void UpdateNavButtons()
        {
            btnBack.Enabled = historyIndex > 0;
            btnFwd.Enabled = historyIndex >= 0 && historyIndex < history.Count - 1;
            string parent = null;
            if (currentPath.Length > 3)
            {
                try { parent = Path.GetDirectoryName(currentPath.TrimEnd('\\')); } catch { }
            }
            btnUp.Enabled = !string.IsNullOrEmpty(parent);
        }

        private void UpdateTitle()
        {
            string name = currentPath;
            if (currentPath.Length > 3)
            {
                try { name = Path.GetFileName(currentPath.TrimEnd('\\')); } catch { }
            }
            if (string.IsNullOrEmpty(name)) name = currentPath;
            titleLbl.Text = "Mini Explorer — " + name;
            tip.SetToolTip(titleLbl, currentPath);
        }

        private static string Norm(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            p = p.TrimEnd('\\');
            if (p.Length == 2 && p[1] == ':') p = p + "\\";
            return p;
        }

        private static bool Eq(string a, string b)
        {
            return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
        }

        // ---------- breadcrumb path bar ----------

        private void UpdateCrumbs()
        {
            if (crumbFlow == null) return;
            crumbFlow.SuspendLayout();
            var old = new List<Control>();
            foreach (Control c in crumbFlow.Controls) old.Add(c);
            crumbFlow.Controls.Clear();
            foreach (var c in old) c.Dispose();

            string root = null;
            try { root = Path.GetPathRoot(currentPath); } catch { }
            if (string.IsNullOrEmpty(root)) root = "C:\\";

            var rb = CrumbButton(root.TrimEnd('\\'));
            string rootCap = root;
            rb.Click += (s, e) => Navigate(rootCap);
            crumbFlow.Controls.Add(rb);

            string rest = currentPath.Length > root.Length ? currentPath.Substring(root.Length) : "";
            rest = rest.Trim('\\');
            if (rest.Length > 0)
            {
                string acc = root;
                string[] parts = rest.Split('\\');
                foreach (string part in parts)
                {
                    if (part.Length == 0) continue;
                    acc = acc.EndsWith("\\") ? acc + part : acc + "\\" + part;
                    var sep = new Label { Text = "›", AutoSize = true, ForeColor = dimColor, Margin = new Padding(1, 5, 1, 0) };
                    crumbFlow.Controls.Add(sep);
                    var b = CrumbButton(part);
                    string cap = acc;
                    b.Click += (s, e) => Navigate(cap);
                    crumbFlow.Controls.Add(b);
                }
            }
            if (crumbFlow.Controls.Count > 0)
            {
                var lastBtn = crumbFlow.Controls[crumbFlow.Controls.Count - 1] as Button;
                if (lastBtn != null) lastBtn.ForeColor = accentColor;
            }
            crumbFlow.ResumeLayout();
        }

        private Button CrumbButton(string text)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                Height = Math.Max(22, TextRenderer.MeasureText("Ag", this.Font).Height + 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Padding = new Padding(6, 0, 6, 0),
                Margin = new Padding(0, 2, 0, 2)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = hoverColor;
            b.FlatAppearance.MouseDownBackColor = panelColor;
            return b;
        }

        private void BeginPathEdit()
        {
            if (pathEdit == null || currentPath.Length == 0) return;
            crumbHost.Visible = false;
            pathEdit.Text = currentPath;
            pathEdit.Visible = true;
            pathEdit.Focus();
            pathEdit.SelectAll();
        }

        private void HidePathEdit()
        {
            if (pathEdit == null) return;
            pathEdit.Visible = false;
            crumbHost.Visible = true;
        }

        private void CommitPathEdit()
        {
            string t = pathEdit.Text != null ? pathEdit.Text.Trim() : "";
            HidePathEdit();
            if (t.Length == 0) return;
            try { t = Environment.ExpandEnvironmentVariables(t); } catch { }
            try
            {
                if (File.Exists(t))
                {
                    string dir = Path.GetDirectoryName(t);
                    string fn = Path.GetFileName(t);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Navigate(dir);
                        SelectEntryByName(fn);
                        return;
                    }
                }
            }
            catch { }
            Navigate(t);
        }

        private void PathEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitPathEdit();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                HidePathEdit();
                e.SuppressKeyPress = true;
            }
        }

        private void SelectEntryByName(string name)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].IsDir && string.Equals(entries[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    fileList.SelectedIndex = i;
                    try { fileList.TopIndex = Math.Max(0, i - 5); } catch { }
                    break;
                }
            }
        }

        private void Mini_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F4 || (e.Control && e.KeyCode == Keys.L))
            {
                BeginPathEdit();
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Escape)
            {
                if (searchMode)
                {
                    try { searchBox.Clear(); } catch { }
                    ExitSearch();
                    e.SuppressKeyPress = true;
                    return;
                }
                if (pathEdit.Visible)
                {
                    HidePathEdit();
                    e.SuppressKeyPress = true;
                    return;
                }
                if (ActiveControl != consoleIn) this.Close();
                return;
            }
            if (e.KeyCode == Keys.F5)
            {
                if (ActiveControl == consoleIn) return;
                LoadDir();
                e.SuppressKeyPress = true;
                return;
            }
            if (e.Alt && e.KeyCode == Keys.Left)
            {
                GoBack();
                e.SuppressKeyPress = true;
                return;
            }
            if (e.Alt && e.KeyCode == Keys.Right)
            {
                GoForward();
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Back && ActiveControl != consoleIn && ActiveControl != pathEdit && !pathEdit.Visible)
            {
                GoUp();
                e.SuppressKeyPress = true;
            }
        }

        // ---------- file list ----------

        private void LoadDir()
        {
            if (searchMode)
            {
                searchMode = false;
                searchItems = null;
                try { if (searchBox != null && searchBox.TextLength > 0) searchBox.Clear(); } catch { }
            }
            entries.Clear();
            int dirs = 0, files = 0;
            bool truncated = false, denied = false;
            try
            {
                var di = new DirectoryInfo(currentPath);
                try
                {
                    foreach (var d in di.GetDirectories())
                    {
                        if (entries.Count >= 800) { truncated = true; break; }
                        entries.Add(new DirEntry { Name = d.Name, FullPath = d.FullName, IsDir = true, Size = 0 });
                        dirs++;
                    }
                }
                catch (UnauthorizedAccessException) { denied = true; }
                catch (DirectoryNotFoundException) { denied = true; }
                try
                {
                    foreach (var f in di.GetFiles())
                    {
                        if (entries.Count >= 800) { truncated = true; break; }
                        long len = 0;
                        try { len = f.Length; } catch { }
                        entries.Add(new DirEntry { Name = f.Name, FullPath = f.FullName, IsDir = false, Size = len });
                        files++;
                    }
                }
                catch (UnauthorizedAccessException) { denied = true; }
                catch (DirectoryNotFoundException) { denied = true; }
            }
            catch { denied = true; }

            entries.Sort(delegate(DirEntry a, DirEntry b)
            {
                if (a.IsDir != b.IsDir) return a.IsDir ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            fileList.BeginUpdate();
            fileList.Items.Clear();
            foreach (var en in entries) fileList.Items.Add(en.Name);
            fileList.EndUpdate();
            fileList.ClearSelected();

            string stat = (Loc.IsRu ? "папок: " : "folders: ") + dirs + " · " + (Loc.IsRu ? "файлов: " : "files: ") + files;
            if (truncated) stat += Loc.IsRu ? " · первые 800" : " · first 800";
            if (denied) stat += Loc.IsRu ? " · нет доступа" : " · access denied";
            statusLbl.Text = stat;
            hoverFile = -1;
            fileList.Invalidate();
        }

        private void FileList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                OpenSelectedEntry();
                e.SuppressKeyPress = true;
            }
        }

        private void OpenSelectedEntry()
        {
            int i = fileList.SelectedIndex;
            if (searchMode)
            {
                if (i >= 0 && searchItems != null && i < searchItems.Count) OpenSearchResult(i);
                return;
            }
            if (i < 0 || i >= entries.Count) return;
            OpenEntry(entries[i]);
        }

        private void OpenEntry(DirEntry en)
        {
            if (en.IsDir) Navigate(en.FullPath);
            else MainForm.LaunchItem(en.FullPath);
        }

        private void FileList_MouseMove(object sender, MouseEventArgs e)
        {
            int i = fileList.IndexFromPoint(e.Location);
            if (i != hoverFile) { hoverFile = i; fileList.Invalidate(); }
        }

        private void FileList_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            if (searchMode) { ShowSearchMenu(e); return; }
            int i = fileList.IndexFromPoint(e.Location);
            var m = new ContextMenu();
            if (i >= 0 && i < entries.Count && i < fileList.Items.Count)
            {
                var en = entries[i];
                fileList.SelectedIndex = i;
                m.MenuItems.Add(Loc.S("Open"), (s2, e2) => OpenEntry(en));
                if (en.IsDir)
                {
                    m.MenuItems.Add(Loc.S("Add to bookmarks"), (s2, e2) => AddFolderBookmark(en.FullPath, en.Name));
                    m.MenuItems.Add(Loc.S("Open in Explorer"), (s2, e2) => OpenInExplorer(en.FullPath, false));
                }
                else
                {
                    m.MenuItems.Add(Loc.S("Show in Explorer"), (s2, e2) => OpenInExplorer(en.FullPath, true));
                    if (IsTarExtractable(en.Name))
                        m.MenuItems.Add(Loc.S("Extract here (tar)", "Распаковать здесь (tar)"), (s2, e2) => ExtractArchiveHere(en));
                }
                m.MenuItems.Add(Loc.S("Copy path"), (s2, e2) => CopyText(en.FullPath));
            }
            else
            {
                m.MenuItems.Add(Loc.S("Refresh"), (s2, e2) => LoadDir());
                m.MenuItems.Add(Loc.S("Copy folder path"), (s2, e2) => CopyText(currentPath));
                m.MenuItems.Add(Loc.S("Open in Explorer"), (s2, e2) => OpenInExplorer(currentPath, false));
                m.MenuItems.Add(Loc.S("Add current folder to bookmarks"), (s2, e2) => AddFolderBookmark(currentPath, FolderNameOf(currentPath)));
                m.MenuItems.Add(Loc.S("Open console window here"), (s2, e2) => OpenRealConsole());
            }
            m.Show(fileList, e.Location);
        }

        private static void CopyText(string t)
        {
            try { if (!string.IsNullOrEmpty(t)) Clipboard.SetText(t); } catch { }
        }

        // Archives the bundled bsdtar can unpack (tar, gz, zip, ...).
        private static bool IsTarExtractable(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name)) return false;
                string n = name.ToLowerInvariant();
                return n.EndsWith(".tar") || n.EndsWith(".tar.gz") || n.EndsWith(".tar.bz2") ||
                       n.EndsWith(".tar.xz") || n.EndsWith(".tgz") || n.EndsWith(".tbz2") ||
                       n.EndsWith(".txz") || n.EndsWith(".zip") || n.EndsWith(".gz") || n.EndsWith(".xz");
            }
            catch { return false; }
        }

        // "tar открыть": unpack the archive into a subfolder next to it through the
        // embedded console (output stays visible), then refresh the listing.
        private void ExtractArchiveHere(DirEntry en)
        {
            try
            {
                string dir = Path.GetFileNameWithoutExtension(en.Name);
                if (string.IsNullOrEmpty(dir)) dir = "extracted";
                foreach (char c in Path.GetInvalidFileNameChars()) dir = dir.Replace(c, '_');
                RunInConsole("mkdir \"" + dir + "\" 2>nul & tar -xf \"" + en.FullPath + "\" -C \"" + dir + "\"");
                var t = new System.Windows.Forms.Timer { Interval = 1500 };
                t.Tick += delegate
                {
                    try { t.Stop(); t.Dispose(); LoadDir(); }
                    catch { }
                };
                t.Start();
            }
            catch (Exception ex)
            {
                AppendConsole(Loc.S("Extract failed: ", "Не удалось распаковать: ") + ex.Message, Color.FromArgb(214, 106, 106));
            }
        }

        private void OpenInExplorer(string path, bool select)
        {
            try
            {
                // The configured folder manager is honored for plain opens of
                // directories ("Open in Explorer" on a folder); "select" keeps
                // the system Explorer - revealing one file is an Explorer
                // feature, and so is opening a bare file.
                if (!select && System.IO.Directory.Exists(path))
                {
                    string fmExe, fmTemplate;
                    if (MainForm.TryGetFolderOpenCommand(out fmExe, out fmTemplate))
                    {
                        Process.Start(MainForm.BuildShellStart(fmExe, MainForm.FolderOpenArgs(fmTemplate, fmExe, path), path));
                        return;
                    }
                }
                if (select) Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch { }
        }

        private static string FolderNameOf(string path)
        {
            try
            {
                string n = Path.GetFileName(path.TrimEnd('\\'));
                if (!string.IsNullOrEmpty(n)) return n;
            }
            catch { }
            return path;
        }

        private void FileList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (searchMode)
            {
                DrawSearchItem(e);
                return;
            }
            if (e.Index < 0 || e.Index >= entries.Count) return;
            var en = entries[e.Index];
            var g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            bool hov = e.Index == hoverFile;
            using (var back = new SolidBrush((sel || hov) ? hoverColor : listColor))
                g.FillRectangle(back, e.Bounds);

            var ic = GetEntryIcon(en);
            if (ic != null) g.DrawImage(ic, new Rectangle(e.Bounds.Left + 6, e.Bounds.Top + 4, 16, 16));

            int ty = e.Bounds.Top + 5;
            TextRenderer.DrawText(g, en.Name, this.Font, new Point(e.Bounds.Left + 28, ty), textColor);

            if (!en.IsDir)
            {
                string s = FormatSize(en.Size);
                var sz = TextRenderer.MeasureText(s, this.Font);
                var nameSz = TextRenderer.MeasureText(en.Name, this.Font);
                int sx = e.Bounds.Right - sz.Width - 10;
                if (sx > e.Bounds.Left + 28 + nameSz.Width + 24)
                    TextRenderer.DrawText(g, s, this.Font, new Point(sx, ty), dimColor);
            }
        }

        private Bitmap GetEntryIcon(DirEntry en)
        {
            string key;
            if (en.IsDir) key = "d:" + en.FullPath.ToLowerInvariant();
            else
            {
                string ext = "";
                try { ext = Path.GetExtension(en.FullPath); } catch { }
                key = ext.Length > 0 ? "f:" + ext.ToLowerInvariant() : "f:" + en.FullPath.ToLowerInvariant();
            }
            return GetCachedIcon(key, en.FullPath);
        }

        private Bitmap GetPathIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return GetCachedIcon("d:" + path.ToLowerInvariant(), path);
        }

        private Bitmap GetCachedIcon(string key, string path)
        {
            if (iconCache.ContainsKey(key)) return iconCache[key];
            // A network path can block the shell for the SMB timeout (unreachable
            // share) - never extract it on the UI thread. The icon arrives in the
            // background, lands in the cache and the list repaints.
            if (MainForm.IsSlowIconPath(path))
            {
                lock (pendingIcons)
                {
                    if (pendingIcons.Contains(key)) return null;
                    pendingIcons.Add(key);
                }
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    Bitmap b16 = null;
                    try
                    {
                        Image big = IconExtractor.GetIcon(path, false);
                        if (big != null)
                        {
                            b16 = new Bitmap(16, 16);
                            using (var g = Graphics.FromImage(b16))
                            {
                                g.Clear(Color.Transparent);
                                IconExtractor.DrawFit(g, big, new Rectangle(0, 0, 16, 16));
                            }
                            big.Dispose();
                        }
                    }
                    catch { }
                    try
                    {
                        if (this.IsDisposed) { if (b16 != null) b16.Dispose(); return; }
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            lock (pendingIcons) { pendingIcons.Remove(key); }
                            if (this.IsDisposed) { if (b16 != null) b16.Dispose(); return; }
                            try
                            {
                                Bitmap old;
                                if (iconCache.TryGetValue(key, out old) && old != null) old.Dispose();
                                iconCache[key] = b16;
                                fileList.Invalidate();
                            }
                            catch { if (b16 != null) try { b16.Dispose(); } catch { } }
                        });
                    }
                    catch
                    {
                        lock (pendingIcons) { pendingIcons.Remove(key); }
                        if (b16 != null) try { b16.Dispose(); } catch { }
                    }
                });
                return null;
            }
            Bitmap b16s = null;
            try
            {
                Image big = IconExtractor.GetIcon(path, false);
                if (big != null)
                {
                    b16s = new Bitmap(16, 16);
                    using (var g = Graphics.FromImage(b16s))
                    {
                        g.Clear(Color.Transparent);
                        IconExtractor.DrawFit(g, big, new Rectangle(0, 0, 16, 16));
                    }
                    big.Dispose();
                }
            }
            catch { }
            iconCache[key] = b16s;
            return b16s;
        }

        // ---------- search ----------

        private void FileList_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!SearchEnabled || searchMode) return; // search disabled: the list keeps native key handling
            if (char.IsControl(e.KeyChar)) return;
            e.Handled = true;
            BeginSearchTyping(e.KeyChar);
        }

        private void BeginSearchTyping(char ch)
        {
            if (searchBox == null) return;
            try { searchBox.Focus(); } catch { }
            searchBox.Text = searchBox.Text + ch;
            searchBox.SelectionStart = searchBox.Text.Length;
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                try { searchBox.Clear(); } catch { }
                try { fileList.Focus(); } catch { }
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (fileList.Items.Count > 0 && fileList.SelectedIndex < 0) fileList.SelectedIndex = 0;
                OpenSelectedEntry();
            }
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true;
                if (fileList.Items.Count == 0) return;
                int next = fileList.SelectedIndex + (e.KeyCode == Keys.Down ? 1 : -1);
                if (next < 0) next = 0;
                if (next >= fileList.Items.Count) next = fileList.Items.Count - 1;
                fileList.SelectedIndex = next;
                try { fileList.Focus(); } catch { }
            }
        }

        private void SetSearchScope(string scope)
        {
            searchScope = scope;
            UpdateScopeButtons();
            if (searchBox != null && searchBox.TextLength > 0) RunSearch(false);
        }

        private void UpdateScopeButtons()
        {
            try
            {
                bool all = searchScope == "all";
                btnScope.Text = all ? Loc.S("All", "Везде") : Loc.S("Folder", "Папка");
                btnScope.BackColor = all ? accentColor : panelColor;
                btnScope.ForeColor = all ? Color.White : textColor;
            }
            catch { }
        }

        private void RunSearch(bool typingUpdate)
        {
            try
            {
                if (!SearchEnabled || searchBox == null || searchBox.IsDisposed) return; // disabled: never build/scan the file index
                if (searchBox == null || searchBox.IsDisposed) return;
                string q = searchBox.Text.Trim();
                if (q.Length == 0) { ExitSearch(); return; }
                bool building;
                int scanned;
                List<SearchItem> src;
                if (searchScope == "all")
                    src = SearchCore.GetIndex(SearchCore.AllKey, out building, out scanned);
                else
                    src = SearchCore.GetIndex(currentPath, out building, out scanned);
                var res = SearchCore.Run(q, src, 400);
                SearchCoreMini.LastQueryLower = q.ToLowerInvariant();
                SearchCoreMini.ApplyMiniSearchToggles(res, settings.SearchInMeta, settings.SearchInPaths, settings.SearchInDesc);
                searchItems = res;
                searchVariants = SearchCore.Variants(q);
                searchMode = true;
                fileList.BeginUpdate();
                fileList.Items.Clear();
                foreach (var r in res) fileList.Items.Add(r.Name);
                fileList.EndUpdate();
                fileList.ClearSelected();
                if (fileList.Items.Count > 0) { try { fileList.TopIndex = 0; } catch { } }
                statusLbl.Text = (Loc.IsRu ? "найдено: " : "found: ") + res.Count +
                    (building ? (Loc.IsRu ? " · индексация: " : " · indexing: ") + scanned : "");
                fileList.Invalidate();
                if (building)
                {
                    searchTimer.Stop();
                    searchTimer.Interval = 700;
                    searchTimer.Start();
                }
            }
            catch { }
        }

        private void ExitSearch()
        {
            bool was = searchMode;
            searchMode = false;
            searchItems = null;
            searchVariants = new List<string>();
            try { searchTimer.Stop(); searchTimer.Interval = 250; } catch { }
            if (was) LoadDir();
        }

        private void OpenSearchResult(int i)
        {
            if (searchItems == null || i < 0 || i >= searchItems.Count) return;
            var it = searchItems[i];
            if (it.IsDir)
            {
                try { searchBox.Clear(); } catch { }
                ExitSearch();
                Navigate(it.FullPath);
            }
            else
            {
                MainForm.LaunchItem(it.FullPath);
            }
        }

        private void ShowSearchMenu(MouseEventArgs e)
        {
            int i = fileList.IndexFromPoint(e.Location);
            if (i < 0 || searchItems == null || i >= searchItems.Count) return;
            fileList.SelectedIndex = i;
            var it = searchItems[i];
            var m = new ContextMenu();
            m.MenuItems.Add(Loc.S("Open"), (s2, e2) => OpenSearchResult(i));
            m.MenuItems.Add(Loc.S("Show in Explorer"), (s2, e2) => OpenInExplorer(it.FullPath, !it.IsDir));
            m.MenuItems.Add(Loc.S("Copy path"), (s2, e2) => CopyText(it.FullPath));
            m.Show(fileList, e.Location);
        }

        private void DrawSearchItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || searchItems == null || e.Index >= searchItems.Count) return;
            var it = searchItems[e.Index];
            var g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            bool hov = e.Index == hoverFile;
            using (var back = new SolidBrush((sel || hov) ? hoverColor : listColor))
                g.FillRectangle(back, e.Bounds);
            try
            {
                string key = (it.IsDir ? "d:" : "f:") + it.FullPath.ToLowerInvariant();
                var ic = GetCachedIcon(key, it.FullPath);
                if (ic != null) g.DrawImage(ic, new Rectangle(e.Bounds.Left + 6, e.Bounds.Top + 4, 16, 16));
            }
            catch { }
            int ty = e.Bounds.Top + 5;

            // Name with the matched characters highlighted.
            int hStart, hLen;
            bool nameHl = UiText.FindHighlight((it.Name ?? "").ToLowerInvariant(), searchVariants, out hStart, out hLen);
            UiText.DrawHighlighted(g, it.Name, nameHl ? hStart : -1, nameHl ? hLen : 0,
                this.Font, new Point(e.Bounds.Left + 28, ty), textColor, accentColor, settings.IsLightTheme);

            string sub = it.Dir;
            if (!string.IsNullOrEmpty(sub))
            {
                var nameSz = TextRenderer.MeasureText(it.Name, this.Font);
                int maxSubW = (e.Bounds.Width / 2);
                string subDisplay = UiText.FitTail(sub, this.Font, maxSubW);
                int subW = TextRenderer.MeasureText(subDisplay, this.Font).Width;
                int sx = e.Bounds.Right - subW - 10;
                if (sx > e.Bounds.Left + 28 + nameSz.Width + 24)
                {
                    int pStart, pLen;
                    bool subHl = UiText.FindHighlight(sub.ToLowerInvariant(), searchVariants, out pStart, out pLen);
                    int cut = sub.Length - (subDisplay.Length - 1);
                    if (subHl && pStart >= cut)
                        UiText.DrawHighlighted(g, subDisplay, pStart - cut, pLen, this.Font, new Point(sx, ty), dimColor, accentColor, settings.IsLightTheme);
                    else
                        TextRenderer.DrawText(g, subDisplay, this.Font, new Point(sx, ty), dimColor);
                }
            }
        }

        private static string FormatSize(long b)
        {
            if (b < 1024) return b.ToString() + " B";
            double kb = b / 1024.0;
            if (kb < 1024) return Math.Round(kb).ToString() + " KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return mb.ToString("0.#") + " MB";
            return (mb / 1024.0).ToString("0.#") + " GB";
        }

        // ---------- bookmarks ----------

        private void ExpandGroupRecursive(ExplorerBookmark b)
        {
            if (b == null || b.Kind != "group") return;
            expandedGroups.Add(b.Name);
            if (b.Children != null)
                foreach (var c in b.Children) ExpandGroupRecursive(c);
        }

        private void RebuildBookmarks()
        {
            bmRows.Clear();
            if (bookmarks != null)
                foreach (var b in bookmarks) AddRows(b, 0);
            bookmarksList.BeginUpdate();
            bookmarksList.Items.Clear();
            foreach (var r in bmRows) bookmarksList.Items.Add(r.Bm.Name);
            bookmarksList.EndUpdate();
            bookmarksList.Invalidate();
            RebuildTopBar();
        }

        private void AddRows(ExplorerBookmark b, int depth)
        {
            if (b == null) return;
            bmRows.Add(new Row { Bm = b, Depth = depth });
            if (b.Kind == "group" && b.Children != null && expandedGroups.Contains(b.Name))
                foreach (var c in b.Children) AddRows(c, depth + 1);
        }

        private void BookmarksList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= bmRows.Count) return;
            var row = bmRows[e.Index];
            var b = row.Bm;
            var g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            bool hov = e.Index == hoverBm;
            using (var back = new SolidBrush((sel || hov) ? hoverColor : listColor))
                g.FillRectangle(back, e.Bounds);

            int x = e.Bounds.Left + 6 + row.Depth * 14;
            int ty = e.Bounds.Top + 4;
            if (b.Kind == "group")
            {
                bool exp = expandedGroups.Contains(b.Name);
                TextRenderer.DrawText(g, exp ? "▾" : "▸", this.Font, new Point(x, ty), accentColor);
                int cnt = b.Children != null ? b.Children.Count : 0;
                TextRenderer.DrawText(g, b.Name + " (" + cnt + ")", boldFont, new Point(x + 16, ty), textColor);
            }
            else if (b.Kind == "folder")
            {
                var ic = GetPathIcon(b.Value);
                if (ic != null) g.DrawImage(ic, new Rectangle(x, e.Bounds.Top + 3, 16, 16));
                TextRenderer.DrawText(g, b.Name, this.Font, new Point(x + 22, ty), textColor);
            }
            else
            {
                TextRenderer.DrawText(g, "›", boldFont, new Point(x, ty), accentColor);
                // The label shows the bookmark's name; the command stays in the tooltip.
                string label = !string.IsNullOrEmpty(b.Name) ? b.Name : b.Value;
                TextRenderer.DrawText(g, label, this.Font, new Point(x + 14, ty), textColor);
            }

            // Insertion line while drag-reordering.
            if (bmDragging && e.Index == bmDragTarget && e.Index != bmDragIndex)
            {
                int ly = bmDragDropAbove ? e.Bounds.Top : e.Bounds.Bottom - 2;
                using (var pen = new Pen(accentColor, 2f))
                    g.DrawLine(pen, e.Bounds.Left + 2, ly, e.Bounds.Right - 2, ly);
            }
        }

        private void BookmarksList_Click(object sender, EventArgs e)
        {
            if (bmSuppressClick) { bmSuppressClick = false; return; }
            int i = bookmarksList.SelectedIndex;
            if (i < 0 || i >= bmRows.Count) return;
            var b = bmRows[i].Bm;
            if (b.Kind == "group")
            {
                if (expandedGroups.Contains(b.Name)) expandedGroups.Remove(b.Name);
                else expandedGroups.Add(b.Name);
                RebuildBookmarks();
            }
            else if (b.Kind == "folder")
            {
                Navigate(b.Value);
            }
            else
            {
                RunInConsole(ExpandBookmarkCommand(b.Value));
            }
        }

        // %1 in a command bookmark stands for the folder currently open in the
        // mini explorer, so one bookmark (e.g. `wt -d "%1"`) opens any program
        // "here". Without the token the command still starts in the folder: the
        // console has already cd-ed into currentPath before running it.
        internal static string ExpandBookmarkCommand(string cmd, string currentPath)
        {
            if (string.IsNullOrEmpty(cmd) || cmd.IndexOf("%1", StringComparison.Ordinal) < 0) return cmd;
            string dir = string.IsNullOrEmpty(currentPath) ? "." : currentPath;
            return cmd.Replace("%1", dir);
        }

        private string ExpandBookmarkCommand(string cmd)
        {
            return ExpandBookmarkCommand(cmd, currentPath);
        }

        private void BookmarksList_MouseMove(object sender, MouseEventArgs e)
        {
            // Drag-and-drop arming: only after the cursor leaves the system drag
            // size, so an ordinary click never turns into a reorder.
            if ((Control.MouseButtons & MouseButtons.Left) != 0 && bmDragIndex >= 0 && bmDragIndex < bmRows.Count)
            {
                if (!bmDragging)
                {
                    Size ds = SystemInformation.DragSize;
                    if (Math.Abs(e.X - bmDragStart.X) > ds.Width || Math.Abs(e.Y - bmDragStart.Y) > ds.Height)
                    {
                        bmDragging = true;
                        bmDragTarget = bmDragIndex;
                        bmDragDropAbove = true;
                        try { bookmarksList.Capture = true; } catch { }
                    }
                }
                if (bmDragging)
                {
                    int ti = bookmarksList.IndexFromPoint(e.Location);
                    bool above = bmDragDropAbove;
                    if (ti >= 0 && ti < bmRows.Count && ti != bmDragIndex)
                    {
                        var rect = bookmarksList.GetItemRectangle(ti);
                        above = e.Y < rect.Top + rect.Height / 2;
                    }
                    else ti = -1;
                    if (ti != bmDragTarget || above != bmDragDropAbove)
                    {
                        bmDragTarget = ti;
                        bmDragDropAbove = above;
                        bookmarksList.Invalidate();
                    }
                    return;
                }
            }
            int i = bookmarksList.IndexFromPoint(e.Location);
            if (i != hoverBm) { hoverBm = i; bookmarksList.Invalidate(); }
            try
            {
                string t = "";
                if (i >= 0 && i < bmRows.Count)
                {
                    var b = bmRows[i].Bm;
                    if (b.Kind == "cmd") t = b.Value;
                    else if (b.Kind == "folder") t = b.Value;
                }
                tip.SetToolTip(bookmarksList, t);
            }
            catch { }
        }

        private void BookmarksList_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int di = bookmarksList.IndexFromPoint(e.Location);
                bmDragIndex = (di >= 0 && di < bmRows.Count) ? di : -1;
                bmDragStart = e.Location;
                bmDragging = false;
                return;
            }
            if (e.Button != MouseButtons.Right) return;
            int i = bookmarksList.IndexFromPoint(e.Location);
            var m = new ContextMenu();
            if (i >= 0 && i < bmRows.Count)
            {
                bookmarksList.SelectedIndex = i;
                var b = bmRows[i].Bm;
                if (b.Kind == "cmd")
                    m.MenuItems.Add(Loc.S("Edit command...", "Изменить команду..."), (s2, e2) => EditBookmarkCommand(b));
                m.MenuItems.Add(Loc.S("Rename..."), (s2, e2) => RenameBookmark(b));
                m.MenuItems.Add(Loc.S("Move up", "Вверх"), (s2, e2) => MoveBookmark(b, -1));
                m.MenuItems.Add(Loc.S("Move down", "Вниз"), (s2, e2) => MoveBookmark(b, 1));
                m.MenuItems.Add(Loc.S("Remove"), (s2, e2) => RemoveBookmark(b));
            }
            m.MenuItems.Add(Loc.S("Add current folder"), (s2, e2) => AddFolderBookmark(currentPath, FolderNameOf(currentPath)));
            m.MenuItems.Add(Loc.S("Add command..."), (s2, e2) => SaveCommandBookmark(""));
            m.MenuItems.Add(Loc.S("Add group..."), (s2, e2) => AddGroup());
            m.Show(bookmarksList, e.Location);
        }

        private void ShowBookmarksAddMenu()
        {
            var m = new ContextMenu();
            m.MenuItems.Add(Loc.S("Add current folder"), (s2, e2) => AddFolderBookmark(currentPath, FolderNameOf(currentPath)));
            m.MenuItems.Add(Loc.S("Add command..."), (s2, e2) => SaveCommandBookmark(""));
            m.MenuItems.Add(Loc.S("Add group..."), (s2, e2) => AddGroup());
            m.Show(btnBmAdd, new Point(0, btnBmAdd.Height));
        }

        private void AddGroup()
        {
            string name = Prompt.ShowDialog(Loc.S("Group name:", "Имя группы:"), Loc.S("Add bookmark group", "Добавить группу закладок"), "");
            if (string.IsNullOrWhiteSpace(name)) return;
            var g = new ExplorerBookmark();
            g.Kind = "group";
            g.Name = name.Trim();
            bookmarks.Add(g);
            expandedGroups.Add(g.Name);
            SaveBookmarks();
            RebuildBookmarks();
        }

        private void AddFolderBookmark(string path, string defaultName)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            string name = Prompt.ShowDialog(Loc.S("Bookmark name:", "Имя закладки:"), Loc.S("Add folder bookmark", "Добавить закладку на папку"), defaultName);
            if (string.IsNullOrWhiteSpace(name)) return;
            var b = new ExplorerBookmark();
            b.Kind = "folder";
            b.Name = name.Trim();
            b.Value = path;
            bookmarks.Add(b);
            SaveBookmarks();
            RebuildBookmarks();
        }

        private void RenameBookmark(ExplorerBookmark b)
        {
            string name = Prompt.ShowDialog(Loc.S("New name:", "Новое имя:"), Loc.S("Rename bookmark", "Переименовать закладку"), b.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            if (b.Kind == "group" && expandedGroups.Contains(b.Name))
            {
                expandedGroups.Remove(b.Name);
                expandedGroups.Add(name.Trim());
            }
            b.Name = name.Trim();
            SaveBookmarks();
            RebuildBookmarks();
        }

        // Edits the command itself of a "cmd" bookmark (Rename only changes the label).
        private void EditBookmarkCommand(ExplorerBookmark b)
        {
            string cmd = Prompt.ShowDialog(Loc.S("Command (%1 = current folder):", "Команда (%1 — текущая папка):"), Loc.S("Edit command", "Изменить команду"), b.Value);
            if (string.IsNullOrWhiteSpace(cmd)) return;
            string old = b.Value;
            b.Value = cmd.Trim();
            // Keep the list label in sync when it simply mirrored the command.
            if (string.IsNullOrEmpty(b.Name) || string.Equals(b.Name, old, StringComparison.Ordinal))
                b.Name = b.Value;
            SaveBookmarks();
            RebuildBookmarks();
        }

        // Moves a bookmark one slot up/down within its own list (top level or group).
        private void MoveBookmark(ExplorerBookmark b, int delta)
        {
            var list = FindOwningList(bookmarks, b);
            if (list == null) return;
            int i = list.IndexOf(b);
            int j = i + delta;
            if (i < 0 || j < 0 || j >= list.Count) return;
            list[i] = list[j];
            list[j] = b;
            SaveBookmarks();
            RebuildBookmarks();
        }

        private static List<ExplorerBookmark> FindOwningList(List<ExplorerBookmark> list, ExplorerBookmark target)
        {
            if (list.Contains(target)) return list;
            foreach (var b in list)
            {
                if (b.Children == null || b.Children.Count == 0) continue;
                var r = FindOwningList(b.Children, target);
                if (r != null) return r;
            }
            return null;
        }

        private void RemoveBookmark(ExplorerBookmark b)
        {
            int kids = b.Children != null ? b.Children.Count : 0;
            string msg = (b.Kind == "group" && kids > 0)
                ? "Remove group \"" + b.Name + "\" with " + kids + " entries?"
                : "Remove \"" + b.Name + "\"?";
            if (!ConfirmDialog.ShowConfirm(this, Loc.S("Bookmarks", "Закладки"), msg)) return;
            RemoveFromList(bookmarks, b);
            SaveBookmarks();
            RebuildBookmarks();
        }

        private static bool RemoveFromList(List<ExplorerBookmark> list, ExplorerBookmark target)
        {
            if (list.Remove(target)) return true;
            foreach (var b in list)
                if (b.Children != null && b.Children.Count > 0 && RemoveFromList(b.Children, target)) return true;
            return false;
        }

        private void SaveBookmarks()
        {
            try { BookmarksStore.Save(bookmarksPath, bookmarks); } catch { }
        }

        private void SaveCommandBookmark(string cmd)
        {
            cmd = cmd == null ? "" : cmd.Trim();
            if (cmd.Length == 0)
            {
                cmd = Prompt.ShowDialog(Loc.S("Command (%1 = current folder):", "Команда (%1 — текущая папка):"), Loc.S("Add command", "Добавить команду"), "");
                if (string.IsNullOrWhiteSpace(cmd)) return;
                cmd = cmd.Trim();
            }
            string group = Prompt.ShowDialog(Loc.S("Group (empty = top level):", "Группа (пусто = верхний уровень):"), Loc.S("Add command", "Добавить команду"), "");
            // Display name (defaults to the command itself so Enter keeps it).
            string displayName = Prompt.ShowDialog(Loc.S("Display name:", "Название:"), Loc.S("Add command", "Добавить команду"), cmd);
            ExplorerBookmark target = null;
            if (!string.IsNullOrWhiteSpace(group))
            {
                string gname = group.Trim();
                foreach (var b in bookmarks)
                    if (b.Kind == "group" && string.Equals(b.Name, gname, StringComparison.OrdinalIgnoreCase)) { target = b; break; }
                if (target == null)
                {
                    target = new ExplorerBookmark();
                    target.Kind = "group";
                    target.Name = gname;
                    bookmarks.Add(target);
                }
                expandedGroups.Add(target.Name);
            }
            var nb = new ExplorerBookmark();
            nb.Kind = "cmd";
            nb.Value = cmd;
            nb.Name = string.IsNullOrWhiteSpace(displayName) ? cmd : displayName.Trim();
            if (target != null) target.Children.Add(nb);
            else bookmarks.Add(nb);
            SaveBookmarks();
            RebuildBookmarks();
        }

        // ---------- console ----------

        private static Encoding GetOemEncoding()
        {
            try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
            catch { }
            try { return Encoding.GetEncoding(866); }
            catch { }
            return Encoding.Default;
        }

        private void StartShell()
        {
            StopShell();
            skipBanner = true;
            lock (sentEcho) { sentEcho.Clear(); }
            try
            {
                var psi = new ProcessStartInfo("cmd.exe");
                psi.UseShellExecute = false;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                try { if (Directory.Exists(currentPath)) psi.WorkingDirectory = currentPath; } catch { }

                shell = new Process();
                shell.StartInfo = psi;
                shell.Start();

                Encoding enc = GetOemEncoding();
                shellIn = new StreamWriter(shell.StandardInput.BaseStream, enc);
                shellIn.AutoFlush = true;

                var t1 = new Thread(delegate() { ReadLoop(new StreamReader(shell.StandardOutput.BaseStream, enc)); });
                t1.IsBackground = true;
                t1.Start();
                var t2 = new Thread(delegate() { ReadLoop(new StreamReader(shell.StandardError.BaseStream, enc)); });
                t2.IsBackground = true;
                t2.Start();

                shell.EnableRaisingEvents = true;
                int shellPid = shell.Id;
                shell.Exited += (s, e) =>
                {
                    // The notice makes sense only while this process is still the
                    // current shell — after Stop it fires during the restart and
                    // would claim an exit that has already been handled.
                    bool current = false;
                    try { current = shell != null && shell.Id == shellPid; } catch { }
                    if (current) Ui(delegate { AppendConsole(Loc.S("[console process exited - press Restart]", "[процесс консоли завершён — нажмите «Перезапуск»]"), dimColor); });
                };

                if (Directory.Exists(currentPath)) SendCmd("cd /d \"" + currentPath + "\"");
                AppendConsole(Loc.S("Tilettes console · ", "Консоль Плиточек · ") + currentPath, dimColor);
                AppendConsole(PromptText(), accentColor);
            }
            catch (Exception ex)
            {
                shell = null;
                AppendConsole("Console error: " + ex.Message, Color.FromArgb(220, 90, 90));
            }
        }

        private void StopShell()
        {
            try { if (shellIn != null) shellIn.Dispose(); } catch { }
            shellIn = null;
            try { if (shell != null && !shell.HasExited) shell.Kill(); } catch { }
            try { if (shell != null) shell.Dispose(); } catch { }
            shell = null;
        }

        // Interrupts the command running in the console: kills the whole shell
        // process tree (taskkill /T — cmd.exe and everything it spawned: ping,
        // powershell, docker stats...) and starts a fresh shell. A graceful
        // Ctrl+C was tried first via GenerateConsoleCtrlEvent, but on redirected
        // child consoles the API reports success while delivering nothing — or
        // kills only cmd.exe and orphans the command. The tree kill always works.
        private void StopConsoleCommand()
        {
            bool dead = true;
            try { dead = shell == null || shell.HasExited; } catch { dead = true; }
            if (dead) return;
            int shellId = 0;
            try { shellId = shell.Id; } catch (Exception ex) { AppLog.Write("Stop: no shell id", ex); return; }
            HardStopShell(shellId);
        }

        // Kills the shell and everything it spawned and starts a fresh shell so
        // the console keeps working.
        private void HardStopShell(int shellId)
        {
            try
            {
                using (var tk = Process.Start(new ProcessStartInfo("taskkill", "/PID " + shellId + " /T /F")
                { CreateNoWindow = true, UseShellExecute = false }))
                {
                    if (!tk.WaitForExit(3000)) AppLog.Write("Stop: taskkill timed out");
                }
            }
            catch (Exception ex) { AppLog.Write("Stop: taskkill failed", ex); }
            StopShell();
            AppendConsole(Loc.S("[stopped]", "[остановлено]"), dimColor);
            StartShell();
        }

        // The shell is read character by character: a plain \n line is appended as
        // usual, while a line ended by a bare \r (tools like docker stats and every
        // progress bar redraw their frame in place) REPLACES the last console line.
        // Frames are stripped of ANSI escape sequences, and a run of empty lines
        // collapses into one — a streaming tool can no longer flood the console
        // with blank output (the old ReadLine loop split on every \r and appended
        // each empty fragment, which re-filled the 150k buffer cyclically).
        private void ReadLoop(StreamReader r)
        {
            var sb = new System.Text.StringBuilder();
            bool prevCr = false;
            bool holdBlank = false;
            try
            {
                int ch;
                while ((ch = r.Read()) >= 0)
                {
                    char c = (char)ch;
                    if (c == '\r') { EmitConsoleLine(sb, true, ref holdBlank); prevCr = true; continue; }
                    if (c == '\n')
                    {
                        if (!prevCr) EmitConsoleLine(sb, false, ref holdBlank);
                        prevCr = false;
                        continue;
                    }
                    sb.Append(c);
                    prevCr = false;
                }
                if (sb.Length > 0) EmitConsoleLine(sb, false, ref holdBlank);
            }
            catch { }
        }

        // One terminated line arrived from the shell; `overwrite` marks a bare-\r
        // refresh frame. Empty overwrite frames are dropped, other empty lines
        // collapse into a single one, everything else keeps the banner / echo /
        // sentinel filtering.
        private void EmitConsoleLine(System.Text.StringBuilder sb, bool overwrite, ref bool holdBlank)
        {
            string l = StripAnsi(sb.ToString());
            sb.Length = 0;
            if (skipBanner)
            {
                if (l.Trim().Length == 0 ||
                    l.StartsWith("Microsoft Windows [Version", StringComparison.OrdinalIgnoreCase) ||
                    l.StartsWith("(c)", StringComparison.OrdinalIgnoreCase))
                    return;
                skipBanner = false;
            }
            if (l.Trim().Length == 0)
            {
                if (overwrite) return; // a redraw frame that erased itself
                if (!holdBlank) holdBlank = true;
                return;                // further blanks of the run collapse away
            }
            if (IsEchoOfSent(l))
            {
                holdBlank = false;
                return;
            }
            if (l.IndexOf(Sentinel, StringComparison.Ordinal) >= 0)
            {
                holdBlank = false;
                Ui(delegate { AppendConsole(PromptText(), accentColor); });
                return;
            }
            if (holdBlank)
            {
                holdBlank = false;
                Ui(delegate { AppendConsole("", textColor); });
            }
            Ui(delegate { AppendConsole(l, textColor, overwrite ? 1 : 2); });
        }

        // Removes ANSI escape sequences (CSI "ESC[...final", OSC "ESC]...BEL/ESC\\",
        // charset and two-char escapes) that streaming tools send with their frames.
        internal static string StripAnsi(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('\x1b') < 0) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\x1b' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    if (n == '[')
                    {
                        i += 2;
                        while (i < s.Length && s[i] >= '\x20' && s[i] <= '\x3f') i++; // params + intermediates
                        if (i < s.Length && s[i] >= '\x40' && s[i] <= '\x7e') i++;    // final byte
                        continue;
                    }
                    if (n == ']')
                    {
                        i += 2;
                        while (i < s.Length && s[i] != '\x07')
                        {
                            if (s[i] == '\x1b' && i + 1 < s.Length && s[i + 1] == '\\') { i++; break; }
                            i++;
                        }
                        if (i < s.Length) i++; // BEL or the ESC\ terminator
                        continue;
                    }
                    if (n == '(' || n == ')') { i += 3; continue; } // charset, e.g. ESC(B
                    i += 2;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        private void BookmarksList_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (!bmDragging) { bmDragIndex = -1; return; }
            try { bookmarksList.Capture = false; } catch { }
            int src = bmDragIndex, tgt = bmDragTarget;
            bool above = bmDragDropAbove;
            bmDragging = false;
            bmDragIndex = -1;
            bmDragTarget = -1;
            bookmarksList.Invalidate();
            if (tgt < 0 || tgt >= bmRows.Count || src < 0 || src >= bmRows.Count || tgt == src) return;
            bmSuppressClick = true; // the release must not also open/run the bookmark
            DropBookmark(src, tgt, above);
        }

        // Drop of a bookmark reorder: above/below the target row; a drop below a
        // group header appends into that group. Groups only reorder among
        // themselves at the top level, and a group can not land inside itself.
        private void DropBookmark(int srcIdx, int tgtIdx, bool above)
        {
            var src = bmRows[srcIdx].Bm;
            var tgt = bmRows[tgtIdx].Bm;
            if (src == null || tgt == null || ReferenceEquals(src, tgt)) return;
            var srcList = FindOwningList(bookmarks, src);

            if (tgt.Kind == "group")
            {
                if (src.Kind == "group")
                {
                    if (!bookmarks.Contains(src)) return;
                    bookmarks.Remove(src);
                    int j = bookmarks.IndexOf(tgt);
                    if (j < 0) { bookmarks.Add(src); }
                    else bookmarks.Insert(above ? j : j + 1, src);
                    SaveBookmarks();
                    RebuildBookmarks();
                    return;
                }
                if (above)
                {
                    // before the group, at the top level
                    if (srcList != null) srcList.Remove(src);
                    int j = bookmarks.IndexOf(tgt);
                    if (j < 0) bookmarks.Add(src);
                    else bookmarks.Insert(j, src);
                    SaveBookmarks();
                    RebuildBookmarks();
                    return;
                }
                // below the header = into the group
                if (ContainsBookmark(src, tgt)) return;                          // into own child
                if (srcList != null && ReferenceEquals(srcList, tgt.Children)) return; // already there
                if (srcList != null) srcList.Remove(src);
                if (tgt.Children == null) tgt.Children = new List<ExplorerBookmark>();
                tgt.Children.Add(src);
                expandedGroups.Add(tgt.Name);
                SaveBookmarks();
                RebuildBookmarks();
                return;
            }

            var list = FindOwningList(bookmarks, tgt);
            if (list == null) return;
            int jj = list.IndexOf(tgt);
            if (jj < 0) return;
            bool sameList = ReferenceEquals(srcList, list);
            if (srcList != null) srcList.Remove(src);
            if (sameList) jj = list.IndexOf(tgt); // the removal may shift the target
            if (jj < 0) { list.Add(src); }
            else list.Insert(above ? jj : jj + 1, src);
            SaveBookmarks();
            RebuildBookmarks();
        }

        // True when `target` sits anywhere inside `container`'s subtree.
        private static bool ContainsBookmark(ExplorerBookmark container, ExplorerBookmark target)
        {
            if (container.Children == null) return false;
            foreach (var c in container.Children)
            {
                if (ReferenceEquals(c, target)) return true;
                if (ContainsBookmark(c, target)) return true;
            }
            return false;
        }

        // The shell echoes every input line back with its prompt ("C:\dir>command").
        // Those echo lines are matched against the queue of lines we actually sent
        // and dropped, so the console shows only real output.
        private bool IsEchoOfSent(string line)
        {
            string expect = null;
            lock (sentEcho)
            {
                if (sentEcho.Count > 0) expect = sentEcho.Peek();
            }
            if (expect == null || expect.Length == 0) return false;
            if (line.Length <= expect.Length) return false;
            if (!line.EndsWith(expect, StringComparison.OrdinalIgnoreCase)) return false;
            string head = line.Substring(0, line.Length - expect.Length);
            if (!head.EndsWith(">")) return false;
            lock (sentEcho)
            {
                if (sentEcho.Count > 0) sentEcho.Dequeue();
            }
            return true;
        }

        private void SendCmd(string text)
        {
            try
            {
                if (shellIn == null) return;
                lock (sentEcho)
                {
                    if (sentEcho.Count > 64) sentEcho.Dequeue();
                    sentEcho.Enqueue(text);
                }
                shellIn.WriteLine(text);
            }
            catch { }
        }

        private string PromptText()
        {
            string p = currentPath;
            if (string.IsNullOrEmpty(p)) p = "?";
            return p + ">";
        }

        private void SyncShellDir()
        {
            if (shell == null) return;
            bool dead = true;
            try { dead = shell.HasExited; } catch { dead = true; }
            if (dead) return;
            SendCmd("cd /d \"" + currentPath + "\"");
        }

        private void RunConsoleInput()
        {
            string cmd = consoleIn.Text;
            consoleIn.Clear();
            RunInConsole(cmd);
        }

        private void RunInConsole(string cmd)
        {
            if (cmd == null) return;
            cmd = cmd.Trim();
            if (cmd.Length == 0) return;
            cmdHistory.Add(cmd);
            cmdHistoryPos = cmdHistory.Count;
            bool dead = true;
            try { dead = shell == null || shell.HasExited; } catch { dead = true; }
            if (dead) StartShell();
            AppendConsole("> " + cmd, accentColor);
            SendCmd("cd /d \"" + currentPath + "\"");
            SendCmd(cmd);
            SendCmd("echo " + Sentinel);
        }

        private void ConsoleIn_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                RunConsoleInput();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (cmdHistoryPos > 0)
                {
                    cmdHistoryPos--;
                    consoleIn.Text = cmdHistory[cmdHistoryPos];
                    consoleIn.SelectionStart = consoleIn.Text.Length;
                }
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                if (cmdHistoryPos < cmdHistory.Count - 1)
                {
                    cmdHistoryPos++;
                    consoleIn.Text = cmdHistory[cmdHistoryPos];
                }
                else
                {
                    cmdHistoryPos = cmdHistory.Count;
                    consoleIn.Clear();
                }
                e.SuppressKeyPress = true;
            }
        }

        // True when the last console line came from a \r refresh frame — only such
        // lines are replaced by the next refresh frame, real output (and the
        // "> command" echo) is never overwritten. UI thread only.
        private bool lastAppendWasOverwrite;
        // Consecutive-duplicate tracking: a plain \n line equal to the previous
        // one is rewritten into a ×N counter instead of being printed again.
        // UI thread only.
        private string lastConsoleLine;
        private int lastConsoleLineDup;
        // Streaming-table redraw: `docker stats` (non-TTY) reprints the whole
        // table every second as plain \n lines. When an incoming line equals the
        // first line of the previous block (the table header), the old block is
        // rewritten in place line by line — the console shows one live-updating
        // table, exactly like a real terminal. Only "columnar" lines (with a
        // double space, like table headers) may start a redraw, so plain-text
        // streams are never touched. UI thread only.
        private readonly List<string> frameBlock = new List<string>();
        private int frameBlockStartChar = -1; // buffer offset where the tracked block begins
        private bool frameReplacing;
        private int frameReplaceChar; // buffer offset of the line being rewritten
        private int frameOldLen;      // line count of the frame being replaced
        private int frameLine;        // index of the next line inside the frame

        private void ResetFrameTracking()
        {
            frameReplacing = false;
            frameBlockStartChar = -1;
            frameBlock.Clear();
        }

        private void AppendConsole(string text, Color color)
        {
            AppendConsole(text, color, 0);
        }

        // mode: 0 = append; 1 = replace the last line, but only when it was a
        // refresh frame; 2 = append with streaming dedupe: a repeated line
        // collapses into ×N, and a line that repeats the block header starts a
        // live redraw of the whole block. Prompts, echoes and banners (mode 0)
        // reset both runs.
        private void AppendConsole(string text, Color color, int mode)
        {
            try
            {
                if (consoleOut == null || consoleOut.IsDisposed) return;
                if (consoleOut.TextLength > 150000)
                {
                    consoleOut.Clear();
                    ResetFrameTracking();
                }

                if (mode == 2 && !string.IsNullOrEmpty(text))
                {
                    if (TryFrameRedraw(text)) return;
                    if (text == lastConsoleLine)
                    {
                        lastConsoleLineDup++;
                        RewriteLastConsoleLine(lastConsoleLine + "  ×" + lastConsoleLineDup, textColor);
                        return;
                    }
                    AppendConsoleCore(text, color); // a fresh line of the block — keep tracking
                    return;
                }
                ResetFrameTracking();
                if (mode == 1 && lastAppendWasOverwrite)
                {
                    RewriteLastConsoleLine(text, color);
                    lastConsoleLine = string.IsNullOrEmpty(text) ? null : text;
                    lastConsoleLineDup = 1;
                    lastAppendWasOverwrite = true;
                    return;
                }
                lastConsoleLine = string.IsNullOrEmpty(text) ? null : text;
                lastConsoleLineDup = 1;
                lastAppendWasOverwrite = false;
                consoleOut.SelectionStart = consoleOut.TextLength;
                consoleOut.SelectionLength = 0;
                consoleOut.SelectionColor = color;
                consoleOut.AppendText(text + "\n");
                consoleOut.SelectionStart = consoleOut.TextLength;
                consoleOut.ScrollToCaret();
            }
            catch { }
        }

        // The streaming-table redraw state machine (see the fields above). Called
        // only for plain-\n lines. Returns true when the line was consumed by the
        // redraw and must not be appended.
        private bool TryFrameRedraw(string text)
        {
            if (frameReplacing)
            {
                bool columnar = text.IndexOf("  ", StringComparison.Ordinal) >= 0;
                if (frameLine >= frameOldLen && text == frameBlock[0] && columnar)
                {
                    // The next frame's header arrived: redraw it over the previous block.
                    frameReplaceChar = frameBlockStartChar;
                    frameOldLen = frameLine;
                    frameLine = 0;
                }
                if (frameLine < frameOldLen)
                {
                    RewriteAt(frameReplaceChar, text);
                    frameReplaceChar += text.Length + 1;
                }
                else
                {
                    if (frameLine == frameOldLen) frameBlockStartChar = consoleOut.TextLength;
                    AppendConsoleCore(text, textColor);
                }
                if (frameLine < frameBlock.Count) frameBlock[frameLine] = text; else frameBlock.Add(text);
                frameLine++;
                return true;
            }
            if (frameBlockStartChar >= 0 && frameBlock.Count >= 1 && frameBlock[0] == text
                && text.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                // The block must still start at the remembered offset.
                bool aligned = false;
                try
                {
                    string t = consoleOut.Text;
                    aligned = frameBlockStartChar + frameBlock[0].Length <= t.Length
                        && t.IndexOf(frameBlock[0], frameBlockStartChar, StringComparison.Ordinal) == frameBlockStartChar;
                }
                catch { }
                if (aligned)
                {
                    frameReplacing = true;
                    frameReplaceChar = frameBlockStartChar;
                    frameOldLen = frameBlock.Count;
                    frameLine = 1;
                    RewriteAt(frameReplaceChar, text);
                    frameReplaceChar += text.Length + 1;
                    return true;
                }
                ResetFrameTracking(); // the block no longer matches the buffer — start over
            }
            if (frameBlock.Count > 400) frameBlock.Clear();
            if (frameBlock.Count == 0) frameBlockStartChar = consoleOut.TextLength; // the pending append starts here
            frameBlock.Add(text);
            return false;
        }

        // Plain append without touching the streaming state.
        private void AppendConsoleCore(string text, Color color)
        {
            lastConsoleLine = string.IsNullOrEmpty(text) ? null : text;
            lastConsoleLineDup = 1;
            lastAppendWasOverwrite = false;
            consoleOut.SelectionStart = consoleOut.TextLength;
            consoleOut.SelectionLength = 0;
            consoleOut.SelectionColor = color;
            consoleOut.AppendText(text + "\n");
            consoleOut.SelectionStart = consoleOut.TextLength;
            consoleOut.ScrollToCaret();
        }

        // Replaces the line content at the character offset `pos` in place (the
        // line break stays); falls back to appending when the offset is out of
        // range. The box is read-only — the lock is lifted for the rewrite.
        private void RewriteAt(int pos, string text)
        {
            try
            {
                string t = consoleOut.Text;
                if (pos < 0 || pos >= t.Length) { AppendConsoleCore(text, textColor); return; }
                int end = t.IndexOf('\n', pos);
                if (end < 0) end = t.Length;
                consoleOut.ReadOnly = false;
                try
                {
                    consoleOut.SelectionStart = pos;
                    consoleOut.SelectionLength = end - pos;
                    consoleOut.SelectionColor = textColor;
                    consoleOut.SelectedText = text;
                }
                finally { consoleOut.ReadOnly = true; }
                consoleOut.SelectionStart = consoleOut.TextLength;
                consoleOut.ScrollToCaret();
            }
            catch { }
        }

        // Replaces the content of the last console line in place. Every append
        // ends with a newline, so the "last line" starts before that trailing
        // break; the box is read-only — the lock is lifted for the one rewrite.
        private void RewriteLastConsoleLine(string text, Color color)
        {
            try
            {
                string t = consoleOut.Text;
                int end = t.Length;
                if (end > 0 && t[end - 1] == '\n') end--;
                consoleOut.ReadOnly = false;
                try
                {
                    if (end > 0)
                    {
                        int idx = t.LastIndexOf('\n', end - 1, end);
                        consoleOut.SelectionStart = idx + 1;
                        consoleOut.SelectionLength = end - (idx + 1);
                        consoleOut.SelectionColor = color;
                        consoleOut.SelectedText = text;
                    }
                    else
                    {
                        consoleOut.SelectionStart = 0;
                        consoleOut.SelectionLength = 0;
                        consoleOut.SelectionColor = color;
                        consoleOut.AppendText(text + "\n");
                    }
                }
                finally { consoleOut.ReadOnly = true; }
                consoleOut.SelectionStart = consoleOut.TextLength;
                consoleOut.ScrollToCaret();
            }
            catch { }
        }

        private void Ui(Action a)
        {
            try
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                this.BeginInvoke(a);
            }
            catch { }
        }

        private void OpenRealConsole()
        {
            try { Process.Start("cmd.exe", "/k cd /d \"" + currentPath + "\""); } catch { }
        }

        // The title bar and the bottom hint cover the very edge of the form; these
        // subclasses keep the normal WinForms behaviour and add the edge hit test
        // (a NativeWindow hook would replace the control procedure and break painting).
        private class EdgeTitlePanel : Panel
        {
            private MiniExplorerForm form;
            public EdgeTitlePanel(MiniExplorerForm f) { form = f; }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x84)
                {
                    base.WndProc(ref m);
                    form.ApplyEdgeHit(ref m);
                    return;
                }
                base.WndProc(ref m);
            }
        }

        private class EdgeHintLabel : Label
        {
            private MiniExplorerForm form;
            public EdgeHintLabel(MiniExplorerForm f) { form = f; }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x84)
                {
                    base.WndProc(ref m);
                    form.ApplyEdgeHit(ref m);
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
