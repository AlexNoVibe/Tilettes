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
        private readonly List<string> history = new List<string>();
        private int historyIndex = -1;

        // controls
        private Label titleLbl, statusLbl;
        private Label lblConsole, hintLbl, promptLbl, bmHeader;
        private Button btnBack, btnFwd, btnUp, btnRefresh, btnEditPath, btnBmAdd, btnToggleBm, btnTopBar;
        private Button btnConsoleWin, btnConsoleRestart, btnConsoleClear, btnSaveCmd, btnRunCmd;
        private Panel topBarHost, splitter;
        private FlowLayoutPanel topBarFlow;
        private int splitGrabDy;
        private bool topBarVisible = true;
        private bool splitterDragging;
        private bool sizing;
        private double consoleFrac = 0.40;
        private Panel crumbHost;
        private FlowLayoutPanel crumbFlow;
        private TextBox pathEdit, consoleIn;
        private ListBox bookmarksList, fileList;
        private RichTextBox consoleOut;
        private TextBox searchBox;
        private Button btnScope;
        private System.Windows.Forms.Timer searchTimer;
        private List<SearchItem> searchItems;
        private List<string> searchVariants = new List<string>();
        private bool searchMode;
        private string searchScope = "folder";
        private int topBarChipH = 24; // chip height follows the UI font

        // file list data
        private class DirEntry { public string Name; public string FullPath; public bool IsDir; public long Size; }
        private readonly List<DirEntry> entries = new List<DirEntry>();
        private readonly Dictionary<string, Bitmap> iconCache = new Dictionary<string, Bitmap>();
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
            bool light = settings.IsLightTheme;
            bgColor = light ? Color.FromArgb(232, 232, 234) : Color.FromArgb(24, 24, 28);
            panelColor = light ? Color.FromArgb(212, 212, 216) : Color.FromArgb(45, 45, 48);
            hoverColor = light ? Color.FromArgb(196, 196, 202) : Color.FromArgb(62, 62, 66);
            textColor = light ? Color.Black : Color.White;
            dimColor = light ? Color.FromArgb(110, 110, 115) : Color.FromArgb(165, 165, 170);
            accentColor = light ? Color.FromArgb(0, 102, 204) : Color.FromArgb(70, 165, 245);
            listColor = light ? Color.FromArgb(246, 246, 248) : Color.FromArgb(18, 18, 22);
            consoleBg = light ? Color.FromArgb(250, 250, 252) : Color.FromArgb(12, 12, 15);

            this.Text = "Mini Explorer";
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
            titleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
            titleLbl = new Label { Text = "Mini Explorer", ForeColor = textColor, AutoSize = true, Location = new Point(10, 7) };
            titleBar.Controls.Add(titleLbl);
            var closeBtn = new Button { Text = "X", Width = 30, Height = 30, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, ForeColor = textColor, BackColor = panelColor };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => this.Close();
            titleBar.Controls.Add(closeBtn);
            this.Controls.Add(titleBar);

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
            bookmarksList.MouseLeave += (s, e) => { if (hoverBm != -1) { hoverBm = -1; bookmarksList.Invalidate(); } };
            this.Controls.Add(bookmarksList);

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
            this.Controls.Add(btnConsoleWin);
            this.Controls.Add(btnConsoleRestart);
            this.Controls.Add(btnConsoleClear);

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
                Text = "Ctrl+L or Edit - edit path · F5 - refresh · Backspace - up · Enter - open · double-click - open",
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
            int top = 30;
            int rightEdge = W - 8;

            btnToggleBm.SetBounds(8, top + 2, 28, 26);
            btnTopBar.SetBounds(44, top + 2, 28, 26);
            btnBack.SetBounds(82, top + 2, 28, 26);
            btnFwd.SetBounds(120, top + 2, 28, 26);
            btnUp.SetBounds(158, top + 2, 28, 26);
            btnRefresh.SetBounds(196, top + 2, 28, 26);

            // Top-right is reserved for search: [scope toggle][search box][status].
            statusLbl.SetBounds(rightEdge - 110, top + 7, 106, 18);
            int searchW = Math.Min(280, Math.Max(150, W / 5));
            int searchX = rightEdge - 110 - 6 - searchW;
            searchBox.SetBounds(searchX, top + 3, searchW, 24);
            btnScope.SetBounds(searchX - 6 - 64, top + 2, 64, 26);

            // The breadcrumb path bar sits on its own row below the nav row.
            int crumbTop = top + 34;
            btnEditPath.SetBounds(rightEdge - 50, crumbTop + 1, 44, 26);
            int crumbW = Math.Max(80, rightEdge - 50 - 6 - 8);
            crumbHost.SetBounds(8, crumbTop, crumbW, 28);
            pathEdit.SetBounds(8, crumbTop + 2, crumbW, 24);
            int crumbBottom = crumbTop + 30;

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
            int splitTop = consTop - 10;
            int listH = splitTop - 4 - listTop;
            if (listH < 60) listH = 60;

            bmHeader.SetBounds(10, listTop - 18, 150, 16);
            btnBmAdd.SetBounds(184, listTop - 22, 24, 20);
            bookmarksList.SetBounds(8, listTop, 200, listH);
            int bmItemH = Math.Max(20, TextRenderer.MeasureText("Ag", this.Font).Height + 8);
            if (bookmarksList.ItemHeight != bmItemH) bookmarksList.ItemHeight = bmItemH;
            bmHeader.Visible = bookmarksVisible;
            btnBmAdd.Visible = bookmarksVisible;
            bookmarksList.Visible = bookmarksVisible;

            int fx = bookmarksVisible ? 216 : 8;
            fileList.SetBounds(fx, listTop, W - fx - 8, listH);

            splitter.SetBounds(8, listTop + listH + 2, W - 16, splitH);

            lblConsole.SetBounds(10, consTop + 6, 120, 18);
            btnConsoleWin.SetBounds(rightEdge - 100, consTop + 3, 100, 22);
            btnConsoleRestart.SetBounds(rightEdge - 100 - 6 - 74, consTop + 3, 74, 22);
            btnConsoleClear.SetBounds(rightEdge - 100 - 6 - 74 - 6 - 64, consTop + 3, 64, 22);

            int ipTop = H - 46;
            int outTop = consTop + 28;
            int outH = ipTop - 6 - outTop;
            if (outH < 40) outH = 40;
            consoleOut.SetBounds(8, outTop, W - 16, outH);

            promptLbl.SetBounds(8, ipTop + 4, 16, 18);
            int runX = rightEdge - 56;
            int saveX = runX - 6 - 64;
            consoleIn.SetBounds(26, ipTop, Math.Max(80, saveX - 6 - 26), 24);
            btnSaveCmd.SetBounds(saveX, ipTop, 64, 24);
            btnRunCmd.SetBounds(runX, ipTop, 56, 24);

            hintLbl.SetBounds(8, H - 20, W - 16, 16);
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
            else text = "\u203A " + (string.IsNullOrEmpty(b.Value) ? b.Name : b.Value);
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
            var cap = b;
            chip.Click += (s, e) => OnChipClick(cap, chip);
            chip.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) ShowTopBarItemMenu(cap, chip, e.Location); };
            return chip;
        }

        private void OnChipClick(ExplorerBookmark b, Button chip)
        {
            if (b.Kind == "group") { ShowGroupMenu(b, chip); return; }
            if (b.Kind == "folder") { Navigate(b.Value); return; }
            RunInConsole(b.Value);
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
                    ? (string.IsNullOrEmpty(c.Value) ? c.Name : c.Value)
                    : (c.Kind == "group" ? c.Name + " \u203A" : c.Name);
                m.MenuItems.Add(label, (s2, e2) =>
                {
                    if (cap.Kind == "cmd") RunInConsole(cap.Value);
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

        public void NavigateExternal(string path)
        {
            if (this.InvokeRequired)
            {
                try { this.BeginInvoke((Action)delegate { Navigate(path); }); } catch { }
                return;
            }
            Navigate(path);
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
            UpdateTitle();
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

        private void OpenInExplorer(string path, bool select)
        {
            try
            {
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
            iconCache[key] = b16;
            return b16;
        }

        // ---------- search ----------

        private void FileList_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (searchMode) return;
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
                this.Font, boldFont, new Point(e.Bounds.Left + 28, ty), textColor, accentColor);

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
                        UiText.DrawHighlighted(g, subDisplay, pStart - cut, pLen, this.Font, boldFont, new Point(sx, ty), dimColor, accentColor);
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
                string label = !string.IsNullOrEmpty(b.Value) ? b.Value : b.Name;
                TextRenderer.DrawText(g, label, this.Font, new Point(x + 14, ty), textColor);
            }
        }

        private void BookmarksList_Click(object sender, EventArgs e)
        {
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
                RunInConsole(b.Value);
            }
        }

        private void BookmarksList_MouseMove(object sender, MouseEventArgs e)
        {
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
            string cmd = Prompt.ShowDialog(Loc.S("Command:", "Команда:"), Loc.S("Edit command", "Изменить команду"), b.Value);
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
            if (MessageBox.Show(this, msg, Loc.S("Bookmarks", "Закладки"), MessageBoxButtons.YesNo) != DialogResult.Yes) return;
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
                cmd = Prompt.ShowDialog(Loc.S("Command:", "Команда:"), Loc.S("Add command", "Добавить команду"), "");
                if (string.IsNullOrWhiteSpace(cmd)) return;
                cmd = cmd.Trim();
            }
            string group = Prompt.ShowDialog(Loc.S("Group (empty = top level):", "Группа (пусто = верхний уровень):"), Loc.S("Add command", "Добавить команду"), "");
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
            nb.Name = cmd;
            nb.Value = cmd;
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
                shell.Exited += (s, e) => Ui(delegate { AppendConsole(Loc.S("[console process exited - press Restart]", "[процесс консоли завершён — нажмите «Перезапуск»]"), dimColor); });

                if (Directory.Exists(currentPath)) SendCmd("cd /d \"" + currentPath + "\"");
                AppendConsole("WinPanel console · " + currentPath, dimColor);
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

        private void ReadLoop(StreamReader r)
        {
            bool holdBlank = false;
            try
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    string l = line;
                    if (skipBanner)
                    {
                        if (l.Trim().Length == 0 ||
                            l.StartsWith("Microsoft Windows [Version", StringComparison.OrdinalIgnoreCase) ||
                            l.StartsWith("(c)", StringComparison.OrdinalIgnoreCase))
                            continue;
                        skipBanner = false;
                    }
                    if (l.Trim().Length == 0)
                    {
                        if (!holdBlank) holdBlank = true;
                        else Ui(delegate { AppendConsole("", textColor); });
                        continue;
                    }
                    if (IsEchoOfSent(l))
                    {
                        holdBlank = false;
                        continue;
                    }
                    if (l.IndexOf(Sentinel, StringComparison.Ordinal) >= 0)
                    {
                        holdBlank = false;
                        Ui(delegate { AppendConsole(PromptText(), accentColor); });
                        continue;
                    }
                    if (holdBlank)
                    {
                        holdBlank = false;
                        Ui(delegate { AppendConsole("", textColor); });
                    }
                    Ui(delegate { AppendConsole(l, textColor); });
                }
            }
            catch { }
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

        private void AppendConsole(string text, Color color)
        {
            try
            {
                if (consoleOut == null || consoleOut.IsDisposed) return;
                if (consoleOut.TextLength > 150000) consoleOut.Clear();
                consoleOut.SelectionStart = consoleOut.TextLength;
                consoleOut.SelectionLength = 0;
                consoleOut.SelectionColor = color;
                consoleOut.AppendText(text + "\n");
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
