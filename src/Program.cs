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
        // Tabs whose tiles are built. LoadTabs used to render EVERY tab up front
        // (tearing down and recreating every TileControl, re-requesting every icon)
        // on startup and on each settings change; now only the active tab is built
        // and the rest render on their first activation.
        private readonly HashSet<TabData> renderedTabs = new HashSet<TabData>();
        private readonly Dictionary<Button, TabData> tabDataByButton = new Dictionary<Button, TabData>();
        private TextBox panelSearchBox;
        private System.Windows.Forms.Timer panelSearchTimer;
        private Panel panelSearchOverlay;
        private Panel panelSearchRow;
        private SearchResultsList panelSearchList;
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
        // Top block of the search overlay: remembered query -> item pairs whose
        // QUERY matches what is being typed ("поиск среди прошлых поисков").
        private readonly List<SearchHistoryEntry> panelSearchPast = new List<SearchHistoryEntry>();
        private const int PastSearchBlockMax = 10;
        private readonly Dictionary<string, Bitmap> panelSearchIcons = new Dictionary<string, Bitmap>();
        private List<string> panelSearchVariants = new List<string>(); // query variants for match highlighting
        // Fully measured row layouts (see PreparePanelSearchRow): rebuilt per query
        // and per list width, reused across the many repaints of the same results.
        private readonly Dictionary<int, PanelSearchRowLayout> panelSearchPrepared = new Dictionary<int, PanelSearchRowLayout>();
        private int panelSearchPreparedWidth = -1;
        private int panelSearchPreparedLeft = -1;

        private class PanelSearchRowLayout
        {
            public int TextH;
            public string PathCombo, PathDisplay;
            public int PathW;
            public bool HasDesc;
            public string Desc;
            public string NameDisplay;
            public int NameHlStart, NameHlLen; // start -1 = no highlight
            public int DescX, DescSpace;
            public string DescDisplay;         // null = does not fit / not drawn
            public int DescHlStart, DescHlLen;
            public bool PathTailHl;
            public int PathHlStart, PathHlLen; // measured on PathCombo, shifted for the tail cut
        }

        // The search result list owns the right-click: without this override
        // WM_CONTEXTMENU bubbles up from the list (empty area below the rows
        // included) through the overlay to the FORM, whose context menu holds
        // only "Settings" — that is the menu the user saw instead of the
        // result actions. Valid rows request the result menu, the empty area
        // shows nothing.
        private class SearchResultsList : ListBox
        {
            public Action<Point, int> RowMenuRequested; // (client point, row index or -1)

            protected override void WndProc(ref Message m)
            {
                const int WM_CONTEXTMENU = 0x007B;
                if (m.Msg == WM_CONTEXTMENU && RowMenuRequested != null)
                {
                    int x = (short)((long)m.LParam & 0xFFFF);
                    int y = (short)(((long)m.LParam >> 16) & 0xFFFF);
                    Point p = (x >= 0 && y >= 0) ? this.PointToClient(new Point(x, y)) : new Point(8, 8);
                    RowMenuRequested(p, this.IndexFromPoint(p));
                    return; // swallowed: never bubble to the parent/form menu
                }
                base.WndProc(ref m);
            }
        }

        // Host panel of the search overlay: a right-click on its uncovered
        // spots (the status strip) must not open the form context menu that
        // sits underneath the overlay.
        private class SearchOverlayPanel : Panel
        {
            protected override void WndProc(ref Message m)
            {
                const int WM_CONTEXTMENU = 0x007B;
                if (m.Msg == WM_CONTEXTMENU) return;
                base.WndProc(ref m);
            }
        }

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

        // Group drag in the multi-select mode: dragging a selected tile moves the
        // whole selection as one block. dragTiles holds every selected tile
        // (the grabbed one first); startPos is the grabbed tile's physical
        // position at mouse-down, from which the cell delta is derived on drop.
        private bool groupDrag = false;
        private List<TileControl> groupDragTiles = null;
        private Point groupDragStartPos;

        // Group reveal preview (single-tile drag only): the group under the
        // dragged tile shows its frame and expands to cover the tile's cells.
        private TileGroup previewGroup;
        private Rectangle previewRect;        // cells
        private GroupControl previewControl;

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

        // Virtual desktops: IVirtualDesktopManager is public COM (Windows 10+)
        // and moves only the calling process's own windows - the panel is one.
        [System.Runtime.InteropServices.ComImport, System.Runtime.InteropServices.Guid("aa509086-5ca9-4c25-8f95-589d3c07b0ea"), System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        private interface IVirtualDesktopManager
        {
            [System.Runtime.InteropServices.PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] out bool onCurrentDesktop);
            [System.Runtime.InteropServices.PreserveSig] int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
            [System.Runtime.InteropServices.PreserveSig] int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
        }

        [System.Runtime.InteropServices.ComImport, System.Runtime.InteropServices.Guid("B2A9D5EA-DC82-4F57-B1E2-91E27A3240E4")]
        private class CVirtualDesktopManager { }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        // DWM cloak (DWMWA_CLOAK, Windows 8+): the window stays alive and paints,
        // but DWM stops compositing it onto the screen. Used around the show/restore
        // so the first frame the user sees is already fully painted.
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        private const int DWMWA_CLOAK = 13;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);
        private const uint RDW_INVALIDATE = 0x1;
        private const uint RDW_ERASE = 0x4;
        private const uint RDW_ALLCHILDREN = 0x80;
        private const uint RDW_UPDATENOW = 0x100;

        // Returns the raw HRESULT (0 = cloaked) so the show path can log it;
        // any failure means "no cloaking available" and the caller falls back
        // to the plain show behavior.
        private static int CloakWindow(IntPtr hwnd, bool cloaked)
        {
            try
            {
                int v = cloaked ? 1 : 0;
                return DwmSetWindowAttribute(hwnd, DWMWA_CLOAK, ref v, 4);
            }
            catch { return -1; }
        }

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

            // First start only: fit the factory window to small monitors (the
            // 900x800 default at Y=100 would hang off screens shorter than
            // ~900px) and trim the grid by the same factor. Saved configs never
            // re-run this.
            if (!settings.FirstRunDone)
            {
                try { Settings.FitFirstStartToScreen(settings, Screen.PrimaryScreen.WorkingArea); }
                catch (Exception ex) { AppLog.Write("First-start screen fit", ex); }
            }

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

            panelSearchOverlay = new SearchOverlayPanel { Visible = false, BackColor = bgColor };
            panelSearchList = new SearchResultsList
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
            // Open mode (settings): double click (classic) or single click.
            panelSearchList.DoubleClick += (s, e) =>
            {
                if (settings.SearchOpenByDoubleClick) OpenPanelSearchResult(panelSearchList.SelectedIndex);
            };
            panelSearchList.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && !settings.SearchOpenByDoubleClick)
                    OpenPanelSearchResult(panelSearchList.IndexFromPoint(e.Location));
            };
            panelSearchList.RowMenuRequested = (p, i) =>
            {
                // Valid result row → the result menu; the past-search block,
                // the headers and the empty area below the rows → no menu at
                // all (never the form's "Settings" menu underneath).
                if (i < 0 || IsHeaderRow(i) || IsPastRow(i)) return;
                int ri = i - PastBlockOffset();
                if (ri < 0 || ri >= panelSearchResults.Count) return;
                panelSearchList.SelectedIndex = i;
                ShowPanelSearchContextMenu(ri, p);
            };
            panelSearchList.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    int i = panelSearchList.SelectedIndex;
                    if (i < 0) i = FirstSelectableSearchRow();
                    if (IsPastHeaderRow(i)) i = 1;             // Enter on "Прошлый поиск": first past row
                    else if (IsResultsHeaderRow(i)) i = PastBlockOffset(); // Enter on "Обычный поиск": first result
                    OpenPanelSearchResult(i);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
                {
                    // Arrow keys move the selection but never land on a
                    // section header.
                    int n = panelSearchList.Items.Count;
                    if (n > 0)
                    {
                        int step = e.KeyCode == Keys.Down ? 1 : -1;
                        int j = panelSearchList.SelectedIndex + step;
                        while (j >= 0 && j < n && IsHeaderRow(j)) j += step;
                        if (j >= 0 && j < n) panelSearchList.SelectedIndex = j;
                    }
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
                    if (e.KeyCode == Keys.ControlKey) InvalidateAllTiles(); // full-name preview while held
                    if (e.Control && e.KeyCode == Keys.F)
                    {
                        if (panelSearchBox != null) { panelSearchBox.Focus(); panelSearchBox.SelectAll(); }
                        e.SuppressKeyPress = true;
                    }
                }
                catch { }
            };
            this.KeyUp += (s, e) =>
            {
                try { if (e.KeyCode == Keys.ControlKey) InvalidateAllTiles(); }
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
                // No LoadTabs() here: the grid tiles follow the panel resize live
                // (ReflowGridTiles on the layout panel's Resize), so a rebuild at
                // drag end would only flash and re-pop the icons - the "rescale
                // jerk" after every move/resize of the window.
            };

            var formMenu = new ContextMenu();
            formMenu.MenuItems.Add(Loc.S("Settings"), (s, e) => OpenSettings());
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

            // Keep the mirrored tab named canonically ("Пуск" / "Start"); the rename
            // must not wait for the next scheduled sync.
            try
            {
                if (StartMenuSync.EnsureTabName(records)) records.Save(recordsPath);
            }
            catch (Exception ex) { AppLog.Write("Start tab rename", ex); }

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

            // The grips raise themselves above everything; the frame segments must
            // sit above them or the opaque grip strips hide the frame along the
            // window edges (the frame only re-raises on resize, which can happen
            // before this point).
            UpdateBorderOverlay();

            // The startup render runs inside the constructor; if the panel bounds
            // settle only past it (docking layout, first Show), realign the active
            // tab once - otherwise the first re-render would visibly rescale every
            // tile of it.
            this.Shown += (s, e) =>
            {
                try
                {
                    if (activeLayoutPanel != null && activeTabData != null)
                        ReflowGridTiles(activeLayoutPanel, activeTabData);
                }
                catch { }
                // Data files load in the constructor, before any UI: a notice
                // about a damaged file waits for this moment.
                try { DataGuard.FlushPending(this); }
                catch { }
            };

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
        // WS_MINIMIZEBOX: a borderless window without it never gets SC_MINIMIZE from
        // the taskbar, so clicking the panel's taskbar button did nothing — the
        // standard toggle (active -> minimize, click again -> restore) was dead.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                cp.Style |= 0x00020000;
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
            // The region is built from the same rounded path the frame overlay
            // paints (radius 15), so the aliased region edge sits exactly under the
            // painted border line. (CreateRoundRectRgn's "15" is an ellipse WIDTH,
            // i.e. a 7.5px corner — its stepped edge used to slice right through
            // the close button's hovered corner, well inside the painted arc.)
            using (var path = GetRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 15))
            {
                var oldRegion = this.Region;
                this.Region = new System.Drawing.Region(path);
                if (oldRegion != null) { try { oldRegion.Dispose(); } catch { } }
            }
            UpdateBorderOverlay();
        }

        // Decorative skin frame: a thin colored line following the rounded window
        // contour. One mouse-transparent overlay whose window region is EXACTLY
        // the painted band (window edge down to the border's inner edge), and
        // every pixel of that region is filled with the border color. The region
        // keeps hover tooltips working outside the band (a full-window
        // region-less overlay defeats the native tooltip tool tracking), and the
        // full fill means the band can never show blank or stale surface over
        // the content beneath - the pale stepped notch on the red close button
        // appeared when the clipped band was wider than the painted stroke.
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
                borderOverlay.UpdateFrameRegion();
                // Must sit above the edge grips: they are opaque strips along the
                // same edges and would otherwise hide the whole frame.
                borderOverlay.BringToFront();
                borderOverlay.Invalidate();
                if (borderOverlay.IsHandleCreated) borderOverlay.Update();
            }
            catch (Exception ex) { AppLog.Write("Border overlay", ex); }
        }

        private class BorderOverlay : Control
        {
            public Color SkinBorder = Color.Gray;
            public int BorderSize = 2;

            public BorderOverlay()
            {
                this.Enabled = false;
                this.TabStop = false;
                // Without UserPaint WinForms never calls OnPaint for this window.
                SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            }
            protected override CreateParams CreateParams
            {
                get { var cp = base.CreateParams; cp.ExStyle |= 0x20; return cp; } // WS_EX_TRANSPARENT
            }

            // The overlay window must cover ONLY the frame band, and the band must
            // be FILLED, not stroked: a stroke leaves the inner part of the clipped
            // band unpainted, and those pixels composite as blank surface over the
            // content beneath (the stepped notch in the rounded corners).
            internal void UpdateFrameRegion()
            {
                if (!IsHandleCreated) return;
                int bw = Math.Max(1, BorderSize);
                using (var outer = GetRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 15))
                using (var inner = GetRoundedRectPath(new Rectangle(bw, bw, Math.Max(1, Width - 1 - 2 * bw), Math.Max(1, Height - 1 - 2 * bw)), 15))
                {
                    var ring = new System.Drawing.Region(outer);
                    ring.Exclude(inner);
                    var old = this.Region;
                    this.Region = ring;
                    if (old != null) { try { old.Dispose(); } catch { } }
                }
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                UpdateFrameRegion();
            }
            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                UpdateFrameRegion();
            }
            protected override void OnPaintBackground(PaintEventArgs e) { }
            protected override void OnPaint(PaintEventArgs e)
            {
                try
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    int bw = Math.Max(1, BorderSize);
                    using (var outer = GetRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 15))
                    using (var inner = GetRoundedRectPath(new Rectangle(bw, bw, Math.Max(1, Width - 1 - 2 * bw), Math.Max(1, Height - 1 - 2 * bw)), 15))
                    {
                        outer.AddPath(inner, false);
                        outer.FillMode = System.Drawing.Drawing2D.FillMode.Alternate;
                        using (var brush = new SolidBrush(SkinBorder))
                            e.Graphics.FillPath(brush, outer);
                    }
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
            AddEdgeGrip(x, y, w, h, cur, hitTest, anchor, Rectangle.Empty);
        }

        // excludeLocal (grip-local coordinates) is cut out of the grip's window
        // region: the grips sit above the whole panel hierarchy, so an opaque grip
        // strip would cover the hovered close button's corner (and the first tab
        // chip) with background color - a stepped notch that only shows when the
        // button turns red. The excluded pixels simply belong to the controls
        // beneath; the grip keeps the remaining edge strip for resizing.
        private void AddEdgeGrip(int x, int y, int w, int h, Cursor cur, int hitTest, AnchorStyles anchor, Rectangle excludeLocal)
        {
            var p = new Panel { Size = new Size(w, h), Location = new Point(x, y), Cursor = cur, BackColor = bgColor, Anchor = anchor };
            if (!excludeLocal.IsEmpty)
            {
                var rgn = new Region(new Rectangle(0, 0, w, h));
                rgn.Exclude(excludeLocal);
                p.Region = rgn;
            }
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
            // The top edge stops before the min/max/close block (the right 105px):
            // those buttons own their corner.
            AddEdgeGrip(0, 0, Math.Max(0, Width - 105), 6, Cursors.SizeNS, 12, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            AddEdgeGrip(0, 0, 6, Height, Cursors.SizeWE, 10, AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom);
            AddEdgeGrip(Width - 6, 0, 6, Height, Cursors.SizeWE, 11, AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom);
            AddEdgeGrip(0, Height - 6, Width, 6, Cursors.SizeNS, 15, AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right);
            // Top-left corner: keep an L outside the first tab chip (it starts
            // around x=8, y=4).
            AddEdgeGrip(0, 0, 14, 14, Cursors.SizeNWSE, 13, AnchorStyles.Top | AnchorStyles.Left, new Rectangle(6, 2, 8, 12));
            // Top-right corner: keep an L outside the close button (its top-left
            // corner sits at grip-local (0, 3), the button reaches grip-local x=11).
            AddEdgeGrip(Width - 14, 0, 14, 14, Cursors.SizeNESW, 14, AnchorStyles.Top | AnchorStyles.Right, new Rectangle(0, 3, 11, 11));
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
                            ConfirmDialog.ShowInfo(this, Loc.S("Restore failed: ", "Восстановление не удалось: ") + err);
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
                    EnsureRealStartTile(); // re-label the built-in tile for the new language
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
                    if (sf.RunBackupNow) BackupManager.RunBackup(this, this.settings, BackupNotify.FailureOnly);
                    if (sf.RunSyncNow) StartMenuSync.Run(this, this.settings, true);
                    if (sf.RunSyncUserStartNow) ImportStartTiles();
                    if (sf.RunWelcomeAgain) RunFirstStartWelcome();
                    if (sf.RebuildIconsNow) RebuildAllIcons();
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

        // Settings → "Rebuild icons & paths": walks every item of every tab,
        // re-checks its path (missing files/folders are counted and reported),
        // drops the cached search metadata and the whole icon cache, then
        // re-renders — the extraction queue refills iconcache\ from scratch.
        private void RebuildAllIcons()
        {
            try
            {
                int items = 0;
                var missing = new List<string>();
                Action<List<ShortcutItem>> walk = null;
                walk = delegate(List<ShortcutItem> list)
                {
                    if (list == null) return;
                    foreach (var it in list)
                    {
                        items++;
                        string p = it.Path;
                        if (!string.IsNullOrEmpty(p) && !p.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                        {
                            bool ok;
                            try { ok = Directory.Exists(p) || File.Exists(p); }
                            catch { ok = false; }
                            if (!ok && missing.Count < 15)
                                missing.Add((it.Name ?? "?") + " — " + p);
                        }
                        PanelSearch.Invalidate(it);
                        if (it.Children != null) walk(it.Children);
                    }
                };
                foreach (var tab in records.Tabs) walk(tab.Items);

                int removed = IconExtractor.ClearAllCaches();
                tabNavigations.Clear();
                LoadTabs(); // re-render: icons re-extract into the fresh cache

                string msg = string.Format(
                    Loc.S("Checked: {0} · missing paths: {1} · cache files removed: {2}",
                          "Проверено: {0} · потерянных путей: {1} · файлов кеша удалено: {2}"),
                    items, missing.Count, removed);
                if (missing.Count > 0) msg += "\n\n" + string.Join("\n", missing.ToArray());
                ConfirmDialog.ShowInfo(this, msg);
                AppLog.Write("Icon rebuild: " + msg.Replace("\n", " | "));
            }
            catch (Exception ex) { AppLog.Write("RebuildAllIcons", ex); }
        }

        // ---- First start: welcome window and example tiles ----

        private void RunFirstStartWelcome()
        {
            try
            {
                bool wasFirstRun = !settings.FirstRunDone;
                bool createExamples = false;
                bool syncUserStart = false;
                using (var wf = new WelcomeForm(settings))
                {
                    wf.ShowDialog(this);
                    createExamples = wf.CreateExamples;
                    syncUserStart = wf.SyncUserStart;
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
                // The welcome's "close & sync" answer: the panel is up and the
                // tabs are loaded, the import can run right away.
                if (syncUserStart) ImportStartTiles();

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
                WarmSearchMeta(it);
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

        // Tray balloon from any thread-safe context; false when the icon is
        // hidden - a balloon would go nowhere then.
        public bool ShowBalloon(string text)
        {
            try
            {
                if (trayIcon == null || !trayIcon.Visible) return false;
                trayIcon.ShowBalloonTip(3000, Loc.S("Tilettes", "Плиточки"), text ?? "", ToolTipIcon.Info);
                return true;
            }
            catch { return false; }
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

        // ---------- Start tiles import ----------

        // "Sync user Start" (the Settings button and the welcome window's
        // close & sync answer). One PowerShell call exports the pinned Start
        // layout (a hard 20 s timeout wraps it - Export-StartLayout hangs
        // forever on systems with a broken Start layer), the tiles are
        // resolved and laid out into the "User Start" tab (Kind = null: no
        // sync ever touches it). No confirmation prompts: the action is
        // repeatable (it rebuilds the same tab) and the report at the end
        // tells what happened.
        private void ImportStartTiles()
        {
            string title = Loc.S("Sync user Start", "Синхронизировать пользовательский Пуск");

            Cursor = Cursors.WaitCursor;
            string layoutXml, nameCsv;
            bool exported;
            try { exported = StartLayoutImport.ExportLayout(out layoutXml, out nameCsv); }
            finally { Cursor = Cursors.Default; }
            if (!exported)
            {
                ConfirmDialog.ShowInfo(this, Loc.S("Could not export the Start layout - see log.txt.",
                    "Не удалось экспортировать раскладку Пуска — подробности в log.txt."), title);
                return;
            }

            StartLayoutImport.Result res;
            try
            {
                var names = StartLayoutImport.ParseNameCsv(nameCsv);
                var lnks = StartLayoutImport.BuildLnkIndex();
                res = StartLayoutImport.BuildTab(layoutXml, names, lnks, settings.GridColumns);
            }
            catch (Exception ex)
            {
                AppLog.Write("Start import: build", ex);
                ConfirmDialog.ShowInfo(this, Loc.S("Could not export the Start layout - see log.txt.",
                    "Не удалось экспортировать раскладку Пуска — подробности в log.txt."), title);
                return;
            }

            if (res == null || res.Tab == null || res.Added == 0)
            {
                ConfirmDialog.ShowInfo(this, Loc.S("No Start tiles found - Windows 11 or the Start menu layer is damaged.",
                    "Плитки не найдены — Windows 11 или слой Пуска повреждён."), title);
                return;
            }

            // Idempotent re-run: drop the previous import tab, rebuild it.
            int maxRow = 0;
            foreach (var t in records.Tabs) if (t.Row > maxRow) maxRow = t.Row;
            records.Tabs.RemoveAll(IsStartImportTab);
            res.Tab.Row = maxRow + 1;
            records.Tabs.Add(res.Tab);
            records.Save(recordsPath);
            OnDataExternallyChanged();
            WarmAllSearchMeta(true);
            AppLog.Write("Start import: tab '" + res.Tab.Name + "' rebuilt with " + res.Added + " tiles (" +
                res.Skipped + " skipped, " + res.Unresolved + " unresolved)");

            string msg = string.Format(Loc.S("Added: {0}", "Добавлено: {0}"), res.Added);
            if (res.Skipped > 0)
                msg += "\n" + string.Format(Loc.S("Skipped (pinned sites, tile folders): {0}",
                    "Пропущено (закреплённые сайты, папки плиток): {0}"), NameList(res.SkippedNames));
            if (res.Unresolved > 0)
                msg += "\n" + string.Format(Loc.S("Missing on this computer: {0}",
                    "Нет на этом компьютере: {0}"), NameList(res.UnresolvedNames));
            ConfirmDialog.ShowInfo(this, msg, title);
        }

        // Matches the import tab in any UI language it could have been baked
        // with, including the v1.1.3-test names ("Windows Start" /
        // "Пуск Windows") - a re-sync replaces that tab instead of adding a
        // second one.
        private static bool IsStartImportTab(TabData t)
        {
            if (t == null || string.IsNullOrEmpty(t.Name)) return false;
            return t.Name == "Windows Start" || t.Name == "Пуск Windows" ||
                   t.Name == "User Start" || t.Name == "Пользовательский Пуск" ||
                   t.Name == Loc.S(StartLayoutImport.TabNameEn, StartLayoutImport.TabNameRu);
        }

        // A comma list capped at six entries, then "and more...".
        private static string NameList(List<string> names)
        {
            const int max = 6;
            if (names.Count <= max) return string.Join(", ", names.ToArray());
            string[] head = new string[max];
            for (int i = 0; i < max; i++) head[i] = names[i];
            return string.Join(", ", head) + Loc.S(" and more...", " и др...");
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
            // A panel left on another virtual desktop must not drag the whole
            // view back there: Activate() follows the window and the shell
            // follows the activation. Re-home it to the current desktop BEFORE
            // any activation - and show it without activating first, because
            // even the plain showing (SW_SHOW) activates, and activating a
            // foreign-desktop window switches the view mid-flight.
            this.TopMost = true;
            // A freshly shown or restored window composites its stale (black)
            // surface for several frames until the first WM_PAINT lands - the
            // ugly black flash on the Win-key show. When the surface is stale
            // (panel hidden or minimized) the whole show therefore happens off
            // screen: hide the window first (so the desktop re-home cannot pop
            // a freshly recreated, unpainted window onto the screen - WinForms
            // recreates handles VISIBLE), cloak the freshly recreated handle
            // (cloaking is reserved for that case), only then show/restore it,
            // paint it synchronously with all children, and uncloak - the
            // first frame on screen is already the finished panel. A re-show
            // without a recreate keeps the previous surface and skips both.
            // Cloaking an already visible panel would just blink it, so the
            // visible path keeps the plain behavior. Systems without DWM
            // cloaking fall back to the plain show too.
            bool staleSurface = !this.Visible || this.WindowState == FormWindowState.Minimized;
            bool cloaked = false;
            System.Diagnostics.Stopwatch showSw = null;
            try
            {
                if (staleSurface)
                {
                    showSw = System.Diagnostics.Stopwatch.StartNew();
                    IntPtr handleBefore = this.Handle;
                    this.Visible = false;          // a re-home recreate (if any) stays invisible
                    EnsureOnCurrentDesktop();
                    bool recreated = this.Handle != handleBefore;
                    // Cloaking is reserved for a freshly recreated handle - the
                    // only case that needs it. A plain re-show composites the
                    // previous painted surface, and the cloak call itself is
                    // exotic enough for antivirus ML heuristics to weigh.
                    int cloakHr = -1;
                    if (recreated)
                    {
                        cloakHr = CloakWindow(this.Handle, true);
                        cloaked = cloakHr == 0;
                    }
                    this.Visible = true;           // a recreated handle comes back under the cloak
                    this.WindowState = FormWindowState.Normal;
                    // Finish the whole first paint now, off screen, synchronously
                    // (children included) so uncloaking presents a complete frame.
                    // Needed after a recreate only - with or without a working
                    // cloak; a plain re-show keeps its old surface.
                    int paintMs = -1;
                    if (recreated)
                    {
                        System.Diagnostics.Stopwatch paintSw = System.Diagnostics.Stopwatch.StartNew();
                        RedrawWindow(this.Handle, IntPtr.Zero, IntPtr.Zero,
                            RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_UPDATENOW);
                        paintMs = (int)paintSw.ElapsedMilliseconds;
                    }
                    showSw.Stop();
                    AppLog.Write(string.Format("Show: recreate={0} cloak={1} paint={2}ms total={3}ms",
                        recreated ? 1 : 0, cloakHr, paintMs, showSw.ElapsedMilliseconds));
                }
                else
                {
                    EnsureOnCurrentDesktop();
                }
                // A plain Activate() can be denied foreground rights when another
                // app owns the focus (this path runs from a hotkey or the Win-key
                // hook): the panel then stayed BEHIND. Raise it into the topmost
                // band for an instant and force the foreground; dropping TOPMOST
                // right away keeps it above the windows it was raised over without
                // pinning it permanently.
                this.Activate();
                ForceForeground();
            }
            finally
            {
                if (cloaked) CloakWindow(this.Handle, false);
                this.TopMost = false;
            }
            if (cloaked)
            {
                // The pre-uncloak paint can land before DWM binds the fresh
                // surface of a recreated window - the presented surface would
                // stay black until the next repaint. Paint once more, now that
                // the window is definitely on screen (same pixels when all is
                // well, a full repaint otherwise).
                try { this.Invalidate(true); this.Update(); } catch { }
            }
            try { if (trayIcon != null) trayIcon.Visible = settings.TrayIconAlways; } catch { }
        }

        // Re-homes the panel to the virtual desktop the user is on now. Whether
        // the panel is elsewhere is decided by comparing desktop GUIDs (public
        // COM): the current desktop's id comes from a throwaway probe window -
        // a freshly created window is born on the current desktop - while an
        // idle window keeps the id of the desktop it was left on. When the COM
        // layer is missing (stripped Windows images ship virtual desktops
        // without the public coclass) or refuses the move, the native window is
        // recreated: a newly created window is born on the current desktop.
        // WinForms carries every managed property over (bounds, topmost,
        // visibility); only the hotkey registrations are bound to the old
        // handle and are re-applied here.
        private void EnsureOnCurrentDesktop()
        {
            try
            {
                if (this.Handle == IntPtr.Zero) return;
                if (MoveToCurrentDesktop(this.Handle)) return;
                AppLog.Write("Desktop move fell back to handle recreation");
                bool wasTopMost = this.TopMost;
                RecreateHandle();
                this.TopMost = wasTopMost;
                ApplyHotkey();
                ApplyWinKeyHotkey();
            }
            catch (Exception ex) { AppLog.Write("EnsureOnCurrentDesktop", ex); }
        }

        // Moves `hwnd` to the current virtual desktop. Returns true when the
        // window is on the current desktop afterwards; false means "could not
        // tell or could not move" and the caller falls back. Systems without
        // the virtual-desktop COM class return true only when they also have no
        // virtual desktops (pre-Windows 10); a stripped Windows 10+ image falls
        // through to the caller's fallback.
        private static bool MoveToCurrentDesktop(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            IVirtualDesktopManager mgr = null;
            try { mgr = (IVirtualDesktopManager)new CVirtualDesktopManager(); }
            catch (Exception ex) { AppLog.Write("Desktop move: COM manager unavailable: " + ex.Message); mgr = null; }
            if (mgr == null)
            {
                // No COM class: virtual desktops can still exist (stripped
                // Windows 10+ images). Judge by the real build number from the
                // registry - Environment.OSVersion underreports without an app
                // manifest. Windows 10+: let the caller re-create the window;
                // older systems have no desktops at all - nothing to fix.
                try
                {
                    string build = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber", null) as string;
                    int b;
                    if (!string.IsNullOrEmpty(build) && int.TryParse(build, out b) && b >= 10240) return false;
                }
                catch { }
                return true;
            }
            NativeWindow probe = null;
            try
            {
                probe = CreateDesktopProbeWindow();
                if (probe == null) { AppLog.Write("Desktop move: probe window failed"); return false; }
                Guid mine, current;
                if (mgr.GetWindowDesktopId(hwnd, out mine) != 0)
                {
                    AppLog.Write("Desktop move: GetWindowDesktopId failed for the panel");
                    return false;
                }
                if (mgr.GetWindowDesktopId(probe.Handle, out current) != 0)
                {
                    AppLog.Write("Desktop move: GetWindowDesktopId failed for the probe");
                    return false;
                }
                if (mine == current) return true; // already on the user's desktop
                bool moved = mgr.MoveWindowToDesktop(hwnd, ref current) == 0;
                if (!moved) AppLog.Write("Desktop move: MoveWindowToDesktop refused");
                return moved;
            }
            catch (Exception ex) { AppLog.Write("Desktop move: " + ex.Message); return false; }
            finally
            {
                if (probe != null) { try { probe.ReleaseHandle(); } catch { } }
            }
        }

        // A zero-sized tool window at (0,0): never painted, never activated,
        // but a real top-level window - the shell associates it with the
        // desktop that is current at creation time. Shown no-activate once,
        // because the desktop assignment settles on visibility.
        private static NativeWindow CreateDesktopProbeWindow()
        {
            try
            {
                NativeWindow probe = new NativeWindow();
                CreateParams cp = new CreateParams();
                cp.ClassName = "STATIC";
                cp.Style = unchecked((int)0x80000000);   // WS_POPUP
                cp.ExStyle = 0x80 | 0x08000000;          // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
                cp.X = 0; cp.Y = 0; cp.Width = 0; cp.Height = 0;
                probe.CreateHandle(cp);
                ShowWindow(probe.Handle, 8);             // SW_SHOWNA
                return probe;
            }
            catch { return null; }
        }

        // SetForegroundWindow is restricted: the caller must already own the
        // foreground or have received the last input event. The Win-key hook
        // swallows that input on the foreground app's thread, so the plain call
        // may not stick — briefly attach our input queue to the foreground
        // thread, which legitimizes the call (classic launcher technique).
        private void ForceForeground()
        {
            try
            {
                IntPtr fore = GetForegroundWindow();
                if (fore == this.Handle) return;
                SetForegroundWindow(this.Handle);
                if (GetForegroundWindow() == this.Handle) return;
                if (fore == IntPtr.Zero) return;
                uint forePid;
                uint foreThread = GetWindowThreadProcessId(fore, out forePid);
                uint thisThread = GetCurrentThreadId();
                if (foreThread == 0 || foreThread == thisThread) return;
                if (AttachThreadInput(thisThread, foreThread, true))
                {
                    try { SetForegroundWindow(this.Handle); }
                    finally { AttachThreadInput(thisThread, foreThread, false); }
                }
            }
            catch { }
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
                // Empty box: no overlay at all — the normal window shows. The
                // past-search block appears only above real results, while typing.
                if (q.Length == 0) { HidePanelSearch(); return; }
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
                // The "past search" block fills in immediately, the regular
                // results replace the old ones when the worker comes back.
                FillPastSearchBlock(qLower);
                RebuildPanelSearchList();
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

        // Fills the top block of the search overlay: remembered query -> item
        // pairs whose QUERY matches what is being typed, best pairs first.
        // Respects the history setting; the panel's own folder groups are not
        // searchable anymore, so entries recorded for them are skipped.
        private void FillPastSearchBlock(string qLower)
        {
            panelSearchPast.Clear();
            try
            {
                if (!settings.SearchSaveHistory || string.IsNullOrEmpty(qLower)) return;
                var top = SearchHistoryStore.Top(60);
                panelSearchPast.AddRange(MatchPastQueries(top, qLower, PastSearchBlockMax));
            }
            catch { }
        }

        // Pure matching/ranking core (unit-tested): an entry survives when the
        // typed text occurs in the remembered query; prefix matches rank above
        // later occurrences, otherwise the store's score order is preserved.
        // qLower is lowercased defensively (callers normally pass it already).
        internal static List<SearchHistoryEntry> MatchPastQueries(List<SearchHistoryEntry> top, string qLower, int max)
        {
            var res = new List<SearchHistoryEntry>();
            if (top == null || string.IsNullOrEmpty(qLower) || max <= 0) return res;
            string needle = qLower.ToLowerInvariant();
            var scored = new List<KeyValuePair<int, SearchHistoryEntry>>();
            for (int i = 0; i < top.Count; i++)
            {
                var e = top[i];
                if (e == null || e.IsFolder) continue;
                string qq = (e.Query ?? "").ToLowerInvariant();
                int at = qq.IndexOf(needle, StringComparison.Ordinal);
                if (at < 0) continue;
                scored.Add(new KeyValuePair<int, SearchHistoryEntry>(at * 1000 + i, e));
            }
            scored.Sort(delegate(KeyValuePair<int, SearchHistoryEntry> a, KeyValuePair<int, SearchHistoryEntry> b)
            {
                return a.Key - b.Key;
            });
            for (int i = 0; i < scored.Count && i < max; i++) res.Add(scored[i].Value);
            return res;
        }

        // Search pool collector: descends into the panel's own folder groups so
        // their contents stay searchable, but never adds the group itself — the
        // groups ("Create Folder" containers, the overflow pseudo-folder) only
        // structure the panel and open by navigation, they are not launchable.
        private static void CollectAllItems(List<ShortcutItem> items, List<ShortcutItem> outList)
        {
            foreach (var it in items)
            {
                if (!it.IsFolder) outList.Add(it);
                if (it.Children != null && it.Children.Count > 0) CollectAllItems(it.Children, outList);
            }
        }

        // ---------- search metadata warm-up ----------
        // Metadata (name, paths, .lnk target, version info) is collected ONCE per
        // item, when the item appears: drag&drop, folder creation, example tiles,
        // Start Menu sync, plus a single pass over already-existing items at
        // startup. Nothing refreshes it afterwards - an edited item is invalidated
        // explicitly (PanelSearch.Invalidate) and rebuilds on the next search.
        internal static void WarmSearchMeta(ShortcutItem item)
        {
            if (item == null) return;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try { ulong a, b; PanelSearch.GetMask(item, out a, out b); } catch { }
            });
        }

        internal static void WarmSearchMeta(List<ShortcutItem> items)
        {
            if (items == null || items.Count == 0) return;
            var snapshot = new List<ShortcutItem>(items);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                foreach (var it in snapshot)
                {
                    try { ulong a, b; PanelSearch.GetMask(it, out a, out b); } catch { }
                }
            });
        }

        private bool metaWarmedOnce;

        // One background pass over every item of every tab. `force` is for bulk
        // add events (Start Menu sync copies); at startup it runs exactly once.
        internal void WarmAllSearchMeta(bool force)
        {
            if (!force && metaWarmedOnce) return;
            metaWarmedOnce = true;
            try
            {
                var snapshot = new List<ShortcutItem>();
                foreach (var tab in records.Tabs) CollectAllItems(tab.Items, snapshot);
                WarmSearchMeta(snapshot);
            }
            catch { }
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
                // Best-ranked first, so the survivor of a duplicate pair is the
                // one the ranking preferred. The final cap is deliberately
                // small: the most relevant 30 are what a launcher is for.
                var flat = new List<ShortcutItem>();
                for (int i = 0; i < found.Count && flat.Count < 400; i++) flat.Add(found[i].Value);
                foreach (var it in DedupeSearchResults(flat, 30))
                    panelSearchResults.Add(it);
                RebuildPanelSearchList();
                panelSearchStatus.Text = (Loc.IsRu ? "Найдено: " : "Found: ") + panelSearchResults.Count +
                    (Loc.IsRu ? "   ·   Enter — открыть, Esc — закрыть" : "   ·   Enter to open, Esc to close");
            }
            catch { }
        }

        // Full duplicates are collapsed: two results that resolve to the SAME
        // final target AND carry the SAME display name are the same tile twice
        // (added on two tabs, mirrored twice, ...). The same file under
        // DIFFERENT names stays — a rename-for-search shortcut pair (an admin
        // launcher next to the normal one) is deliberate, not a duplicate.
        internal static List<ShortcutItem> DedupeSearchResults(List<ShortcutItem> sorted, int max)
        {
            var res = new List<ShortcutItem>();
            if (sorted == null) return res;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < sorted.Count && res.Count < max; i++)
            {
                var it = sorted[i];
                if (it == null) continue;
                string target = "";
                try { target = PanelSearch.GetTarget(it); } catch { }
                if (string.IsNullOrEmpty(target)) target = it.Path ?? "";
                try { target = Path.GetFullPath(target); } catch { }
                string key = target.ToLowerInvariant() + "\n" + (it.Name ?? "").ToLowerInvariant();
                if (!seen.Add(key)) continue;
                res.Add(it);
            }
            return res;
        }

        // ---------- search overlay rows: [past-search block][results] ----------
        // Row layout in the listbox with a past block present: index 0 is the
        // "Прошлый поиск" section header, then the past rows, then the
        // "Обычный поиск" section header (with a divider line), then the
        // regular results. Without a past block the results start at 0 and
        // carry no header (as before).

        private int PastBlockOffset()
        {
            return panelSearchPast.Count > 0 ? panelSearchPast.Count + 2 : 0;
        }

        private bool IsPastHeaderRow(int rowIndex)
        {
            return panelSearchPast.Count > 0 && rowIndex == 0;
        }

        private bool IsResultsHeaderRow(int rowIndex)
        {
            return panelSearchPast.Count > 0 && rowIndex == panelSearchPast.Count + 1;
        }

        private bool IsHeaderRow(int rowIndex)
        {
            return IsPastHeaderRow(rowIndex) || IsResultsHeaderRow(rowIndex);
        }

        private bool IsPastRow(int rowIndex)
        {
            return panelSearchPast.Count > 0 && rowIndex > 0 && rowIndex <= panelSearchPast.Count;
        }

        private int FirstSelectableSearchRow()
        {
            return panelSearchPast.Count > 0 ? 1 : 0;
        }

        // One rebuild for every content change (past block and/or results):
        // refills the items, drops the stale measured layouts, clears selection.
        private void RebuildPanelSearchList()
        {
            panelSearchList.BeginUpdate();
            panelSearchList.Items.Clear();
            if (panelSearchPast.Count > 0)
            {
                panelSearchList.Items.Add(Loc.S("Past search", "Прошлый поиск")); // section header
                for (int i = 0; i < panelSearchPast.Count; i++)
                {
                    var he = panelSearchPast[i];
                    panelSearchList.Items.Add((he.Query ?? "") + "  →  " + (he.Name ?? ""));
                }
                panelSearchList.Items.Add(Loc.S("Regular search", "Обычный поиск")); // section header
            }
            for (int i = 0; i < panelSearchResults.Count; i++)
                panelSearchList.Items.Add(panelSearchResults[i].Name);
            panelSearchList.EndUpdate();
            panelSearchList.ClearSelected();
            panelSearchList.Invalidate();
            InvalidatePreparedSearchRows();
        }

        private void HidePanelSearch()
        {
            panelSearchGen++;
            if (panelSearchOverlay != null) panelSearchOverlay.Visible = false;
            panelSearchActive = false;
            if (panelSearchResults != null) panelSearchResults.Clear();
            panelSearchPast.Clear();
            panelSearchVariants = new List<string>();
            InvalidatePreparedSearchRows();
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

        // Dispatches by LIST ROW: a section header (inert), a past-search row
        // (opens the remembered item) or a regular result row.
        private void OpenPanelSearchResult(int row)
        {
            if (IsHeaderRow(row)) return;
            if (IsPastRow(row))
            {
                OpenPastSearchEntry(panelSearchPast[row - 1]);
                return;
            }
            OpenResultIndex(row - PastBlockOffset());
        }

        // Clicking a "Прошлый поиск" row re-opens the item that query led to
        // last time; the pair is re-recorded under its own query so its rank
        // keeps growing (the currently typed text is NOT recorded for it).
        private void OpenPastSearchEntry(SearchHistoryEntry he)
        {
            if (he == null || string.IsNullOrEmpty(he.Path)) return;
            ClearPanelSearch();
            try
            {
                if (settings.SearchSaveHistory)
                {
                    SearchHistoryStore.Record(he.Query, he.Name, he.Path, he.IsFolder);
                    SearchHistoryStore.Save();
                }
            }
            catch { }
            if (he.IsFolder)
            {
                var it = new ShortcutItem();
                it.Name = he.Name;
                it.Path = he.Path;
                it.IsFolder = true;
                var nav = tabNavigations[activeTabData];
                nav.Push(it);
                RenderCurrentFolder(activeLayoutPanel, activeTabData);
            }
            else
            {
                LaunchItem(he.Path);
            }
        }

        private void OpenResultIndex(int ri)
        {
            if (ri < 0 || ri >= panelSearchResults.Count) return;
            var it = panelSearchResults[ri];
            string q = panelSearchBox != null && panelSearchBox.Text != null ? panelSearchBox.Text.Trim() : "";
            ClearPanelSearch();
            // Remember "query -> opened item" so the same pair ranks higher next time.
            if (settings.SearchSaveHistory && !string.IsNullOrEmpty(it.Path))
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

        // Right-click menu of a panel-search result row (index into
        // panelSearchResults): besides opening the result it can jump into the
        // mini explorer at the file's original folder, or reveal that folder in
        // Explorer.
        private void ShowPanelSearchContextMenu(int i, Point location)
        {
            if (i < 0 || i >= panelSearchResults.Count) return;
            var it = panelSearchResults[i];
            var m = new ContextMenu();
            m.MenuItems.Add(Loc.S("Open"), (s2, e2) => OpenResultIndex(i));
            m.MenuItems.Add(Loc.S("Open in Mini Explorer"), (s2, e2) => OpenSearchResultInMiniExplorer(it));
            m.MenuItems.Add(Loc.S("Open containing folder"), (s2, e2) => RevealInExplorer(SearchResultRevealPath(it)));
            m.Show(panelSearchList, location);
        }

        // The path a search result refers to for "open the original folder": the
        // resolved target for shortcuts (the actual app/folder), the item path
        // otherwise.
        private static string SearchResultRevealPath(ShortcutItem it)
        {
            try
            {
                string t = PanelSearch.GetTarget(it);
                if (!string.IsNullOrEmpty(t)) return t;
            }
            catch { }
            return it == null ? null : it.Path;
        }

        // Mini explorer at a search result's original folder: folder results
        // navigate into the folder itself, files/folders reveal their parent
        // with the entry selected.
        private void OpenSearchResultInMiniExplorer(ShortcutItem it)
        {
            string p = SearchResultRevealPath(it);
            if (string.IsNullOrEmpty(p)) return;
            try
            {
                bool isDir;
                try { isDir = Directory.Exists(p); }
                catch { isDir = false; }
                if (isDir)
                {
                    OpenMiniExplorerAt(p, null);
                    return;
                }
                string dir = null;
                try { dir = Path.GetDirectoryName(p); }
                catch { }
                if (string.IsNullOrEmpty(dir)) return;
                OpenMiniExplorerAt(dir, Path.GetFileName(p));
            }
            catch (Exception ex) { AppLog.Write("OpenSearchResultInMiniExplorer", ex); }
        }

        // Creates (or reuses) the mini explorer window at `folder`, optionally
        // selecting the child entry named `selectName`.
        private void OpenMiniExplorerAt(string folder, string selectName)
        {
            try
            {
                bool exists;
                try { exists = Directory.Exists(folder); }
                catch { exists = false; }
                if (!exists) return;
                if (miniExplorer == null || miniExplorer.IsDisposed)
                    miniExplorer = new MiniExplorerForm(folder, settings, settingsPath);
                miniExplorer.OpenInTabSelect(folder, selectName);
                // Shown WITHOUT owner: an owned window is pinned above its owner,
                // which made the mini explorer impossible to send behind the panel.
                if (!miniExplorer.Visible) miniExplorer.Show();
                else
                {
                    // Left open on another virtual desktop: activating it as is
                    // would drag the view back there - re-home it first.
                    MoveToCurrentDesktop(miniExplorer.Handle);
                    miniExplorer.Activate();
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("OpenMiniExplorerAt", ex);
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
                    int first = FirstSelectableSearchRow();
                    if (panelSearchList.Items.Count > first && panelSearchList.SelectedIndex < first)
                        panelSearchList.SelectedIndex = first;
                }
                catch { }
                e.SuppressKeyPress = true;
            }
        }

        // Measuring a result row is expensive (GetTarget may resolve a .lnk through
        // COM; FitTail/FitEnd are binary searches of MeasureText). All of it depends
        // only on (item, query variants, font, row width), so it is computed once on
        // the row's first paint and reused for every hover/scroll repaint.
        private void InvalidatePreparedSearchRows()
        {
            panelSearchPrepared.Clear();
            panelSearchPreparedWidth = -1;
            panelSearchPreparedLeft = -1;
        }

        private PanelSearchRowLayout PreparePanelSearchRow(int index, int width, int boundsLeft)
        {
            if (width != panelSearchPreparedWidth || boundsLeft != panelSearchPreparedLeft)
            {
                panelSearchPrepared.Clear();
                panelSearchPreparedWidth = width;
                panelSearchPreparedLeft = boundsLeft;
            }
            PanelSearchRowLayout r;
            if (panelSearchPrepared.TryGetValue(index, out r)) return r;

            r = new PanelSearchRowLayout();
            var it = panelSearchResults[index];
            Font f = this.Font;
            int left = boundsLeft + 32;

            // Right column: for shortcuts both paths are shown (lnk -> target), the
            // query can match either of them; when it does not fit, only its tail.
            string path = it.Path ?? "";
            string targetPath = "";
            try { if (path.ToLowerInvariant().EndsWith(".lnk")) targetPath = PanelSearch.GetTarget(it); }
            catch { }
            r.PathCombo = path;
            if (targetPath.Length > 0 && !string.Equals(targetPath, path, StringComparison.OrdinalIgnoreCase))
                r.PathCombo = path + "  →  " + targetPath;
            int maxPathW = (int)(width * 0.45);
            r.PathDisplay = r.PathCombo.Length > 0 ? UiText.FitTail(r.PathCombo, f, maxPathW) : "";
            r.PathW = r.PathDisplay.Length > 0 ? TextRenderer.MeasureText(r.PathDisplay, f).Width : 0;
            int pathX = boundsLeft + width - 8 - r.PathW;

            // Middle column: the user description
            r.Desc = PanelSearch.GetDescription(it);
            r.HasDesc = !string.IsNullOrEmpty(r.Desc);

            // Left column: name (the query match is highlighted)
            int hlStart, hlLen;
            bool nameHl = UiText.FindHighlight((it.Name ?? "").ToLowerInvariant(), panelSearchVariants, out hlStart, out hlLen);
            int maxNameW = (r.HasDesc ? boundsLeft + (int)(width * 0.38) - 24 : pathX - 16) - left;
            r.NameDisplay = it.Name;
            if (TextRenderer.MeasureText(r.NameDisplay, f).Width > maxNameW && maxNameW > 30)
                r.NameDisplay = UiText.FitEnd(it.Name, f, maxNameW);
            UiText.ClampHighlight(r.NameDisplay, ref nameHl, ref hlStart, ref hlLen);
            r.NameHlStart = nameHl ? hlStart : -1;
            r.NameHlLen = nameHl ? hlLen : 0;

            if (r.HasDesc)
            {
                int descX = Math.Max(left + TextRenderer.MeasureText(r.NameDisplay, f).Width + 24,
                                     boundsLeft + (int)(width * 0.38));
                r.DescX = descX;
                int descSpace = pathX - 16 - descX;
                r.DescSpace = descSpace;
                if (descSpace > 40)
                {
                    int dStart, dLen;
                    bool descHl = UiText.FindHighlight(r.Desc.ToLowerInvariant(), panelSearchVariants, out dStart, out dLen);
                    string descDisplay = r.Desc;
                    if (TextRenderer.MeasureText(r.Desc, f).Width > descSpace)
                        descDisplay = UiText.FitEnd(r.Desc, f, descSpace);
                    UiText.ClampHighlight(descDisplay, ref descHl, ref dStart, ref dLen);
                    r.DescDisplay = descDisplay;
                    r.DescHlStart = descHl ? dStart : -1;
                    r.DescHlLen = descHl ? dLen : 0;
                }
            }

            if (r.PathDisplay.Length > 0)
            {
                int pStart, pLen;
                bool pathHl = UiText.FindHighlight(r.PathCombo.ToLowerInvariant(), panelSearchVariants, out pStart, out pLen);
                int cut = r.PathCombo.Length - (r.PathDisplay.Length - 1); // chars hidden by the leading "…"
                r.PathTailHl = pathHl && pStart >= cut;
                r.PathHlStart = r.PathTailHl ? pStart - cut : -1;
                r.PathHlLen = r.PathTailHl ? pLen : 0;
            }

            r.TextH = TextRenderer.MeasureText("Ag", f).Height;
            panelSearchPrepared[index] = r;
            return r;
        }

        private void PanelSearchList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            if (IsPastHeaderRow(e.Index)) { DrawSectionHeader(e, Loc.S("Past search", "Прошлый поиск"), false); return; }
            if (IsResultsHeaderRow(e.Index)) { DrawSectionHeader(e, Loc.S("Regular search", "Обычный поиск"), true); return; }
            if (IsPastRow(e.Index)) { DrawPastSearchRow(e, panelSearchPast[e.Index - 1]); return; }
            int ri = e.Index - PastBlockOffset();
            if (ri < 0 || ri >= panelSearchResults.Count) return;
            var it = panelSearchResults[ri];
            var g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var back = new SolidBrush(sel ? hoverColor : bgColor))
                g.FillRectangle(back, e.Bounds);
            Bitmap ic = SearchRowIcon(it.IsFolder, it.Path);
            int iconY = e.Bounds.Top + Math.Max(3, (panelSearchList.ItemHeight - 16) / 2);
            if (ic != null) g.DrawImage(ic, new Rectangle(e.Bounds.Left + 8, iconY, 16, 16));

            Font f = this.Font;
            Color acc = settings.IsLightTheme ? Color.FromArgb(0, 102, 204) : Color.FromArgb(96, 180, 255);
            bool light = settings.IsLightTheme;
            Color subColor = settings.IsLightTheme ? Color.FromArgb(120, 120, 125) : Color.FromArgb(150, 150, 155);
            var r = PreparePanelSearchRow(ri, e.Bounds.Width, e.Bounds.Left);
            int ty = e.Bounds.Top + Math.Max(4, (panelSearchList.ItemHeight - r.TextH) / 2);
            int left = e.Bounds.Left + 32;
            int pathX = e.Bounds.Right - 8 - r.PathW;

            UiText.DrawHighlighted(g, r.NameDisplay, r.NameHlStart, r.NameHlLen, f, new Point(left, ty), textColor, acc, light);

            if (r.HasDesc && r.DescDisplay != null)
                UiText.DrawHighlighted(g, r.DescDisplay, r.DescHlStart, r.DescHlLen, f, new Point(r.DescX, ty), subColor, acc, light);

            if (r.PathDisplay.Length > 0)
            {
                if (r.PathTailHl)
                    UiText.DrawHighlighted(g, r.PathDisplay, r.PathHlStart, r.PathHlLen, f, new Point(pathX, ty), subColor, acc, light);
                else
                    TextRenderer.DrawText(g, r.PathDisplay, f, new Point(pathX, ty), subColor);
            }
        }

        // Icon for a search row (regular results AND past-search rows): cached
        // by folder/file + path. Slow (network) sources are fetched on a worker
        // thread, cached by path and the list repaints when the icon arrives.
        private Bitmap SearchRowIcon(bool isFolder, string path)
        {
            Bitmap ic = null;
            try
            {
                string key = ((isFolder ? "d:" : "f:") + (path ?? "")).ToLowerInvariant();
                // The icon cache is session-long and small rows add up: start over
                // instead of growing without bound.
                if (panelSearchIcons.Count > 600)
                {
                    foreach (var b in panelSearchIcons.Values) { try { b.Dispose(); } catch { } }
                    panelSearchIcons.Clear();
                }
                if (!panelSearchIcons.TryGetValue(key, out ic))
                {
                    if (IsSlowIconPath(path))
                    {
                        string gkey = key, gpath = path;
                        int gen = panelSearchGen;
                        System.Threading.ThreadPool.QueueUserWorkItem(delegate
                        {
                            Bitmap small = null;
                            try
                            {
                                Image big = null;
                                try { if (!string.IsNullOrEmpty(gpath)) big = IconExtractor.GetIconAuto(gpath, false); } catch { }
                                if (big != null)
                                {
                                    small = new Bitmap(16, 16);
                                    using (var gg = Graphics.FromImage(small))
                                    {
                                        gg.Clear(Color.Transparent);
                                        IconExtractor.DrawFit(gg, big, new Rectangle(0, 0, 16, 16));
                                    }
                                    big.Dispose();
                                }
                            }
                            catch { }
                            try
                            {
                                if (this.IsDisposed) { if (small != null) small.Dispose(); return; }
                                this.BeginInvoke((MethodInvoker)delegate
                                {
                                    try
                                    {
                                        if (this.IsDisposed) { if (small != null) small.Dispose(); return; }
                                        Bitmap old;
                                        if (panelSearchIcons.TryGetValue(gkey, out old) && old != null) old.Dispose();
                                        panelSearchIcons[gkey] = small;
                                        if (gen == panelSearchGen) panelSearchList.Invalidate();
                                    }
                                    catch { if (small != null) try { small.Dispose(); } catch { } }
                                });
                            }
                            catch { if (small != null) try { small.Dispose(); } catch { } }
                        });
                        ic = null;
                    }
                    else
                    {
                        Image big = null;
                        try { if (!string.IsNullOrEmpty(path)) big = IconExtractor.GetIconAuto(path, false); }
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
                }
            }
            catch { }
            return ic;
        }

        // Section header of the two search blocks: dim caption, never drawn as
        // selected even if the selection mechanically lands on it. The divider
        // variant carries the thin separating line between the blocks.
        private void DrawSectionHeader(DrawItemEventArgs e, string caption, bool divider)
        {
            using (var back = new SolidBrush(bgColor))
                e.Graphics.FillRectangle(back, e.Bounds);
            Color subColor = settings.IsLightTheme ? Color.FromArgb(120, 120, 125) : Color.FromArgb(150, 150, 155);
            if (divider)
            {
                Color lineColor = settings.IsLightTheme ? Color.FromArgb(208, 208, 212) : Color.FromArgb(58, 58, 62);
                using (var pen = new Pen(lineColor))
                    e.Graphics.DrawLine(pen, e.Bounds.Left + 8, e.Bounds.Top + 3, e.Bounds.Right - 8, e.Bounds.Top + 3);
                TextRenderer.DrawText(e.Graphics, caption, this.Font,
                    new Point(e.Bounds.Left + 8, e.Bounds.Top + 5), subColor);
            }
            else
            {
                TextRenderer.DrawText(e.Graphics, caption, this.Font,
                    new Point(e.Bounds.Left + 8, e.Bounds.Top + 4), subColor);
            }
        }

        // A past-search row: the remembered item's regular app icon, the query
        // (highlighted with the typed text) and the item name it last opened.
        private void DrawPastSearchRow(DrawItemEventArgs e, SearchHistoryEntry he)
        {
            if (he == null) return;
            var g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var back = new SolidBrush(sel ? hoverColor : bgColor))
                g.FillRectangle(back, e.Bounds);
            Bitmap ic = SearchRowIcon(he.IsFolder, he.Path);
            int iconY = e.Bounds.Top + Math.Max(3, (panelSearchList.ItemHeight - 16) / 2);
            if (ic != null) g.DrawImage(ic, new Rectangle(e.Bounds.Left + 8, iconY, 16, 16));

            Font f = this.Font;
            Color acc = settings.IsLightTheme ? Color.FromArgb(0, 102, 204) : Color.FromArgb(96, 180, 255);
            bool light = settings.IsLightTheme;
            Color subColor = settings.IsLightTheme ? Color.FromArgb(120, 120, 125) : Color.FromArgb(150, 150, 155);
            int ty = e.Bounds.Top + Math.Max(4, (panelSearchList.ItemHeight - TextRenderer.MeasureText("Ag", f).Height) / 2);

            int maxW = e.Bounds.Width - 32 - 8;
            string query = he.Query ?? "";
            string qPart = query;
            bool hl; int hs, hlen;
            hl = UiText.FindHighlight(query.ToLowerInvariant(), panelSearchVariants, out hs, out hlen);
            if (TextRenderer.MeasureText(qPart, f).Width > maxW && maxW > 30)
            {
                qPart = UiText.FitTail(qPart, f, maxW);
                UiText.ClampHighlight(qPart, ref hl, ref hs, ref hlen);
            }
            UiText.DrawHighlighted(g, qPart, hl ? hs : -1, hl ? hlen : 0, f,
                new Point(e.Bounds.Left + 32, ty), textColor, acc, light);

            string name = he.Name ?? "";
            if (name.Length > 0)
            {
                int qw = TextRenderer.MeasureText(qPart, f).Width;
                string arrow = "  →  ";
                int aw = TextRenderer.MeasureText(arrow, f).Width;
                int nameW = e.Bounds.Width - 32 - qw - aw - 8;
                if (nameW > 20)
                {
                    string nPart = name;
                    if (TextRenderer.MeasureText(nPart, f).Width > nameW)
                        nPart = UiText.FitTail(nPart, f, nameW);
                    TextRenderer.DrawText(g, arrow + nPart, f, new Point(e.Bounds.Left + 32 + qw, ty), subColor);
                }
            }
        }

        // Applies the "search results" font from the settings and syncs the row height.
        private void ApplySearchListFont()
        {
            try
            {
                panelSearchList.Font = Settings.MakeFont(panelSearchList.Font.FontFamily.Name, Math.Max(7, Math.Min(30, settings.SearchResultsFontSize)));
                panelSearchList.ItemHeight = Math.Max(26, TextRenderer.MeasureText("Ag", panelSearchList.Font).Height + 10);
                InvalidatePreparedSearchRows();
            }
            catch { }
        }
        // Hovering a result row shows a tooltip with the full path and description;
        // past-search rows show their query and remembered path.
        private void PanelSearchList_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (itemTip == null) return;
                int i = panelSearchList.IndexFromPoint(e.Location);
                string t = "";
                if (IsHeaderRow(i))
                {
                    t = "";
                }
                else if (IsPastRow(i))
                {
                    var he = panelSearchPast[i - 1];
                    if (he != null)
                        t = (he.Query ?? "") + "\n" + (he.Path ?? "");
                }
                else
                {
                    int ri = i - PastBlockOffset();
                    if (ri >= 0 && ri < panelSearchResults.Count)
                    {
                        var it = panelSearchResults[ri];
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
                trayIcon.ShowBalloonTip(2500, Loc.S("Tilettes", "Плиточки"), Loc.S("Hotkey ", "Комбинация ") + settings.HotkeyShow + Loc.S(" is already in use by another program.", " уже занята другой программой."), ToolTipIcon.Warning);
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

#pragma warning disable 0649
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
#pragma warning restore 0649

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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

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

        // Clicks on the physical Start button (the screen corner): with the Win
        // capture on, a plain left click opens the panel too. A WH_MOUSE_LL hook
        // compares the click point against the taskbar's Start button window
        // (class "Start" inside Shell_TrayWnd); modifier clicks and the right
        // button (the Win+X menu) pass through. Fail-soft: when the button
        // window cannot be found, only the keyboard capture stays active.
        private IntPtr startMouseHook = IntPtr.Zero;
        private LowLevelHookProc mouseHookRef;
        private bool startClickSwallowed;

        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSE_LDOWN_LL = 0x0201;
        private const int WM_MOUSE_LUP_LL = 0x0202;

#pragma warning disable 0649
        private struct MSLLHOOKSTRUCT
        {
            public System.Drawing.Point pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
#pragma warning restore 0649

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "FindWindowW", SetLastError = true)]
        private static extern IntPtr FindWindowNative(string cls, string title);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "FindWindowExW")]
        private static extern IntPtr FindWindowExNative(IntPtr parent, IntPtr after, string cls, string title);

        private struct Win32Rect { public int L, T, R, B; }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowRect")]
        private static extern bool GetWindowRectNative(IntPtr h, out Win32Rect r);

        private static Rectangle StartButtonRect()
        {
            try
            {
                IntPtr tray = FindWindowNative("Shell_TrayWnd", null);
                if (tray == IntPtr.Zero) return Rectangle.Empty;
                Win32Rect tr;
                if (!GetWindowRectNative(tray, out tr)) return Rectangle.Empty;
                int tw = tr.R - tr.L, th = tr.B - tr.T;
                if (tw <= 0 || th <= 0) return Rectangle.Empty;
                // The button is a window of class "Start" on some builds (exact
                // rect); on others it has no HWND at all — then derive it as the
                // leftmost square of the primary taskbar, one taskbar-thickness
                // side wide (+ the small padding before the next tray button).
                IntPtr btn = FindWindowExNative(tray, IntPtr.Zero, "Start", null);
                if (btn == IntPtr.Zero) btn = FindStartButtonNested(tray);
                if (btn != IntPtr.Zero)
                {
                    Win32Rect r;
                    if (GetWindowRectNative(btn, out r) && r.R > r.L && r.B > r.T)
                        return new Rectangle(r.L, r.T, r.R - r.L, r.B - r.T);
                }
                if (tw >= th) return new Rectangle(tr.L, tr.T, Math.Min(tw, th + 8), th);
                return new Rectangle(tr.L, tr.T, tw, Math.Min(th, tw + 8));
            }
            catch (Exception ex)
            {
                // Throttled: this runs on every mouse click system-wide.
                if (!startRectErrLogged)
                {
                    startRectErrLogged = true;
                    AppLog.Write("Start button rect lookup failed, click capture may be off", ex);
                }
                return Rectangle.Empty;
            }
        }

        private static bool startRectErrLogged;

        // Depth-first search for the class-"Start" window inside the taskbar tree.
        private static IntPtr FindStartButtonNested(IntPtr parent)
        {
            IntPtr child = IntPtr.Zero;
            do
            {
                child = FindWindowExNative(parent, child, null, null);
                if (child == IntPtr.Zero) return IntPtr.Zero;
                var sb = new System.Text.StringBuilder(64);
                if (GetClassNameNative(child, sb, 64) > 0 && sb.ToString() == "Start") return child;
                IntPtr deep = FindStartButtonNested(child);
                if (deep != IntPtr.Zero) return deep;
            } while (true);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "GetClassNameW")]
        private static extern int GetClassNameNative(IntPtr h, System.Text.StringBuilder sb, int max);

        private IntPtr StartMouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    int msg = wParam.ToInt32();
                    if (msg == WM_MOUSE_LDOWN_LL || msg == WM_MOUSE_LUP_LL)
                    {
                        var m = (MSLLHOOKSTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                        Rectangle r = StartButtonRect();
                        bool modified = (Control.ModifierKeys & (Keys.Control | Keys.Shift | Keys.Alt)) != 0;
                        bool inside = !r.IsEmpty && r.Contains(m.pt);
                        if (msg == WM_MOUSE_LDOWN_LL)
                        {
                            if (inside && !modified)
                            {
                                startClickSwallowed = true;
                                try { BeginInvoke((MethodInvoker)delegate { ToggleByWinKey(); }); }
                                catch { }
                                return (IntPtr)1;
                            }
                            startClickSwallowed = false;
                        }
                        else if (startClickSwallowed)
                        {
                            // pair of a swallowed down: eat the orphan up as well
                            startClickSwallowed = false;
                            return (IntPtr)1;
                        }
                    }
                }
            }
            catch { }
            return CallNextHookEx(startMouseHook, nCode, wParam, lParam);
        }

        // The built-in tile that opens the real Start menu (its path is a virtual
        // protocol, see LaunchItem).
        private const string StartMenuPath = "startmenu:";

        // The four built-in power tiles: virtual "power:" paths the launch
        // dispatcher (LaunchItem) turns into the system power actions. Never
        // real files, so every file-path pipeline must route them out.
        private const string PowerShutdownPath = "power:shutdown";
        private const string PowerRestartPath = "power:restart";
        private const string PowerSleepPath = "power:sleep";
        private const string PowerHibernatePath = "power:hibernate";

        private static bool IsPowerTilePath(string path)
        {
            return path != null && path.StartsWith("power:", StringComparison.OrdinalIgnoreCase);
        }

        private static Image startMenuIcon;
        private static readonly Dictionary<string, Image> powerIcons = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        // The Start glyph: four rounded squares in the Windows accent blue, drawn
        // once and shared (crisp at any tile size, no shell resource to hunt for).
        internal static Image GetStartMenuIcon()
        {
            if (startMenuIcon == null)
            {
                try
                {
                    int s = 128;
                    var bmp = new Bitmap(s, s);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        int q = s * 40 / 100;        // quadrant size
                        int gap = (s - 2 * q) / 3;   // gap between and around
                        using (var br = new SolidBrush(Color.FromArgb(0, 120, 215)))
                        {
                            g.FillRectangle(br, gap, gap, q, q);
                            g.FillRectangle(br, s - gap - q, gap, q, q);
                            g.FillRectangle(br, gap, s - gap - q, q, q);
                            g.FillRectangle(br, s - gap - q, s - gap - q, q, q);
                        }
                    }
                    startMenuIcon = bmp;
                }
                catch { }
                if (startMenuIcon == null)
                {
                    try { startMenuIcon = SystemIcons.WinLogo.ToBitmap(); } catch { }
                }
            }
            try { return (Image)startMenuIcon.Clone(); }
            catch { return SystemIcons.Application.ToBitmap(); }
        }

        // The power-tile glyphs, drawn once and shared (the Start-glyph
        // technique): plain geometric signs in the Windows accent blue. Drawn
        // by hand instead of font glyphs so the power sign looks the same on
        // every system (Segoe UI Symbol gained U+23FB only in Windows 8).
        internal static Image GetPowerIcon(string path)
        {
            Image cached;
            lock (powerIcons)
            {
                if (powerIcons.TryGetValue(path ?? "", out cached) && cached != null) return (Image)cached.Clone();
            }
            Image made = null;
            try
            {
                int s = 128;
                var bmp = new Bitmap(s, s);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(Color.FromArgb(0, 120, 215), s / 9f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        int d = s * 66 / 100;               // ring diameter
                        int o = (s - d) / 2;                // ring offset
                        if (string.Equals(path, PowerShutdownPath, StringComparison.OrdinalIgnoreCase))
                        {
                            // The classic power sign: a ring with a gap on
                            // top and a vertical stem through the gap.
                            g.DrawArc(pen, o, o, d, d, -45, 270);
                            g.DrawLine(pen, s / 2, s * 10 / 100, s / 2, s * 46 / 100);
                        }
                        else if (string.Equals(path, PowerRestartPath, StringComparison.OrdinalIgnoreCase))
                        {
                            // Circular arrow: a 300-degree arc plus a
                            // triangular head at its clockwise end.
                            g.DrawArc(pen, o, o, d, d, -60, 300);
                            double endRad = 240.0 * Math.PI / 180.0; // -60 + 300
                            double ex = s / 2.0 + d / 2.0 * Math.Cos(endRad);
                            double ey = s / 2.0 + d / 2.0 * Math.Sin(endRad);
                            double tx = -Math.Sin(endRad), ty = Math.Cos(endRad); // clockwise tangent
                            float ah = s * 15 / 100f;       // arrowhead size
                            var pts = new[]
                            {
                                new PointF((float)(ex + tx * ah), (float)(ey + ty * ah)),
                                new PointF((float)(ex - tx * ah * 0.35 - ty * ah * 0.6), (float)(ey - ty * ah * 0.35 + tx * ah * 0.6)),
                                new PointF((float)(ex - tx * ah * 0.35 + ty * ah * 0.6), (float)(ey - ty * ah * 0.35 - tx * ah * 0.6))
                            };
                            using (var br = new SolidBrush(pen.Color)) g.FillPolygon(br, pts);
                        }
                        else if (string.Equals(path, PowerSleepPath, StringComparison.OrdinalIgnoreCase))
                        {
                            // Crescent moon: a full disc minus an offset disc.
                            using (var moon = new System.Drawing.Drawing2D.GraphicsPath())
                            using (var cut = new System.Drawing.Drawing2D.GraphicsPath())
                            {
                                moon.AddEllipse(s * 18 / 100, s * 12 / 100, s * 64 / 100, s * 64 / 100);
                                cut.AddEllipse(s * 40 / 100, s * 2 / 100, s * 64 / 100, s * 64 / 100);
                                using (var reg = new Region(moon))
                                {
                                    reg.Exclude(cut);
                                    using (var br = new SolidBrush(pen.Color)) g.FillRegion(br, reg);
                                }
                            }
                        }
                        else // hibernate
                        {
                            using (var f = new Font("Segoe UI", s * 34 / 100f, FontStyle.Bold, GraphicsUnit.Pixel))
                            using (var br = new SolidBrush(pen.Color))
                            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("Zz", f, br, new RectangleF(0, 0, s, s), sf);
                            }
                        }
                    }
                }
                made = bmp;
            }
            catch { }
            lock (powerIcons) { powerIcons[path ?? ""] = made; }
            return made == null ? null : (Image)made.Clone();
        }

        // Adds the four built-in power tiles (shut down, restart, sleep,
        // hibernate) to the mirrored Start tab. Created once as a whole set:
        // a tile the user deleted stays away until all four are gone (a fresh
        // install or a brand-new Start tab receives the full set again).
        // Names follow the current language, like the "Open Start menu" tile.
        // Called from LoadTabs, so a language switch re-labels the existing
        // tiles for free.
        private void EnsurePowerTiles()
        {
            try
            {
                TabData startTab = null;
                foreach (var t in records.Tabs)
                    if (t.Kind == "startmenu") { startTab = t; break; }
                if (startTab == null) return;

                string nShut = Loc.S("Shut down", "Завершение работы");
                string nRestart = Loc.S("Restart", "Перезагрузка");
                string nSleep = Loc.S("Sleep", "Спящий режим");
                string nHib = Loc.S("Hibernate", "Гибернация");

                bool changed = false;
                foreach (var it in startTab.Items)
                {
                    string want = null;
                    if (string.Equals(it.Path, PowerShutdownPath, StringComparison.OrdinalIgnoreCase)) want = nShut;
                    else if (string.Equals(it.Path, PowerRestartPath, StringComparison.OrdinalIgnoreCase)) want = nRestart;
                    else if (string.Equals(it.Path, PowerSleepPath, StringComparison.OrdinalIgnoreCase)) want = nSleep;
                    else if (string.Equals(it.Path, PowerHibernatePath, StringComparison.OrdinalIgnoreCase)) want = nHib;
                    if (want != null && !string.Equals(it.Name, want, StringComparison.Ordinal)) { it.Name = want; changed = true; }
                }

                bool any = false;
                foreach (var it in startTab.Items)
                    if (IsPowerTilePath(it.Path)) { any = true; break; }

                if (!any)
                {
                    int cols = Math.Max(1, settings.GridColumns);
                    int rows = Math.Max(1, settings.GridRows);
                    foreach (var p in new[]
                    {
                        new { Name = nShut, Path = PowerShutdownPath, Src = "builtin:power-shutdown" },
                        new { Name = nRestart, Path = PowerRestartPath, Src = "builtin:power-restart" },
                        new { Name = nSleep, Path = PowerSleepPath, Src = "builtin:power-sleep" },
                        new { Name = nHib, Path = PowerHibernatePath, Src = "builtin:power-hibernate" }
                    })
                    {
                        var item = new ShortcutItem { Name = p.Name, Path = p.Path, Src = p.Src, Size = ClampItemSize(settings.DefaultItemSize) };
                        PlaceIntoGridStatic(startTab.Items, item, cols, rows);
                        startTab.Items.Add(item);
                    }
                    changed = true;
                }
                if (changed) records.Save(recordsPath);
            }
            catch (Exception ex) { AppLog.Write("EnsurePowerTiles", ex); }
        }

        // The power-tile dispatcher: a virtual "power:" path into the system
        // power action. Shut down and restart ask once (a mis-click must not
        // power off the machine); sleep and hibernate are instantly
        // reversible and go straight away.
        private static void RunPowerAction(string path)
        {
            bool shutdown = string.Equals(path, PowerShutdownPath, StringComparison.OrdinalIgnoreCase);
            bool restart = string.Equals(path, PowerRestartPath, StringComparison.OrdinalIgnoreCase);
            bool ok = true;
            if (shutdown || restart)
            {
                string title = shutdown ? Loc.S("Shut down", "Завершение работы") : Loc.S("Restart", "Перезагрузка");
                string question = shutdown ? Loc.S("Shut down the computer?", "Выключить компьютер?")
                                           : Loc.S("Restart the computer?", "Перезагрузить компьютер?");
                ok = ConfirmDialog.ShowConfirm(ActivePanelForm(), title, question, null, title);
            }
            if (!ok) return;
            bool started = false;
            try
            {
                if (shutdown) started = PowerActions.Shutdown();
                else if (restart) started = PowerActions.Restart();
                else if (string.Equals(path, PowerSleepPath, StringComparison.OrdinalIgnoreCase)) started = PowerActions.Sleep();
                else if (string.Equals(path, PowerHibernatePath, StringComparison.OrdinalIgnoreCase)) started = PowerActions.Hibernate();
            }
            catch (Exception ex) { AppLog.Write("Power action", ex); }
            if (!started) AppLog.Write("Power action did not start: " + path);
        }

        // v1.1.2 briefly shipped built-in Calculator / Alarms & Clock tiles in
        // the Start tab root; the idea is gone (the apps live only in the
        // "Apps (system)" folder of the mirrored Start tab, with the ordinary
        // icon pipeline), and tiles a v1.1.2 build already saved are removed
        // here. Runs from LoadTabs; a no-op once nothing matches.
        private void RemoveLegacyUwpTiles()
        {
            try
            {
                bool changed = false;
                foreach (var t in records.Tabs)
                    if (StripLegacyUwpTiles(t.Items)) changed = true;
                if (changed) records.Save(recordsPath);
            }
            catch (Exception ex) { AppLog.Write("RemoveLegacyUwpTiles", ex); }
        }

        private static bool StripLegacyUwpTiles(List<ShortcutItem> items)
        {
            bool changed = false;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                if (it.Children != null && it.Children.Count > 0 && StripLegacyUwpTiles(it.Children)) changed = true;
                if (it.Src != null && it.Src.StartsWith("builtin:uwp-", StringComparison.OrdinalIgnoreCase))
                {
                    items.RemoveAt(i);
                    changed = true;
                }
            }
            return changed;
        }

        private static IWin32Window ActivePanelForm()
        {
            foreach (Form f in Application.OpenForms)
            {
                var mf = f as MainForm;
                if (mf != null && !mf.IsDisposed) return mf;
            }
            return null;
        }

        // Adds the "Open Start menu" tile to the mirrored Start tab once (the
        // sync keeps foreign items — their Src is not a sync key). Only when a
        // capture is on: without it the real menu is still reachable normally.
        // Also re-labels a tile created by an older build / another language.
        private void EnsureRealStartTile()
        {
            try
            {
                TabData startTab = null;
                foreach (var t in records.Tabs)
                    if (t.Kind == "startmenu") { startTab = t; break; }
                if (startTab == null) return;
                string name = Loc.S("Open Start menu", "Открыть меню Пуск");
                foreach (var it in startTab.Items)
                {
                    if (!string.Equals(it.Path, StartMenuPath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(it.Name, name, StringComparison.Ordinal))
                    {
                        it.Name = name;
                        records.Save(recordsPath);
                        if (activeTabData == startTab && activeLayoutPanel != null)
                            RenderCurrentFolder(activeLayoutPanel, startTab);
                    }
                    return;
                }
                // Creating the tile makes sense only when a capture is on (the Win
                // key or the corner click): otherwise the real Start menu is still
                // reachable the normal way. The re-label above runs regardless —
                // a tile left behind by an older build stays correctly named.
                if (!settings.HotkeyWin && !settings.HotkeyStartClick) return;
                var item = new ShortcutItem
                {
                    Name = name,
                    Path = StartMenuPath,
                    Src = "builtin:startmenu",
                    Size = ClampItemSize(settings.DefaultItemSize)
                };
                MainForm.PlaceIntoGridStatic(startTab.Items, item,
                    Math.Max(1, settings.GridColumns), Math.Max(1, settings.GridRows));
                startTab.Items.Add(item);
                records.Save(recordsPath);
                if (activeTabData == startTab && activeLayoutPanel != null)
                    RenderCurrentFolder(activeLayoutPanel, startTab);
                else
                    renderedTabs.Remove(startTab); // re-render lazily on next activation
            }
            catch (Exception ex) { AppLog.Write("EnsureRealStartTile", ex); }
        }

        // The real Start menu: the Win capture swallows the Win key, so the tile
        // inside the panel opens it with Ctrl+Esc — the system shortcut the hook
        // never touches. The panel hides first (the tile lives inside it).
        internal static void OpenRealStartMenu()
        {
            try
            {
                foreach (Form f in Application.OpenForms)
                {
                    var mf = f as MainForm;
                    if (mf != null && !mf.IsDisposed)
                    {
                        if (mf.Visible) mf.Hide();
                        break;
                    }
                }
            }
            catch { }
            try
            {
                keybd_event(0x11, 0, 0, System.UIntPtr.Zero);                  // Ctrl down
                keybd_event(0x1B, 0, 0, System.UIntPtr.Zero);                  // Esc down
                keybd_event(0x1B, 0, KEYEVENTF_KEYUP_LL, System.UIntPtr.Zero); // Esc up
                keybd_event(0x11, 0, KEYEVENTF_KEYUP_LL, System.UIntPtr.Zero); // Ctrl up
            }
            catch { }
        }

        private void ApplyWinKeyHotkey()
        {
            try
            {
                bool wantKey = settings != null && settings.HotkeyWin;
                bool wantClick = settings != null && settings.HotkeyStartClick;
                // The Win KEY (keyboard hook, hotkey fallback) is independent of
                // the Start button CLICK (mouse hook).
                if (!wantKey)
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
                }
                else if (winKeyHook == IntPtr.Zero && !hotkeyWinRegistered)
                {
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
                if (!wantClick)
                {
                    if (startMouseHook != IntPtr.Zero)
                    {
                        try { UnhookWindowsHookEx(startMouseHook); } catch { }
                        startMouseHook = IntPtr.Zero;
                    }
                }
                else if (startMouseHook == IntPtr.Zero)
                {
                    // Route plain left clicks on the physical Start button (screen
                    // corner) to the panel, and make sure the real Start menu
                    // stays reachable via the tile.
                    mouseHookRef = new LowLevelHookProc(StartMouseHookProc);
                    startMouseHook = SetWindowsHookEx(WH_MOUSE_LL, mouseHookRef, GetModuleHandle(null), 0);
                    if (startMouseHook == IntPtr.Zero)
                        AppLog.Write("Start button mouse hook failed err=" + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                    else
                        AppLog.Write("Start button click capture on, rect=" + StartButtonRect().ToString());
                }
                if (wantKey || wantClick) EnsureRealStartTile();
            }
            catch (Exception ex) { AppLog.Write("Win key hook", ex); }
        }

        // The captured Start button: show the panel (search focused) or hide it.
        private void ToggleByWinKey()
        {
            try
            {
                // Toggle on real foreground state, not just Visible: a panel that
                // is visible but COVERED (e.g. by the mini explorer) used to hit
                // the Hide() branch, so pressing Win made the covering window pop
                // to front instead of bringing the panel up.
                bool isActive = this.Visible && this.WindowState != FormWindowState.Minimized
                                && GetForegroundWindow() == this.Handle;
                if (isActive)
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

        // "Ctrl+Alt+P" style strings; the key part is a letter, a digit,
        // "F1".."F24" or "Space". "None"/empty = no hotkey (vk 0).
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
                else if (p.Equals("Space", StringComparison.OrdinalIgnoreCase)) vk = 0x20;
                else if (p.Length == 2 && (p[0] == 'F' || p[0] == 'f') && p[1] >= '1' && p[1] <= '9')
                    vk = (uint)(0x70 + (p[1] - '1')); // F1..F9 -> 0x70..0x78
                else if (p.Length == 3 && (p[0] == 'F' || p[0] == 'f') && p[1] >= '1' && p[1] <= '2'
                    && p[2] >= '0' && p[2] <= '9')
                {
                    int f = (p[1] - '0') * 10 + (p[2] - '0'); // F10..F24 -> 0x79..0x87
                    if (f >= 10 && f <= 24) vk = (uint)(0x79 + (f - 10));
                }
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

        // Photo/video extensions that get a live shell preview (photo itself /
        // video frame) instead of the generic file icon.
        private static readonly string[] MediaExtensions =
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff",
            ".jfif", ".heic", ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v",
            ".webm", ".mpg", ".mpeg", ".3gp", ".flv", ".ts"
        };

        internal static bool IsMediaFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || ShellItemApi.IsShellPath(path)) return false;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                foreach (var e in MediaExtensions) if (ext == e) return true;
            }
            catch { }
            return false;
        }

        private static readonly string[] VideoExtensions =
        {
            ".mp4", ".m4v", ".mkv", ".mov", ".avi", ".wmv", ".webm", ".mpg",
            ".mpeg", ".3gp", ".ts"
        };

        internal static bool IsVideoFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return false;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                foreach (var e in VideoExtensions) if (ext == e) return true;
            }
            catch { }
            return false;
        }

        // Shell thumbnail for a photo/video (photo itself / video frame) or a
        // FOLDER (the preview the shell composes from the files inside); null
        // when the shell cannot make one (the caller then falls back to the
        // normal icon chain).
        //
        // The preview is ALSO stored in our own disk cache: the Windows thumbnail
        // cache evicts entries over time, and a tile that once showed a picture
        // must not fall back to a generic icon later ("the thumbnail disappeared").
        // The cache key includes the source mtime, so a replaced file re-previews.
        internal static Image LoadMediaThumbnail(string path, int size, bool isFolder)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || ShellItemApi.IsShellPath(path)) return null;
                if (!isFolder && !IsMediaFile(path)) return null;
                string key = "mediathumb|" + path.ToLowerInvariant() + "|" +
                             File.GetLastWriteTimeUtc(path).Ticks + "|" + size;
                Image own = IconExtractor.DiskCacheGetByKey(key);
                if (own != null) return own;
                if (isFolder)
                {
                    // A folder without composeable pictures fails the attempt;
                    // remember it for the session so every tab switch does not
                    // re-run the composition. The mtime in the key re-arms the
                    // entry when the folder content changes.
                    string negKey = key.Substring(0, key.LastIndexOf('|'));
                    lock (FolderNoThumb)
                    {
                        if (FolderNoThumb.ContainsKey(negKey)) return null;
                    }
                    Image fimg = ShellItemApi.GetShellThumbnail(path, size);
                    if (fimg != null)
                        IconExtractor.DiskCachePutByKey(key, fimg);
                    else
                        lock (FolderNoThumb)
                        {
                            if (FolderNoThumb.Count > 512) FolderNoThumb.Clear();
                            FolderNoThumb[negKey] = 1;
                        }
                    return fimg;
                }
                Image img = ShellItemApi.GetShellThumbnail(path, size);
                if (img == null && IsVideoFile(path))
                    img = MediaFrame.ExtractFrame(path, 512); // own extractor + own cache
                if (img != null)
                    IconExtractor.DiskCachePutByKey(key, img);
                return img;
            }
            catch { return null; }
        }

        // Session memory of folders whose thumbnail attempt failed (nothing the
        // shell can compose a preview from) - see LoadMediaThumbnail.
        private static readonly Dictionary<string, byte> FolderNoThumb = new Dictionary<string, byte>();

        // Session cache of "is this path a filesystem directory" (UI-thread safe:
        // network paths are excluded here, they are probed directly only inside
        // the slow-source worker branches). The user's directory tiles do not
        // carry any folder flag - IsFolder on ShortcutItem marks group tiles.
        private static readonly Dictionary<string, bool> IsDirCache = new Dictionary<string, bool>();

        internal static bool IsFolderPathCached(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || ShellItemApi.IsShellPath(path)) return false;
                if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return false;
                if (IconExtractor.IsNetworkPath(path)) return false;
                string key = path.ToLowerInvariant();
                lock (IsDirCache)
                {
                    bool hit;
                    if (IsDirCache.TryGetValue(key, out hit)) return hit;
                }
                bool isDir = Directory.Exists(path);
                lock (IsDirCache)
                {
                    if (IsDirCache.Count > 2048) IsDirCache.Clear();
                    IsDirCache[key] = isDir;
                }
                return isDir;
            }
            catch { return false; }
        }

        private static Image LoadIconForItem(ShortcutItem item)
        {
            try
            {
                // 1) direct icon of the item (Change Icon)
                if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath))
                    return IconExtractor.LoadAny(item.CustomIconPath);
                // The built-in "Open Start menu" tile: its path is a virtual
                // protocol — draw the Start glyph instead of an extraction that
                // would only fail.
                if (string.Equals(item.Path, StartMenuPath, StringComparison.OrdinalIgnoreCase))
                    return GetStartMenuIcon();
                // The built-in power tiles: virtual paths too - draw the
                // matching sign instead of an extraction that would only fail.
                if (IsPowerTilePath(item.Path))
                    return GetPowerIcon(item.Path);
                // 2) icon assigned to the file type
                string typeIcon = FileTypes.GetIconForPath(item.Path);
                if (!string.IsNullOrEmpty(typeIcon) && File.Exists(typeIcon))
                    return IconExtractor.LoadAny(typeIcon);
                // 3) photo/video live preview (photo itself, video frame) and the
                // folder preview for directory tiles. Slow (network) sources run
                // here on a worker thread, so the direct Directory.Exists probe
                // is safe exactly when IsSlowIconSource is true.
                bool isFolderPreview = item.IsFolder || IsFolderPathCached(item.Path) ||
                                       (IsSlowIconSource(item) && Directory.Exists(item.Path ?? ""));
                Image mediaThumb = LoadMediaThumbnail(item.Path, 256, isFolderPreview);
                if (mediaThumb != null) return mediaThumb;
                // 4) standard shell icon (shell: paths = UWP apps)
                return IconExtractor.GetIconAuto(item.Path, true);
            }
            catch (Exception ex)
            {
                AppLog.Write("LoadIconForItem", ex);
                return null;
            }
        }

        // Cached-only mirror of LoadIconForItem: hands out the icon when it is
        // already in the session/disk cache and never extracts. Used by the
        // panel rebuild to render known icons synchronously instead of popping
        // them in from the extraction queue (the flicker on every move).
        private static bool TryGetCachedIconForItem(ShortcutItem item, out Image img)
        {
            img = null;
            try
            {
                if (item == null) return false;
                // Media previews and folder previews must NOT come from the icon
                // cache: it still holds the pre-preview generic icon for that
                // path, and serving it would freeze the tile on the generic icon
                // forever. Media and folder items always go through
                // LoadIconForItem (shell thumbnail pipeline). (Network paths are
                // not probed here - their slow-source routing never uses this
                // synchronous path anyway.) The built-in startmenu: tile too —
                // an older build may have cached a failed-extraction icon for it.
                if (IsMediaFile(item.Path) || item.IsFolder || IsFolderPathCached(item.Path) ||
                    string.Equals(item.Path, StartMenuPath, StringComparison.OrdinalIgnoreCase) ||
                    IsPowerTilePath(item.Path)) return false;
                if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath))
                    return IconExtractor.TryGetCachedAny(item.CustomIconPath, out img);
                string typeIcon = FileTypes.GetIconForPath(item.Path);
                if (!string.IsNullOrEmpty(typeIcon) && File.Exists(typeIcon))
                    return IconExtractor.TryGetCachedAny(typeIcon, out img);
                return IconExtractor.TryGetCachedAuto(item.Path, true, out img);
            }
            catch { img = null; return false; }
        }

        // ---------- network / slow icon sources ----------
        // A path on a network share (or a .lnk pointing into one) can block the
        // shell for the SMB timeout on an unreachable server - seconds or tens of
        // seconds. Such icons must never be extracted on the UI thread: the tile
        // loaders hand them to a worker thread instead (see LoadTileIcon /
        // LoadFolderChildIcon / the search row icons / the popup / mini explorer).

        // True when the icon extraction for this ITEM may touch the network.
        internal static bool IsSlowIconSource(ShortcutItem item)
        {
            try
            {
                if (item == null) return false;
                if (!string.IsNullOrEmpty(item.CustomIconPath) && IconExtractor.IsNetworkPath(item.CustomIconPath)) return true;
                string p = item.Path ?? "";
                if (p.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    return IconExtractor.IsNetworkPath(p) || IconExtractor.IsNetworkPath(PanelSearch.ResolveTarget(p));
                return IconExtractor.IsNetworkPath(p);
            }
            catch { }
            return false;
        }

        // Same check for a raw path (search result rows, mini explorer entries).
        internal static bool IsSlowIconPath(string path)
        {
            try
            {
                if (IconExtractor.IsNetworkPath(path)) return true;
                if (path != null && path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    return IconExtractor.IsNetworkPath(PanelSearch.ResolveTarget(path));
            }
            catch { }
            return false;
        }

        private void LoadTileIcon(TileControl tile, ShortcutItem item)
        {
            if (tile.IsDisposed) return;
            if (IsSlowIconSource(item))
            {
                // Network source: extract on a worker thread - an unreachable share
                // may stall it for the SMB timeout, the UI keeps running and the
                // tile keeps its placeholder until the icon arrives.
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    Image img = null;
                    try { img = LoadIconForItem(item); } catch { }
                    if (img == null) { try { img = SystemIcons.Application.ToBitmap(); } catch { } }
                    try
                    {
                        if (tile.IsDisposed) { if (img != null) img.Dispose(); return; }
                        tile.BeginInvoke((MethodInvoker)delegate
                        {
                            if (tile.IsDisposed) { if (img != null) img.Dispose(); return; }
                            try
                            {
                                if (tile.IconImage != null) tile.IconImage.Dispose();
                                tile.IconImage = img;
                                tile.Invalidate();
                            }
                            catch { if (img != null) try { img.Dispose(); } catch { } }
                        });
                    }
                    catch { if (img != null) try { img.Dispose(); } catch { } }
                });
                return;
            }
            Image img2 = null;
            try
            {
                img2 = LoadIconForItem(item);
            }
            catch (Exception ex)
            {
                AppLog.Write("LoadTileIcon: LoadIconForItem", ex);
            }
            if (img2 == null)
            {
                try { img2 = SystemIcons.Application.ToBitmap(); }
                catch { img2 = SystemIcons.Error.ToBitmap(); }
            }
            if (tile.IsDisposed) { if (img2 != null) img2.Dispose(); return; }
            try
            {
                if (tile.IconImage != null) tile.IconImage.Dispose();
                tile.IconImage = img2;
                tile.Invalidate();
            }
            catch (Exception ex)
            {
                AppLog.Write("LoadTileIcon: assign IconImage", ex);
                if (img2 != null) img2.Dispose();
            }
        }

        // The closed-folder icon is fetched from the shell once and cloned per use:
        // folder children would otherwise repeat the (slow) shell lookup per tile.
        // Shared with the folder popup tiles (which used to leak a shell Icon each).
        private static Image _closedFolderIcon;

        internal static Image GetFolderIconImage()
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
            if (IsSlowIconSource(child))
            {
                // Network source: extract on a worker thread (see LoadTileIcon).
                // Folder children too - they may need a shell preview, which must
                // not touch an unreachable share on the UI thread.
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    Image bimg = null;
                    try
                    {
                        // This is the slow-source worker branch: a direct
                        // directory probe is safe here (network path or not).
                        bool cfolder = child.IsFolder || IsFolderPathCached(child.Path) ||
                                       Directory.Exists(child.Path ?? "");
                        if (!string.IsNullOrEmpty(child.CustomIconPath) && File.Exists(child.CustomIconPath))
                            bimg = IconExtractor.LoadAny(child.CustomIconPath);
                        else
                        {
                            string typeIcon = FileTypes.GetIconForPath(child.Path);
                            if (!string.IsNullOrEmpty(typeIcon) && File.Exists(typeIcon))
                                bimg = IconExtractor.LoadAny(typeIcon);
                            else
                            {
                                bimg = LoadMediaThumbnail(child.Path, 96, cfolder);
                                if (bimg == null && cfolder)
                                    bimg = GetFolderIconImage();
                                if (bimg == null)
                                    bimg = IconExtractor.GetIconAuto(child.Path, true);
                            }
                        }
                    }
                    catch { }
                    if (bimg == null) { try { bimg = SystemIcons.Application.ToBitmap(); } catch { } }
                    try
                    {
                        if (tile.IsDisposed) { if (bimg != null) bimg.Dispose(); return; }
                        tile.BeginInvoke((MethodInvoker)delegate
                        {
                            if (tile.IsDisposed || index >= tile.ChildIcons.Count) { if (bimg != null) bimg.Dispose(); return; }
                            try
                            {
                                var old = tile.ChildIcons[index];
                                if (old != null) old.Dispose();
                                tile.ChildIcons[index] = bimg;
                                tile.Invalidate();
                            }
                            catch { if (bimg != null) try { bimg.Dispose(); } catch { } }
                        });
                    }
                    catch { if (bimg != null) try { bimg.Dispose(); } catch { } }
                });
                return;
            }
            Image img = null;
            try
            {
                // Group tiles and local directory children (the cached probe
                // skips network paths - those never take this synchronous branch).
                bool cfolder = child.IsFolder || IsFolderPathCached(child.Path);
                if (cfolder)
                {
                    // Folder preview composed by the shell from the files inside;
                    // the shared folder icon when it has none.
                    img = LoadMediaThumbnail(child.Path, 96, true);
                    if (img == null) img = GetFolderIconImage();
                }
                else
                {
                    // 1) direct icon of the child, 2) file type icon, 3) media
                    // preview (photo/video), 4) standard icon
                    if (!string.IsNullOrEmpty(child.CustomIconPath) && File.Exists(child.CustomIconPath))
                        img = IconExtractor.LoadAny(child.CustomIconPath);
                    else
                    {
                        string typeIcon = FileTypes.GetIconForPath(child.Path);
                        if (!string.IsNullOrEmpty(typeIcon) && File.Exists(typeIcon))
                            img = IconExtractor.LoadAny(typeIcon);
                        else
                        {
                            img = LoadMediaThumbnail(child.Path, 96, false);
                            if (img == null)
                                img = IconExtractor.GetIconAuto(child.Path, true);
                        }
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
            // The built-in "Start menu" tile: open the real Start menu and stop.
            if (string.Equals(path, StartMenuPath, StringComparison.OrdinalIgnoreCase))
            {
                if (SuppressDoubleLaunch(StartMenuPath)) return;
                OpenRealStartMenu();
                return;
            }
            // The built-in power tiles: virtual paths into system power actions.
            if (IsPowerTilePath(path))
            {
                if (SuppressDoubleLaunch(path)) return;
                RunPowerAction(path);
                return;
            }
            if (SuppressDoubleLaunch("item:" + path)) return;

            // Packaged apps (the built-in Calculator / Alarms & Clock tiles and
            // the "Apps (system)" folder items): an AppsFolder AUMID is
            // activated through the shell, never opened as a file.
            if (UwpApps.IsAppsFolderAumid(path))
            {
                if (!UwpApps.LaunchAumid(path))
                    ReportLaunchError(ShortLaunchNotice(path));
                return;
            }

            // File-type rule: open with the program assigned to this extension (if any).
            string target = path;
            string args = null;
            string workDir = null;
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
                        // The editor opens on the document, so its working
                        // directory is the document's folder - like Explorer.
                        try { workDir = Path.GetDirectoryName(path); } catch { }
                    }
                }
            }
            catch (Exception ex) { AppLog.Write("LaunchItem: file-type rule", ex); }

            try { StartDetached(target, args, workDir); }
            catch (Exception ex) { AppLog.Write("LaunchItem: StartDetached", ex); ReportLaunchError(ShortLaunchNotice(path)); }
        }

        // The folder-opening command from the settings. Accepted forms:
        //   C:\tools\TOTALCMD64.EXE                - the folder is the one argument
        //   C:\tools\TOTALCMD64.EXE /O             - exe + fixed switches (folder appended, quoted)
        //   C:\tools\TOTALCMD64.EXE /O "%1"        - %1 is replaced by the quoted folder
        // The exe is separated tolerating spaces in its path (the longest
        // existing-file prefix wins). Returns false (= keep the system default)
        // when the setting is empty, names Explorer itself ("если проводник — то
        // по умолчанию") or the executable does not exist.
        internal static bool TryGetFolderOpenCommand(out string exe, out string argsTemplate)
        {
            exe = null; argsTemplate = null;
            try
            {
                string p = CurrentSettings != null ? CurrentSettings.FolderOpenProgram : null;
                string rest;
                if (!SplitCommandTemplate(p, out exe, out rest)) return false;
                string name = Path.GetFileName(exe).ToLowerInvariant();
                if (name == "explorer" || name == "explorer.exe") return false;
                if (!File.Exists(exe))
                {
                    AppLog.Write("FolderOpenProgram not found, using the system default: " + exe);
                    return false;
                }
                argsTemplate = rest;
                return true;
            }
            catch { return false; }
        }

        // Splits a settings command line "exe + arguments" tolerating spaces
        // in the exe path: a quoted exe wins, otherwise the longest existing-
        // file prefix is taken as the exe and the remainder is the template.
        internal static bool SplitCommandTemplate(string p, out string exe, out string rest)
        {
            exe = null; rest = "";
            try
            {
                if (string.IsNullOrEmpty(p)) return false;
                p = p.Trim();
                if (p.Length == 0) return false;
                if (p.StartsWith("\"", StringComparison.Ordinal))
                {
                    int close = p.IndexOf('"', 1);
                    if (close < 0) exe = p.Trim('"');
                    else { exe = p.Substring(1, close - 1); rest = p.Substring(close + 1).Trim(); }
                }
                else
                {
                    exe = p;
                    if (!File.Exists(p))
                    {
                        // "C:\Program Files\TC\tc.exe /O "%1"": walk the spaces and
                        // cut at the first prefix that exists as a file.
                        for (int i = 0; i < p.Length; i++)
                        {
                            if (p[i] != ' ') continue;
                            string cand = p.Substring(0, i);
                            if (File.Exists(cand)) { exe = cand; rest = p.Substring(i + 1).Trim(); break; }
                        }
                        // Still nothing and the line starts with a bare command
                        // name ("cmd /K ...", "wt -d ..."): resolve it the way
                        // the shell would - through System32 and PATH - so the
                        // short forms from the hint work as-is.
                        if (rest.Length == 0 && p.IndexOf('\\') < 0)
                        {
                            int sp = p.IndexOf(' ');
                            string first = sp > 0 ? p.Substring(0, sp) : p;
                            string[] dirs = null;
                            try { dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'); } catch { }
                            if (dirs != null && dirs.Length > 0)
                            {
                                foreach (string dir in dirs)
                                {
                                    if (string.IsNullOrEmpty(dir)) continue;
                                    string cand;
                                    try { cand = System.IO.Path.Combine(dir.Trim(), first + ".exe"); }
                                    catch { continue; }
                                    if (File.Exists(cand)) { exe = cand; rest = sp > 0 ? p.Substring(sp + 1).Trim() : ""; break; }
                                }
                            }
                        }
                    }
                }
                return !string.IsNullOrEmpty(exe);
            }
            catch { exe = null; rest = ""; return false; }
        }

        // Ctrl + right-click on a folder tile: run the console command from the
        // settings ("FolderConsole", %1 = the folder, e.g. wt -d "%1"). Returns
        // false (and does nothing) when the setting is empty or the path is not
        // an existing directory, so the regular context menu shows instead.
        private bool LaunchFolderConsole(string folder)
        {
            return LaunchFolderConsoleCmd(settings, folder);
        }

        // The Ctrl + right-click action on a folder tile. With the console
        // command configured it runs it; with the command EMPTY the mini
        // explorer opens on the folder (it used to fall through to the
        // regular menu). A shortcut to a folder resolves to its target, so
        // .lnk tiles act like real folder tiles here. Returns false — and
        // the regular menu shows — when no directory is reachable or the
        // launch fails.
        private bool LaunchFolderConsoleOrMini(ShortcutItem item)
        {
            try
            {
                string folder = ResolveShortcutFolder(item == null ? null : item.Path);
                if (string.IsNullOrEmpty(folder)) return false;
                if (!string.IsNullOrWhiteSpace(settings != null ? settings.FolderConsole : null))
                    return LaunchFolderConsole(folder);
                OpenMiniExplorer(new ShortcutItem { Path = folder });
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Write("LaunchFolderConsoleOrMini", ex);
                return false;
            }
        }

        // The folder a Ctrl + right-click acts on: the path itself when it is
        // a directory, otherwise — for a .lnk shortcut — the directory its
        // target points at. Null when no directory is reachable. Every
        // attempt lands one line in log.txt, so a failed reveal always has a
        // reason on record.
        internal static string ResolveShortcutFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                if (Directory.Exists(path)) return path;
                if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    // Binary parse first: WScript.Shell (the COM path
                    // PanelSearch uses) can be dead on machines with a broken
                    // shell COM layer — this dev box logs E_NOINTERFACE and
                    // REGDB_E_CLASSNOTREG from several shell classes — and the
                    // folder open must not depend on COM.
                    string target = ParseLnkLocalBasePath(path);
                    if (string.IsNullOrEmpty(target))
                    {
                        try { target = PanelSearch.ResolveTarget(path); }
                        catch { target = null; }
                    }
                    if (!string.IsNullOrEmpty(target) && Directory.Exists(target))
                    {
                        AppLog.Write("ResolveShortcutFolder: " + path + " -> " + target);
                        return target;
                    }
                    AppLog.Write("ResolveShortcutFolder: no directory for " + path);
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("ResolveShortcutFolder: " + ex.Message);
            }
            return null;
        }

        // COM-free .lnk target parse: reads LocalBasePath straight out of the
        // Shell Link binary (MS-SHLLINK). Returns null for shortcuts that
        // carry no local target (UWP-style empty targets, network-only).
        internal static string ParseLnkLocalBasePath(string lnkPath)
        {
            try
            {
                byte[] b = File.ReadAllBytes(lnkPath);
                if (b.Length < 0x4C) return null;
                if (b[0] != 0x4C || b[1] != 0 || b[2] != 0 || b[3] != 0) return null; // shell link signature
                int flags = b[0x14] | (b[0x15] << 8) | (b[0x16] << 16) | (b[0x17] << 24);
                int off = 0x4C;
                if ((flags & 0x01) != 0) // HasLinkTargetIDList: skip the item-id list
                {
                    while (off + 2 <= b.Length)
                    {
                        int sz = b[off] | (b[off + 1] << 8);
                        off += 2 + sz;
                        if (sz == 0) break;
                    }
                }
                if ((flags & 0x02) == 0) return null; // no LinkInfo: target lives in the ID list only
                if (off + 0x1C > b.Length) return null;
                int localBasePathOffset = b[off + 0x10] | (b[off + 0x11] << 8) | (b[off + 0x12] << 16) | (b[off + 0x13] << 24);
                int p = off + localBasePathOffset;
                if (p <= off || p >= b.Length) return null;
                int end = p;
                while (end < b.Length && b[end] != 0) end++;
                string basePath = System.Text.Encoding.Default.GetString(b, p, end - p);
                if (string.IsNullOrEmpty(basePath))
                {
                    // Non-conformant shortcut: real .lnk files exist whose
                    // LinkInfo block is absent while the shell still resolves
                    // the target through the string data (eng1-style). Scan
                    // the raw bytes for a UTF-16 absolute path instead.
                    basePath = ScanUtf16AbsolutePath(b);
                    if (string.IsNullOrEmpty(basePath)) return null;
                }
                if (basePath.EndsWith("\\", StringComparison.Ordinal))
                {
                    // drive roots keep the rest in CommonPathSuffix ("C:\" + "Windows")
                    int commonOffset = b[off + 0x18] | (b[off + 0x19] << 8) | (b[off + 0x1A] << 16) | (b[off + 0x1B] << 24);
                    int q = off + commonOffset;
                    if (q > off && q < b.Length)
                    {
                        int end2 = q;
                        while (end2 < b.Length && b[end2] != 0) end2++;
                        basePath += System.Text.Encoding.Default.GetString(b, q, end2 - q);
                    }
                }
                return basePath;
            }
            catch (Exception ex)
            {
                AppLog.Write("ParseLnkLocalBasePath: " + ex.Message);
                return null;
            }
        }

        // Scans raw .lnk bytes for a UTF-16 absolute path ("X:\..." or
        // "\\server\..."): non-conformant shortcuts on this machine carry
        // the target only as strings while their LinkInfo block is absent
        // or unreadable (garbage offsets beyond the file), and the shell
        // still resolves them through the ID list — so the folder open
        // must scan the raw bytes too instead of trusting the spec layout.
        internal static string ScanUtf16AbsolutePath(byte[] b)
        {
            for (int i = 0x14; i + 12 <= b.Length; i += 2)
            {
                bool drive = b[i] >= 'A' && b[i] <= 'Z' && b[i + 1] == 0 && b[i + 2] == ':' && b[i + 3] == 0 && b[i + 4] == '\\' && b[i + 5] == 0;
                bool unc = b[i] == '\\' && b[i + 1] == 0 && b[i + 2] == '\\' && b[i + 3] == 0 && b[i + 4] != 0 && b[i + 5] != 0;
                if (!drive && !unc) continue;
                int end = drive ? i + 6 : i + 4;
                while (end + 1 < b.Length && !(b[end] == 0 && b[end + 1] == 0)) end += 2;
                if (end + 2 > b.Length) continue;
                string s = System.Text.Encoding.Unicode.GetString(b, i, end - i);
                if (s.Length < 3 || s.IndexOfAny(Path.GetInvalidPathChars()) >= 0) continue;
                if (Directory.Exists(s)) return s;
            }
            return null;
        }

        // Core shared with the folder popup (which carries its own settings).
        internal static bool LaunchFolderConsoleCmd(Settings st, string folder)
        {
            try
            {
                string cmd = st != null ? st.FolderConsole : null;
                if (string.IsNullOrWhiteSpace(cmd)) return false;
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return false;
                string exe, argsTemplate;
                if (!SplitCommandTemplate(cmd, out exe, out argsTemplate)) return false;
                StartDetached(exe, FolderOpenArgs(argsTemplate, exe, folder), folder);
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Write("LaunchFolderConsole", ex);
                return false;
            }
        }

        // Builds the argument line for opening `folder` with the configured
        // manager. A "%1" placeholder (quoted or bare) becomes the quoted folder;
        // fixed switches are followed by the quoted folder; a bare exe gets just
        // the quoted folder — except Total Commander, which ignores a bare path
        // when an instance is already running: its documented switch for opening
        // in the running instance is /O, so that is defaulted for it.
        internal static string FolderOpenArgs(string argsTemplate, string exe, string folder)
        {
            string q = "\"" + folder + "\"";
            if (!string.IsNullOrEmpty(argsTemplate))
            {
                if (argsTemplate.IndexOf("%1", StringComparison.Ordinal) >= 0)
                    return argsTemplate.Replace("\"%1\"", q).Replace("%1", q);
                return argsTemplate + " " + q;
            }
            string name = Path.GetFileName(exe).ToLowerInvariant();
            if (name.StartsWith("totalcmd", StringComparison.Ordinal)) return "/O " + q;
            return q;
        }

        private static bool IsDirectoryPath(string path)
        {
            try { return !string.IsNullOrEmpty(path) && Directory.Exists(path); }
            catch { return false; }
        }

        // Launches on its own STA thread so the panel stays responsive even when the system
        // takes seconds to hand the launch over (cold start, antivirus inspection, UAC).
        private static void StartDetached(string fileName, string arguments)
        {
            StartDetached(fileName, arguments, null);
        }

        // workDir: preferred working directory for the launched process (e.g. the
        // document's folder when a file-type rule supplies the program). When it
        // is empty, the folder of the launched file itself is used - double-click
        // semantics, so a .bat keeps finding its neighbouring .env and an .exe
        // its resources no matter where Tilettes was started from.
        private static void StartDetached(string fileName, string arguments, string workDir)
        {
            var t = new Thread(delegate()
            {
                try
                {
                    if (arguments == null)
                    {
                        // Folder tiles: redirect directories to the file manager
                        // chosen in the settings. The directory probe runs here on
                        // the worker thread - a network folder must not stall the UI.
                        string fmExe, fmTemplate;
                        if (TryGetFolderOpenCommand(out fmExe, out fmTemplate) && IsDirectoryPath(fileName))
                        {
                            System.Diagnostics.Process.Start(BuildShellStart(fmExe, FolderOpenArgs(fmTemplate, fmExe, fileName), fileName));
                            return;
                        }
                        System.Diagnostics.Process.Start(BuildShellStart(fileName, null, workDir));
                    }
                    else
                        System.Diagnostics.Process.Start(BuildShellStart(fileName, arguments, workDir));
                }
                catch (System.ComponentModel.Win32Exception wex)
                {
                    if (wex.NativeErrorCode == 1223) return; // "No" in the UAC prompt
                    AppLog.Write("StartDetached: Win32Exception", wex);
                    ReportLaunchError(ShortLaunchNotice(fileName));
                }
                catch (Exception ex)
                {
                    AppLog.Write("StartDetached: Exception", ex);
                    ReportLaunchError(ShortLaunchNotice(fileName));
                }
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        // Builds a ShellExecute start for `fileName` with the working directory
        // pinned the way a double-click in Explorer does: `workDir` when given
        // and existing; for a shortcut its own "Start in" (or the target's
        // folder when the shortcut has none); otherwise the launched file's
        // own folder. Anything unresolvable (a URL, an env-var path) keeps the
        // previous behavior and inherits Tilettes' working directory.
        internal static System.Diagnostics.ProcessStartInfo BuildShellStart(string fileName, string arguments, string workDir)
        {
            var psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = fileName;
            if (!string.IsNullOrEmpty(arguments)) psi.Arguments = arguments;
            try
            {
                string wd = null;
                if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir)) wd = workDir;
                string p = string.IsNullOrEmpty(fileName) ? null : fileName.Trim('"');
                if (wd == null && !string.IsNullOrEmpty(p) &&
                    string.Equals(Path.GetExtension(p), ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string target, lnkDir;
                    if (TryResolveLnk(p, out target, out lnkDir))
                    {
                        if (!string.IsNullOrEmpty(lnkDir) && Directory.Exists(lnkDir)) wd = lnkDir;
                        else if (!string.IsNullOrEmpty(target))
                        {
                            if (File.Exists(target)) wd = Path.GetDirectoryName(target);
                            else if (Directory.Exists(target)) wd = target;
                        }
                    }
                }
                if (wd == null && !string.IsNullOrEmpty(p))
                {
                    if (Directory.Exists(p)) wd = p;
                    else if (File.Exists(p)) wd = Path.GetDirectoryName(p);
                }
                if (!string.IsNullOrEmpty(wd)) psi.WorkingDirectory = wd;
            }
            catch (Exception ex) { AppLog.Write("BuildShellStart: working directory", ex); }
            return psi;
        }

        // Reads a .lnk's target and "Start in" via WScript.Shell (reflection, no
        // referenced assemblies). Used to pin the working directory of shortcut
        // launches: with an empty "Start in" the child would otherwise inherit
        // Tilettes' own working directory and lose the file's surroundings.
        internal static bool TryResolveLnk(string lnkPath, out string target, out string workDir)
        {
            target = null; workDir = null;
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return false;
                object sh = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { lnkPath });
                target = sc.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, sc, null) as string;
                workDir = sc.GetType().InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.GetProperty, null, sc, null) as string;
                return !string.IsNullOrEmpty(target);
            }
            catch { return false; }
        }

        // Short, localized notice for a failed launch: the user sees what did
        // not open; the technical reason lives only in log.txt.
        internal static string ShortLaunchNotice(string target)
        {
            string name = target;
            try { name = System.IO.Path.GetFileName((target ?? "").TrimEnd('\\', '/')); } catch { }
            if (string.IsNullOrEmpty(name)) name = target ?? "";
            return Loc.S("Cannot open \"", "Не удаётся открыть \"") + name + "\"";
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
                        // A tray balloon when the icon is around (short, hides
                        // itself); a small dialog when it is not.
                        mf.BeginInvoke((MethodInvoker)delegate
                        {
                            if (!mf.ShowBalloon(message)) ConfirmDialog.ShowInfo(mf, message);
                        });
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
            EnsurePowerTiles();
            RemoveLegacyUwpTiles();
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
            renderedTabs.Clear();
            tabBar.Controls.Clear();
            rightPanel.Controls.Clear();
            contentPanel.Controls.Clear();

            int tabRows = 1;
            foreach (var tab in records.Tabs) tabRows = Math.Max(tabRows, tab.Row + 1);
            int searchRowH = 31;
            try { searchRowH = Math.Max(31, Math.Max(7, Math.Min(30, settings.SearchBoxFontSize)) + 22); } catch { }
            topSearchRowH = searchRowH;
            // Tab size follows the tabs font: the caption must fit at any size.
            // One font instance is built here and reused for every tab button
            // (MakeFont walks the family cache on each call).
            Font tabsFont = Settings.MakeFont(settings.FontTabsName, settings.FontTabsSize);
            int tabH = tabsFont.Height + 12;
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

                var layoutPanel = new ScrollPanel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    AllowDrop = true,
                    Tag = tabData,
                    BackColor = bgColor,
                    Visible = false
                };
                layoutPanel.SetTheme(bgColor, settings.IsLightTheme);
                // Panel.DoubleBuffered is protected; without it every tile drag
                // erased+repainted the exposed background and made the icons of
                // the neighbouring tiles blink. (ScrollPanel also sets it; kept
                // for any plain Panel fallback.)
                typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(layoutPanel, true, null);
                layoutPanel.DragEnter += LayoutPanel_DragEnter;
                layoutPanel.DragDrop += LayoutPanel_DragDrop;
                layoutPanel.Paint += LayoutPanel_Paint;
                layoutPanel.Resize += (s, e) =>
                {
                    ((Panel)s).Invalidate();
                    ReflowGridTiles((Panel)s, tabData);
                };

                var panelMenu = new ContextMenu();
                panelMenu.Popup += (s, e) =>
                {
                    panelMenu.MenuItems[0].Enabled = isEditMode;
                    panelMenu.MenuItems[1].Enabled = isEditMode && tabData.IsGridLayout;
                };
                panelMenu.MenuItems.Add(Loc.S("Create Folder", "Создать папку"), (s, e) => CreateFolder(layoutPanel, tabData));
                panelMenu.MenuItems.Add(Loc.S("Create Group", "Создать группу"), (s, e) => CreateGroup(layoutPanel, tabData));
                panelMenu.MenuItems.Add(Loc.S("Settings"), (s, e) => OpenSettings());
                layoutPanel.ContextMenu = panelMenu;

                contentPanel.Controls.Add(layoutPanel);

                Font tabFont = tabsFont;
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
                tabMenu.MenuItems.Add(Loc.S("Delete Tab", "Удалить вкладку"), (s, e) => {
                    if (records.Tabs.Count > 1) {
                        bool confirmed = ConfirmDialog.ShowConfirm(this,
                            Loc.S("Delete Tab", "Удалить вкладку"),
                            Loc.S("Are you sure you want to delete this tab?", "Вы уверены, что хотите удалить эту вкладку?"),
                            null, Loc.S("Delete", "Удалить"));
                        if (confirmed) {
                            records.Tabs.Remove(tabData);
                            tabNavigations.Remove(tabData);
                            records.Save(recordsPath);
                            LoadTabs();
                        }
                    } else {
                        ConfirmDialog.ShowInfo(this, Loc.S("Cannot remove the last tab.", "Нельзя удалить последнюю вкладку."));
                    }
                });
                tabMenu.MenuItems.Add(Loc.S("Rename Tab", "Переименовать вкладку"), (s, e) => {
                    string newName = Prompt.ShowDialog(Loc.S("New Tab Name", "Новое имя вкладки"), Loc.S("Rename Tab", "Переименовать вкладку"), tabData.Name);
                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        tabData.Name = newName;
                        records.Save(recordsPath);
                        tabBtn.Text = newName;
                    }
                });
                tabMenu.MenuItems.Add(Loc.S("Toggle Layout (Free / Grid)", "Переключить раскладку (свободная / сетка)"), (s, e) => {
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
                // Empty box: the overlay closes (RunPanelSearch hides it) — the
                // normal window shows. Unless the box was just cleared
                // programmatically (tab click / Esc), which has already closed it.
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

            // Tiles render lazily: ActivateTab built the active tab above, every
            // other tab renders on its first activation. Per-tab folder state
            // lives in tabNavigations and the search works on the data, so nothing
            // is lost by not pre-rendering hidden panels (which never get real
            // docking bounds anyway).

            // Search metadata is collected once per item (at its add event) plus
            // one pass over already-existing items at startup - see WarmAllSearchMeta.
            WarmAllSearchMeta(false);
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
            var activatedScroll = layoutPanels[tabBtn] as ScrollPanel;
            if (activatedScroll != null) activatedScroll.ScrollToTop(); // tabs open at the top

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
                // Leaving a tab while inside a folder (same-window navigation)
                // must not hang the folder view on return: the tab always opens
                // at its root, the Back button included.
                if (tabNavigations.ContainsKey(td) && tabNavigations[td].Count > 0)
                {
                    tabNavigations[td].Clear();
                    renderedTabs.Remove(td); // force the root re-render below
                }
            }
            // Lazy rendering: build this tab's tiles on its first activation. The
            // panel is visible now, so the layout has real bounds to work with.
            if (td != null && !renderedTabs.Contains(td))
                RenderCurrentFolder(layoutPanels[tabBtn], td);
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

        // ---- Extra rows below the visible grid (scrollable, settings) ----
        private int ExtraGridRows
        {
            get { return Math.Max(0, settings.GridExtraRows); }
        }

        // Visible grid rows + the extra ones: the capacity that placement uses
        // (items that do not fit the visible grid flow into the extra rows).
        private int TotalGridRows
        {
            get { return Math.Max(1, settings.GridRows) + ExtraGridRows; }
        }

        // Extends the scrollable area of a grid-layout panel over the extra rows;
        // a free-layout panel must not keep a stale scroll range after a toggle.
        private void UpdateGridScrollArea(Panel panel, TabData tabData)
        {
            try
            {
                if (!tabData.IsGridLayout)
                {
                    if (panel.AutoScrollMinSize != Size.Empty) panel.AutoScrollMinSize = Size.Empty;
                    return;
                }
                int rows = Math.Max(1, settings.GridRows);
                int cellHeight = Math.Max(1, panel.ClientSize.Height / rows);
                var minSize = new Size(0, (rows + ExtraGridRows) * cellHeight);
                if (panel.AutoScrollMinSize != minSize) panel.AutoScrollMinSize = minSize;
            }
            catch { }
        }

        // Live tile reflow: grid tiles are sized from the panel's cell size at
        // render time only; without this a window resize left the tiles stale
        // until the next full re-render, which then snapped everything at once
        // (the "rescale jerk" after a drag). Bounds-only recompute - the tiles,
        // icons and their async loaders stay alive, DrawFit rescales the icon on
        // the next paint. Free-layout tabs keep their fixed 40px cells.
        private void ReflowGridTiles(Panel panel, TabData tabData)
        {
            try
            {
                if (!tabData.IsGridLayout) return;
                int cols = Math.Max(1, settings.GridColumns);
                int rows = Math.Max(1, settings.GridRows);
                int cellWidth = Math.Max(1, panel.ClientSize.Width / cols);
                int cellHeight = Math.Max(1, panel.ClientSize.Height / rows);
                // Keep the scrollable area over the extra rows (rows below the
                // visible grid): even with no tiles there the panel scrolls.
                var minSize = new Size(0, (rows + Math.Max(0, settings.GridExtraRows)) * cellHeight);
                if (panel.AutoScrollMinSize != minSize) panel.AutoScrollMinSize = minSize;
                // Child Location inside an AutoScroll panel is PHYSICAL while
                // scrolled (the scroll shifts children): add the scroll offset
                // so the logical grid slot is preserved. At scroll 0 this is a
                // no-op and the layout is exactly as before.
                var scroll = panel.AutoScrollPosition;
                foreach (Control c in panel.Controls)
                {
                    var gc = c as GroupControl;
                    if (gc != null) { gc.UpdateBoundsFromCells(); continue; }
                    var tile = c as TileControl;
                    if (tile == null || tile.Item == null) continue;
                    int s = ClampItemSize(tile.Item.Size);
                    // Same clamps as AddShortcutControl: columns must stay inside
                    // the visible grid, rows may overflow into AutoScroll.
                    int col = Math.Max(0, Math.Min(cols - s, tile.Item.GridX));
                    int row = Math.Max(0, tile.Item.GridY);
                    var bounds = new Rectangle(col * cellWidth + scroll.X, row * cellHeight + scroll.Y, s * cellWidth, s * cellHeight);
                    if (tile.Bounds != bounds) tile.Bounds = bounds;
                }
            }
            catch { }
        }

        private void RenderCurrentFolder(Panel layoutPanel, TabData tabData)
        {
            renderedTabs.Add(tabData);
            ClearMultiSelection();
            ClearGroupPreview();
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
                // Unpositioned items fill the visible grid first, then the
                // extra rows below it (the panel scrolls to them).
                int rows = TotalGridRows;
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

            // Thousands of tiles: every Controls.Add on an AutoScroll panel
            // recomputes the layout, which made a 5000-tile tab take ~18 s to
            // appear. Suspend the layout for the bulk and do a single pass.
            layoutPanel.SuspendLayout();
            try
            {
                foreach (var item in itemsToRender)
                {
                    AddShortcutControl(layoutPanel, item, tabData);
                }
            }
            finally { layoutPanel.ResumeLayout(true); }

            // Tile groups live on grid tabs at the root view only: fit the auto
            // axes to the member tiles, then add the group surfaces - they go
            // in after the tiles, so they stay underneath them.
            if (tabData.IsGridLayout && navStack.Count == 0)
            {
                var groups = tabData.EnsureGroups();
                if (groups.Count > 0)
                {
                    FitGroupsToItems(tabData, itemsToRender);
                    foreach (var grp in groups)
                    {
                        var gc = new GroupControl
                        {
                            Group = grp,
                            Tab = tabData,
                            OwnerPanel = layoutPanel,
                            BaseColor = bgColor,
                            MainTextColor = textColor,
                            GetCols = () => Math.Max(1, settings.GridColumns),
                            GetRows = () => Math.Max(1, settings.GridRows),
                            SaveAndRelayout = () => { records.Save(recordsPath); ReflowGridTiles(layoutPanel, tabData); },
                            DeleteAction = () => DeleteTileGroup(layoutPanel, tabData, grp)
                        };
                        gc.UpdateBoundsFromCells();
                        layoutPanel.Controls.Add(gc);
                        gc.BringToFront(); // the group draws OVER the tiles; the body stays click-through via its window region
                    }
                }
            }

            UpdateGridScrollArea(layoutPanel, tabData);
            var scrollPanel = layoutPanel as ScrollPanel;
            if (scrollPanel != null) scrollPanel.EnsureThumb(); // overlay survives Controls.Clear
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
        // Returns the preferred cell when it is free. skipSet (optional) excludes a
        // whole group - the moving multi-selection does not collide with itself.
        private Point FindFreeGridCell(List<ShortcutItem> items, ShortcutItem skip, int size, int cols, int rows, int preferX, int preferY, List<ShortcutItem> skipSet = null)
        {
            var occ = new bool[rows, cols];
            foreach (var it in items)
            {
                if (ReferenceEquals(it, skip)) continue;
                if (skipSet != null && skipSet.Contains(it)) continue;
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

        // Moves a whole multi-selection block by (dxCells, dyCells) cells. Every
        // tile keeps its relative position when all the target slots are free at
        // once (the group does not collide with itself); otherwise each blocked
        // tile shifts to the nearest free cell from its preferred slot, while the
        // tiles placed before it act as obstacles for the rest.
        private void PlaceGroupInGrid(TabData tabData, List<TileControl> tiles, int dxCells, int dyCells, int cols, int rowsTotal)
        {
            var current = GetCurrentItems(tabData);
            var group = new List<ShortcutItem>();
            foreach (var t in tiles) group.Add(t.Item);

            // Stable placement order: reading order of the current positions.
            var ordered = new List<TileControl>(tiles);
            ordered.Sort(delegate(TileControl a, TileControl b)
            {
                int r = a.Item.GridY.CompareTo(b.Item.GridY);
                return r != 0 ? r : a.Item.GridX.CompareTo(b.Item.GridX);
            });

            // All-or-nothing first: the group keeps its shape when every
            // preferred slot is free at once (the group vacates its own cells).
            var occ = new bool[rowsTotal, cols];
            foreach (var it in current)
            {
                if (group.Contains(it)) continue;
                int os = Math.Min(ClampItemSize(it.Size), Math.Min(cols, rowsTotal));
                int gx = Math.Max(0, Math.Min(cols - os, it.GridX));
                int gy = Math.Max(0, Math.Min(rowsTotal - os, it.GridY));
                for (int dy = 0; dy < os; dy++)
                    for (int dx = 0; dx < os; dx++)
                        if (gy + dy < rowsTotal && gx + dx < cols) occ[gy + dy, gx + dx] = true;
            }
            bool allFit = true;
            foreach (var t in ordered)
            {
                int s = ClampItemSize(t.Item.Size);
                if (!CellFits(occ, cols, rowsTotal, t.Item.GridX + dxCells, t.Item.GridY + dyCells, s))
                {
                    allFit = false;
                    break;
                }
            }
            if (allFit)
            {
                foreach (var t in ordered)
                {
                    t.Item.GridX += dxCells;
                    t.Item.GridY += dyCells;
                }
                return;
            }

            // Something blocks: place tile by tile from each preferred slot;
            // pending holds the members not yet placed, so their old cells do
            // not block the search, while the placed ones protect their new cells.
            var pending = new List<ShortcutItem>(group);
            foreach (var t in ordered)
            {
                var it = t.Item;
                int s = ClampItemSize(it.Size);
                int px = it.GridX + dxCells;
                int py = it.GridY + dyCells;
                Point cell = FindFreeGridCell(current, null, s, cols, rowsTotal, px, py, pending);
                if (cell.X < 0)
                    cell = FindFreeGridCell(current, null, s, cols, Math.Max(rowsTotal, s) * 3, px, py, pending);
                if (cell.X >= 0)
                {
                    it.GridX = cell.X;
                    it.GridY = cell.Y;
                }
                else
                {
                    // Grid completely full: keep the preferred slot, clamped.
                    it.GridX = Math.Max(0, Math.Min(cols - s, px));
                    it.GridY = Math.Max(0, py);
                }
                pending.Remove(it);
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
            // Extra rows below the fold count toward the capacity: the grid is
            // "full" only when the extra rows are full too (the reserved rows
            // stay at the very bottom, out of sight).
            int totalRows = rows + Math.Max(0, settings.GridExtraRows);
            int usableRows = totalRows - ReservedRows;
            if (usableRows < 1) usableRows = totalRows;

            ShortcutItem overflow = null;
            foreach (var it in tab.Items)
                if (it.IsFolder && it.Src == OverflowSrcKey) { overflow = it; break; }

            bool changed = false;

            // 1) Unpack a previous overflow folder when everything fits again.
            if (overflow != null && overflow.Children != null && overflow.Children.Count > 0)
            {
                long need = 0;
                foreach (var it in tab.Items)
                    if (!ReferenceEquals(it, overflow)) need += CellCount(it, cols, totalRows);
                foreach (var ch in overflow.Children) need += CellCount(ch, cols, totalRows);
                if (need <= (long)cols * usableRows)
                {
                    foreach (var ch in overflow.Children)
                    {
                        PlaceInGrid(tab.Items, ch, Math.Max(0, ch.GridX), Math.Max(0, ch.GridY), cols, totalRows);
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
                if (!ReferenceEquals(it, overflow)) total += CellCount(it, cols, totalRows);

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
            string name = Prompt.ShowDialog(Loc.S("Folder Name", "Имя папки"), Loc.S("Create Folder", "Создать папку"));
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
                    int cellHeight = Math.Max(1, layoutPanel.ClientSize.Height / Math.Max(1, settings.GridRows));
                    PlaceInGrid(targetList, folder, Math.Max(0, folder.X / Math.Max(1, layoutPanel.ClientSize.Width / cols)), Math.Max(0, folder.Y / cellHeight), cols, TotalGridRows);
                }
                targetList.Add(folder);
                records.Save(recordsPath);
                WarmSearchMeta(folder);
                RenderCurrentFolder(layoutPanel, tabData);
            }
        }

        // Creates an auto-sized group (8 x 3 cells by default) below the
        // existing ones and opens the inline name editor on it right away -
        // an unnamed group stays completely invisible once the editor closes.
        private void CreateGroup(Panel layoutPanel, TabData tabData)
        {
            try
            {
                if (!tabData.IsGridLayout) return;
                var groups = tabData.EnsureGroups();
                int cols = Math.Max(1, settings.GridColumns);
                int totalRows = TotalGridRows;
                int y = 1; // row 0 would clip the header line above the group
                foreach (var g in groups) y = Math.Max(y, g.Y + g.H);
                var grp = new TileGroup { X = 0, Y = Math.Min(y, Math.Max(1, totalRows - 3)), W = Math.Min(8, cols), H = 3 };
                groups.Add(grp);
                records.Save(recordsPath);
                RenderCurrentFolder(layoutPanel, tabData);
                foreach (Control c in layoutPanel.Controls)
                {
                    var gc = c as GroupControl;
                    if (gc != null && ReferenceEquals(gc.Group, grp)) gc.BeginEdit();
                }
            }
            catch (Exception ex) { AppLog.Write("CreateGroup", ex); }
        }

        // Removes the group only; the tiles keep their grid cells untouched.
        private void DeleteTileGroup(Panel layoutPanel, TabData tabData, TileGroup grp)
        {
            try
            {
                tabData.EnsureGroups().Remove(grp);
                records.Save(recordsPath);
                RenderCurrentFolder(layoutPanel, tabData);
            }
            catch (Exception ex) { AppLog.Write("DeleteTileGroup", ex); }
        }

        // Auto-sized group axes: the width is a constant 8 cells (making it
        // smaller is what the fixed width is for), the height hugs the member
        // tiles but never drops below the default 3 rows. Fixed axes keep the
        // user-set size. Empty groups keep their rect.
        private void FitGroupsToItems(TabData tabData, List<ShortcutItem> items)
        {
            var groups = tabData.Groups;
            if (groups == null || groups.Count == 0 || items == null) return;
            int cols = Math.Max(1, settings.GridColumns);
            foreach (var g in groups)
            {
                if (g.FixedW <= 0) g.W = Math.Min(8, cols);
                if (g.FixedH > 0) continue;
                int maxY = int.MinValue;
                foreach (var it in items)
                {
                    if (it.GridX < g.X || it.GridX >= g.X + g.W || it.GridY < g.Y || it.GridY >= g.Y + g.H) continue;
                    int s = ClampItemSize(it.Size);
                    if (it.GridY + s > maxY) maxY = it.GridY + s;
                }
                if (maxY != int.MinValue) g.H = Math.Max(3, maxY - g.Y);
            }
        }

        // While a single tile is dragged (multi-drags skip the group effects),
        // the group whose one-cell halo overlaps the tile's cells reveals
        // itself and grows to cover them - the expansion commits on drop.
        private void UpdateGroupPreview(Panel panel, TabData tabData, TileControl tile)
        {
            if (groupDrag || !tabData.IsGridLayout || tabNavigations[tabData].Count > 0)
            {
                ClearGroupPreview();
                return;
            }
            var groups = tabData.Groups;
            if (groups == null || groups.Count == 0) { ClearGroupPreview(); return; }

            int cols = Math.Max(1, settings.GridColumns);
            int cellWidth = Math.Max(1, panel.ClientSize.Width / cols);
            int cellHeight = Math.Max(1, panel.ClientSize.Height / Math.Max(1, settings.GridRows));
            int s = ClampItemSize(tile.Item.Size);
            // The dragged tile's own cell rect (same math as the drop path).
            int col = Math.Max(0, Math.Min(cols - s, (tile.Left + cellWidth / 2) / cellWidth));
            int row = Math.Max(0, (tile.Top - panel.AutoScrollPosition.Y + cellHeight / 2) / cellHeight);

            TileGroup near = null;
            Rectangle expand = Rectangle.Empty;
            foreach (var g in groups)
            {
                if (col > g.X + g.W || col + s - 1 < g.X - 1 || row > g.Y + g.H || row + s - 1 < g.Y - 1) continue;
                near = g;
                // Expansion union; a fixed axis never grows (and never joins).
                int nx = g.X, ny = g.Y, nr = g.X + g.W, nb = g.Y + g.H;
                if (g.FixedW <= 0) { nx = Math.Min(nx, col); nr = Math.Max(nr, col + s); }
                if (g.FixedH <= 0) { ny = Math.Min(ny, row); nb = Math.Max(nb, row + s); }
                expand = new Rectangle(nx, ny, nr - nx, nb - ny);
                break;
            }

            if (near == null) { ClearGroupPreview(); return; }
            if (ReferenceEquals(previewGroup, near) && previewRect == expand) return;
            ClearGroupPreview();
            foreach (Control c in panel.Controls)
            {
                var gc = c as GroupControl;
                if (gc != null && ReferenceEquals(gc.Group, near))
                {
                    previewGroup = near;
                    previewRect = expand;
                    previewControl = gc;
                    gc.SetPreview(expand);
                    break;
                }
            }
        }

        private void ClearGroupPreview()
        {
            if (previewControl != null && !previewControl.IsDisposed)
                previewControl.SetPreview(null);
            previewControl = null;
            previewGroup = null;
            previewRect = Rectangle.Empty;
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
            var dropped = new List<ShortcutItem>();
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
                        // Prefer the drop cell; overflow goes into the extra rows.
                        PlaceInGrid(targetList, shortcut, displayPt.X / cellWidth, displayPt.Y / cellHeight, cols, TotalGridRows);
                    }
                    targetList.Add(shortcut);
                }
                dropped.Add(shortcut);
                offset += 20; // stagger drops
            }
            records.Save(recordsPath);
            WarmSearchMeta(dropped);
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
            // The dashed grid continues over the extra rows below the fold.
            int totalRows = rows + ExtraGridRows;

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
            if (totalRows > rows)
            {
                // Extra rows: the bottom border sits at the end of the virtual
                // grid (a floating dashed line, the window edge is far above).
                bottom = totalRows * cellHeight + scroll.Y;
            }

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
                for (int i = 0; i <= totalRows; i++)
                {
                    float y = i * cellHeight + scroll.Y;
                    if (i == 0) y = top;
                    else if (i == totalRows) y = bottom;
                    if (y >= 0 && y <= panel.ClientSize.Height)
                        e.Graphics.DrawLine(gridPen, left, y, right, y);
                }
            }
        }

        private void AddShortcutControl(Panel panel, ShortcutItem item, TabData tabData)
        {
            int cols = Math.Max(1, settings.GridColumns);
            int rows = Math.Max(1, settings.GridRows);
            int rowsTotal = TotalGridRows; // drag-drop target rows include the extra ones
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

            // Child Location inside an AutoScroll panel is PHYSICAL while the
            // panel is scrolled (the scroll shifts children): add the scroll
            // offset so the stored logical slot is preserved. At scroll 0 this
            // is a no-op.
            xPos += panel.AutoScrollPosition.X;
            yPos += panel.AutoScrollPosition.Y;

            var tile = new TileControl
            {
                Item = item,
                TabData = tabData,
                Width = tileWidth,
                Height = tileHeight,
                Location = new Point(xPos, yPos)
            };

            // Tooltip: description (priority) + full name only when the tile
            // face has to abbreviate the label.
            if (itemTip != null)
                itemTip.SetToolTip(tile, BuildItemTooltipText(item, tile.IsLabelShownPartial()));

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
                    Image cachedChild;
                    if (TryGetCachedIconForItem(child, out cachedChild))
                        tile.ChildIcons[idx] = cachedChild;
                    else
                        EnqueueIconTask(() => LoadFolderChildIcon(tile, child, idx));
                }
            }
            else
            {
                Image cachedIcon;
                if (TryGetCachedIconForItem(item, out cachedIcon))
                {
                    tile.IconImage = cachedIcon;
                }
                else
                {
                    EnqueueIconTask(() => LoadTileIcon(tile, item));
                }
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
                    // Multi-select mode: grabbing a selected tile drags the whole
                    // selection as one block; a grab of an unselected tile (or a
                    // single-item selection) stays a plain single-tile drag.
                    groupDrag = editState == 2 && multiSelection.Count > 1 && multiSelection.Contains(item);
                    groupDragTiles = null;
                    if (groupDrag)
                    {
                        groupDragTiles = new List<TileControl>();
                        groupDragTiles.Add(tile);
                        foreach (Control c in panel.Controls)
                        {
                            var gt = c as TileControl;
                            if (gt != null && !ReferenceEquals(gt, tile) && multiSelection.Contains(gt.Item))
                                groupDragTiles.Add(gt);
                        }
                        foreach (var gt in groupDragTiles) gt.BringToFront();
                        groupDragStartPos = new Point(tile.Left, tile.Top);
                    }
                }
                else if (e.Button == MouseButtons.Right)
                {
                    // Red multi-select mode: right-click opens the bulk menu instead.
                    if (editState == 2)
                    {
                        ShowMultiSelectMenu(tile, item, panel, tabData, e.Location);
                        return;
                    }
                    // Ctrl + right-click on a folder tile: open it in the console
                    // command from the settings; with the command empty the mini
                    // explorer opens on the folder instead (the regular menu
                    // below stays for non-directories and failed launches).
                    if ((Control.ModifierKeys & Keys.Control) == Keys.Control && LaunchFolderConsoleOrMini(item))
                        return;
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
                            fMenu.MenuItems.Add(Loc.S("Aura color...", "Цвет ауры..."), (s2, e2) => EditItemAura(item, tile));
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
                            Loc.S("Aura color...", "Цвет ауры..."),
                            (Action)delegate { EditItemAura(item, tile); },
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
                        int oldLeft = tile.Left;
                        int oldTop = tile.Top;
                        tile.Left = tile.Left + e.X - dragStartPoint.X;
                        tile.Top = tile.Top + e.Y - dragStartPoint.Y;
                        // The captured mouse sends every MouseMove here, so the
                        // rest of the selection just follows the grabbed tile.
                        if (groupDrag && groupDragTiles != null)
                        {
                            int dx = tile.Left - oldLeft;
                            int dy = tile.Top - oldTop;
                            foreach (var gt in groupDragTiles)
                            {
                                if (ReferenceEquals(gt, tile)) continue;
                                gt.Left += dx;
                                gt.Top += dy;
                            }
                        }
                        // Group reveal preview (single-tile drags only).
                        UpdateGroupPreview(panel, tabData, tile);
                    }
                }
            };

            tile.MouseUp += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left && isDragging && draggingTile == tile)
                {
                    bool wasGroupDrag = groupDrag && groupDragTiles != null && groupDragTiles.Count > 1;
                    List<TileControl> dragTiles = groupDragTiles;
                    groupDrag = false;
                    groupDragTiles = null;
                    TileGroup dropGroup = previewGroup;
                    Rectangle dropRect = previewRect;
                    ClearGroupPreview();
                    isDragging = false;
                    if (dragFired)
                    {
                        if (!isEditMode) return;

                        // Red-mode selection survives the drag: the re-render
                        // below clears it by convention, the flags and the list
                        // are restored right after it.
                        List<ShortcutItem> keepSelection =
                            (editState == 2 && multiSelection.Contains(item)) ? new List<ShortcutItem>(multiSelection) : null;

                        var ptClient = panel.PointToClient(Cursor.Position);
                        ShortcutItem targetFolder = null;
                        if (!wasGroupDrag)
                        {
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
                        }

                        if (wasGroupDrag)
                        {
                            // The whole selection was moved: keep the relative
                            // positions where the targets are free, push the
                            // blocked ones to the nearest free cell. Dropping
                            // the block onto a folder tile just places it there
                            // (bulk folder merge stays a menu action).
                            if (tabData.IsGridLayout)
                            {
                                int dxCells = (int)Math.Round((tile.Left - groupDragStartPos.X) / (double)cellWidth);
                                int dyCells = (int)Math.Round((tile.Top - groupDragStartPos.Y) / (double)cellHeight);
                                PlaceGroupInGrid(tabData, dragTiles, dxCells, dyCells, cols, rowsTotal);
                            }
                            else
                            {
                                foreach (var gt in dragTiles)
                                {
                                    gt.Item.X = gt.Left - panel.DisplayRectangle.X;
                                    gt.Item.Y = gt.Top - panel.DisplayRectangle.Y;
                                }
                            }
                        }
                        else if (targetFolder != null)
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
                                // tile.Top is a PHYSICAL coordinate while the
                                // panel is scrolled (AutoScrollPosition is
                                // negative then): convert to the logical slot.
                                int topLogical = tile.Top - panel.AutoScrollPosition.Y;
                                int col = Math.Max(0, Math.Min(cols - s, (tile.Left + cellWidth / 2) / cellWidth));
                                int row = Math.Max(0, Math.Min(rowsTotal - s, (topLogical + cellHeight / 2) / cellHeight));
                                PlaceInGrid(GetCurrentItems(tabData), item, col, row, cols, rowsTotal);
                            }
                            else
                            {
                                item.X = tile.Left - panel.DisplayRectangle.X;
                                item.Y = tile.Top - panel.DisplayRectangle.Y;
                            }
                        }
                        // Commit the previewed group expansion (single-tile
                        // drop into / near a group; folder merge wins over it).
                        if (dropGroup != null && targetFolder == null)
                        {
                            dropGroup.X = Math.Max(0, dropRect.X);
                            dropGroup.Y = Math.Max(1, dropRect.Y); // row 0 would clip the header line away
                            dropGroup.W = dropRect.Width;
                            dropGroup.H = dropRect.Height;
                        }
                        records.Save(recordsPath);
                        RenderCurrentFolder(panel, tabData);
                        if (keepSelection != null)
                        {
                            multiSelection.Clear();
                            multiSelection.AddRange(keepSelection);
                            foreach (Control c in panel.Controls)
                            {
                                var t = c as TileControl;
                                if (t == null) continue;
                                bool sel = keepSelection.Contains(t.Item);
                                if (t.MultiSelected != sel) { t.MultiSelected = sel; t.Invalidate(); }
                            }
                        }
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
                        else if (!settings.TilesOpenByDoubleClick)
                        {
                            // Single-click open (the classic behavior). In the
                            // double-click mode the MouseDoubleClick handler opens.
                            OpenTileItem(panel, tabData, tile, item);
                        }
                    }
                    draggingTile = null;
                    draggingItem = null;
                }
            };

            // Double-click open mode (settings): a double click navigates into a
            // folder or launches the item; the plain click above stays free.
            tile.MouseDoubleClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left && settings.TilesOpenByDoubleClick && editState != 2)
                    OpenTileItem(panel, tabData, tile, item);
            };

            panel.Controls.Add(tile);
        }

        private List<ShortcutItem> GetCurrentItems(TabData tabData)
        {
            var navStack = tabNavigations[tabData];
            return navStack.Count > 0 ? navStack.Peek().Children : tabData.Items;
        }

        // Opens a tile's item: navigate into folders (same window or popup) or
        // launch the path. Shared by the single-click MouseUp path and the
        // double-click handler used when TilesOpenByDoubleClick is on.
        private void OpenTileItem(Panel panel, TabData tabData, TileControl tile, ShortcutItem item)
        {
            if (item.IsFolder)
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

        // The tile-open setting as a static read (PopupTile holds no settings
        // reference of its own; CurrentSettings carries the live values).
        internal static bool TilesOpenByDoubleClick
        {
            get { Settings s = CurrentSettings; return s != null && s.TilesOpenByDoubleClick; }
        }

        internal static void OpenContainingFolder(ShortcutItem item)
        {
            if (SuppressDoubleLaunch("select:" + item.Path)) return;
            RevealInExplorer(item.Path);
        }

        // Opens Explorer with `path` selected in its folder (a plain Explorer
        // window when the parent cannot be determined). With the folder-opening
        // program set (settings), the parent folder opens in it instead — most
        // file managers have no "/select", so the folder itself is opened.
        internal static void RevealInExplorer(string path)
        {
            string dir = null;
            try { dir = Path.GetDirectoryName(path); }
            catch (Exception ex) { AppLog.Write("RevealInExplorer: GetDirectoryName", ex); }
            string fmExe, fmTemplate;
            if (TryGetFolderOpenCommand(out fmExe, out fmTemplate))
            {
                string openDir = !string.IsNullOrEmpty(dir) ? dir : path;
                if (IsDirectoryPath(openDir))
                {
                    StartDetached(fmExe, FolderOpenArgs(fmTemplate, fmExe, openDir), openDir);
                    return;
                }
            }
            if (!string.IsNullOrEmpty(dir))
            {
                bool dirExists = false;
                try { dirExists = Directory.Exists(dir); }
                catch (Exception ex) { AppLog.Write("RevealInExplorer: Directory.Exists", ex); }
                if (dirExists)
                    StartDetached("explorer.exe", "/select,\"" + path + "\"");
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
                        PlaceInGrid(targetList, child, folder.GridX + 1, folder.GridY, cols, TotalGridRows);
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
                },
                OpenMiniExplorer);
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
                // Shown WITHOUT owner: an owned window is pinned above its owner, which
                // made the mini explorer impossible to send behind the main panel.
                if (!miniExplorer.Visible) miniExplorer.Show();
                else
                {
                    // Left open on another virtual desktop: activating it as is
                    // would drag the view back there - re-home it first.
                    MoveToCurrentDesktop(miniExplorer.Handle);
                    miniExplorer.Activate();
                }
                // A folder always opens as a new tab of the existing window.
                miniExplorer.OpenInTab(item.Path);
            }
            catch (Exception ex)
            {
                AppLog.Write("OpenMiniExplorer: create/show", ex);
                ReportLaunchError(Loc.S("Mini Explorer did not open - details in log.txt",
                    "Мини-проводник не открылся — подробности в log.txt"));
            }
        }

        // ---- Aura: color + transparency ----
        // AuraColor stores "RRGGBB" (legacy records may carry "AARRGGBB"),
        // empty = no aura. The effective alpha is the tile's own AuraAlpha
        // override, or the global transparency setting - except a legacy
        // color that already carries the old default transparency (it means
        // "default" too); any other legacy value keeps acting as that tile's
        // own override. Painted by TileControl.OnPaint / PopupTile.OnPaint.

        // The slider is "transparency" 1..100%; the aura dialog's leftmost
        // position (0) is its own "from settings" state. The stored alpha
        // keeps a small floor so a tile never turns fully invisible - the
        // lightest tint is meant to match the very light tiles of the
        // Windows 10 Start menu.
        internal const int DefaultAuraTransparency = 55;
        private const int MinAuraAlpha = 12;
        // Alpha baked into aura colors by pre-v1.0.2 records ("AARRGGBB" with
        // the then-default transparency).
        private const int LegacyDefaultAuraAlpha = 132;

        internal static int AuraAlphaOf(int transparencyPct)
        {
            int a = 255 - transparencyPct * 243 / 100;
            if (a < MinAuraAlpha) a = MinAuraAlpha;
            return a;
        }

        internal static int AuraTransparencyOf(int alpha)
        {
            int t = (255 - alpha) * 100 / 243;
            if (t < 0) t = 0;
            if (t > 100) t = 100;
            return t;
        }

        internal static int GlobalAuraTransparency()
        {
            try
            {
                Settings st = CurrentSettings;
                if (st != null) return Math.Max(0, Math.Min(100, st.AuraTransparency));
            }
            catch { }
            return DefaultAuraTransparency;
        }

        internal static int GlobalAuraAlpha() { return AuraAlphaOf(GlobalAuraTransparency()); }

        internal static bool TryGetAura(ShortcutItem item, out Color color)
        {
            color = Color.Empty;
            if (item == null || string.IsNullOrEmpty(item.AuraColor)) return false;
            Color baseColor;
            if (!TryGetAura(item.AuraColor, out baseColor)) return false;
            int alpha = item.AuraAlpha;
            if (alpha <= 0)
            {
                string h = item.AuraColor.Trim().TrimStart('#');
                alpha = (h.Length == 8 && baseColor.A != LegacyDefaultAuraAlpha) ? baseColor.A : GlobalAuraAlpha();
            }
            if (alpha > 255) alpha = 255;
            color = Color.FromArgb(alpha, baseColor);
            return true;
        }

        internal static bool TryGetAura(string hex, out Color color)
        {
            color = Color.Empty;
            try
            {
                if (string.IsNullOrEmpty(hex)) return false;
                string h = hex.Trim().TrimStart('#');
                if (h.Length == 6) h = "FF" + h;
                if (h.Length != 8) return false;
                uint argb = uint.Parse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                color = Color.FromArgb(unchecked((int)argb));
                return color.A > 0;
            }
            catch { return false; }
        }

        // Slider seed for the aura dialog: 0 = the tile follows the global
        // transparency setting (the dialog's leftmost "from settings"
        // position); otherwise the tile's own alpha - an explicit AuraAlpha
        // or a legacy alpha baked into an 8-digit color (anything but the
        // old default).
        internal static int AuraSeedAlpha(ShortcutItem item)
        {
            if (item == null) return 0;
            if (item.AuraAlpha > 0) return item.AuraAlpha;
            string h = (item.AuraColor ?? "").Trim().TrimStart('#');
            Color c;
            if (h.Length == 8 && TryGetAura(h, out c) && c.A != LegacyDefaultAuraAlpha) return c.A;
            return 0;
        }

        internal static string AuraToString(Color c)
        {
            return c.A.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) +
                   c.R.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) +
                   c.G.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) +
                   c.B.ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static string AuraToRgbString(Color c)
        {
            return c.R.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) +
                   c.G.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) +
                   c.B.ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
        }

        // Solid translucent fill of a tile aura: one uniform tint with a hard
        // rounded edge, the way the Windows 10 Start tiles are tinted - no
        // gradient.
        internal static void DrawAura(Graphics g, System.Drawing.Drawing2D.GraphicsPath path, Color aura)
        {
            using (var brush = new SolidBrush(aura))
            {
                g.FillPath(brush, path);
            }
        }

        // Opens the aura picker for one tile (the multi-select menu applies its
        // result to the whole selection itself).
        private void EditItemAura(ShortcutItem item, Control tileToInvalidate)
        {
            int alpha;
            // The slider seeds at the tile's own alpha, or at 0 - the dialog's
            // "from settings" position - when the tile follows the setting.
            string r = AuraDialog.Show(this, item.AuraColor, AuraSeedAlpha(item), item.Name, out alpha);
            if (r == null) return;
            item.AuraColor = r;
            item.AuraAlpha = r.Length == 0 ? 0 : alpha;
            records.Save(recordsPath);
            if (tileToInvalidate != null) tileToInvalidate.Invalidate();
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

        // Refreshes the hover tooltip of the tile that shows this item. Tiles
        // are not direct children of contentPanel: each tab renders into its
        // own layout panel nested inside, so the walk has to be recursive.
        private void UpdateItemTooltip(ShortcutItem item)
        {
            if (itemTip == null) return;
            UpdateTooltipInControl(contentPanel, item);
        }

        private void UpdateTooltipInControl(Control root, ShortcutItem item)
        {
            foreach (Control c in root.Controls)
            {
                var tc = c as TileControl;
                if (tc != null)
                {
                    if (tc.Item == item)
                        itemTip.SetToolTip(tc, BuildItemTooltipText(item, tc.IsLabelShownPartial()));
                    continue;
                }
                if (c.Controls.Count > 0)
                    UpdateTooltipInControl(c, item);
            }
        }

        // Ctrl full-name preview: repaint every tile so the labels switch between
        // the abbreviated and the full form while the key is held / released.
        private void InvalidateAllTiles()
        {
            try
            {
                if (settings == null || !settings.LabelCtrlFullNames) return;
                foreach (Control c in contentPanel.Controls)
                {
                    var lp = c as Panel;
                    if (lp == null) continue;
                    foreach (Control t in lp.Controls)
                    {
                        var tc = t as TileControl;
                        if (tc != null) tc.Invalidate();
                    }
                }
            }
            catch { }
        }

        // Tooltip text for any item: 1) the description has priority (first
        // line); 2) the full (untruncated) name is added whenever the tile
        // face does not show it in full — the label is abbreviated
        // ("head…tail") or there is no label band at all (tiles below 56px,
        // every 1x1: the icon is all the face has, the tooltip is the only
        // place the name exists). A tile whose name is fully visible and has
        // no description shows NO tooltip — there is nothing to reveal.
        // Never returns empty/whitespace: an empty tip balloon is worse than
        // no tip. No paths: they made the tip noisy.
        internal static string BuildItemTooltipText(ShortcutItem item, bool labelPartial)
        {
            try
            {
                if (item == null) return null;
                string desc = item.ShortDescription;
                bool hasDesc = !string.IsNullOrWhiteSpace(desc);
                string label = null;
                if (labelPartial || hasDesc)
                    label = UiText.TileLabel(item.Name, item.Path, true, true);
                if (string.IsNullOrWhiteSpace(label)) label = null;
                if (!hasDesc) return label;
                if (!labelPartial || label == null) return desc.Trim();
                return desc.Trim() + "\n" + label;
            }
            catch { return null; }
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
                // After resizing, keep the tile inside the grid and off other tiles.
                PlaceInGrid(GetCurrentItems(tabData), item, item.GridX, item.GridY, cols, TotalGridRows);
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
                PlaceInGrid(targetList, item, currentFolder.GridX + 1, currentFolder.GridY, cols, TotalGridRows);
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
                int rows = TotalGridRows;
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
                    if (!ConfirmDialog.ShowConfirm(this, caption, text)) return;
                    var currentList = GetCurrentItems(tabData);
                    foreach (var it in doomed) currentList.Remove(it);
                    ClearMultiSelection();
                    records.Save(recordsPath);
                    RenderCurrentFolder(panel, tabData);
                });
                if (tabNavigations[tabData].Count > 0)
                {
                    // Raise every selected tile one level out of the open folder.
                    m.MenuItems.Add(Loc.S("Move out of folder"), (s2, e2) =>
                    {
                        var up = new List<ShortcutItem>(multiSelection);
                        foreach (var it in up) MoveItemOutOfFolder(panel, tabData, it);
                        ClearMultiSelection();
                    });
                }
                m.MenuItems.Add("-");
                // Bulk description: one dialog seeded from the first selected
                // item, the result lands on every selected tile.
                var descTargets = new List<ShortcutItem>(multiSelection);
                m.MenuItems.Add(Loc.S("Description...", "Описание..."), (s2, e2) =>
                {
                    if (descTargets.Count == 0) return;
                    string subject = descTargets.Count + " " + Loc.S("selected", "выбрано");
                    string d = DescriptionDialog.Show(this, descTargets[0], subject);
                    if (d == null) return;
                    foreach (var it in descTargets)
                    {
                        it.ShortDescription = d;
                        PanelSearch.Invalidate(it);
                        UpdateItemTooltip(it);
                    }
                    records.Save(recordsPath);
                });
                // Bulk size: the same 1x1..6x6 submenu a single folder tile has.
                var sizeMenu = m.MenuItems.Add(Loc.S("Size", "Размер"));
                var sizeTargets = new List<ShortcutItem>(multiSelection);
                for (int s = 1; s <= 6; s++)
                {
                    int sz = s;
                    sizeMenu.MenuItems.Add(sz + " x " + sz, (s2, e2) =>
                    {
                        if (sizeTargets.Count == 0) return;
                        foreach (var it in sizeTargets)
                        {
                            it.Size = sz;
                            if (tabData.IsGridLayout)
                            {
                                int cols = Math.Max(1, settings.GridColumns);
                                PlaceInGrid(GetCurrentItems(tabData), it, it.GridX, it.GridY, cols, TotalGridRows);
                            }
                        }
                        records.Save(recordsPath);
                        ClearMultiSelection();
                        RenderCurrentFolder(panel, tabData);
                    });
                }
                // Bulk icon: one file picked once, applied to every selected
                // tile (the same consolidation a single tile gets).
                var iconTargets = new List<ShortcutItem>(multiSelection);
                m.MenuItems.Add(Loc.S("Change Icon", "Сменить иконку"), (s2, e2) =>
                {
                    if (iconTargets.Count == 0) return;
                    using (var ofd = new OpenFileDialog())
                    {
                        ofd.Filter = "Icon Files (*.ico;*.exe)|*.ico;*.exe|All Files (*.*)|*.*";
                        if (ofd.ShowDialog() != DialogResult.OK) return;
                        string icoPath = ConsolidateFilePath(ofd.FileName);
                        foreach (var it in iconTargets) it.CustomIconPath = icoPath;
                        records.Save(recordsPath);
                        ClearMultiSelection();
                        RenderCurrentFolder(panel, tabData);
                    }
                });
                m.MenuItems.Add(Loc.S("Aura color...", "Цвет ауры..."), (s2, e2) =>
                {
                    var targets = new List<ShortcutItem>(multiSelection);
                    if (targets.Count == 0) return;
                    string subject = targets.Count + " " + Loc.S("selected", "выбрано");
                    int alpha;
                    string r = AuraDialog.Show(this, targets[0].AuraColor, MainForm.AuraSeedAlpha(targets[0]), subject, out alpha);
                    if (r == null) return;
                    foreach (var it in targets)
                    {
                        it.AuraColor = r;
                        it.AuraAlpha = r.Length == 0 ? 0 : alpha;
                    }
                    records.Save(recordsPath);
                    ClearMultiSelection();
                    RenderCurrentFolder(panel, tabData);
                });
                var moveTo = m.MenuItems.Add(Loc.S("Move to tab", "Переместить на вкладку"));
                FillMoveToTabMenu(moveTo, panel, tabData, new List<ShortcutItem>(multiSelection));
                // Reveal where the selection lives: one window per distinct
                // parent folder, not one per tile (ten selected tiles from one
                // directory would otherwise open ten Explorers).
                var perFolder = new Dictionary<string, ShortcutItem>(StringComparer.OrdinalIgnoreCase);
                foreach (var it in multiSelection)
                {
                    string dir = null;
                    try { dir = string.IsNullOrEmpty(it.Path) ? null : Path.GetDirectoryName(it.Path); }
                    catch { }
                    if (string.IsNullOrEmpty(dir)) continue;
                    if (!perFolder.ContainsKey(dir)) perFolder[dir] = it;
                }
                if (perFolder.Count > 0)
                    m.MenuItems.Add(Loc.S("Open containing folder", "Открыть содержащую папку"), (s2, e2) =>
                    {
                        foreach (var it in perFolder.Values) OpenContainingFolder(it);
                    });
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
        private Action<ShortcutItem> onOpenMini;
        private Color bgColor;
        private Color panelColor;
        private Color hoverColor;
        private Color textColor;
        private FlowLayoutPanel flow;
        private Label titleLbl;
        private Button backBtn;
        private Screen openScreen;
        private bool suppressDeactivate;
        // Popup-local icon queue: same time-budgeted idea as the main panel's
        // queue, so child icons fill in without blocking the popup on show.
        private readonly Queue<Action> iconTasks = new Queue<Action>();
        private System.Windows.Forms.Timer iconTimer;

        private void EnqueuePopupIcon(Action task)
        {
            iconTasks.Enqueue(task);
            if (iconTimer == null)
            {
                iconTimer = new System.Windows.Forms.Timer { Interval = 15 };
                iconTimer.Tick += (s, e) =>
                {
                    if (this.IsDisposed)
                    {
                        iconTasks.Clear();
                        iconTimer.Stop();
                        return;
                    }
                    if (iconTasks.Count == 0) { iconTimer.Stop(); return; }
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (iconTasks.Count > 0 && sw.ElapsedMilliseconds < 15)
                    {
                        Action t = iconTasks.Dequeue();
                        try { t(); } catch { }
                    }
                };
            }
            if (!iconTimer.Enabled) iconTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                iconTasks.Clear();
                if (iconTimer != null) { try { iconTimer.Stop(); iconTimer.Dispose(); } catch { } iconTimer = null; }
            }
            base.Dispose(disposing);
        }

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
            Action<ShortcutItem> onMoveOutOfFolder, Action onChanged, Action<ShortcutItem> onOpenMini)
        {
            this.folder = folderItem;
            this.settings = settings;
            this.editMode = editMode;
            this.onMoveOutOfFolder = onMoveOutOfFolder;
            this.onChanged = onChanged;
            this.onOpenMini = onOpenMini;

            // UiPalette: follows the active skin, falls back to the classic
            // light/dark colors without one (used to branch on IsLightTheme,
            // which ignored skins).
            bgColor = UiPalette.Bg;
            panelColor = UiPalette.Panel;
            hoverColor = UiPalette.Hover;
            textColor = UiPalette.Text;

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
                var added = new List<ShortcutItem>();
                foreach (var f in files)
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (string.IsNullOrEmpty(name)) name = Path.GetFileName(f);
                    var it = new ShortcutItem
                    {
                        Path = MainForm.ConsolidateFilePath(f),
                        Name = name
                    };
                    current.Children.Add(it);
                    added.Add(it);
                }
                MainForm.WarmSearchMeta(added);
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
                    Text = Loc.S("Empty", "Пусто"),
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
                var t = tile;
                EnqueuePopupIcon(delegate
                {
                    if (t.IsDisposed || this.IsDisposed) return;
                    if (MainForm.IsSlowIconSource(child))
                    {
                        // Network source: extract off the UI thread so an
                        // unreachable share can not stall the popup.
                        string cpath = child.Path;
                        bool cgroup = child.IsFolder;
                        System.Threading.ThreadPool.QueueUserWorkItem(delegate
                        {
                            Image img = null;
                            try
                            {
                                // Worker thread: the directory probe is safe here.
                                bool cfolder = cgroup || MainForm.IsFolderPathCached(cpath) ||
                                               Directory.Exists(cpath ?? "");
                                img = MainForm.LoadMediaThumbnail(cpath, 256, cfolder);
                                if (img == null && cfolder)
                                    img = MainForm.GetFolderIconImage();
                                if (img == null) img = IconExtractor.GetIconAuto(cpath, true);
                            }
                            catch { }
                            try
                            {
                                if (this.IsDisposed || t.IsDisposed) { if (img != null) img.Dispose(); return; }
                                this.BeginInvoke((MethodInvoker)delegate { t.AssignIcon(img); });
                            }
                            catch { if (img != null) try { img.Dispose(); } catch { } }
                        });
                        return;
                    }
                    try
                    {
                        // Photo/video/folder preview first, the generic icon as
                        // fallback (local paths only - network took the worker
                        // branch above).
                        Image img2 = MainForm.LoadMediaThumbnail(child.Path, 256,
                            child.IsFolder || MainForm.IsFolderPathCached(child.Path));
                        if (img2 == null && (child.IsFolder || MainForm.IsFolderPathCached(child.Path)))
                            img2 = MainForm.GetFolderIconImage();
                        if (img2 == null) img2 = IconExtractor.GetIconAuto(child.Path, true);
                        t.AssignIcon(img2);
                    }
                    catch { }
                });
                tile.Margin = new Padding(TileGap / 2);
                AttachTileHandlers(tile, child);
                tip.SetToolTip(tile, MainForm.BuildItemTooltipText(child, tile.IsLabelShownPartial()));
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
                if (child.IsFolder && !MainForm.TilesOpenByDoubleClick) NavigateInto(child);
            };

            // Double-click open mode: nested folders navigate on a double click
            // (files are launched by PopupTile.OnMouseDoubleClick).
            tile.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && child.IsFolder && MainForm.TilesOpenByDoubleClick)
                    NavigateInto(child);
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
                    // Ctrl + right-click on a folder tile: open it in the
                    // console command from the settings (empty = the menu).
                    if ((Control.ModifierKeys & Keys.Control) == Keys.Control && OpenFolderConsole(child))
                        return;
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

        private bool OpenFolderConsole(ShortcutItem child)
        {
            // Empty console command: the mini explorer opens on the folder
            // through the main window (the popup carries no window of its
            // own). A shortcut to a folder resolves to its target like on
            // the panel; non-directories keep the regular tile menu.
            string folder = MainForm.ResolveShortcutFolder(child == null ? null : child.Path);
            if (string.IsNullOrWhiteSpace(settings != null ? settings.FolderConsole : null))
            {
                if (onOpenMini != null && !string.IsNullOrEmpty(folder))
                {
                    onOpenMini(new ShortcutItem { Path = folder });
                    return true;
                }
                return false;
            }
            if (string.IsNullOrEmpty(folder)) return false;
            return MainForm.LaunchFolderConsoleCmd(settings, folder);
        }

        private void ShowTileMenu(PopupTile tile, ShortcutItem child, Point location)
        {
            var menu = new ContextMenu();
            if (!child.IsFolder && !string.IsNullOrEmpty(child.Path))
            {
                menu.MenuItems.Add(Loc.S("Open containing folder"), (s2, e2) => MainForm.OpenContainingFolder(child));
            }
            if (editMode && navStack.Count == 0) // "move out" makes sense only for the root level
            {
                menu.MenuItems.Add(Loc.S("Move out of folder"), (s2, e2) =>
                {
                    if (onMoveOutOfFolder != null) onMoveOutOfFolder(child);
                    Rebuild();
                });
            }
            if (editMode)
            {
                menu.MenuItems.Add(Loc.S("Aura color...", "Цвет ауры..."), (s2, e2) =>
                {
                    int alpha;
                    string r = AuraDialog.Show(this, child.AuraColor, MainForm.AuraSeedAlpha(child), child.Name, out alpha);
                    if (r == null) return;
                    child.AuraColor = r;
                    child.AuraAlpha = r.Length == 0 ? 0 : alpha;
                    tile.Invalidate();
                    if (onChanged != null) onChanged();
                });
                menu.MenuItems.Add(Loc.S("Remove from Panel"), (s2, e2) =>
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

            // Non-folder icons load asynchronously (AssignIcon from the popup's icon
            // queue): building the tiles must not block the popup on one shell
            // extraction per child. Folders reuse the shared cached folder icon.
            if (!item.IsFolder)
            {
                IconImage = SystemIcons.Application.ToBitmap();
            }
            else
            {
                IconImage = MainForm.GetFolderIconImage();
                if (IconImage == null) IconImage = SystemIcons.WinLogo.ToBitmap();
            }
        }

        public Image IconImage { get; private set; }

        // Shared label font/format and a cached rounded outline, same idea as in
        // TileControl: no per-repaint Font/StringFormat/GraphicsPath allocations.
        private static Font sharedLabelFont;
        private static string sharedLabelName;
        private static int sharedLabelSize;
        private static readonly Font FallbackLabelFont = new Font("Segoe UI", 8f);
        private static readonly StringFormat TileLabelFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

        private static Font GetSharedLabelFont(string name, int size)
        {
            if (sharedLabelFont == null || sharedLabelName != name || sharedLabelSize != size)
            {
                Font nf = null;
                try { nf = Settings.MakeFont(name, size); } catch { }
                if (nf != null)
                {
                    var old = sharedLabelFont;
                    sharedLabelFont = nf;
                    sharedLabelName = name;
                    sharedLabelSize = size;
                    if (old != null) { try { old.Dispose(); } catch { } }
                }
            }
            return sharedLabelFont != null ? sharedLabelFont : FallbackLabelFont;
        }

        // True when the popup tile's one-row label is abbreviated
        // ("head…tail") — the hover tooltip then carries the full name.
        public bool IsLabelShownPartial()
        {
            try
            {
                if (item == null) return false;
                Settings cfg = MainForm.CurrentSettings;
                string label = UiText.TileLabel(item.Name, item.Path,
                    cfg == null || cfg.LabelTrimShortcut, cfg == null || cfg.LabelTrimExtension);
                Font font = cfg != null && !string.IsNullOrEmpty(cfg.FontItemsName)
                    ? GetSharedLabelFont(cfg.FontItemsName, cfg.FontItemsSize)
                    : FallbackLabelFont;
                return UiText.LabelShownPartial(label, font, this.Width - 4, false);
            }
            catch { return false; }
        }

        private GraphicsPath roundPath;
        private Size roundPathSize;

        private GraphicsPath RoundPath()
        {
            Size s = new Size(this.Width, this.Height);
            if (roundPath == null || roundPathSize != s)
            {
                var old = roundPath;
                roundPath = new GraphicsPath();
                Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
                int d = 16;
                Rectangle arc = new Rectangle(rect.Location, new Size(d, d));
                roundPath.AddArc(arc, 180, 90);
                arc.X = rect.Right - d; roundPath.AddArc(arc, 270, 90);
                arc.Y = rect.Bottom - d; roundPath.AddArc(arc, 0, 90);
                arc.X = rect.Left; roundPath.AddArc(arc, 90, 90);
                roundPath.CloseFigure();
                roundPathSize = s;
                if (old != null) { try { old.Dispose(); } catch { } }
            }
            return roundPath;
        }

        // Swaps the placeholder for the real icon once the queue delivers it.
        public void AssignIcon(Image img)
        {
            if (img == null) return;
            if (IsDisposed) { try { img.Dispose(); } catch { } return; }
            var old = IconImage;
            IconImage = img;
            if (old != null) { try { old.Dispose(); } catch { } }
            Invalidate();
        }

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
                if (MainForm.TilesOpenByDoubleClick) return; // double-click mode: opened below
                MainForm.LaunchItem(item.Path);
            }
        }

        // Double-click open mode (settings): a double click launches the file.
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (SuppressClick)
            {
                SuppressClick = false;
                return;
            }
            if (e.Button == MouseButtons.Left && MainForm.TilesOpenByDoubleClick && !item.IsFolder)
                MainForm.LaunchItem(item.Path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                var path = RoundPath();
                try
                {
                    Color aura;
                    if (MainForm.TryGetAura(item, out aura))
                    {
                        MainForm.DrawAura(e.Graphics, path, aura);
                        if (hovered)
                            using (var hoverBrush = new SolidBrush(hoverColor))
                                e.Graphics.FillPath(hoverBrush, path);
                    }
                    else
                    {
                        using (var brush = new SolidBrush(hovered ? hoverColor : tileColor))
                        {
                            e.Graphics.FillPath(brush, path);
                        }
                    }
                }
                catch { }

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
                string labelName = null;
                int labelSize = 0;
                Settings cfg = MainForm.CurrentSettings;
                if (cfg != null)
                {
                    try { tColor = Settings.ParseColor(cfg.FontItemsColor, tColor); }
                    catch { }
                    labelName = cfg.FontItemsName;
                    labelSize = cfg.FontItemsSize;
                }
                try
                {
                    Font font = labelName != null ? GetSharedLabelFont(labelName, labelSize) : FallbackLabelFont;
                    using (var brush = new SolidBrush(tColor))
                    {
                        Rectangle textRect = new Rectangle(2, this.Height - 18, this.Width - 4, 16);
                        // Same display-only transform as the main tiles.
                        string label = UiText.TileLabel(item.Name, item.Path,
                            cfg == null || cfg.LabelTrimShortcut, cfg == null || cfg.LabelTrimExtension);
                        bool ctrlFull = (Control.ModifierKeys & Keys.Control) != 0 &&
                                        (cfg == null || cfg.LabelCtrlFullNames);
                        if (ctrlFull)
                        {
                            float fs = labelSize > 0 ? labelSize : 8f;
                            if (fs < 6f) fs = 6f;
                            Font fit = labelName != null
                                ? new Font(labelName, fs)
                                : new Font(FallbackLabelFont.FontFamily, fs);
                            try
                            {
                                while (fs > 5.5f && TextRenderer.MeasureText(label, fit,
                                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width > textRect.Width)
                                {
                                    fit.Dispose();
                                    fs -= 0.5f;
                                    fit = labelName != null
                                        ? new Font(labelName, fs)
                                        : new Font(FallbackLabelFont.FontFamily, fs);
                                }
                                e.Graphics.DrawString(label, fit, brush, textRect, TileLabelFormat);
                            }
                            finally { fit.Dispose(); }
                        }
                        else
                        {
                            string shown = UiText.AbbreviateMiddle(label, e.Graphics, font, textRect.Width);
                            e.Graphics.DrawString(shown, font, brush, textRect, TileLabelFormat);
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                AppLog.Write("PopupTile.OnPaint", ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (IconImage != null) IconImage.Dispose();
                if (roundPath != null) { try { roundPath.Dispose(); } catch { } roundPath = null; }
            }
            base.Dispose(disposing);
        }
    }

    // A visual container for tile groups ("grouping, not folders"): the tiles
    // keep their own grid cells, the group draws a header line above them and
    // a frame around them. Idle the group is invisible - only the header strip
    // stays live as a hover target; hovering it (or dragging a tile into the
    // group) reveals the frame. The header carries the editable name, the size
    // menu on the right, and grabbing it moves the whole group with its tiles.
    public class GroupControl : Control
    {
        public TileGroup Group;
        public TabData Tab;
        public Panel OwnerPanel;
        public Color BaseColor;
        public Color MainTextColor;
        public Func<int> GetCols;
        public Func<int> GetRows;
        public Action SaveAndRelayout;
        public Action DeleteAction;

        private int cellW = 60, cellH = 60;
        private Font ownFont; // GroupControl-owned; the ambient Font must never be disposed by painting
        private bool hover;
        private Rectangle? preview; // cells, when a tile is dragged into the group
        private bool dragging;
        private Point grabPoint;
        private Point startLocation;
        private List<TileControl> dragTiles;
        private List<Point> dragStarts;
        private TextBox nameBox;
        private static readonly StringFormat Sf = new StringFormat(StringFormatFlags.NoWrap)
        {
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };

        private const int EM_SETCUEBANNER = 0x1501;
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, string lParam);

        public GroupControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            ownFont = new Font("Segoe UI", 10f);
            Font = ownFont;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // The group surface is an opaque panel-colored plate; over it only
            // the (optional) caption, the frame and the header fill are drawn.
            using (var b = new SolidBrush(BaseColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }

        private int HeaderPx { get { return Math.Max(24, cellH / 2); } }
        private bool HasName { get { return !string.IsNullOrEmpty(Group.Name); } }
        private bool Revealed { get { return hover || preview.HasValue || dragging; } }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            UpdateRegion();
        }

        // Positions the control over its cell rect: the body is W x H cells,
        // the header strip floats one strip-height above the body.
        internal void UpdateBoundsFromCells()
        {
            if (OwnerPanel == null || Group == null) return;
            int cols = GetCols != null ? GetCols() : 8;
            int rows = GetRows != null ? GetRows() : 6;
            cellW = Math.Max(1, OwnerPanel.ClientSize.Width / Math.Max(1, cols));
            cellH = Math.Max(1, OwnerPanel.ClientSize.Height / Math.Max(1, rows));
            var scroll = OwnerPanel.AutoScrollPosition;
            int hp = HeaderPx;
            Bounds = new Rectangle(
                Group.X * cellW + scroll.X,
                Group.Y * cellH - hp + scroll.Y,
                Math.Max(1, Group.W) * cellW,
                Math.Max(1, Group.H) * cellH + hp);
        }

        internal void SetPreview(Rectangle? cells)
        {
            preview = cells;
            UpdateRegion();
            Invalidate();
        }

        private void UpdateRegion()
        {
            // The control sits above the tiles, so the window region carries
            // the whole interaction model: only the header strip and the thin
            // frame band catch the mouse and paint; the body interior is
            // excluded - clicks and the grid dots there fall through to the
            // tiles underneath.
            Region r;
            if (Revealed)
            {
                r = new Region(new Rectangle(0, 0, Width, HeaderPx));
                const int b = 3;
                r.Union(new Rectangle(0, HeaderPx, Width, b));
                r.Union(new Rectangle(0, Height - b, Width, b));
                r.Union(new Rectangle(0, HeaderPx, b, Height - HeaderPx));
                r.Union(new Rectangle(Width - b, HeaderPx, b, Height - HeaderPx));
            }
            else if (HasName)
                r = new Region(new Rectangle(0, 0, Width, HeaderPx));
            else
            {
                // Unnamed and idle: fully invisible, only a thin live line
                // along the group's top edge stays as the hover target.
                int hit = Math.Min(12, HeaderPx);
                r = new Region(new Rectangle(0, HeaderPx - hit, Width, hit));
            }
            if (Region != null) Region.Dispose();
            Region = r;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateRegion();
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hover = true;
            UpdateRegion();
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            // Crossing from the strip into the body fires MouseLeave (the body
            // is not part of the window region): collapse only when the pointer
            // is truly outside the strip and the frame band.
            var p = PointToClient(Cursor.Position);
            const int b = 3;
            bool inStrip = p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y <= HeaderPx;
            bool inFrame = p.X >= 0 && p.X < Width && p.Y > HeaderPx && p.Y < Height &&
                           (p.X < b || p.X >= Width - b || p.Y >= Height - b);
            if (inStrip || inFrame) return;
            hover = false;
            UpdateRegion();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = e.Y <= HeaderPx ? Cursors.SizeAll : Cursors.Default;
            if (!dragging) return;
            int oldLeft = Left, oldTop = Top;
            Left += e.X - grabPoint.X;
            Top += e.Y - grabPoint.Y;
            int dx = Left - oldLeft, dy = Top - oldTop;
            for (int i = 0; i < dragTiles.Count; i++)
            {
                dragTiles[i].Left += dx;
                dragTiles[i].Top += dy;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (nameBox != null)
            {
                if (!nameBox.Bounds.Contains(e.Location)) EndEdit(true);
                return;
            }
            if (e.Y > HeaderPx) return;
            if (e.X >= Width - 26) { ShowSizeMenu(); return; }
            StartDrag(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !dragging) return;
            dragging = false;
            int dcx = (int)Math.Round((Left - startLocation.X) / (double)cellW);
            int dcy = (int)Math.Round((Top - startLocation.Y) / (double)cellH);
            for (int i = 0; i < dragTiles.Count; i++)
                dragTiles[i].Location = dragStarts[i]; // snap back; the relayout re-snaps to the grid
            Location = startLocation;
            if (dcx != 0 || dcy != 0)
            {
                Group.X = Math.Max(0, Group.X + dcx);
                Group.Y = Math.Max(1, Group.Y + dcy); // row 0 would clip the header line away
                foreach (var t in dragTiles)
                {
                    t.Item.GridX = Math.Max(0, t.Item.GridX + dcx);
                    t.Item.GridY = Math.Max(0, t.Item.GridY + dcy);
                }
                if (SaveAndRelayout != null) SaveAndRelayout();
            }
            UpdateRegion();
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left && e.Y <= HeaderPx && e.X < Width - 26 && nameBox == null)
                BeginEdit();
        }

        // The members: tiles whose top-left cell is inside the group rect.
        private void StartDrag(Point grab)
        {
            dragging = true;
            grabPoint = grab;
            startLocation = Location;
            dragTiles = new List<TileControl>();
            dragStarts = new List<Point>();
            if (OwnerPanel == null) return;
            foreach (Control c in OwnerPanel.Controls)
            {
                var t = c as TileControl;
                if (t == null || t.Item == null) continue;
                if (t.Item.GridX >= Group.X && t.Item.GridX < Group.X + Group.W &&
                    t.Item.GridY >= Group.Y && t.Item.GridY < Group.Y + Group.H)
                {
                    dragTiles.Add(t);
                    dragStarts.Add(t.Location);
                }
            }
        }

        private void ShowSizeMenu()
        {
            var m = new ContextMenu();
            m.MenuItems.Add(new MenuItem(Loc.S("Group size: auto", "Размер группы: авто"), delegate
            {
                Group.FixedW = 0;
                Group.FixedH = 0;
                if (SaveAndRelayout != null) SaveAndRelayout();
            }));
            m.MenuItems.Add(new MenuItem(Loc.S("Fix width...", "Зафиксировать ширину..."), delegate
            {
                string v = Prompt.ShowDialog(Loc.S("Width in cells", "Ширина в ячейках"),
                    Loc.S("Fix width...", "Зафиксировать ширину..."), Math.Max(1, Group.W).ToString());
                int w;
                if (int.TryParse((v ?? "").Trim(), out w) && w > 0 && w <= (GetCols != null ? GetCols() : Group.W))
                {
                    Group.FixedW = w;
                    Group.W = w;
                    if (SaveAndRelayout != null) SaveAndRelayout();
                }
            }));
            m.MenuItems.Add(new MenuItem(Loc.S("Fix width and height...", "Зафиксировать ширину и высоту..."), delegate
            {
                string v = Prompt.ShowDialog(Loc.S("Size as WxH, e.g. 8x3", "Размер ШxВ, например 8x3"),
                    Loc.S("Fix width and height...", "Зафиксировать ширину и высоту..."),
                    Math.Max(1, Group.W) + "x" + Math.Max(1, Group.H));
                string[] parts = (v ?? "").ToLowerInvariant().Split('x', 'х', 'Х', '×');
                int w, h;
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out w) && int.TryParse(parts[1].Trim(), out h) &&
                    w > 0 && h > 0 && w <= (GetCols != null ? GetCols() : w) && h <= (GetRows != null ? GetRows() * 3 : h))
                {
                    Group.FixedW = w;
                    Group.FixedH = h;
                    Group.W = w;
                    Group.H = h;
                    if (SaveAndRelayout != null) SaveAndRelayout();
                }
            }));
            m.MenuItems.Add("-");
            m.MenuItems.Add(new MenuItem(Loc.S("Rename group", "Переименовать группу"), delegate { BeginEdit(); }));
            m.MenuItems.Add(new MenuItem(Loc.S("Delete group", "Удалить группу"), delegate
            {
                if (DeleteAction != null) DeleteAction();
            }));
            m.Show(this, new Point(Math.Max(0, Width - 12), HeaderPx + 2));
        }

        // Inline name editor in the header strip ("Назвать группу" cue).
        public void BeginEdit()
        {
            if (nameBox != null) { nameBox.Focus(); return; }
            hover = true;
            UpdateRegion();
            nameBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = BaseColor,
                ForeColor = MainTextColor,
                Font = Font
            };
            nameBox.SetBounds(6, (HeaderPx - 18) / 2, Math.Max(40, Width - 34), 18);
            nameBox.Text = Group.Name == null ? "" : Group.Name;
            nameBox.KeyDown += delegate(object s, KeyEventArgs e2)
            {
                if (e2.KeyCode == Keys.Enter) { e2.SuppressKeyPress = true; EndEdit(true); }
                else if (e2.KeyCode == Keys.Escape) { e2.SuppressKeyPress = true; EndEdit(false); }
            };
            nameBox.LostFocus += delegate { EndEdit(true); };
            Controls.Add(nameBox);
            nameBox.BringToFront();
            nameBox.Focus();
            SendMessage(nameBox.Handle, EM_SETCUEBANNER, 1, Loc.S("Name the group", "Назвать группу"));
        }

        private void EndEdit(bool commit)
        {
            var box = nameBox;
            if (box == null) return;
            nameBox = null;
            if (commit)
            {
                Group.Name = box.Text.Trim();
                if (SaveAndRelayout != null) SaveAndRelayout();
            }
            Controls.Remove(box);
            box.Dispose();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            bool strong = preview.HasValue;
            if (Revealed)
            {
                using (var b = new SolidBrush(Color.FromArgb(strong ? 26 : 14, MainTextColor)))
                    g.FillRectangle(b, 0, 0, Width, HeaderPx);
            }
            string caption = HasName ? Group.Name : (Revealed ? Loc.S("Name the group", "Назвать группу") : null);
            if (caption != null)
            {
                bool ghost = !HasName;
                // Only the italic variant is disposable here; the control's own
                // Font must survive the paint (disposing it made every next
                // repaint throw "Parameter is not valid").
                Font drawFont = ghost ? new Font(Font, FontStyle.Italic) : Font;
                try
                {
                    using (var b = new SolidBrush(Color.FromArgb(ghost ? 110 : 150, MainTextColor)))
                        g.DrawString(caption, drawFont, b, new Rectangle(8, 1, Math.Max(20, Width - 32), HeaderPx - 2), Sf);
                }
                finally { if (ghost) drawFont.Dispose(); }
            }
            if (!Revealed) return;
            // The size toggle on the right of the header line.
            int bx = Width - 17, by = HeaderPx / 2;
            using (var p = new Pen(Color.FromArgb(170, MainTextColor), 1.4f))
            {
                g.DrawLine(p, bx - 7, by - 3, bx + 7, by - 3);
                g.DrawLine(p, bx - 7, by, bx + 7, by);
                g.DrawLine(p, bx - 7, by + 3, bx + 7, by + 3);
            }
            // Outer perimeter of the group body: barely visible over the header
            // hover, clearly visible while something is dragged into it. The
            // control is above the tiles, so only this 2px ring (plus the
            // header) is ever painted - the body interior stays with the tiles.
            using (var p = new Pen(Color.FromArgb(strong ? 170 : 70, MainTextColor), 2f))
                g.DrawRectangle(p, 1, HeaderPx + 1, Math.Max(2, Width - 2), Math.Max(2, Height - HeaderPx - 2));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (nameBox != null)
                {
                    nameBox.Dispose();
                    nameBox = null;
                }
                if (ownFont != null)
                {
                    ownFont.Dispose();
                    ownFont = null;
                }
            }
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

        // Per-paint GDI allocations were heavy on big grids: the label font (a new
        // Font every repaint), the fallback font, the StringFormat and the rounded
        // outline path are shared/cached now.
        private static Font sharedLabelFont;
        private static string sharedLabelName;
        private static int sharedLabelSize;
        private static readonly Font FallbackLabelFont = new Font("Segoe UI", 9f);
        private static readonly StringFormat TileLabelFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

        // Horizontal alignment of the two-row label (setting): 0 left, 1 center, 2 right.
        private static readonly StringFormat[] RowFormats =
        {
            new StringFormat { Alignment = StringAlignment.Near,   LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap },
            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap },
            new StringFormat { Alignment = StringAlignment.Far,    LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }
        };

        private static StringFormat RowFormat(int align)
        {
            int a = align < 0 || align > 2 ? 1 : align;
            return RowFormats[a];
        }

        private static Font GetSharedLabelFont(string name, int size)
        {
            if (sharedLabelFont == null || sharedLabelName != name || sharedLabelSize != size)
            {
                Font nf = null;
                try { nf = Settings.MakeFont(name, size); } catch { }
                if (nf != null)
                {
                    var old = sharedLabelFont;
                    sharedLabelFont = nf;
                    sharedLabelName = name;
                    sharedLabelSize = size;
                    if (old != null) { try { old.Dispose(); } catch { } }
                }
            }
            return sharedLabelFont != null ? sharedLabelFont : FallbackLabelFont;
        }

        private GraphicsPath roundPath;
        private Size roundPathSize;

        private GraphicsPath RoundPath()
        {
            Size s = new Size(this.Width, this.Height);
            if (roundPath == null || roundPathSize != s)
            {
                var old = roundPath;
                roundPath = GetRoundRectangle(new Rectangle(0, 0, this.Width - 1, this.Height - 1), 15);
                roundPathSize = s;
                if (old != null) { try { old.Dispose(); } catch { } }
            }
            return roundPath;
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
                if (roundPath != null) { try { roundPath.Dispose(); } catch { } roundPath = null; }
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
        // Height reserved for the label. Tiles below 56px get no label; from
        // 56px one line; when the tile is tall enough for two text lines and
        // still leaves ≥36px for the icon, the label gets two rows (the icon
        // area shrinks accordingly — the folder previews use the same number).
        // The two-row mode is a setting (LabelTwoRows).
        private int GetTextSpace()
        {
            if (this.Height < 56) return 0;
            int lineH = LabelLineHeight();
            int twoRows = 2 * lineH - 6;
            bool wantTwo = true;
            try { var c = MainForm.CurrentSettings; if (c != null && !c.LabelTwoRows) wantTwo = false; } catch { }
            // Two rows as soon as the tile can host them and still leave the icon
            // ~20px (the icon scales down anyway); the old 36px reserve silently
            // disabled two rows on 2x2 tiles with larger tile fonts, which looked
            // like the mode being off until the next full repaint.
            if (wantTwo && twoRows > 24 && this.Height >= twoRows + 20) return twoRows;
            return 20;
        }

        private static int LabelLineHeight()
        {
            try
            {
                var cfg = MainForm.CurrentSettings;
                if (cfg != null && !string.IsNullOrEmpty(cfg.FontItemsName))
                {
                    Font f = GetSharedLabelFont(cfg.FontItemsName, cfg.FontItemsSize);
                    if (f != null) return f.Height;
                }
            }
            catch { }
            return 20;
        }

        // True when the face does not show this tile's full label: either
        // OnPaint abbreviates it ("head…tail") or there is no label band at
        // all (tiles below 56px — every 1x1). The hover tooltip then carries
        // the full name; on a band-less tile it is the only place the name
        // exists. Uses the same label text, font and band logic as OnPaint.
        public bool IsLabelShownPartial()
        {
            try
            {
                if (Item == null) return false;
                Settings cfg = MainForm.CurrentSettings;
                string label = UiText.TileLabel(Item.Name, Item.Path,
                    cfg == null || cfg.LabelTrimShortcut, cfg == null || cfg.LabelTrimExtension);
                int labelSpace = GetTextSpace();
                if (labelSpace <= 0) return true; // no label band: the face shows no name at all
                string fontName = cfg != null ? cfg.FontItemsName : null;
                int fontSize = cfg != null ? cfg.FontItemsSize : 0;
                Font font = fontName != null ? GetSharedLabelFont(fontName, fontSize) : FallbackLabelFont;
                return UiText.LabelShownPartial(label, font, this.Width - 4, labelSpace > 30);
            }
            catch { return false; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                var path = RoundPath();

                if (Item.IsFolder)
                {
                    try
                    {
                        Color aura;
                        if (MainForm.TryGetAura(Item, out aura))
                        {
                            // The folder's own aura replaces the neutral gray fill.
                            MainForm.DrawAura(e.Graphics, path, aura);
                        }
                        else
                        {
                            using (var brush = new SolidBrush(Color.FromArgb(50, 128, 128, 128)))
                            {
                                e.Graphics.FillPath(brush, path);
                            }
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
                    // Per-tile "aura": a translucent color fill behind the icon.
                    // Without one the icon sits directly on the panel background,
                    // with only a soft highlight when hovered.
                    try
                    {
                        Color aura;
                        if (MainForm.TryGetAura(Item, out aura))
                        {
                            MainForm.DrawAura(e.Graphics, path, aura);
                        }
                    }
                    catch { }
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
                string labelName = null;
                int labelSize = 0;
                Settings cfg = MainForm.CurrentSettings;
                if (cfg != null)
                {
                    try { tColor = Settings.ParseColor(cfg.FontItemsColor, tColor); }
                    catch { }
                    labelName = cfg.FontItemsName;
                    labelSize = cfg.FontItemsSize;
                }
                int labelSpace = GetTextSpace();
                if (labelSpace > 0)
                {
                    try
                    {
                        Font font = labelName != null ? GetSharedLabelFont(labelName, labelSize) : FallbackLabelFont;
                        using (var brush = new SolidBrush(tColor))
                        {
                            Rectangle textRect = new Rectangle(2, this.Height - labelSpace - 1, this.Width - 4, labelSpace);
                            // Display-only name transform (settings checkboxes):
                            // hide the shortcut suffix and/or the path extension.
                            string label = UiText.TileLabel(Item.Name, Item.Path,
                                cfg == null || cfg.LabelTrimShortcut, cfg == null || cfg.LabelTrimExtension);
                            // Ctrl held (and enabled in settings): show the full
                            // name instead of the abbreviated one - the label font
                            // shrinks until the whole text fits one line.
                            bool ctrlFull = (Control.ModifierKeys & Keys.Control) != 0 &&
                                            (cfg == null || cfg.LabelCtrlFullNames);
                            string[] rows = (!ctrlFull && labelSpace > 30 && (cfg == null || cfg.LabelTwoRows))
                                ? UiText.WrapTwo(label, e.Graphics, font, textRect.Width) : null;
                            if (rows == null)
                            {
                                // One row, anchored to the bottom of the band so a
                                // short name sits where the old label sat; the
                                // "head…tail" abbreviation applies when too wide.
                                int stripH = Math.Min(labelSpace, font.Height + 4);
                                Rectangle oneRect = new Rectangle(2, this.Height - stripH - 1, this.Width - 4, stripH);
                                if (ctrlFull)
                                {
                                    float fs = labelSize > 0 ? labelSize : 9f;
                                    if (fs < 6f) fs = 6f;
                                    Font fit = labelName != null
                                        ? new Font(labelName, fs)
                                        : new Font(FallbackLabelFont.FontFamily, fs);
                                    try
                                    {
                                        while (fs > 5.5f && TextRenderer.MeasureText(label, fit,
                                            new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width > textRect.Width)
                                        {
                                            fit.Dispose();
                                            fs -= 0.5f;
                                            fit = labelName != null
                                                ? new Font(labelName, fs)
                                                : new Font(FallbackLabelFont.FontFamily, fs);
                                        }
                                        e.Graphics.DrawString(label, fit, brush, oneRect, TileLabelFormat);
                                    }
                                    finally { fit.Dispose(); }
                                }
                                else
                                {
                                    string shown = UiText.AbbreviateMiddle(label, e.Graphics, font, oneRect.Width);
                                    e.Graphics.DrawString(shown, font, brush, oneRect, TileLabelFormat);
                                }
                            }
                            else
                            {
                                int lineH = font.Height;
                                int y1 = textRect.Top + (textRect.Height - lineH * 2) / 2;
                                var fmt = RowFormat(cfg == null ? 1 : cfg.LabelAlign2Rows);
                                e.Graphics.DrawString(rows[0], font, brush, new Rectangle(textRect.X, y1, textRect.Width, lineH), fmt);
                                string shown2 = UiText.AbbreviateMiddle(rows[1], e.Graphics, font, textRect.Width);
                                e.Graphics.DrawString(shown2, font, brush, new Rectangle(textRect.X, y1 + lineH, textRect.Width, lineH), fmt);
                            }
                        }
                    }
                    catch { }
                }

                // Multi-select highlight (red edit-button mode): a red outline
                // plus a red checkmark badge in the top-right corner.
                if (MultiSelected)
                {
                    try
                    {
                        using (var pen = new Pen(Color.FromArgb(235, 60, 40), 2f))
                            e.Graphics.DrawPath(pen, path);
                        int d = Math.Max(12, Math.Min(20, Math.Min(this.Width, this.Height) / 4));
                        int bx = this.Width - d - 3;
                        int by = 3;
                        using (var brush = new SolidBrush(Color.FromArgb(235, 60, 40)))
                            e.Graphics.FillEllipse(brush, bx, by, d, d);
                        float k = d / 20f;
                        using (var checkPen = new Pen(Color.White, Math.Max(2f, 3.4f * k)))
                        {
                            checkPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                            checkPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                            e.Graphics.DrawLines(checkPen, new[]
                            {
                                new PointF(bx + 5f * k, by + 10.5f * k),
                                new PointF(bx + 9f * k, by + 14.5f * k),
                                new PointF(bx + 15f * k, by + 6.5f * k)
                            });
                        }
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

    // Tab layout panel with a custom floating scrollbar. The native bar is
    // removed for good: the WS_VSCROLL style bit is stripped whenever WinForms
    // re-adds it (the scroll info and the wheel keep working without the
    // style, and the client area always keeps the full width). A slim pill is
    // drawn instead (FloatingScrollThumb); it appears on any scroll activity
    // and fades out after a short idle, so the panel is clean while not being
    // scrolled. NOTE: hiding the bar by expanding the client rect over it
    // (WM_NCCALCSIZE) was tried and rejected - in the reclaimed strip the
    // native bar's own painting still lands on top and child controls render
    // unreliably.
    public class ScrollPanel : Panel
    {
        private const int GWL_STYLE = -16;
        private const int WS_VSCROLL = 0x00200000;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private FloatingScrollThumb thumb;
        private System.Windows.Forms.Timer fade;
        private int idleTicks;          // fade ticks since the last scroll pulse
        private float opacity = 1f;     // current thumb opacity (0..1)
        private Color bgColor = Color.FromArgb(240, 240, 240);
        private bool lightTheme = true;

        public ScrollPanel()
        {
            DoubleBuffered = true;
            thumb = new FloatingScrollThumb(this);
            thumb.Visible = false;
            Controls.Add(thumb);
            // Any scroll source (wheel, thumb drag, AutoScrollPosition) fires
            // this: show the thumb and reset the idle countdown.
            Scroll += delegate { Pulse(); };
            fade = new System.Windows.Forms.Timer { Interval = 50 };
            fade.Tick += FadeTick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                // WS_CLIPCHILDREN: without it the panel's own (double-buffered)
                // background repaints cover the pill child window's pixels.
                var cp = base.CreateParams;
                cp.Style |= 0x02000000;
                return cp;
            }
        }

        // Theme look for the thumb, blended over the panel background.
        public void SetTheme(Color bg, bool light)
        {
            bgColor = bg;
            lightTheme = light;
            thumb.Invalidate();
        }

        // Re-attaches the thumb after a re-render: RenderCurrentFolder clears
        // the control collection (disposing the old thumb with it), so the
        // overlay has to be recreated before it can float above the tiles.
        public void EnsureThumb()
        {
            try
            {
                if (thumb == null || thumb.IsDisposed) thumb = new FloatingScrollThumb(this);
                if (!Controls.Contains(thumb))
                {
                    thumb.Visible = false;
                    Controls.Add(thumb);
                }
                // Controls.Add appends at the BOTTOM of the z-order (index 0 is
                // the front): without this the tiles added before the thumb
                // cover its opaque surface and the pill never shows.
                thumb.BringToFront();
                RepositionThumb();
            }
            catch { }
        }

        public Color PanelBg { get { return bgColor; } }
        public bool LightBg { get { return lightTheme; } }
        public float ThumbOpacity { get { return opacity; } }

        // Tab switch: a tab always opens at the top - the scroll offset belonged
        // to the previous session of this tab. The pill stays hidden (no pulse:
        // a programmatic position set raises no .NET Scroll event).
        public void ScrollToTop()
        {
            try
            {
                if (AutoScrollPosition.X == 0 && AutoScrollPosition.Y == 0) return;
                AutoScrollPosition = new Point(0, 0);
                if (thumb != null && !thumb.IsDisposed) RepositionThumb();
            }
            catch { }
        }

        private bool CanScroll()
        {
            return DisplayRectangle.Height - ClientSize.Height > 0;
        }

        // Thumb metrics for the current scroll state: top/height inside the
        // client, scrollMax = how many pixels the content can move.
        public void ThumbMetrics(out int top, out int height, out int scrollMax)
        {
            int viewport = Math.Max(1, ClientSize.Height);
            int content = Math.Max(viewport, DisplayRectangle.Height);
            scrollMax = content - viewport;
            height = viewport * viewport / content;
            if (height > viewport - 8) height = viewport - 8;
            if (height < 28) height = 28;
            int pos = -AutoScrollPosition.Y;
            if (pos < 0) pos = 0;
            if (pos > scrollMax) pos = scrollMax;
            top = 4 + (viewport - 8 - height) * pos / Math.Max(1, scrollMax);
        }

        private void RepositionThumb()
        {
            try
            {
                if (thumb.IsDisposed) return;
                int top, height, scrollMax;
                ThumbMetrics(out top, out height, out scrollMax);
                // The control IS the pill (6px), 2px away from the window edge.
                var bounds = new Rectangle(Math.Max(0, ClientSize.Width - 8), top, 6, height);
                if (thumb.Bounds != bounds) thumb.Bounds = bounds;
            }
            catch { }
        }

        // Scroll activity happened: fully visible thumb, idle countdown from 0.
        private void Pulse()
        {
            try
            {
                if (!CanScroll())
                {
                    opacity = 0f;
                    thumb.Visible = false;
                    fade.Stop();
                    return;
                }
                idleTicks = 0;
                opacity = 1f;
                bool wasVisible = thumb.Visible;
                RepositionThumb();
                thumb.Visible = true;
                thumb.BringToFront();
                if (!wasVisible || !fade.Enabled) thumb.Invalidate();
                if (!fade.Enabled) fade.Start();
            }
            catch { }
        }

        // Called by the thumb while it is being dragged with the mouse.
        public void ScrollByThumb(int thumbTop)
        {
            try
            {
                int top, height, scrollMax;
                ThumbMetrics(out top, out height, out scrollMax);
                if (scrollMax <= 0) return;
                int pos = (thumbTop - 4) * scrollMax / Math.Max(1, ClientSize.Height - 8 - height);
                if (pos < 0) pos = 0;
                if (pos > scrollMax) pos = scrollMax;
                AutoScrollPosition = new Point(0, pos);
                // A programmatic AutoScrollPosition set does not raise the .NET
                // Scroll event: pulse explicitly so the thumb tracks the drag.
                Pulse();
            }
            catch { }
        }

        // Page jump on a strip click above/below the thumb.
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            try
            {
                if (e.Button != MouseButtons.Left || !CanScroll()) return;
                if (e.X < ClientSize.Width - 14) return;
                int top, height, scrollMax;
                ThumbMetrics(out top, out height, out scrollMax);
                int pos = -AutoScrollPosition.Y;
                if (pos < 0) pos = 0; if (pos > scrollMax) pos = scrollMax;
                int page = Math.Max(1, ClientSize.Height - height);
                pos = e.Y < top ? pos - page : pos + page;
                if (pos < 0) pos = 0;
                if (pos > scrollMax) pos = scrollMax;
                AutoScrollPosition = new Point(0, pos);
                Pulse(); // show/refresh the thumb (no Scroll event for this)
            }
            catch { }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            try { if (thumb.Visible) RepositionThumb(); } catch { }
        }

        private void FadeTick(object sender, EventArgs e)
        {
            try
            {
                if (thumb.IsDisposed) { fade.Stop(); return; }
                // Grabbed or hovered thumb stays fully visible.
                if (thumb.Dragging || thumb.Hovered)
                {
                    idleTicks = 0;
                    opacity = 1f;
                    thumb.Invalidate();
                    return;
                }
                if (idleTicks < 14) { idleTicks++; return; } // ~0.7s solid, then fade
                opacity -= 0.12f;
                if (opacity <= 0f)
                {
                    opacity = 0f;
                    thumb.Visible = false;
                    fade.Stop();
                }
                thumb.Invalidate();
            }
            catch { }
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            // The thumb is a child of the scrolling panel: every scroll shifts
            // it with the content. Snap it back to the strip after the scroll.
            try { if (thumb != null && !thumb.IsDisposed && thumb.Visible) RepositionThumb(); } catch { }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            // Strip the vertical scrollbar style whenever WinForms re-adds it:
            // the native bar never appears and the client keeps the full width.
            // Scrolling itself (scroll info, wheel, AutoScrollPosition) does not
            // need the style bit. The check is a no-op while the bit is clear,
            // and clearing it triggers its own recalc that finds it clear, so
            // there is no loop.
            try
            {
                int style = GetWindowLong(m.HWnd, GWL_STYLE);
                if ((style & WS_VSCROLL) != 0)
                    SetWindowLong(m.HWnd, GWL_STYLE, style & ~WS_VSCROLL);
            }
            catch { }
            // The wheel arrives here directly (focused panel) or bubbled from a
            // focused tile by DefWindowProc.
            if (m.Msg == WM_MOUSEWHEEL)
            {
                Pulse();
                base.WndProc(ref m);
                // The scroll above shifted the thumb with the content: snap it
                // back before the frame paints.
                try { if (thumb != null && !thumb.IsDisposed && thumb.Visible) RepositionThumb(); } catch { }
                return;
            }
            base.WndProc(ref m);
        }
    }

    // The floating scrollbar pill: a slim bar at the right edge of a
    // ScrollPanel, floating above the content. The control is exactly the
    // pill (6px wide) and fills itself in OnPaintBackground - a window Region
    // was tried for the rounded caps and silently broke the painting of the
    // whole control (nothing rendered), so the pill is a plain 6px strip.
    public class FloatingScrollThumb : Control
    {
        private readonly ScrollPanel owner;
        internal bool Dragging;
        internal bool Hovered;

        public FloatingScrollThumb(ScrollPanel owner)
        {
            this.owner = owner;
            // Selectable=false keeps clicks from stealing the focus; plain
            // default paint styles + DoubleBuffered. The explicit
            // Opaque|AllPaintingInWmPaint|UserPaint combo was tried here and
            // silently killed ALL painting of this window (no WM_PAINT ever
            // reached OnPaintBackground) - do not re-add it.
            SetStyle(ControlStyles.Selectable, false);
            DoubleBuffered = true;
            TabStop = false;
            Cursor = Cursors.Arrow;
            Width = 6;
            Height = 60;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Dragging = true;
                grabY = e.Y;
                grabTop = Top;
                Capture = true;
                Invalidate();
            }
        }
        private int grabY;
        private int grabTop;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Dragging)
            {
                // The Scroll event (fired by AutoScrollPosition) calls back into
                // Pulse and repositions the thumb; the drag origin stays fixed,
                // so the target slot is always derived from the press point.
                owner.ScrollByThumb(grabTop + e.Y - grabY);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (Dragging)
            {
                Dragging = false;
                Capture = false;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Hovered = false;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // The whole control IS the pill: one blended fill, opacity-aware.
            try
            {
                float op = owner.ThumbOpacity;
                if (op <= 0f) return;
                Color fg = owner.LightBg ? Color.FromArgb(30, 30, 30) : Color.FromArgb(240, 240, 240);
                float a = 0.28f + 0.32f * op;                 // fades with opacity
                if (Hovered || Dragging) a = Math.Min(0.85f, a + 0.2f);
                using (var brush = new SolidBrush(Blend(owner.PanelBg, fg, a)))
                    e.Graphics.FillRectangle(brush, ClientRectangle);
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Everything is drawn in OnPaintBackground.
        }

        private static Color Blend(Color bg, Color fg, float a)
        {
            return Color.FromArgb(
                (int)Math.Round(bg.R + (fg.R - bg.R) * a),
                (int)Math.Round(bg.G + (fg.G - bg.G) * a),
                (int)Math.Round(bg.B + (fg.B - bg.B) * a));
        }
    }

    public static class Prompt
    {
        public static string ShowDialog(string text, string caption, string defaultValue = "")
        {
            // Follow the active theme/skin: this dialog used to be hard-coded
            // dark, so "Create Folder" / "Rename" popped up as a dark box inside
            // a light-themed app.
            Color bg = UiPalette.Bg;
            Color panel = UiPalette.Panel;
            Color fg = UiPalette.Text;
            Form prompt = new Form()
            {
                Width = 400,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = bg,
                ForeColor = fg
            };
            if (MainForm.CurrentSettings != null)
                prompt.Font = Settings.MakeFont(MainForm.CurrentSettings.FontUiName, MainForm.CurrentSettings.FontUiSize);

            // Row positions follow the (possibly large) UI font.
            int fh = prompt.Font.Height;
            Label textLabel = new Label() { Left = 20, Top = 14, Text = text, Width = 350, Height = fh + 4, AutoSize = false };
            TextBox textBox = new TextBox() { Left = 20, Top = 14 + fh + 10, Width = 350, Text = defaultValue, Font = prompt.Font, BackColor = panel, ForeColor = fg };
            Button confirmation = new Button() { Text = Loc.S("OK", "ОК"), Left = 280, Width = 100, Height = fh + 12, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = panel, ForeColor = fg };
            confirmation.FlatAppearance.BorderSize = 0;

            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            // The OK button is placed from the font-derived height, NOT from
            // textBox.Height: a single-line TextBox carries the FixedHeight
            // style and only syncs its Height to the ambient (possibly large)
            // UI font when its handle is recreated - asynchronously. Measuring
            // it earlier yields the 9pt default, and with the 14pt UI font the
            // grown box ran under the button, shoving it up into the textbox.
            confirmation.Top = textBox.Top + textBox.PreferredHeight + 12;
            prompt.ClientSize = new Size(400, confirmation.Top + confirmation.Height + 14);

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
    }

    // Themed confirmation dialog used when removing elements.
    // Removing a folder shows a red warning that everything inside will be deleted too.
    public class ConfirmDialog : Form
    {
        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        // okText = caption of the confirm button (null → "Удалить", red button).
        // cancelVisible=false turns the dialog into an info box with a single
        // neutral OK button.
        private ConfirmDialog(string title, string message, string danger, string okText, bool cancelVisible)
        {
            Settings st = MainForm.CurrentSettings;
            Color bg = UiPalette.Bg;
            Color panel = UiPalette.Panel;
            Color txt = UiPalette.Text;

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

            string okCaption = okText ?? Loc.S("Remove", "Удалить");
            int btnH = fh + 12;
            int okW = Math.Max(95, System.Windows.Forms.TextRenderer.MeasureText(okCaption, this.Font).Width + 26);
            int cancelW = Math.Max(95, System.Windows.Forms.TextRenderer.MeasureText(Loc.S("Cancel", "Отмена"), this.Font).Width + 26);
            int btnTop = contentBottom + 14;
            this.ClientSize = new Size(430, btnTop + btnH + 16);

            // Destructive confirm = red button; info box = neutral panel button.
            Color okBg = cancelVisible ? Color.FromArgb(170, 48, 48) : panel;
            var okBtn = new Button { Text = okCaption, Left = this.ClientSize.Width - 20 - okW, Top = btnTop, Width = okW, Height = btnH, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = okBg, ForeColor = cancelVisible ? Color.White : txt };
            okBtn.FlatAppearance.BorderSize = 0;
            this.Controls.Add(okBtn);
            this.AcceptButton = okBtn;

            Button cancelBtn = null;
            if (cancelVisible)
            {
                cancelBtn = new Button { Text = Loc.S("Cancel", "Отмена"), Left = this.ClientSize.Width - 20 - okW - 10 - cancelW, Top = btnTop, Width = cancelW, Height = btnH, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = panel, ForeColor = txt };
                cancelBtn.FlatAppearance.BorderSize = 0;
                this.Controls.Add(cancelBtn);
                this.AcceptButton = cancelBtn;
            }
            this.CancelButton = cancelBtn ?? okBtn;

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

            return ShowConfirm(owner, title, message, danger);
        }

        // Themed Yes/No replacement: red confirm button (default caption
        // "Удалить"), optional danger line, themed cancel. True = confirmed.
        public static bool ShowConfirm(IWin32Window owner, string title, string message, string danger = null, string okText = null)
        {
            using (var dlg = new ConfirmDialog(title, message, danger, okText, true))
            {
                return (owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog()) == DialogResult.OK;
            }
        }

        // Themed MessageBox replacement: single neutral OK button.
        public static void ShowInfo(IWin32Window owner, string message, string title = null)
        {
            using (var dlg = new ConfirmDialog(title ?? Loc.S("Tilettes", "Плиточки"), message, null, Loc.S("OK", "ОК"), false))
            {
                if (owner != null) dlg.ShowDialog(owner); else dlg.ShowDialog();
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

        private DescriptionDialog(ShortcutItem item, string subject)
        {
            Settings st = MainForm.CurrentSettings;
            Color bg = UiPalette.Bg;
            Color panel = UiPalette.Panel;
            Color txt = UiPalette.Text;
            Color dim = UiPalette.Dim;

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
                Text = Loc.S("Description", "Описание") + " — " + (string.IsNullOrEmpty(subject) ? item.Name : subject),
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
            cancel.FlatAppearance.MouseOverBackColor = UiPalette.Hover;
            var ok = new Button { Text = Loc.S("OK", "ОК"), Left = 355, Top = btnTop, Width = 95, Height = btnH, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = panel, ForeColor = txt };
            ok.FlatAppearance.BorderSize = 0;
            ok.FlatAppearance.MouseOverBackColor = UiPalette.Hover;
            ok.Click += (s, e) => { accepted = true; };
            this.Controls.Add(cancel);
            this.Controls.Add(ok);
            this.AcceptButton = ok;
            this.CancelButton = cancel;
        }

        // Returns the entered text, or null when the dialog was cancelled.
        public static string Show(IWin32Window owner, ShortcutItem item)
        {
            return Show(owner, item, null);
        }

        // `subject` replaces the item name in the title (bulk edit: "N selected").
        public static string Show(IWin32Window owner, ShortcutItem item, string subject)
        {
            using (var dlg = new DescriptionDialog(item, subject))
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

    // Themed picker for the per-tile background color ("aura"): preset swatches,
    // a custom color via the system color dialog, a transparency slider (the
    // leftmost position = the global setting's value) and a live preview.
    // Returns null on Cancel, "" when the aura is removed, otherwise the
    // picked "RRGGBB" plus alphaOverride (0 = follow the global transparency
    // setting, otherwise the tile's own alpha).
    public class AuraDialog : Form
    {
        private Color baseColor;      // picked color; alpha always comes from the slider
        private bool hasColor;
        private bool accepted;
        private bool resetRequested;
        private string result;
        private int resultAlpha;      // 0 = follow the global transparency setting
        private TrackBar transparency;
        private Label transparencyLabel;   // static caption above the slider
        private Label valueLabel;          // the value line under the slider
        private Panel preview;
        private Button selectedSwatch;

        private static readonly Color[] Presets = new Color[]
        {
            Color.FromArgb(231, 76, 60),   // red
            Color.FromArgb(230, 126, 34),  // orange
            Color.FromArgb(241, 196, 15),  // yellow
            Color.FromArgb(46, 204, 113),  // green
            Color.FromArgb(26, 188, 156),  // teal
            Color.FromArgb(52, 152, 219),  // blue
            Color.FromArgb(93, 109, 192),  // indigo
            Color.FromArgb(155, 89, 182),  // purple
            Color.FromArgb(233, 30, 99),   // pink
            Color.FromArgb(141, 110, 99),  // brown
            Color.FromArgb(149, 165, 166), // gray
            Color.FromArgb(52, 73, 94)     // dark slate
        };

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        private AuraDialog(string currentAura, int currentAlpha, string subject)
        {
            Settings st = MainForm.CurrentSettings;
            Color bg = UiPalette.Bg;
            Color panel = UiPalette.Panel;
            Color txt = UiPalette.Text;
            Color dim = UiPalette.Dim;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = bg;
            this.ForeColor = txt;
            if (st != null) this.Font = Settings.MakeFont(st.FontUiName, st.FontUiSize);

            int fh = this.Font.Height;
            Font titleFont = Settings.MakeFont(st != null ? st.FontUiName : "Segoe UI", (st != null ? st.FontUiSize : 9) + 1, System.Drawing.FontStyle.Bold);
            int tfh = titleFont.Height;

            string caption = Loc.S("Aura", "Аура") + (string.IsNullOrEmpty(subject) ? "" : " — " + subject);
            var title = new Label { Text = caption, Left = 20, Top = 14, Width = 430, Height = tfh + 6, Font = titleFont };
            this.Controls.Add(title);

            string infoText = Loc.S("Background color of the element; transparency softens it over the panel.",
                                    "Фоновый цвет элемента; прозрачность делает его мягче поверх панели.");
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

            int contentTop = info.Bottom + 10;

            // Live preview: a rounded tile-like rect over the panel background.
            preview = new Panel { Left = 20, Top = contentTop, Width = 110, Height = 86, BackColor = panel };
            preview.Paint += PreviewPaint;
            this.Controls.Add(preview);

            // Preset swatches: two rows of six.
            var swatches = new FlowLayoutPanel
            {
                Left = 142,
                Top = contentTop,
                Width = 6 * 32 + 4,
                Height = 2 * 32 + 2,
                BackColor = bg
            };
            foreach (var c in Presets)
            {
                var sw = new Button
                {
                    Size = new Size(28, 28),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = c,
                    Margin = new Padding(2),
                    Tag = c,
                    Cursor = Cursors.Hand
                };
                sw.FlatAppearance.BorderSize = 0;
                sw.FlatAppearance.MouseOverBackColor = c;
                sw.Click += (s, e) => PickBaseColor((Color)((Button)s).Tag, (Button)s);
                swatches.Controls.Add(sw);
            }
            this.Controls.Add(swatches);

            transparencyLabel = new Label { Text = Loc.S("Transparency:", "Прозрачность:"), Left = 142, Top = swatches.Bottom + 6, Width = 316, Height = fh + 2, ForeColor = txt };
            this.Controls.Add(transparencyLabel);
            transparency = new TrackBar
            {
                Left = 140,
                Top = transparencyLabel.Bottom + 2,
                Width = 318,
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 10,
                SmallChange = 5,
                LargeChange = 10
            };
            transparency.ValueChanged += (s, e) => UpdateTransparencyLabel();
            this.Controls.Add(transparency);
            // The value line under the slider: a percentage, or the leftmost
            // position's own name - "the value from the settings".
            valueLabel = new Label { Left = 142, Top = transparency.Bottom + 2, Width = 316, Height = fh + 2, ForeColor = txt };
            this.Controls.Add(valueLabel);

            // Initial state: the tile's current aura, or the first preset swatch
            // so that OK always has a color to store (Reset removes the aura).
            // The slider starts at the tile's current effective transparency, or
            // the global default when the tile has no aura yet.
            Color existing;
            if (MainForm.TryGetAura(currentAura, out existing))
            {
                hasColor = true;
                baseColor = Color.FromArgb(255, existing);
                PickBaseColor(baseColor, null);
            }
            else
            {
                PickBaseColor(Presets[0], swatches.Controls.Count > 0 ? (Button)swatches.Controls[0] : null);
            }
            // A seed of 0 means the tile follows the global transparency
            // setting: the slider starts at its leftmost "from settings"
            // position. An explicit alpha seeds the slider itself; one that
            // maps to 0 (fully opaque) sits at 1 - the same look, and the
            // position stays distinct from the "from settings" state.
            transparency.Value = currentAlpha <= 0
                ? 0
                : Math.Max(1, Math.Min(100, MainForm.AuraTransparencyOf(currentAlpha)));
            UpdateTransparencyLabel();

            var custom = new Button
            {
                Text = Loc.S("Custom color...", "Другой цвет..."),
                Left = 20,
                Top = preview.Bottom + 8,
                Width = 110,
                Height = fh + 12,
                FlatStyle = FlatStyle.Flat,
                BackColor = panel,
                ForeColor = txt,
                Cursor = Cursors.Hand
            };
            custom.FlatAppearance.BorderSize = 0;
            custom.FlatAppearance.MouseOverBackColor = UiPalette.Hover;
            custom.Click += (s, e) =>
            {
                using (var cd = new ColorDialog { FullOpen = true, Color = hasColor ? baseColor : SystemColors.Control })
                {
                    if (cd.ShowDialog(this) == DialogResult.OK)
                        PickBaseColor(cd.Color, null);
                }
            };
            this.Controls.Add(custom);

            // Jumps to the leftmost position: the tile follows the global
            // transparency setting again.
            var standard = new Button
            {
                Text = Loc.S("Standard value", "Стандартное значение"),
                Left = 140,
                Top = valueLabel.Bottom + 4,
                Width = 190,
                Height = fh + 12,
                FlatStyle = FlatStyle.Flat,
                BackColor = panel,
                ForeColor = txt,
                Cursor = Cursors.Hand
            };
            standard.FlatAppearance.BorderSize = 0;
            standard.FlatAppearance.MouseOverBackColor = UiPalette.Hover;
            standard.Click += (s, e) => { transparency.Value = 0; };
            this.Controls.Add(standard);

            int btnH = fh + 12;
            int bottom = Math.Max(custom.Bottom + 6, standard.Bottom + 6);
            int btnTop = bottom + 12;
            this.ClientSize = new Size(470, btnTop + btnH + 16);
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));

            var reset = new Button { Text = Loc.S("Reset", "Сбросить"), Left = 20, Top = btnTop, Width = 95, Height = btnH, FlatStyle = FlatStyle.Flat, BackColor = panel, ForeColor = txt };
            reset.FlatAppearance.BorderSize = 0;
            reset.FlatAppearance.MouseOverBackColor = UiPalette.Hover;
            reset.Click += (s, e) => { resetRequested = true; this.Close(); };
            var cancel = new Button { Text = Loc.S("Cancel", "Отмена"), Left = 250, Top = btnTop, Width = 95, Height = btnH, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = panel, ForeColor = txt };
            cancel.FlatAppearance.BorderSize = 0;
            cancel.FlatAppearance.MouseOverBackColor = UiPalette.Hover;
            var ok = new Button { Text = Loc.S("OK", "ОК"), Left = 355, Top = btnTop, Width = 95, Height = btnH, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = panel, ForeColor = txt };
            ok.FlatAppearance.BorderSize = 0;
            ok.FlatAppearance.MouseOverBackColor = UiPalette.Hover;
            ok.Click += (s, e) =>
            {
                accepted = true;
                result = MainForm.AuraToRgbString(baseColor);
                // The leftmost position stores no per-tile override, so the
                // tile keeps following the global setting when it changes.
                resultAlpha = transparency.Value == 0
                    ? 0
                    : MainForm.AuraAlphaOf(transparency.Value);
            };
            this.Controls.Add(reset);
            this.Controls.Add(cancel);
            this.Controls.Add(ok);
            this.AcceptButton = ok;
            this.CancelButton = cancel;
        }

        private void PickBaseColor(Color c, Button sw)
        {
            baseColor = Color.FromArgb(255, c);
            hasColor = true;
            if (selectedSwatch != null) selectedSwatch.FlatAppearance.BorderSize = 0;
            selectedSwatch = sw;
            if (sw != null) sw.FlatAppearance.BorderSize = 2;
            preview.Invalidate();
        }

        private void UpdateTransparencyLabel()
        {
            // The leftmost position is not 0% transparency - it is the state
            // where the tile follows the global setting; the preview paints
            // it with that value.
            valueLabel.Text = transparency.Value == 0
                ? Loc.S("Value from settings", "Значение из настроек")
                : transparency.Value + "%";
            preview.Invalidate();
        }

        private void PreviewPaint(object sender, PaintEventArgs e)
        {
            try
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var r = new Rectangle(8, 8, preview.Width - 17, preview.Height - 17);
                using (var p = RoundRect(r, 10))
                {
                    if (hasColor)
                    {
                        int a = transparency.Value == 0
                            ? MainForm.GlobalAuraAlpha()
                            : MainForm.AuraAlphaOf(transparency.Value);
                        MainForm.DrawAura(g, p, Color.FromArgb(a, baseColor));
                    }
                    using (var pen = new Pen(Color.FromArgb(120, UiPalette.Dim)))
                        g.DrawPath(pen, p);
                }
            }
            catch { }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Returns null on Cancel, "" when the aura is removed, otherwise the
        // picked color as "RRGGBB"; alphaOverride is 0 (= follow the global
        // transparency setting) or the tile's own alpha.
        public static string Show(IWin32Window owner, string currentAura, int currentAlpha, string subject, out int alphaOverride)
        {
            alphaOverride = 0;
            using (var dlg = new AuraDialog(currentAura, currentAlpha, subject))
            {
                dlg.ShowDialog(owner);
                if (dlg.resetRequested) return "";
                if (!dlg.accepted) return null;
                alphaOverride = dlg.resultAlpha;
                return dlg.result;
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

        // Tile labels: when the whole name does not fit, draw "head…tail" —
        // the head takes as much room as is left (but never fewer than 3
        // characters), the tail keeps the last 3 characters. When the room is
        // really tight the tail shrinks (3 → 2 → 1 → 0), and in the extreme it
        // degrades to a plain "head…". Measured with the same GDI+ engine the
        // tiles draw with (DrawString/MeasureString), so what we build here is
        // what fits. maxWidth is the drawing rectangle's width in pixels.
        private static readonly StringFormat AbbrevFormat = new StringFormat(StringFormatFlags.NoWrap);
        private static readonly Dictionary<string, string> AbbrevCache = new Dictionary<string, string>();

        private static readonly object measureLock = new object();
        private static Bitmap measureBitmap;
        private static Graphics measureGraphics;

        // Scratch Graphics for the one-off width measurements made outside
        // OnPaint (tooltip building): MeasureString needs a Graphics, but the
        // result only depends on font metrics, so one bitmap-backed instance
        // serves every caller. Tooltip building runs on the UI thread only.
        private static Graphics MeasureGraphics()
        {
            lock (measureLock)
            {
                if (measureGraphics == null)
                {
                    measureBitmap = new Bitmap(1, 1);
                    measureGraphics = Graphics.FromImage(measureBitmap);
                }
                return measureGraphics;
            }
        }

        // True when the label does not fit the tile's text band and OnPaint
        // shows it abbreviated ("head…tail") — the hover tooltip must then
        // reveal the full name. Mirrors TileControl.OnPaint's composition:
        // one row when two rows are off (or WrapTwo fits the text on a single
        // line), otherwise it is the second wrapped row that may get cut.
        public static bool LabelShownPartial(string label, Font f, int textWidth, bool twoRows)
        {
            if (string.IsNullOrEmpty(label) || f == null || textWidth <= 4) return false;
            try
            {
                Graphics g = MeasureGraphics();
                string[] rows = twoRows ? WrapTwo(label, g, f, textWidth) : null;
                if (rows == null)
                    return AbbreviateMiddle(label, g, f, textWidth) != label;
                return AbbreviateMiddle(rows[1], g, f, textWidth) != rows[1];
            }
            catch { return false; }
        }

        public static string AbbreviateMiddle(string s, Graphics g, Font f, int maxWidth)
        {
            if (string.IsNullOrEmpty(s) || maxWidth <= 0) return s;
            string key = s + "|" + f.Name + "|" + f.Size.ToString("0.#") + "|" + maxWidth.ToString();
            string hit;
            lock (AbbrevCache)
            {
                if (AbbrevCache.TryGetValue(key, out hit)) return hit;
            }
            string result = AbbreviateMiddleCore(s, g, f, maxWidth);
            lock (AbbrevCache)
            {
                if (AbbrevCache.Count > 1024) AbbrevCache.Clear();
                AbbrevCache[key] = result;
            }
            return result;
        }

        private static string AbbreviateMiddleCore(string s, Graphics g, Font f, int maxWidth)
        {
            try
            {
                if (g.MeasureString(s, f, int.MaxValue, AbbrevFormat).Width <= maxWidth) return s;
                const int minHead = 3;
                const string ell = "…";
                float ellW = g.MeasureString(ell, f, int.MaxValue, AbbrevFormat).Width;
                // Head must keep at least 3 characters: smaller tails leave MORE
                // room for the head, so walk the tail down instead of breaking.
                for (int tail = 3; tail >= 1; tail--)
                {
                    if (s.Length - tail < minHead) continue;
                    string tailS = s.Substring(s.Length - tail);
                    float tailW = g.MeasureString(tailS, f, int.MaxValue, AbbrevFormat).Width;
                    string head = FitPrefix(g, f, s, s.Length - tail, maxWidth - (int)Math.Ceiling(ellW + tailW), minHead);
                    if (head != null) return head + ell + tailS;
                }
                // Very tight: plain head + "…" (the end is cut off entirely).
                string headOnly = FitPrefix(g, f, s, s.Length, maxWidth - (int)Math.Ceiling(ellW), 1);
                return headOnly != null ? headOnly + ell : s; // s = GDI trims as the last resort
            }
            catch { return s; }
        }

        // Longest prefix of s (up to maxChars characters) that fits into
        // maxWidth; null when even minChars characters are too wide. Binary
        // search over MeasureString.
        private static string FitPrefix(Graphics g, Font f, string s, int maxChars, float maxWidth, int minChars)
        {
            if (maxWidth <= 0 || maxChars <= 0 || s.Length == 0) return null;
            int hi = Math.Min(maxChars, s.Length);
            int min = Math.Min(minChars, hi);
            if (min > 0 && g.MeasureString(s.Substring(0, min), f, int.MaxValue, AbbrevFormat).Width > maxWidth) return null;
            if (min == hi) return s.Substring(0, hi);
            if (g.MeasureString(s.Substring(0, hi), f, int.MaxValue, AbbrevFormat).Width <= maxWidth) return s.Substring(0, hi);
            int lo = min; // lo fits, hi does not
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (g.MeasureString(s.Substring(0, mid), f, int.MaxValue, AbbrevFormat).Width <= maxWidth) lo = mid;
                else hi = mid;
            }
            return s.Substring(0, lo);
        }

        // Greedy two-line wrap for tile labels: line 1 takes as many whole
        // words as fit (a first word wider than the tile is character-filled),
        // line 2 gets the rest. Returns null when s already fits one line or
        // nothing sensible can be split off.
        public static string[] WrapTwo(string s, Graphics g, Font f, int maxWidth)
        {
            if (string.IsNullOrEmpty(s) || maxWidth <= 0) return null;
            try
            {
                if (g.MeasureString(s, f, int.MaxValue, AbbrevFormat).Width <= maxWidth) return null;
                string[] words = s.Split(' ');
                string line1 = null;
                int i = 0;
                for (; i < words.Length; i++)
                {
                    string cand = line1 == null ? words[i] : line1 + " " + words[i];
                    if (g.MeasureString(cand, f, int.MaxValue, AbbrevFormat).Width > maxWidth) break;
                    line1 = cand;
                }
                string rest;
                if (line1 == null)
                {
                    line1 = FitPrefix(g, f, s, s.Length, maxWidth, 1);
                    if (line1 == null) return null;
                    rest = s.Substring(line1.Length).TrimStart();
                }
                else
                {
                    rest = s.Substring(line1.Length).TrimStart();
                }
                if (rest.Length == 0) return null;
                return new[] { line1, rest };
            }
            catch { return null; }
        }

        // Explorer shortcut suffixes across languages ("Name - Shortcut",
        // "Имя — ярлык", "Name - Verknüpfung", ...). The suffix must be
        // separated by a dash; a name that merely ends with the word
        // ("Мой ярлык") is never touched.
        private static readonly string[] ShortcutSuffixes =
        {
            "ярлык", "ярлик", "shortcut", "verknüpfung", "raccourci",
            "acceso directo", "collegamento", "skrót", "atalho", "kısayol"
        };

        private static readonly Dictionary<string, string> TileLabelCache = new Dictionary<string, string>();

        // Display-only transform of a tile label (the two settings checkboxes):
        // hides the shortcut suffix and the real extension of the item's path
        // (".mp4" ...). The stored name (records.xml), all paths and the search
        // metadata are never touched - unticking brings the full label back.
        // Cached: labels repaint on every hover/drag.
        public static string TileLabel(string name, string path, bool trimShortcut, bool trimExtension)
        {
            if (string.IsNullOrEmpty(name)) return name ?? "";
            string key = name + "\x1" + (path ?? "") + "\x1" + (trimShortcut ? "s" : "-") + (trimExtension ? "x" : "-");
            string hit;
            lock (TileLabelCache)
            {
                if (TileLabelCache.TryGetValue(key, out hit)) return hit;
            }
            bool isShortcut = false;
            try { isShortcut = (path ?? "").EndsWith(".lnk", StringComparison.OrdinalIgnoreCase); } catch { }
            string s = name;
            if (trimShortcut)
            {
                // Explorer names duplicate shortcuts "Name - Ярлык (2)" or
                // "Video.mp4 - Ярлык (2).lnk": the copy counter and the extension
                // sit AFTER the suffix and stop the match. Peel them off for the
                // match; when no suffix matched everything is restored (a plain
                // "Отчёт (2).docx" keeps its counter and extension). When one DID
                // match, the counter goes away with the suffix - "(2)" only ever
                // numbered the shortcut itself - and the extension is appended
                // back so the extension checkbox below still sees it.
                string core = s;
                string tail = "";
                try
                {
                    string ext = System.IO.Path.GetExtension(path ?? "");
                    if (!string.IsNullOrEmpty(ext) && ext.Length <= 6 && core.Length > ext.Length &&
                        core.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        tail = ext;
                        core = core.Substring(0, core.Length - ext.Length);
                    }
                }
                catch { }
                string counter = null;
                if (core.EndsWith(")", StringComparison.Ordinal))
                {
                    int open = core.LastIndexOf('(');
                    if (open > 0 && core[open - 1] == ' ')
                    {
                        bool digits = true;
                        for (int i = open + 1; i < core.Length - 1; i++)
                            if (!char.IsDigit(core[i])) { digits = false; break; }
                        if (digits && open + 1 < core.Length - 1)
                        {
                            counter = core.Substring(open - 1); // " (2)" including the space
                            core = core.Substring(0, open - 1);
                        }
                    }
                }
                core = core.TrimEnd();
                // Twice: rare but real "Name - Shortcut - Shortcut" nesting.
                // TrimEnd between passes: "Name - Ярлык " must lose its trailing
                // space before the next suffix check and the extension heuristic.
                string t = TrimShortcutSuffix(core);
                t = t.TrimEnd();
                t = TrimShortcutSuffix(t);
                t = t.TrimEnd();
                if (t.Length < core.Length) s = t + tail; // counter dropped with the suffix
            }
            if (trimExtension)
            {
                try
                {
                    string ext = System.IO.Path.GetExtension(path ?? "");
                    if (!string.IsNullOrEmpty(ext) && ext.Length <= 6 && s.Length > ext.Length &&
                        s.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                        s = s.Substring(0, s.Length - ext.Length);
                    else if (isShortcut && trimShortcut)
                    {
                        // "Video.mp4 - Ярлык.lnk": Explorer copies the target's extension
                        // into the shortcut name, so after the suffix is gone the label
                        // still ends with that extension. Drop one final token of 2-5
                        // letters/digits that contains at least one letter (a numeric
                        // tail like "версия 2.0" or "отчёт 2024" is never touched).
                        int dot = s.LastIndexOf('.');
                        if (dot > 0)
                        {
                            int len = s.Length - dot - 1;
                            if (len >= 2 && len <= 5)
                            {
                                bool word = true, hasLetter = false;
                                for (int i = dot + 1; i < s.Length; i++)
                                {
                                    char c = s[i];
                                    if (!char.IsLetterOrDigit(c)) { word = false; break; }
                                    if (char.IsLetter(c)) hasLetter = true;
                                }
                                if (word && hasLetter) s = s.Substring(0, dot);
                            }
                        }
                    }
                }
                catch { }
            }
            s = s.TrimEnd(); // trailing space too
            lock (TileLabelCache)
            {
                if (TileLabelCache.Count > 1024) TileLabelCache.Clear();
                TileLabelCache[key] = s;
            }
            return s;
        }

        private static string TrimShortcutSuffix(string s)
        {
            if (s.Length < 5) return s;
            string lower = s.ToLowerInvariant();
            foreach (string w in ShortcutSuffixes)
            {
                if (!lower.EndsWith(w, StringComparison.Ordinal)) continue;
                int j = s.Length - w.Length;
                while (j > 0 && s[j - 1] == ' ') j--;
                if (j == 0) continue;
                char d = s[j - 1];
                if (d != '-' && d != '\u2013' && d != '\u2014') continue; // - – —
                return s.Substring(0, j - 1);
            }
            return s;
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
            // Autostart (HKCU Run) launches the exe with C:\Windows\System32 as the
            // working directory, which breaks every relative data path (settings.ini,
            // records.xml, ...). Anchor the working directory to the exe folder once:
            // the app stays fully portable, no absolute paths are stored anywhere.
            try { Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory; }
            catch (Exception ex) { AppLog.Write("CWD anchor", ex); }
            if (!SingleInstance.Start())
            {
                SingleInstance.NotifyExisting();
                return;
            }
            // A moved folder: fix stored <exe>\ico paths that point to the old
            // location (must happen before anything reads records/filetypes).
            FolderMigration.RebaseIfNeeded();
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
