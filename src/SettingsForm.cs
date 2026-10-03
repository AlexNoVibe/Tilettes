using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace WinPanel
{
    public class SettingsForm : Form
    {
        private Settings settings;
        private string settingsPath;

        private NumericUpDown numWidth;
        private NumericUpDown numHeight;
        private NumericUpDown numX;
        private NumericUpDown numY;
        private CheckBox chkMinimizeToTray;
        private ComboBox cmbFolders;
        private ComboBox cmbHotkey;
        private Label lblLive;
        private Rectangle liveRect;

        private NumericUpDown numGridTransparency;
        private NumericUpDown numGridCols;
        private NumericUpDown numGridRows;
        private NumericUpDown numDefaultItemSize;
        private CheckBox chkEditMode;
        private CheckBox chkMiniExplorer;
        // Tile label display options (see Settings.Label*).
        private CheckBox chkTwoRows;
        private CheckBox chkTrimShortcut;
        private CheckBox chkTrimExt;
        private CheckBox chkCtrlNames;
        private ComboBox cmbLabelAlign;

        private NumericUpDown numIconScale;
        private NumericUpDown numFuzzy, numSearchBoxFont, numSearchResultsFont;
        private CheckBox chkSearchMeta, chkSearchPaths, chkSearchDesc, chkSearchStart;
        private NumericUpDown numFolderExit;
    private TextBox txtFolderOpenProgram;
    private CheckBox chkWinClick;

    // A bold section subtitle: the settings are split into blocks. Returns the
    // y for the first row of the section. UseMnemonic=false keeps the "&" of
    // e.g. "Panel & hotkeys" visible instead of hiding it as a shortcut prefix.
    private int AddSection(string en, string ru, int y)
    {
        var lbl = new Label { Text = Loc.S(en, ru), Left = 20, Top = y, Width = 480, ForeColor = textColor, Font = new Font(this.Font, FontStyle.Bold), UseMnemonic = false };
        scrollPanel.Controls.Add(lbl);
        return y + 26;
    }
        private ComboBox cmbLang;
        private CheckBox chkAutoStart, chkAutoStartMin, chkTrayAlways, chkKeepTab;

        private NumericUpDown numFontItemsSize;
        private Button btnFontItemsColor;
        private ComboBox cmbFontItems;

        private NumericUpDown numFontTabsSize;
        private Button btnFontTabsColor;
        private ComboBox cmbFontTabs;

        private NumericUpDown numFontUiSize;
        private Button btnFontUiColor;
        private ComboBox cmbFontUi;

        private Button btnSave;
        private Button btnCancel;
        private Button btnBackup;
        private Button btnRestore;

        // New in v2: skin picker, scheduled backup + Start Menu sync, search
        // history, Win-key capture. Flags tell MainForm to run things "now".
        private ComboBox cmbSkin;
        private NumericUpDown numBackupDays;
        private NumericUpDown numSyncHours;
        private CheckBox chkSaveHistory;
        private CheckBox chkWinKey;
        // Updates section (v0.5): check is live, install is a stub.
        private CheckBox chkUpdateCheck;
        private NumericUpDown numUpdateDays;
        private CheckBox chkAutoInstall;
        public bool RunBackupNow { get; private set; }
        public bool RunSyncNow { get; private set; }
        // Maintenance: re-check tile paths, drop the icon cache, re-extract icons.
        public bool RebuildIconsNow { get; private set; }
        // "Do it now" flags for the update section: a manual GitHub check and a
        // replay of the first-start welcome window.
        public bool RunCheckNow { get; private set; }
        public bool RunWelcomeAgain { get; private set; }

        private Color bgColor;
        private Color panelColor;
        private Color hoverColor;
        private Color textColor;

        private Panel scrollPanel;
        private Panel bottomBar;
        private ToolTip tips = new ToolTip();

        // Set when the user picked a backup zip in "Restore archive"; MainForm
        // unpacks it right after the dialog closes and reloads everything.
        public string RestoreZipPath { get; private set; }

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

        // AutoScroll pulls a newly focused control into view: clicking a
        // checkbox near the fold used to yank the whole content, the mouse-up
        // landed elsewhere and the tick was lost. This panel keeps the current
        // scroll position on focus changes; the scrollbar and wheel still work.
        private class NoJumpPanel : Panel
        {
            protected override Point ScrollToControl(Control activeControl)
            {
                return DisplayRectangle.Location;
            }
        }

        public SettingsForm(Settings settings, string settingsPath, Rectangle liveWindowRect)
        {
            this.settings = settings;
            this.settingsPath = settingsPath;
            this.liveRect = liveWindowRect;

            // Follow the active skin when one is on, otherwise the classic
            // light/dark theme (see UiPalette): the dialog used to be classic
            // gray even with a decorative skin active.
            bgColor = UiPalette.Bg;
            panelColor = UiPalette.Panel;
            hoverColor = UiPalette.Hover;
            textColor = UiPalette.Text;

            this.Text = "Settings";
            this.Width = 588;
            // The content is ~1000px tall; on small monitors the window is capped
            // at 2/3 of the screen height and everything below the fold stays
            // reachable through the scrollbar of the content panel.
            this.Height = Math.Min(1058, (Screen.PrimaryScreen.WorkingArea.Height * 2) / 3);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;

            this.ForeColor = textColor;
            this.BackColor = bgColor; // labels/checkboxes inherit it: no white-on-white text
            this.Font = new Font("Segoe UI", 9f);

            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));

            // All settings rows live in a scrollable panel; Save/Cancel sit on a
            // fixed bottom bar, so they are reachable at any window height.
            scrollPanel = new NoJumpPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = bgColor };
            bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = panelColor };
            var titleBar = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = panelColor };
            var titleLbl = new Label { Text = "Settings", ForeColor = textColor, AutoSize = true, Location = new Point(10, 7) };
            titleBar.Controls.Add(titleLbl);
            // App version in the top-right corner (next to the close button).
            var verLbl = new Label
            {
                Text = "v" + AppInfo.AppVersion,
                ForeColor = UiPalette.Dim,
                AutoSize = true
            };
            titleBar.Controls.Add(verLbl);
            titleBar.Resize += (s, e) => { verLbl.Location = new Point(titleBar.Width - verLbl.Width - 40, 8); };
            verLbl.Location = new Point(400, 8);
            var closeBtn = new Button { Text = "X", Width = 30, Height = 30, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, ForeColor = textColor, BackColor = panelColor };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => this.Close();
            titleBar.Controls.Add(closeBtn);
            titleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
            // Docking lays out children from the LAST one backwards: titleBar is
            // added last so its Top strip does not consume the whole client area.
            this.Controls.Add(scrollPanel);
            this.Controls.Add(bottomBar);
            this.Controls.Add(titleBar);

            // Borderless form: a 6px strip along the very bottom acts as a resize
            // grip — dragging it resizes the window height via the native loop.
            var resizeGrip = new Panel { Dock = DockStyle.Bottom, Height = 6, BackColor = panelColor, Cursor = Cursors.SizeNS };
            resizeGrip.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x0F, 0); // WM_NCLBUTTONDOWN, HTBOTTOM
                }
            };
            Tip(resizeGrip, "Drag to resize the window vertically", "Потяните, чтобы менять высоту окна");
            bottomBar.Controls.Add(resizeGrip);
            this.MinimumSize = new Size(588, 320);

            int y = 42;

            y = AddSection("Window", "Окно", y);

            // Startup size on one row, fields labelled x/y: [900] x [800] y
            var lblSize = new Label { Text = Loc.S("Startup Size:", "Размер при запуске:"), Left = 20, Top = y, Width = 130 };
            numWidth = new NumericUpDown { Left = 155, Top = y - 2, Width = 70, Maximum = 4000, Minimum = 200, Value = settings.StartupWidth, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblMul = new Label { Text = "x", Left = 228, Top = y, Width = 16 };
            numHeight = new NumericUpDown { Left = 246, Top = y - 2, Width = 70, Maximum = 4000, Minimum = 200, Value = settings.StartupHeight, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblSizeY = new Label { Text = "y", Left = 319, Top = y, Width = 16 };
            Tip(numWidth, "Window width at startup", "Ширина окна при запуске");
            Tip(numHeight, "Window height at startup", "Высота окна при запуске");
            y += 30;

            // Window position on one row: [X] x [Y]
            var lblPos = new Label { Text = Loc.S("Window Position:", "Положение окна:"), Left = 20, Top = y, Width = 130 };
            numX = new NumericUpDown { Left = 155, Top = y - 2, Width = 70, Maximum = 4000, Minimum = -4000, Value = settings.WindowX, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblMul2 = new Label { Text = "x", Left = 228, Top = y, Width = 16 };
            numY = new NumericUpDown { Left = 246, Top = y - 2, Width = 70, Maximum = 4000, Minimum = -4000, Value = settings.WindowY, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblPosY = new Label { Text = "y", Left = 319, Top = y, Width = 16 };
            Tip(numX, "Window position: distance from the left screen edge", "Положение окна: отступ от левого края экрана");
            Tip(numY, "Window position: distance from the top screen edge", "Положение окна: отступ от верхнего края экрана");
            y += 30;

            var lblTrans = new Label { Text = Loc.S("Grid Transp. (0-255):", "Прозрачность сетки (0-255):"), Left = 20, Top = y, Width = 130 };
            numGridTransparency = new NumericUpDown { Left = 155, Top = y - 2, Width = 120, Maximum = 255, Minimum = 0, Value = settings.GridTransparency, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            Tip(numGridTransparency, "Grid line opacity: 0 = invisible, 255 = solid", "Насыщенность линий сетки: 0 = невидима, 255 = сплошные");
            y += 30;

            // Columns and rows share one row, each field labelled.
            var lblGrid = new Label { Text = Loc.S("Grid:", "Сетка:"), Left = 20, Top = y, Width = 130 };
            numGridCols = new NumericUpDown { Left = 155, Top = y - 2, Width = 60, Maximum = 100, Minimum = 1, Value = settings.GridColumns, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblColsCap = new Label { Text = Loc.S("columns x", "столбцов ×"), Left = 220, Top = y, Width = 85 };
            numGridRows = new NumericUpDown { Left = 307, Top = y - 2, Width = 60, Maximum = 100, Minimum = 1, Value = settings.GridRows, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblRowsCap = new Label { Text = Loc.S("rows", "строк"), Left = 372, Top = y, Width = 60 };
            Tip(numGridCols, "Grid cells horizontally", "Клеток сетки по горизонтали");
            Tip(numGridRows, "Grid cells vertically", "Клеток сетки по вертикали");
            y += 30;

            var lblDefSize = new Label { Text = "Def. Item Size:", Left = 20, Top = y, Width = 120 };
            numDefaultItemSize = new NumericUpDown { Left = 150, Top = y - 2, Width = 120, Maximum = 6, Minimum = 1, Value = settings.DefaultItemSize, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            Tip(numDefaultItemSize, "Size of newly added tiles (1x1 ... 6x6)", "Размер новых плиток (1x1 ... 6x6)");
            y += 30;

            // Icon scale, percent (100 = default)
            var lblIconScale = new Label { Text = "Icon Scale (%):", Left = 20, Top = y, Width = 120 };
            numIconScale = new NumericUpDown { Left = 150, Top = y - 2, Width = 120, Maximum = 400, Minimum = 25, Value = Math.Max(25, Math.Min(400, settings.IconScale)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            Tip(numIconScale, "Icon size inside a tile, percent of the designed size", "Размер значка внутри плитки, процентов от стандартного");
            y += 30;

            // ---- Tile labels: two rows + alignment, display-only trimming ----
            y = AddSection("Tile labels", "Подписи плиток", y);
            chkTwoRows = new CheckBox { Text = Loc.S("Tile label: two rows", "Подпись плитки: две строки"), Left = 20, Top = y, Width = 260, Checked = settings.LabelTwoRows, ForeColor = textColor };
            Tip(chkTwoRows, "Tall tiles wrap the label onto two rows (the icon shrinks a bit); untick for one row",
                "Высокие плитки переносят подпись на две строки (значок чуть уменьшается); снимите галочку для одной строки");
            var lblAlign2 = new Label { Text = Loc.S("2 rows align:", "Выравнивание 2 строк:"), Left = 290, Top = y, Width = 130 };
            cmbLabelAlign = new ComboBox { Left = 425, Top = y - 2, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            cmbLabelAlign.Items.AddRange(new object[] { Loc.S("Left", "Слева"), Loc.S("Center", "По центру"), Loc.S("Right", "Справа") });
            cmbLabelAlign.SelectedIndex = Math.Max(0, Math.Min(2, settings.LabelAlign2Rows));
            Tip(cmbLabelAlign, "Horizontal alignment of the two-row label", "Выравнивание двухстрочной подписи по горизонтали");
            scrollPanel.Controls.Add(chkTwoRows);
            scrollPanel.Controls.Add(lblAlign2);
            scrollPanel.Controls.Add(cmbLabelAlign);
            y += 30;

            chkTrimShortcut = new CheckBox { Text = Loc.S("Hide shortcut suffix ( - Shortcut)", "Скрывать суффикс ярлыка ( — ярлык)"), Left = 20, Top = y, Width = 540, Checked = settings.LabelTrimShortcut, ForeColor = textColor };
            Tip(chkTrimShortcut, "Display only: dash variants and several languages; the stored name and search are untouched, untick to bring it back",
                "Только отображение: варианты тире и несколько языков; сохранённое имя и поиск не трогаются, снятие галочки возвращает суффикс");
            scrollPanel.Controls.Add(chkTrimShortcut);
            y += 28;

            chkTrimExt = new CheckBox { Text = Loc.S("Hide file extension (.mp4)", "Скрывать расширение файла (.mp4)"), Left = 20, Top = y, Width = 540, Checked = settings.LabelTrimExtension, ForeColor = textColor };
            Tip(chkTrimExt, "Display only: hides the real extension of the item's path on the label; untick to bring it back",
                "Только отображение: скрывает на подписи реальное расширение пути элемента; снятие галочки возвращает расширение");
            scrollPanel.Controls.Add(chkTrimExt);
            y += 28;

            chkCtrlNames = new CheckBox { Text = Loc.S("Hold Ctrl — show full names on tiles", "Зажать Ctrl — полные названия на плитках"), Left = 20, Top = y, Width = 540, Checked = settings.LabelCtrlFullNames, ForeColor = textColor };
            Tip(chkCtrlNames, "While Ctrl is held the tiles show their full untruncated names (smaller font); tooltips also show the full name and full paths",
                "Пока Ctrl зажат, плитки показывают полные названия (шрифт уменьшается); в подсказках тоже полное название и полные пути");
            scrollPanel.Controls.Add(chkCtrlNames);
            y += 30;

            y = AddSection("Folders", "Папки", y);
            var lblFolders = new Label { Text = "Open folders in:", Left = 20, Top = y, Width = 120 };
            cmbFolders = new ComboBox { Left = 150, Top = y - 2, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            cmbFolders.Items.Add(Loc.S("Same window"));
            cmbFolders.Items.Add(Loc.S("Popup window"));
            cmbFolders.SelectedIndex = settings.OpenFoldersInPopup ? 1 : 0;
            numFolderExit = new NumericUpDown { Left = 296, Top = y - 2, Width = 56, Maximum = 600, Minimum = 0, Value = Math.Max(0, Math.Min(600, settings.FolderAutoExitSeconds)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblExit = new Label { Text = Loc.S("sec idle", "сек простоя"), Left = 358, Top = y, Width = 105 };
            Tip(cmbFolders, "Where folder tiles open their contents", "Где открывать содержимое папок");
            var exitTip = new ToolTip();
            exitTip.SetToolTip(numFolderExit, Loc.S("Return from a folder after this many seconds without activity (0 = off)", "Выходить из папки после стольких секунд без активности (0 = выкл)"));
            y += 30;

            // Program that opens directories instead of Explorer (empty = system
            // default). Editable: type a path or pick one with "...".
            var lblOpenWith = new Label { Text = Loc.S("Open folders with:", "Папки открывать через:"), Left = 20, Top = y, Width = 130 };
            txtFolderOpenProgram = new TextBox { Left = 150, Top = y - 2, Width = 380, Text = settings.FolderOpenProgram ?? "", BorderStyle = BorderStyle.FixedSingle, BackColor = panelColor, ForeColor = textColor };
            var btnPickFm = new Button { Text = "...", Left = 536, Top = y - 3, Width = 32, Height = txtFolderOpenProgram.Height + 2, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor, Cursor = Cursors.Hand };
            btnPickFm.FlatAppearance.BorderSize = 0;
            btnPickFm.Click += (s, e) =>
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = Loc.S("Program that opens folders", "Программа для открытия папок");
                    ofd.Filter = Loc.S("Programs (*.exe)|*.exe|All files (*.*)|*.*", "Программы (*.exe)|*.exe|Все файлы (*.*)|*.*");
                    try
                    {
                        string cur = (txtFolderOpenProgram.Text ?? "").Trim();
                        if (cur.Length > 0 && File.Exists(cur)) ofd.InitialDirectory = Path.GetDirectoryName(cur);
                        else if (Directory.Exists(@"C:\Program Files")) ofd.InitialDirectory = @"C:\Program Files";
                    }
                    catch { }
                    if (ofd.ShowDialog(this) == DialogResult.OK) txtFolderOpenProgram.Text = ofd.FileName;
                }
            };
            Tip(txtFolderOpenProgram, "Total Commander etc.: the .exe that opens directory tiles and \"open containing folder\". Switches are allowed, %1 marks the folder position, e.g. C:\\totalcmd\\TOTALCMD64.EXE /O \"%1\". Empty or explorer.exe = the system default. For Total Commander /O is added automatically",
                "Total Commander и т.п.: .exe, которым открываются папки и «открыть содержащую папку». Можно указывать ключи, %1 — место папки, например C:\\totalcmd\\TOTALCMD64.EXE /O \"%1\". Пусто или explorer.exe — системный проводник. Для Total Commander ключ /O подставляется автоматически");
            scrollPanel.Controls.Add(lblOpenWith);
            scrollPanel.Controls.Add(txtFolderOpenProgram);
            scrollPanel.Controls.Add(btnPickFm);
            y += 30;

            y = AddSection("Panel & hotkeys", "Панель и клавиши", y);
            chkEditMode = new CheckBox { Text = Loc.S("Allow adding icons", "Разрешать добавлять значки"), Left = 20, Top = y, Width = 280, Checked = settings.EditMode, ForeColor = textColor };
            Tip(chkEditMode, "New tiles can be added by dropping files onto the panel", "Новые плитки можно добавлять перетаскиванием файлов на панель");
            y += 30;

            var lblHotkey = new Label { Text = "Show window hotkey:", Left = 20, Top = y, Width = 130 };
            // Editable dropdown: pick a preset or type any Mod+Key combination
            // (Ctrl/Alt/Shift/Win + a letter or digit), e.g. "Ctrl+Alt+P".
            cmbHotkey = new ComboBox { Left = 150, Top = y - 2, Width = 185, DropDownStyle = ComboBoxStyle.DropDown, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            cmbHotkey.Items.AddRange(new object[] { "None", "Ctrl+Q", "Ctrl+Shift+Q", "Alt+Q", "Ctrl+J", "Ctrl+Shift+J", "Ctrl+Alt+J", "Ctrl+K", "Ctrl+Shift+K", "Alt+J" });
            string hk = string.IsNullOrEmpty(settings.HotkeyShow) ? "Ctrl+Q" : settings.HotkeyShow;
            if (!cmbHotkey.Items.Contains(hk)) cmbHotkey.Items.Add(hk);
            cmbHotkey.Text = hk;
            Tip(cmbHotkey, "Global hotkey that shows the panel; pick a preset or type your own: Ctrl/Alt/Shift/Win + letter or digit",
                "Глобальная горячая клавиша показа панели; выберите пресет или впишите свою: Ctrl/Alt/Shift/Win + буква или цифра");
            chkWinKey = new CheckBox
            {
                Text = Loc.S("Capture the Win key", "Захват клавиши Win"),
                Left = 345,
                Top = y,
                Width = 235,
                Checked = settings.HotkeyWin,
                ForeColor = textColor
            };
            var winKeyTip = new ToolTip();
            winKeyTip.SetToolTip(chkWinKey, Loc.S("Pressing the Win key opens the panel instead of the Start menu",
                "Клавиша Win открывает панель вместо меню Пуск"));
            scrollPanel.Controls.Add(chkWinKey);
            y += 30;
            // The Start button CLICK capture is a separate setting: some users
            // want only the key, others only the corner click.
            chkWinClick = new CheckBox
            {
                Text = Loc.S("Capture Start button click", "Захват клика по кнопке Пуск"),
                Left = 345,
                Top = y,
                Width = 235,
                Checked = settings.HotkeyStartClick,
                ForeColor = textColor
            };
            var winClickTip = new ToolTip();
            winClickTip.SetToolTip(chkWinClick, Loc.S("A left click on the Start button (screen corner) opens the panel instead of the Start menu",
                "Левый клик по кнопке Пуск в углу экрана открывает панель вместо меню Пуск"));
            scrollPanel.Controls.Add(chkWinClick);
            y += 30;

            y = AddSection("Fonts", "Шрифты", y);
            // Font rows: [size] [color] [family] — one row per group
            AddFontRow("Tiles Font:", y, settings.FontItemsSize, settings.FontItemsColor, settings.FontItemsName,
                out numFontItemsSize, out btnFontItemsColor, out cmbFontItems);
            y += 30;
            AddFontRow("Tabs Font:", y, settings.FontTabsSize, settings.FontTabsColor, settings.FontTabsName,
                out numFontTabsSize, out btnFontTabsColor, out cmbFontTabs);
            y += 30;
            AddFontRow("UI Font:", y, settings.FontUiSize, settings.FontUiColor, settings.FontUiName,
                out numFontUiSize, out btnFontUiColor, out cmbFontUi);
            y += 34;
            // ---- Search section ----
            y = AddSection("Search", "Поиск", y);
            var lblFuzzy = new Label { Text = "Fuzzy accuracy (0-3):", Left = 20, Top = y, Width = 150 };
            numFuzzy = new NumericUpDown { Left = 175, Top = y - 2, Width = 45, Minimum = 0, Maximum = 3, Value = Math.Max(0, Math.Min(3, settings.SearchFuzzyLevel)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            Tip(numFuzzy, "0 = exact matches only, 1-3 = increasingly loose fuzzy matching", "0 = только точные совпадения, 1-3 = всё более свободный поиск");
            y += 26;
            chkSearchMeta = new CheckBox { Text = "Search in metadata (exe, product)", Left = 20, Top = y, Width = 530, Checked = settings.SearchInMeta, ForeColor = textColor };
            Tip(chkSearchMeta, "Also search in program name, version and company from the exe", "Искать также в имени программы, версии и компании из exe");
            y += 24;
            chkSearchPaths = new CheckBox { Text = "Search in full paths", Left = 20, Top = y, Width = 530, Checked = settings.SearchInPaths, ForeColor = textColor };
            Tip(chkSearchPaths, "Also search in the full file and folder paths", "Искать также в полных путях файлов и папок");
            y += 24;
            chkSearchDesc = new CheckBox { Text = "Search in descriptions", Left = 20, Top = y, Width = 530, Checked = settings.SearchInDesc, ForeColor = textColor };
            Tip(chkSearchDesc, "Also search in the user descriptions of the tiles", "Искать также в описаниях плиток");
            y += 24;
            chkSearchStart = new CheckBox { Text = Loc.S("Search in the Start Menu tab", "Искать во вкладке Пуск"), Left = 20, Top = y, Width = 530, Checked = settings.SearchInStart, ForeColor = textColor };
            Tip(chkSearchStart, "Include the mirrored Start Menu tab in panel search", "Включать зеркальную вкладку Пуск в поиск по панели");
            y += 26;
            chkSaveHistory = new CheckBox
            {
                Text = Loc.S("Save search history", "Сохранять историю поиска"),
                Left = 20,
                Top = y,
                Width = 250,
                Checked = settings.SearchSaveHistory,
                ForeColor = textColor
            };
            var btnClearHistory = new Button
            {
                Text = Loc.S("Clear history", "Очистить историю"),
                Left = 290,
                Top = y - 3,
                Width = 130,
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor
            };
            btnClearHistory.FlatAppearance.BorderSize = 0;
            btnClearHistory.Click += (s, e) => { SearchHistoryStore.Clear(); ConfirmDialog.ShowInfo(this, Loc.S("Search history cleared.", "История поиска очищена.")); };
            Tip(chkSaveHistory, "Remember queries and clicks into the past-search section", "Помнить запросы и клики для раздела «Прошлый поиск»");
            Tip(btnClearHistory, "Erase all saved search history", "Стереть всю сохранённую историю поиска");
            scrollPanel.Controls.Add(chkSaveHistory);
            scrollPanel.Controls.Add(btnClearHistory);
            y += 26;
            var lblSearchFonts = new Label { Text = "Search fonts:", Left = 20, Top = y, Width = 125 };
            numSearchBoxFont = new NumericUpDown { Left = 150, Top = y - 2, Width = 50, Minimum = 7, Maximum = 30, Value = Math.Max(7, Math.Min(30, settings.SearchBoxFontSize)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblSearchBoxFont = new Label { Text = Loc.S("box", "строка поиска"), Left = 205, Top = y, Width = 150 };
            numSearchResultsFont = new NumericUpDown { Left = 360, Top = y - 2, Width = 45, Minimum = 7, Maximum = 30, Value = Math.Max(7, Math.Min(30, settings.SearchResultsFontSize)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            Tip(numSearchBoxFont, "Font size of the search box", "Размер шрифта строки поиска");
            Tip(numSearchResultsFont, "Font size of the search results list", "Размер шрифта списка результатов");
            var lblSearchResultsFont = new Label { Text = Loc.S("results", "результаты"), Left = 410, Top = y, Width = 150 };
            scrollPanel.Controls.Add(lblFuzzy);
            scrollPanel.Controls.Add(numFuzzy);
            scrollPanel.Controls.Add(chkSearchMeta);
            scrollPanel.Controls.Add(chkSearchPaths);
            scrollPanel.Controls.Add(chkSearchDesc);
            scrollPanel.Controls.Add(chkSearchStart);
            scrollPanel.Controls.Add(lblSearchFonts);
            scrollPanel.Controls.Add(numSearchBoxFont);
            scrollPanel.Controls.Add(lblSearchBoxFont);
            scrollPanel.Controls.Add(numSearchResultsFont);
            scrollPanel.Controls.Add(lblSearchResultsFont);

            y += 30; // the search fonts row must not overlap the file type buttons below

            y = AddSection("File types & explorer", "Типы файлов и проводник", y);
            var lblTypes = new Label { Text = "File types:", Left = 20, Top = y, Width = 92 };
            var btnTypeIcons = new Button { Text = "Icons by type...", Left = 115, Top = y - 3, Width = 140, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnTypeIcons.FlatAppearance.BorderSize = 0;
            btnTypeIcons.Click += (s, e) => { using (var ft = new FileTypesForm(FileTypesForm.Mode.Icons, FileTypes.DefaultFilePath)) ft.ShowDialog(this); };
            Tip(btnTypeIcons, "Assign a custom icon per file type", "Назначить свой значок для типа файлов");
            var btnTypeOpen = new Button { Text = "Open with by type...", Left = 260, Top = y - 3, Width = 150, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnTypeOpen.FlatAppearance.BorderSize = 0;
            btnTypeOpen.Click += (s, e) => { using (var ft = new FileTypesForm(FileTypesForm.Mode.OpenWith, FileTypes.DefaultFilePath)) ft.ShowDialog(this); };
            Tip(btnTypeOpen, "Choose the program that opens each file type", "Выбрать программу, открывающую каждый тип файлов");
            y += 36;

            chkMiniExplorer = new CheckBox { Text = "Ctrl+Click a folder opens Mini Explorer", Left = 20, Top = y, Width = 530, Checked = settings.MiniExplorerCtrlClick, ForeColor = textColor };
            Tip(chkMiniExplorer, "Ctrl+Click on a folder tile opens the mini explorer (file window with console)", "Ctrl+клик по папке открывает мини-проводник (окно файлов с консолью)");
            y += 30;

            y = AddSection("Autostart & tray", "Автозапуск и трей", y);
            chkAutoStart = new CheckBox { Text = "Autostart with Windows", Left = 20, Top = y, Width = 205, Checked = settings.AutoStart, ForeColor = textColor };
            Tip(chkAutoStart, "Start Tilettes automatically when Windows starts", "Запускать Плиточки автоматически при старте Windows");
            chkAutoStartMin = new CheckBox { Text = "After autostart - go to tray", Left = 240, Top = y, Width = 225, Checked = settings.AutoStartMinimized, ForeColor = textColor };
            Tip(chkAutoStartMin, "After autostart only the tray icon is shown", "После автозапуска показывать только значок в трее");
            y += 26;
            chkMinimizeToTray = new CheckBox { Text = "Minimize instead of close", Left = 20, Top = y, Width = 215, Checked = settings.MinimizeToTray, ForeColor = textColor };
            Tip(chkMinimizeToTray, "The close button hides the panel to the tray instead of exiting", "Кнопка закрытия прячет панель в трей вместо выхода");
            chkTrayAlways = new CheckBox { Text = "Always keep tray icon", Left = 240, Top = y, Width = 225, Checked = settings.TrayIconAlways, ForeColor = textColor };
            Tip(chkTrayAlways, "Show the tray icon even when the panel is open", "Показывать значок в трее даже при открытой панели");
            y += 26;
            chkKeepTab = new CheckBox { Text = "Remember active tab", Left = 20, Top = y, Width = 330, Checked = settings.KeepActiveTab, ForeColor = textColor };
            Tip(chkKeepTab, "Reopen the same tab on the next launch", "Открывать ту же вкладку при следующем запуске");
            y += 30;
            var lblLang = new Label { Text = "Language:", Left = 20, Top = y, Width = 90 };
            cmbLang = new ComboBox { Left = 110, Top = y - 2, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            foreach (var code in Loc.Languages) cmbLang.Items.Add(Loc.NativeName(code));
            int langIdx = 0;
            for (int i = 0; i < Loc.Languages.Length; i++)
                if (string.Equals(Loc.Languages[i], settings.Language, StringComparison.OrdinalIgnoreCase)) { langIdx = i; break; }
            cmbLang.SelectedIndex = langIdx;
            Tip(cmbLang, "Interface language", "Язык интерфейса");
            y += 30;

            // ---- Maintenance: skin, scheduled backup, Start Menu sync ----
            y = AddSection("Maintenance & Start Menu", "Обслуживание и Пуск", y);

            // One control for both theme and skin: the first two items are the
            // classic looks without a decorative skin (dark / light), the rest
            // are the real skins. This replaces the removed Light Theme checkbox.
            var lblSkin = new Label { Text = Loc.S("Skin & theme:", "Шкурка и тема:"), Left = 20, Top = y, Width = 130, UseMnemonic = false };
            cmbSkin = new ComboBox { Left = 155, Top = y - 2, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            cmbSkin.Items.Add(Loc.S("None (dark)", "Отключено (тёмная)"));
            cmbSkin.Items.Add(Loc.S("Light", "Светлая"));
            for (int i = 0; i < Skin.All.Count; i++)
            {
                if (ReferenceEquals(Skin.All[i], Skin.None)) continue;
                cmbSkin.Items.Add(Skin.All[i].DisplayName);
            }
            int skinIdx = settings.IsLightTheme ? 1 : 0;
            if (!string.IsNullOrEmpty(settings.SkinName))
            {
                for (int i = 0; i < Skin.All.Count; i++)
                    if (!ReferenceEquals(Skin.All[i], Skin.None) && string.Equals(Skin.All[i].Id, settings.SkinName, StringComparison.OrdinalIgnoreCase))
                    {
                        skinIdx = i + 1; // item 0/1 are the classic themes
                        break;
                    }
            }
            cmbSkin.SelectedIndex = skinIdx;
            Tip(cmbSkin, "Color look of the window: skins change colors and add a border, the first two items are the classic theme without a skin",
                "Цветовой облик окна: шкурки меняют цвета и добавляют рамку, первые два пункта — классическая тема без шкурки");
            y += 28;

            var lblBackupDays = new Label { Text = Loc.S("Backup every N days (0 = off):", "Бэкап раз в N дней (0 = выкл):"), Left = 20, Top = y, Width = 210 };
            numBackupDays = new NumericUpDown { Left = 235, Top = y - 2, Width = 50, Maximum = 365, Minimum = 0, Value = Math.Max(0, Math.Min(365, settings.BackupDays)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblSyncHours = new Label { Text = Loc.S("Sync Start Menu every N h (0 = off):", "Синхр. Пуска раз в N ч (0 = выкл):"), Left = 300, Top = y, Width = 190 };
            numSyncHours = new NumericUpDown { Left = 492, Top = y - 2, Width = 50, Maximum = 8760, Minimum = 0, Value = Math.Max(0, Math.Min(8760, settings.StartMenuSyncHours)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var backupTip = new ToolTip();
            backupTip.SetToolTip(numBackupDays, Loc.S("Full backup (settings + shortcuts + bookmarks) into autoBackup\\; runs 3 minutes after launch when due",
                "Полный бэкап (настройки + ярлыки + закладки) в autoBackup\\; делается через 3 минуты после запуска, когда подошёл срок"));
            Tip(numSyncHours, "Rebuild the mirrored Start Menu tab every N hours (0 = off)", "Перестраивать зеркальную вкладку Пуск раз в N часов (0 = выкл)");
            y += 30;

            var btnRunBackup = new Button
            {
                Text = Loc.S("Backup now", "Бэкап сейчас"),
                Left = 20,
                Top = y - 3,
                Width = 130,
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor
            };
            btnRunBackup.FlatAppearance.BorderSize = 0;
            btnRunBackup.Click += (s, e) => { RunBackupNow = true; this.DialogResult = DialogResult.OK; this.Close(); };
            Tip(btnRunBackup, "Create a full backup zip in autoBackup right now", "Создать полный zip-бэкап в autoBackup прямо сейчас");
            var btnRunSync = new Button
            {
                Text = Loc.S("Sync Start Menu now", "Синхронизировать Пуск"),
                Left = 165,
                Top = y - 3,
                Width = 185,
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor
            };
            btnRunSync.FlatAppearance.BorderSize = 0;
            btnRunSync.Click += (s, e) => { RunSyncNow = true; this.DialogResult = DialogResult.OK; this.Close(); };
            Tip(btnRunSync, "Re-scan the system Start Menu and update the mirror tab now", "Пересканировать системный Пуск и обновить зеркальную вкладку");
            var btnRebuildIcons = new Button
            {
                Text = Loc.S("Rebuild icons & paths", "Пересобрать иконки и пути"),
                UseMnemonic = false,
                Left = 365,
                Top = y - 3,
                Width = 185,
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor
            };
            btnRebuildIcons.FlatAppearance.BorderSize = 0;
            btnRebuildIcons.Click += (s, e) => { RebuildIconsNow = true; this.DialogResult = DialogResult.OK; this.Close(); };
            Tip(btnRebuildIcons, "Re-check every tile's path, drop the icon cache and re-extract all icons",
                "Перепроверить пути всех плиток, сбросить кеш иконок и извлечь все значки заново");
            scrollPanel.Controls.Add(lblSkin);
            scrollPanel.Controls.Add(cmbSkin);
            scrollPanel.Controls.Add(lblBackupDays);
            scrollPanel.Controls.Add(numBackupDays);
            scrollPanel.Controls.Add(lblSyncHours);
            scrollPanel.Controls.Add(numSyncHours);
            scrollPanel.Controls.Add(btnRunBackup);
            scrollPanel.Controls.Add(btnRunSync);
            scrollPanel.Controls.Add(btnRebuildIcons);
            y += 30;

            btnBackup = new Button { Text = Loc.S("Save backup (zip)", "Сохранить бэкап (zip)"), Left = 20, Top = y, Width = 190, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnBackup.FlatAppearance.BorderSize = 0;
            btnBackup.Click += BtnBackup_Click;
            Tip(btnBackup, "Save a full backup zip (settings, tiles, icons, bookmarks) to a chosen file - the same format Restore archive expects",
                "Сохранить полный zip-бэкап (настройки, плитки, значки, закладки) в выбранный файл — тот же формат, что ждёт «Восстановить из архива»");

            btnRestore = new Button { Text = Loc.S("Restore archive...", "Восстановить из архива..."), Left = 220, Top = y, Width = 190, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnRestore.FlatAppearance.BorderSize = 0;
            btnRestore.Click += BtnRestore_Click;
            var restoreTip = new ToolTip();
            restoreTip.SetToolTip(btnRestore, Loc.S("Expects a .zip created by \"Backup now\" / scheduled backup; files are unpacked into the working folder, Tilettes.exe is not replaced",
                "Ожидается .zip, созданный «Бэкапом сейчас» или по расписанию; файлы распаковываются в рабочую папку, Tilettes.exe не заменяется"));

            y += 44;

            // ---- Updates: check = live (GitHub Releases), install = TODO stub ----
            y = AddSection("Updates", "Обновления", y);
            chkUpdateCheck = new CheckBox { Text = Loc.S("Check for updates automatically", "Проверять обновления автоматически"), Left = 20, Top = y, Width = 260, Checked = settings.UpdateCheckEnabled, ForeColor = textColor };
            Tip(chkUpdateCheck, "Ask GitHub Releases for a newer version once every N days (never runs when unchecked)", "Спрашивать GitHub Releases о новой версии раз в N дней (при выключенной галочке не запускается никогда)");
            scrollPanel.Controls.Add(chkUpdateCheck);
            y += 26;

            var lblUpdateDays = new Label { Text = Loc.S("Check every N days:", "Проверять раз в N дней:"), Left = 20, Top = y, Width = 210 };
            numUpdateDays = new NumericUpDown { Left = 235, Top = y - 2, Width = 50, Minimum = 1, Maximum = 365, Value = Math.Max(1, Math.Min(365, settings.UpdateCheckDays)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            Tip(numUpdateDays, "How often to check for a new version, in days", "Как часто проверять новую версию, в днях");
            var btnCheckNow = new Button { Text = Loc.S("Check now", "Проверить сейчас"), Left = 300, Top = y - 3, Width = 140, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnCheckNow.FlatAppearance.BorderSize = 0;
            btnCheckNow.FlatAppearance.MouseOverBackColor = hoverColor;
            btnCheckNow.Click += (s, e) => { RunCheckNow = true; this.DialogResult = DialogResult.OK; this.Close(); };
            Tip(btnCheckNow, "Ask GitHub Releases right now (manual check works even with the automatic one off)", "Спросить GitHub Releases прямо сейчас (ручная проверка работает даже при выключенной автопроверке)");
            scrollPanel.Controls.Add(lblUpdateDays);
            scrollPanel.Controls.Add(numUpdateDays);
            scrollPanel.Controls.Add(btnCheckNow);
            y += 28;

            chkAutoInstall = new CheckBox { Text = Loc.S("Install updates automatically", "Автоустановка обновлений"), Left = 20, Top = y, Width = 260, Checked = settings.UpdateAutoInstall, ForeColor = textColor };
            Tip(chkAutoInstall, "Not implemented yet (stub): for now the corner button only opens the releases page", "Пока не реализовано (заглушка): кнопка в углу лишь открывает страницу загрузок");
            scrollPanel.Controls.Add(chkAutoInstall);
            y += 26;

            // Donate line: the button opens a popup menu with the wallet list
            // (a click copies the address) and the GitHub donate section.
            var lblDonate = new Label { Text = Loc.S("Like Tilettes? Support the author with crypto:", "Понравились Плиточки? Поддержать автора криптой:"), Left = 20, Top = y, Width = 420 };
            scrollPanel.Controls.Add(lblDonate);
            y += 26;

            var btnDonate = new Button { Text = Loc.S("♥ Donate", "♥ Донат"), Left = 20, Top = y - 3, Width = 130, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter };
            btnDonate.FlatAppearance.BorderSize = 0;
            btnDonate.FlatAppearance.MouseOverBackColor = hoverColor;
            var donateMenu = new ContextMenu();
            foreach (var wlt in DonateWallets.All)
            {
                var w = wlt;
                donateMenu.MenuItems.Add(DonateWallets.Display(w), (s2, e2) =>
                {
                    try { Clipboard.SetText(w.Address); } catch { }
                    FlashCopied(btnDonate);
                });
            }
            donateMenu.MenuItems.Add("-");
            donateMenu.MenuItems.Add(Loc.S("Open the donate section on GitHub", "Открыть раздел доната на GitHub"), (s2, e2) =>
            {
                try { Process.Start(AppInfo.DonateUrl); } catch { }
            });
            btnDonate.Click += (s2, e2) => donateMenu.Show(btnDonate, new Point(0, btnDonate.Height));
            Tip(btnDonate, "Pick a wallet - its address is copied to the clipboard; the last item opens the GitHub donate section",
                "Выберите кошелёк — адрес скопируется в буфер; последний пункт открывает раздел доната на GitHub");
            var lblDonateHint = new Label { Text = "github.com/AlexNoVibe/Tilettes#donate", Left = 160, Top = y + 3, Width = 300, ForeColor = settings.IsLightTheme ? Color.FromArgb(120, 120, 120) : Color.FromArgb(150, 150, 155) };
            scrollPanel.Controls.Add(btnDonate);
            scrollPanel.Controls.Add(lblDonateHint);
            y += 32;

            var btnWelcome = new Button { Text = Loc.S("Show the welcome window again", "Показать приветственное окно"), Left = 20, Top = y - 3, Width = 230, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnWelcome.FlatAppearance.BorderSize = 0;
            btnWelcome.FlatAppearance.MouseOverBackColor = hoverColor;
            btnWelcome.Click += (s, e) => { RunWelcomeAgain = true; this.DialogResult = DialogResult.OK; this.Close(); };
            Tip(btnWelcome, "Replay the first-start window: thanks, beta note, language and update-check questions, example tiles",
                "Повторить окно первого запуска: приветствие, о бете, язык и вопрос об обновлениях, примеры плиток");
            scrollPanel.Controls.Add(btnWelcome);
            y += 32;

            // Save/Cancel live on the fixed bottom bar (outside the scroll), so
            // they are visible at any window height.
            btnSave = new Button { Text = "Save", Left = 378, Top = 11, Width = 90, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.FlatAppearance.MouseOverBackColor = hoverColor;
            btnSave.FlatAppearance.MouseDownBackColor = panelColor;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button { Text = "Cancel", Left = 478, Top = 11, Width = 90, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.FlatAppearance.MouseOverBackColor = hoverColor;
            btnCancel.FlatAppearance.MouseDownBackColor = panelColor;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            scrollPanel.Controls.Add(lblSize);
            scrollPanel.Controls.Add(numWidth);
            scrollPanel.Controls.Add(lblMul);
            scrollPanel.Controls.Add(numHeight);
            scrollPanel.Controls.Add(lblSizeY);
            scrollPanel.Controls.Add(lblPos);
            scrollPanel.Controls.Add(numX);
            scrollPanel.Controls.Add(lblMul2);
            scrollPanel.Controls.Add(numY);
            scrollPanel.Controls.Add(lblPosY);
            scrollPanel.Controls.Add(lblTrans);
            scrollPanel.Controls.Add(numGridTransparency);
            scrollPanel.Controls.Add(lblGrid);
            scrollPanel.Controls.Add(numGridCols);
            scrollPanel.Controls.Add(lblColsCap);
            scrollPanel.Controls.Add(numGridRows);
            scrollPanel.Controls.Add(lblRowsCap);
            scrollPanel.Controls.Add(lblDefSize);
            scrollPanel.Controls.Add(numDefaultItemSize);
            scrollPanel.Controls.Add(lblIconScale);
            scrollPanel.Controls.Add(numIconScale);
            scrollPanel.Controls.Add(chkMinimizeToTray);
            scrollPanel.Controls.Add(lblFolders);
            scrollPanel.Controls.Add(cmbFolders);
            scrollPanel.Controls.Add(chkEditMode);
            scrollPanel.Controls.Add(chkMiniExplorer);
            scrollPanel.Controls.Add(numFolderExit);
            scrollPanel.Controls.Add(lblExit);
            scrollPanel.Controls.Add(chkAutoStart);
            scrollPanel.Controls.Add(chkAutoStartMin);
            scrollPanel.Controls.Add(chkTrayAlways);
            scrollPanel.Controls.Add(chkKeepTab);
            scrollPanel.Controls.Add(lblLang);
            scrollPanel.Controls.Add(cmbLang);
            scrollPanel.Controls.Add(lblHotkey);
            scrollPanel.Controls.Add(cmbHotkey);
            scrollPanel.Controls.Add(lblTypes);
            scrollPanel.Controls.Add(btnTypeIcons);
            scrollPanel.Controls.Add(btnTypeOpen);
            scrollPanel.Controls.Add(btnBackup);
            scrollPanel.Controls.Add(btnRestore);
            bottomBar.Controls.Add(btnSave);
            bottomBar.Controls.Add(btnCancel);

            // Bottom info: the real window position/size — shown only when it differs
            // from the values entered above (numbers only, no separate button).
            lblLive = new Label
            {
                Left = 20,
                Top = 17,
                Width = 350,
                Height = 20,
                ForeColor = settings.IsLightTheme ? Color.FromArgb(90, 90, 90) : Color.FromArgb(170, 170, 170)
            };
            bottomBar.Controls.Add(lblLive);
            numWidth.ValueChanged += (s2, e2) => UpdateLiveLabel();
            numHeight.ValueChanged += (s2, e2) => UpdateLiveLabel();
            numX.ValueChanged += (s2, e2) => UpdateLiveLabel();
            numY.ValueChanged += (s2, e2) => UpdateLiveLabel();
            scrollPanel.AutoScrollMargin = new Size(0, 14);
            Loc.Walk(this);
            UpdateLiveLabel();
        }

        // One-line tooltip helper: EN/RU through Loc.
        private void Tip(Control c, string en, string ru)
        {
            tips.SetToolTip(c, Loc.S(en, ru));
        }

        // Brief feedback after a wallet address was copied: the button caption
        // flashes "Copied" and then returns to normal.
        private void FlashCopied(Button b)
        {
            string orig = b.Text;
            b.Text = Loc.S("Copied ✓", "Скопировано ✓");
            var t = new System.Windows.Forms.Timer { Interval = 1200 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); b.Text = orig; };
            t.Start();
        }

        // Shows the actual (current) window numbers at the bottom, but only when at
        // least one of them differs from the values in the fields.
        private void UpdateLiveLabel()
        {
            if (lblLive == null) return;
            bool differs = liveRect.X != (int)numX.Value || liveRect.Y != (int)numY.Value ||
                           liveRect.Width != (int)numWidth.Value || liveRect.Height != (int)numHeight.Value;
            lblLive.Visible = differs;
            if (differs)
                lblLive.Text = Loc.S("Current window: ", "Текущее окно: ") + liveRect.Width + " x " + liveRect.Height +
                               Loc.S(" at (", " в (") + liveRect.X + ", " + liveRect.Y + ")";
        }

        // One settings row for a font group: [size] [color swatch] [family]
        private void AddFontRow(string labelText, int y, int size, string colorHex, string family,
            out NumericUpDown numSize, out Button colorBtn, out ComboBox combo)
        {
            var lbl = new Label { Text = labelText, Left = 20, Top = y, Width = 100 };
            numSize = new NumericUpDown { Left = 125, Top = y - 2, Width = 45, Maximum = 24, Minimum = 6, Value = Math.Max(6, Math.Min(24, size)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            Tip(numSize, "Caption size", "Размер подписей");

            colorBtn = new Button
            {
                Left = 175,
                Top = y - 2,
                Width = 40,
                Height = 23,
                FlatStyle = FlatStyle.Flat,
                BackColor = Settings.ParseColor(colorHex, textColor),
                ForeColor = textColor
            };
            colorBtn.FlatAppearance.BorderSize = 1;
            Tip(colorBtn, "Caption color; empty button = theme default", "Цвет подписей; пустая кнопка = цвет темы");
            var btn = colorBtn; // lambdas cannot capture out parameters
            colorBtn.Click += (s, e) =>
            {
                using (var cd = new ColorDialog { FullOpen = true, Color = btn.BackColor })
                {
                    if (cd.ShowDialog(this) == DialogResult.OK)
                    {
                        btn.BackColor = cd.Color;
                        btn.Tag = "custom";
                    }
                }
            };

            combo = new ComboBox
            {
                Left = 220,
                Top = y - 2,
                Width = 185,
                DropDownStyle = ComboBoxStyle.DropDown,
                BackColor = panelColor,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            };
            try
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var f in FontFamily.Families) names.Add(f.Name);
                names.Sort(StringComparer.OrdinalIgnoreCase);
                if (!names.Contains(family)) names.Insert(0, family);
                combo.Items.AddRange(names.ToArray());
            }
            catch { }
            combo.Text = family;
            Tip(combo, "Font family", "Шрифт");

            scrollPanel.Controls.Add(lbl);
            scrollPanel.Controls.Add(numSize);
            scrollPanel.Controls.Add(colorBtn);
            scrollPanel.Controls.Add(combo);
        }

        private string ColorValue(Button colorBtn, string original)
        {
            if (colorBtn.Tag == null) return original == null ? "" : original;
            try { return ColorTranslator.ToHtml(colorBtn.BackColor); }
            catch { return original == null ? "" : original; }
        }

        private string FamilyValue(ComboBox combo, string original)
        {
            string name = (combo.Text ?? "").Trim();
            return name.Length > 0 ? name : original;
        }

        // The window can be resized by the bottom grip: re-fit the rounded
        // contour (and the drawn border, which uses Width/Height) to the new size.
        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            try { this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15)); }
            catch { }
        }

        // The dialog gets its own slightly darker gradient so its contour is visible
        // against the main window behind it.
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var brush = new LinearGradientBrush(this.ClientRectangle, bgColor, ControlPaint.Dark(bgColor, 0.08f), LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, this.ClientRectangle);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(120, textColor)))
            {
                e.Graphics.DrawPath(pen, GetRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 15));
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle bounds, int radius)
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

        private void BtnSave_Click(object sender, EventArgs e)
        {
            settings.StartupWidth = (int)numWidth.Value;
            settings.StartupHeight = (int)numHeight.Value;
            settings.WindowX = (int)numX.Value;
            settings.WindowY = (int)numY.Value;
            settings.MinimizeToTray = chkMinimizeToTray.Checked;
            settings.OpenFoldersInPopup = cmbFolders.SelectedIndex == 1;
            settings.FolderOpenProgram = (txtFolderOpenProgram.Text ?? "").Trim();
            settings.MiniExplorerCtrlClick = chkMiniExplorer.Checked;
            // Any Mod+Key combination typed into the editable dropdown; invalid
            // input keeps the dialog open with an explanation.
            string hotkey = (cmbHotkey.Text ?? "").Trim();
            if (hotkey.Length == 0) hotkey = "None";
            if (!hotkey.Equals("None", StringComparison.OrdinalIgnoreCase) && !MainForm.TryParseHotkey(hotkey))
            {
                ConfirmDialog.ShowInfo(this,
                    Loc.S("Cannot parse the hotkey \"", "Не удалось разобрать комбинацию \"") + hotkey +
                    Loc.S("\". Use Ctrl/Alt/Shift/Win + a letter or digit, e.g. Ctrl+Alt+P (or None).",
                          "\". Формат: Ctrl/Alt/Shift/Win + буква или цифра, например Ctrl+Alt+P (или None)."));
                return;
            }
            settings.HotkeyShow = hotkey;
            settings.GridTransparency = (int)numGridTransparency.Value;
            settings.GridColumns = (int)numGridCols.Value;
            settings.GridRows = (int)numGridRows.Value;
            settings.DefaultItemSize = (int)numDefaultItemSize.Value;
            settings.EditMode = chkEditMode.Checked;
            settings.LabelTwoRows = chkTwoRows.Checked;
            settings.LabelTrimShortcut = chkTrimShortcut.Checked;
            settings.LabelTrimExtension = chkTrimExt.Checked;
            settings.LabelCtrlFullNames = chkCtrlNames.Checked;
            settings.LabelAlign2Rows = cmbLabelAlign.SelectedIndex < 0 ? 1 : cmbLabelAlign.SelectedIndex;
            // The 3rd (red, multi-select) state changes only from the panel button;
            // this checkbox just switches it on/off preserving state 2.
            if (!chkEditMode.Checked) settings.EditModeState = 0;
            else if (settings.EditModeState == 0) settings.EditModeState = 1;
            settings.FolderAutoExitSeconds = (int)numFolderExit.Value;
            settings.SearchFuzzyLevel = (int)numFuzzy.Value;
            settings.SearchInMeta = chkSearchMeta.Checked;
            settings.SearchInPaths = chkSearchPaths.Checked;
            settings.SearchInDesc = chkSearchDesc.Checked;
            settings.SearchInStart = chkSearchStart.Checked;
            settings.SearchBoxFontSize = (int)numSearchBoxFont.Value;
            settings.SearchResultsFontSize = (int)numSearchResultsFont.Value;
            settings.FolderAutoExitSeconds = (int)numFolderExit.Value;
            settings.Language = Loc.Languages[Math.Max(0, Math.Min(Loc.Languages.Length - 1, cmbLang.SelectedIndex))];
            settings.AutoStart = chkAutoStart.Checked;
            settings.AutoStartMinimized = chkAutoStartMin.Checked;
            settings.TrayIconAlways = chkTrayAlways.Checked;
            settings.KeepActiveTab = chkKeepTab.Checked;

            settings.IconScale = (int)numIconScale.Value;

            settings.FontItemsSize = (int)numFontItemsSize.Value;
            settings.FontItemsColor = ColorValue(btnFontItemsColor, settings.FontItemsColor);
            settings.FontItemsName = FamilyValue(cmbFontItems, settings.FontItemsName);

            settings.FontTabsSize = (int)numFontTabsSize.Value;
            settings.FontTabsColor = ColorValue(btnFontTabsColor, settings.FontTabsColor);
            settings.FontTabsName = FamilyValue(cmbFontTabs, settings.FontTabsName);

            settings.FontUiSize = (int)numFontUiSize.Value;
            settings.FontUiColor = ColorValue(btnFontUiColor, settings.FontUiColor);
            settings.FontUiName = FamilyValue(cmbFontUi, settings.FontUiName);

            settings.HotkeyWin = chkWinKey.Checked;
            settings.HotkeyStartClick = chkWinClick.Checked;
            settings.SearchSaveHistory = chkSaveHistory.Checked;
            settings.BackupDays = (int)numBackupDays.Value;
            settings.StartMenuSyncHours = (int)numSyncHours.Value;
            settings.UpdateCheckEnabled = chkUpdateCheck.Checked;
            settings.UpdateCheckDays = (int)numUpdateDays.Value;
            settings.UpdateAutoInstall = chkAutoInstall.Checked;
            // Items 0/1 are the classic themes (dark/light, no skin), the rest
            // map back to Skin.All entries (Skin.All[0] is None itself).
            int si = cmbSkin.SelectedIndex;
            if (si <= 0) { settings.SkinName = ""; settings.IsLightTheme = false; }
            else if (si == 1) { settings.SkinName = ""; settings.IsLightTheme = true; }
            else
            {
                // A skin owns the palette: derive the light/dark flag from it so
                // the settings dialog, welcome and mini explorer follow the skin
                // instead of keeping the previous classic theme.
                var chosen = Skin.All[si - 1];
                settings.SkinName = chosen.Id;
                settings.IsLightTheme = !chosen.IsDark;
            }

            settings.Save(settingsPath);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // Saves a full backup zip (settings, tiles/records, bookmarks, file types,
        // search history, the ico folder and the exe) to a user-chosen file —
        // the same archive the "Restore archive..." button accepts.
        private void BtnBackup_Click(object sender, EventArgs e)
        {
            string created = BackupManager.RunBackup(null, settings, false);
            if (created == null)
            {
                ConfirmDialog.ShowInfo(this, Loc.S("Backup failed - see log.txt", "Бэкап не удался — подробности в log.txt"));
                return;
            }
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = Loc.S("Tilettes backup archives (*.zip)|*.zip|All files (*.*)|*.*",
                                   "Архивы бэкапа Tilettes (*.zip)|*.zip|Все файлы (*.*)|*.*");
                sfd.FileName = "Tilettes_backup_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".zip";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        File.Copy(created, sfd.FileName, true);
                        ConfirmDialog.ShowInfo(this, Loc.S("Backup saved: ", "Бэкап сохранён: ") + sfd.FileName);
                    }
                    catch (Exception ex)
                    {
                        ConfirmDialog.ShowInfo(this, Loc.S("Save failed: ", "Сохранить не удалось: ") + ex.Message);
                    }
                }
            }
        }

        // Picks a backup zip created by this app ("Backup now" or the scheduled
        // backup). The archive is unpacked by MainForm right after the dialog
        // closes, so the restored files take effect immediately.
        private void BtnRestore_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = Loc.S("Tilettes backup archives (*.zip)|*.zip|All files (*.*)|*.*",
                                   "Архивы бэкапа Tilettes (*.zip)|*.zip|Все файлы (*.*)|*.*");
                ofd.Title = Loc.S("Restore from a backup archive", "Восстановление из архива бэкапа");
                try
                {
                    string bd = BackupManager.BackupDir();
                    if (Directory.Exists(bd)) ofd.InitialDirectory = bd;
                }
                catch { }
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    string reject = BackupManager.ValidateBackupZip(ofd.FileName);
                    if (reject != null)
                    {
                        ConfirmDialog.ShowInfo(this, reject);
                        return;
                    }
                    RestoreZipPath = ofd.FileName;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
        }
    }
}
