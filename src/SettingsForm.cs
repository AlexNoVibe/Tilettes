using System;
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
        private CheckBox chkLightTheme;
        private CheckBox chkEditMode;
        private CheckBox chkMiniExplorer;

        private NumericUpDown numIconScale;
        private NumericUpDown numFuzzy, numSearchBoxFont, numSearchResultsFont;
        private CheckBox chkSearchMeta, chkSearchPaths, chkSearchDesc, chkSearchStart;
        private NumericUpDown numFolderExit;
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
        public bool RunBackupNow { get; private set; }
        public bool RunSyncNow { get; private set; }

        private Color bgColor;
        private Color panelColor;
        private Color hoverColor;
        private Color textColor;

        private Panel scrollPanel;
        private Panel bottomBar;
        private ToolTip themeTip = new ToolTip();

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

        public SettingsForm(Settings settings, string settingsPath, Rectangle liveWindowRect)
        {
            this.settings = settings;
            this.settingsPath = settingsPath;
            this.liveRect = liveWindowRect;

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
            scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = bgColor };
            bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = panelColor };
            var titleBar = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = panelColor };
            var titleLbl = new Label { Text = "Settings", ForeColor = textColor, AutoSize = true, Location = new Point(10, 7) };
            titleBar.Controls.Add(titleLbl);
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

            int y = 42;

            // Startup size on one row: [W] x [H]
            var lblSize = new Label { Text = "Startup Size:", Left = 20, Top = y, Width = 110 };
            numWidth = new NumericUpDown { Left = 135, Top = y - 2, Width = 70, Maximum = 4000, Minimum = 200, Value = settings.StartupWidth, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblMul = new Label { Text = "x", Left = 208, Top = y, Width = 14 };
            numHeight = new NumericUpDown { Left = 224, Top = y - 2, Width = 70, Maximum = 4000, Minimum = 200, Value = settings.StartupHeight, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            // Window position on one row: [X] , [Y]
            var lblPos = new Label { Text = "Window Position:", Left = 20, Top = y, Width = 110 };
            numX = new NumericUpDown { Left = 135, Top = y - 2, Width = 70, Maximum = 4000, Minimum = -4000, Value = settings.WindowX, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblComma = new Label { Text = ",", Left = 208, Top = y, Width = 14 };
            numY = new NumericUpDown { Left = 224, Top = y - 2, Width = 70, Maximum = 4000, Minimum = -4000, Value = settings.WindowY, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            var lblTrans = new Label { Text = "Grid Transp. (0-255):", Left = 20, Top = y, Width = 120 };
            numGridTransparency = new NumericUpDown { Left = 150, Top = y - 2, Width = 120, Maximum = 255, Minimum = 0, Value = settings.GridTransparency, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            var lblCols = new Label { Text = "Grid Columns:", Left = 20, Top = y, Width = 120 };
            numGridCols = new NumericUpDown { Left = 150, Top = y - 2, Width = 120, Maximum = 100, Minimum = 1, Value = settings.GridColumns, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            var lblRows = new Label { Text = "Grid Rows:", Left = 20, Top = y, Width = 120 };
            numGridRows = new NumericUpDown { Left = 150, Top = y - 2, Width = 120, Maximum = 100, Minimum = 1, Value = settings.GridRows, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            var lblDefSize = new Label { Text = "Def. Item Size:", Left = 20, Top = y, Width = 120 };
            numDefaultItemSize = new NumericUpDown { Left = 150, Top = y - 2, Width = 120, Maximum = 6, Minimum = 1, Value = settings.DefaultItemSize, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            // Icon scale, percent (100 = default)
            var lblIconScale = new Label { Text = "Icon Scale (%):", Left = 20, Top = y, Width = 120 };
            numIconScale = new NumericUpDown { Left = 150, Top = y - 2, Width = 120, Maximum = 400, Minimum = 25, Value = Math.Max(25, Math.Min(400, settings.IconScale)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            var lblFolders = new Label { Text = "Open folders in:", Left = 20, Top = y, Width = 120 };
            cmbFolders = new ComboBox { Left = 150, Top = y - 2, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            cmbFolders.Items.Add(Loc.S("Same window"));
            cmbFolders.Items.Add(Loc.S("Popup window"));
            cmbFolders.SelectedIndex = settings.OpenFoldersInPopup ? 1 : 0;
            numFolderExit = new NumericUpDown { Left = 296, Top = y - 2, Width = 56, Maximum = 600, Minimum = 0, Value = Math.Max(0, Math.Min(600, settings.FolderAutoExitSeconds)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblExit = new Label { Text = Loc.S("sec idle", "сек простоя"), Left = 358, Top = y, Width = 105 };
            var exitTip = new ToolTip();
            exitTip.SetToolTip(numFolderExit, Loc.S("Return from a folder after this many seconds without activity (0 = off)", "Выходить из папки после стольких секунд без активности (0 = выкл)"));
            y += 30;

            chkLightTheme = new CheckBox { Text = "Light Theme", Left = 20, Top = y, Width = 250, Checked = settings.IsLightTheme, ForeColor = textColor };
            chkEditMode = new CheckBox { Text = "Allow adding icons", Left = 290, Top = y, Width = 280, Checked = settings.EditMode, ForeColor = textColor };
            y += 30;

            var lblHotkey = new Label { Text = "Show window hotkey:", Left = 20, Top = y, Width = 130 };
            cmbHotkey = new ComboBox { Left = 150, Top = y - 2, Width = 185, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            cmbHotkey.Items.AddRange(new object[] { "None", "Ctrl+J", "Ctrl+Shift+J", "Ctrl+Alt+J", "Ctrl+K", "Ctrl+Shift+K", "Alt+J" });
            string hk = string.IsNullOrEmpty(settings.HotkeyShow) ? "Ctrl+J" : settings.HotkeyShow;
            if (!cmbHotkey.Items.Contains(hk)) cmbHotkey.Items.Add(hk);
            cmbHotkey.SelectedItem = hk;
            chkWinKey = new CheckBox
            {
                Text = Loc.S("Capture Start button (Win)", "Захват кнопки Пуск (Win)"),
                Left = 345,
                Top = y,
                Width = 235,
                Checked = settings.HotkeyWin,
                ForeColor = textColor
            };
            var winKeyTip = new ToolTip();
            winKeyTip.SetToolTip(chkWinKey, Loc.S("Pressing the Win key shows the panel instead of the Start menu",
                "Кнопка Пуск (Win) открывает панель вместо меню Пуск"));
            scrollPanel.Controls.Add(chkWinKey);
            y += 30;

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
            var lblSearchSection = new Label { Text = "Search", Left = 20, Top = y, Width = 250, ForeColor = textColor, Font = new Font(this.Font, FontStyle.Bold) };
            y += 24;
            var lblFuzzy = new Label { Text = "Fuzzy accuracy (0-3):", Left = 20, Top = y, Width = 150 };
            numFuzzy = new NumericUpDown { Left = 175, Top = y - 2, Width = 45, Minimum = 0, Maximum = 3, Value = Math.Max(0, Math.Min(3, settings.SearchFuzzyLevel)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 26;
            chkSearchMeta = new CheckBox { Text = "Search in metadata (exe, product)", Left = 20, Top = y, Width = 530, Checked = settings.SearchInMeta, ForeColor = textColor };
            y += 24;
            chkSearchPaths = new CheckBox { Text = "Search in full paths", Left = 20, Top = y, Width = 530, Checked = settings.SearchInPaths, ForeColor = textColor };
            y += 24;
            chkSearchDesc = new CheckBox { Text = "Search in descriptions", Left = 20, Top = y, Width = 530, Checked = settings.SearchInDesc, ForeColor = textColor };
            y += 24;
            chkSearchStart = new CheckBox { Text = Loc.S("Search in the Start Menu tab", "Искать во вкладке Пуск"), Left = 20, Top = y, Width = 530, Checked = settings.SearchInStart, ForeColor = textColor };
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
            btnClearHistory.Click += (s, e) => { SearchHistoryStore.Clear(); MessageBox.Show(Loc.S("Search history cleared.", "История поиска очищена."), "WinPanel"); };
            scrollPanel.Controls.Add(chkSaveHistory);
            scrollPanel.Controls.Add(btnClearHistory);
            y += 26;
            var lblSearchFonts = new Label { Text = "Search fonts:", Left = 20, Top = y, Width = 125 };
            numSearchBoxFont = new NumericUpDown { Left = 150, Top = y - 2, Width = 50, Minimum = 7, Maximum = 30, Value = Math.Max(7, Math.Min(30, settings.SearchBoxFontSize)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblSearchBoxFont = new Label { Text = Loc.S("box", "строка поиска"), Left = 205, Top = y, Width = 150 };
            numSearchResultsFont = new NumericUpDown { Left = 360, Top = y - 2, Width = 45, Minimum = 7, Maximum = 30, Value = Math.Max(7, Math.Min(30, settings.SearchResultsFontSize)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblSearchResultsFont = new Label { Text = Loc.S("results", "результаты"), Left = 410, Top = y, Width = 150 };
            scrollPanel.Controls.Add(lblSearchSection);
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

            var lblTypes = new Label { Text = "File types:", Left = 20, Top = y, Width = 92 };
            var btnTypeIcons = new Button { Text = "Icons by type...", Left = 115, Top = y - 3, Width = 140, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnTypeIcons.FlatAppearance.BorderSize = 0;
            btnTypeIcons.Click += (s, e) => { using (var ft = new FileTypesForm(FileTypesForm.Mode.Icons, FileTypes.DefaultFilePath)) ft.ShowDialog(this); };
            var btnTypeOpen = new Button { Text = "Open with by type...", Left = 260, Top = y - 3, Width = 150, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnTypeOpen.FlatAppearance.BorderSize = 0;
            btnTypeOpen.Click += (s, e) => { using (var ft = new FileTypesForm(FileTypesForm.Mode.OpenWith, FileTypes.DefaultFilePath)) ft.ShowDialog(this); };
            y += 36;

            chkMiniExplorer = new CheckBox { Text = "Ctrl+Click a folder opens Mini Explorer", Left = 20, Top = y, Width = 530, Checked = settings.MiniExplorerCtrlClick, ForeColor = textColor };
            y += 30;

            var lblSection = new Label { Text = "Autostart & window", Left = 20, Top = y, Width = 250, ForeColor = textColor, Font = new Font(this.Font, FontStyle.Bold) };
            y += 24;
            chkAutoStart = new CheckBox { Text = "Autostart with Windows", Left = 20, Top = y, Width = 205, Checked = settings.AutoStart, ForeColor = textColor };
            chkAutoStartMin = new CheckBox { Text = "After autostart - go to tray", Left = 240, Top = y, Width = 225, Checked = settings.AutoStartMinimized, ForeColor = textColor };
            y += 26;
            chkMinimizeToTray = new CheckBox { Text = "Minimize instead of close", Left = 20, Top = y, Width = 215, Checked = settings.MinimizeToTray, ForeColor = textColor };
            chkTrayAlways = new CheckBox { Text = "Always keep tray icon", Left = 240, Top = y, Width = 225, Checked = settings.TrayIconAlways, ForeColor = textColor };
            y += 26;
            chkKeepTab = new CheckBox { Text = "Remember active tab", Left = 20, Top = y, Width = 330, Checked = settings.KeepActiveTab, ForeColor = textColor };
            y += 30;
            var lblLang = new Label { Text = "Language:", Left = 20, Top = y, Width = 90 };
            cmbLang = new ComboBox { Left = 110, Top = y - 2, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            cmbLang.Items.Add("Русский");
            cmbLang.Items.Add("English");
            cmbLang.SelectedIndex = string.Equals(settings.Language, "en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            y += 30;

            // ---- Maintenance: skin, scheduled backup, Start Menu sync ----
            var lblMaint = new Label { Text = Loc.S("Maintenance & Start Menu", "Обслуживание и Пуск"), Left = 20, Top = y, Width = 350, ForeColor = textColor, Font = new Font(this.Font, FontStyle.Bold) };
            y += 24;

            var lblSkin = new Label { Text = Loc.S("Skin:", "Шкурка:"), Left = 20, Top = y, Width = 90 };
            cmbSkin = new ComboBox { Left = 110, Top = y - 2, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            int skinIdx = 0;
            for (int i = 0; i < Skin.All.Count; i++)
            {
                cmbSkin.Items.Add(Skin.All[i].DisplayName);
                if (string.Equals(Skin.All[i].Id, settings.SkinName, StringComparison.OrdinalIgnoreCase)) skinIdx = i;
            }
            cmbSkin.SelectedIndex = skinIdx;
            y += 28;

            var lblBackupDays = new Label { Text = Loc.S("Backup every N days (0 = off):", "Бэкап раз в N дней (0 = выкл):"), Left = 20, Top = y, Width = 210 };
            numBackupDays = new NumericUpDown { Left = 235, Top = y - 2, Width = 50, Maximum = 365, Minimum = 0, Value = Math.Max(0, Math.Min(365, settings.BackupDays)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblSyncHours = new Label { Text = Loc.S("Sync Start Menu every N h (0 = off):", "Синхр. Пуска раз в N ч (0 = выкл):"), Left = 300, Top = y, Width = 190 };
            numSyncHours = new NumericUpDown { Left = 492, Top = y - 2, Width = 50, Maximum = 8760, Minimum = 0, Value = Math.Max(0, Math.Min(8760, settings.StartMenuSyncHours)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var backupTip = new ToolTip();
            backupTip.SetToolTip(numBackupDays, Loc.S("Full backup (settings + shortcuts + bookmarks) into autoBackup\\; runs 3 minutes after launch when due",
                "Полный бэкап (настройки + ярлыки + закладки) в autoBackup\\; делается через 3 минуты после запуска, когда подошёл срок"));
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
            scrollPanel.Controls.Add(lblMaint);
            scrollPanel.Controls.Add(lblSkin);
            scrollPanel.Controls.Add(cmbSkin);
            scrollPanel.Controls.Add(lblBackupDays);
            scrollPanel.Controls.Add(numBackupDays);
            scrollPanel.Controls.Add(lblSyncHours);
            scrollPanel.Controls.Add(numSyncHours);
            scrollPanel.Controls.Add(btnRunBackup);
            scrollPanel.Controls.Add(btnRunSync);
            y += 30;

            btnBackup = new Button { Text = Loc.S("Save backup", "Сохранить бэкап"), Left = 20, Top = y, Width = 130, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnBackup.FlatAppearance.BorderSize = 0;
            btnBackup.Click += BtnBackup_Click;

            btnRestore = new Button { Text = Loc.S("Restore archive...", "Восстановить из архива..."), Left = 165, Top = y, Width = 190, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnRestore.FlatAppearance.BorderSize = 0;
            btnRestore.Click += BtnRestore_Click;
            var restoreTip = new ToolTip();
            restoreTip.SetToolTip(btnRestore, Loc.S("Expects a .zip created by \"Backup now\" / scheduled backup; files are unpacked into the working folder, WinPanel.exe is not replaced",
                "Ожидается .zip, созданный «Бэкапом сейчас» или по расписанию; файлы распаковываются в рабочую папку, WinPanel.exe не заменяется"));

            y += 44;

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
            scrollPanel.Controls.Add(lblPos);
            scrollPanel.Controls.Add(numX);
            scrollPanel.Controls.Add(lblComma);
            scrollPanel.Controls.Add(numY);
            scrollPanel.Controls.Add(lblTrans);
            scrollPanel.Controls.Add(numGridTransparency);
            scrollPanel.Controls.Add(lblCols);
            scrollPanel.Controls.Add(numGridCols);
            scrollPanel.Controls.Add(lblRows);
            scrollPanel.Controls.Add(numGridRows);
            scrollPanel.Controls.Add(lblDefSize);
            scrollPanel.Controls.Add(numDefaultItemSize);
            scrollPanel.Controls.Add(lblIconScale);
            scrollPanel.Controls.Add(numIconScale);
            scrollPanel.Controls.Add(chkMinimizeToTray);
            scrollPanel.Controls.Add(lblFolders);
            scrollPanel.Controls.Add(cmbFolders);
            scrollPanel.Controls.Add(chkLightTheme);
            scrollPanel.Controls.Add(chkEditMode);
            scrollPanel.Controls.Add(chkMiniExplorer);
            scrollPanel.Controls.Add(numFolderExit);
            scrollPanel.Controls.Add(lblExit);
            scrollPanel.Controls.Add(lblSection);
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
            // The skin replaces the theme palette entirely: while one is active the
            // Light Theme checkbox would do nothing visible, so it is disabled with
            // an explanation instead of silently ignoring the setting.
            cmbSkin.SelectedIndexChanged += (s2, e2) => UpdateThemeAvailability();
            UpdateThemeAvailability();
            scrollPanel.AutoScrollMargin = new Size(0, 14);
            Loc.Walk(this);
            UpdateLiveLabel();
        }

        // Light Theme is only meaningful without a decorative skin.
        private void UpdateThemeAvailability()
        {
            if (chkLightTheme == null || cmbSkin == null) return;
            bool skinOn = cmbSkin.SelectedIndex > 0; // index 0 = "Отключено"
            chkLightTheme.Enabled = !skinOn;
            themeTip.SetToolTip(chkLightTheme, skinOn
                ? Loc.S("The skin defines the colors - set Skin to \"None\" to control the theme", "Шкурка задаёт цвета сама — выберите Шкурку «Отключено», чтобы управлять темой")
                : null);
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
            settings.MiniExplorerCtrlClick = chkMiniExplorer.Checked;
            settings.HotkeyShow = cmbHotkey.SelectedItem != null ? cmbHotkey.SelectedItem.ToString() : "Ctrl+J";
            settings.GridTransparency = (int)numGridTransparency.Value;
            settings.GridColumns = (int)numGridCols.Value;
            settings.GridRows = (int)numGridRows.Value;
            settings.DefaultItemSize = (int)numDefaultItemSize.Value;
            settings.IsLightTheme = chkLightTheme.Checked;
            settings.EditMode = chkEditMode.Checked;
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
            settings.Language = cmbLang.SelectedIndex == 1 ? "en" : "ru";
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
            settings.SearchSaveHistory = chkSaveHistory.Checked;
            settings.BackupDays = (int)numBackupDays.Value;
            settings.StartMenuSyncHours = (int)numSyncHours.Value;
            try { settings.SkinName = cmbSkin.SelectedIndex >= 0 && cmbSkin.SelectedIndex < Skin.All.Count ? Skin.All[cmbSkin.SelectedIndex].Id : ""; }
            catch { settings.SkinName = ""; }

            settings.Save(settingsPath);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnBackup_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "INI Files (*.ini)|*.ini|All Files (*.*)|*.*";
                sfd.FileName = "settings_backup.ini";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    settings.Save(sfd.FileName);
                    MessageBox.Show("Backup created successfully.", "Backup");
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
                ofd.Filter = Loc.S("WinPanel backup archives (*.zip)|*.zip|All files (*.*)|*.*",
                                   "Архивы бэкапа WinPanel (*.zip)|*.zip|Все файлы (*.*)|*.*");
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
                        MessageBox.Show(this, reject, "WinPanel");
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
