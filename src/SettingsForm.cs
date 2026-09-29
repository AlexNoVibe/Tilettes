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
        private CheckBox chkMiniExplorer;

        private NumericUpDown numIconScale;
        private NumericUpDown numFuzzy, numSearchBoxFont, numSearchResultsFont;
        private CheckBox chkSearchMeta, chkSearchPaths, chkSearchDesc;
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

        private Color bgColor;
        private Color panelColor;
        private Color hoverColor;
        private Color textColor;

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
            this.Width = 470;
            this.Height = 880;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;

            this.ForeColor = textColor;
            this.Font = new Font("Segoe UI", 9f);

            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));

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
            numDefaultItemSize = new NumericUpDown { Left = 150, Top = y - 2, Width = 120, Maximum = 4, Minimum = 1, Value = settings.DefaultItemSize, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
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

            chkLightTheme = new CheckBox { Text = "Light Theme", Left = 20, Top = y, Width = 300, Checked = settings.IsLightTheme, ForeColor = textColor };
            y += 30;

            var lblHotkey = new Label { Text = "Show window hotkey:", Left = 20, Top = y, Width = 130 };
            cmbHotkey = new ComboBox { Left = 150, Top = y - 2, Width = 185, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            cmbHotkey.Items.AddRange(new object[] { "None", "Ctrl+J", "Ctrl+Shift+J", "Ctrl+Alt+J", "Ctrl+K", "Ctrl+Shift+K", "Alt+J" });
            string hk = string.IsNullOrEmpty(settings.HotkeyShow) ? "Ctrl+J" : settings.HotkeyShow;
            if (!cmbHotkey.Items.Contains(hk)) cmbHotkey.Items.Add(hk);
            cmbHotkey.SelectedItem = hk;
            y += 30;

            // Font rows: [size] [color] [family] — one row per group
            AddFontRow("Tiles Font:", y, settings.FontItemsSize, settings.FontItemsColor, settings.FontItemsName,
                out numFontItemsSize, out btnFontItemsColor, out cmbFontItems);
            y += 30;
            AddFontRow("Tabs Font:", y, settings.FontTabsSize, settings.FontTabsColor, settings.FontTabsName,
                out numFontTabsSize, out btnFontTabsColor, out cmbFontTabs);
            y += 30;
            AddFontRow("UI Font:", y, settings.FontUiSize, settings.FontUiColor, settings.FontUiName,
                out numFontUiSize, out btnFontItemsColor, out cmbFontUi);
            y += 34;
            // ---- Search section ----
            var lblSearchSection = new Label { Text = "Search", Left = 20, Top = y, Width = 250, ForeColor = textColor, Font = new Font(this.Font, FontStyle.Bold) };
            y += 24;
            var lblFuzzy = new Label { Text = "Fuzzy accuracy (0-3):", Left = 20, Top = y, Width = 150 };
            numFuzzy = new NumericUpDown { Left = 175, Top = y - 2, Width = 45, Minimum = 0, Maximum = 3, Value = Math.Max(0, Math.Min(3, settings.SearchFuzzyLevel)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 26;
            chkSearchMeta = new CheckBox { Text = "Search in metadata (exe, product)", Left = 20, Top = y, Width = 420, Checked = settings.SearchInMeta, ForeColor = textColor };
            y += 24;
            chkSearchPaths = new CheckBox { Text = "Search in full paths", Left = 20, Top = y, Width = 420, Checked = settings.SearchInPaths, ForeColor = textColor };
            y += 24;
            chkSearchDesc = new CheckBox { Text = "Search in descriptions", Left = 20, Top = y, Width = 420, Checked = settings.SearchInDesc, ForeColor = textColor };
            y += 26;
            var lblSearchFonts = new Label { Text = "Search fonts:", Left = 20, Top = y, Width = 120 };
            numSearchBoxFont = new NumericUpDown { Left = 150, Top = y - 2, Width = 50, Minimum = 7, Maximum = 30, Value = Math.Max(7, Math.Min(30, settings.SearchBoxFontSize)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblSearchBoxFont = new Label { Text = "box", Left = 205, Top = y, Width = 35 };
            numSearchResultsFont = new NumericUpDown { Left = 250, Top = y - 2, Width = 50, Minimum = 7, Maximum = 30, Value = Math.Max(7, Math.Min(30, settings.SearchResultsFontSize)), BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            var lblSearchResultsFont = new Label { Text = "results", Left = 305, Top = y, Width = 60 };
            this.Controls.Add(lblSearchSection);
            this.Controls.Add(lblFuzzy);
            this.Controls.Add(numFuzzy);
            this.Controls.Add(chkSearchMeta);
            this.Controls.Add(chkSearchPaths);
            this.Controls.Add(chkSearchDesc);
            this.Controls.Add(lblSearchFonts);
            this.Controls.Add(numSearchBoxFont);
            this.Controls.Add(numSearchResultsFont);
            this.Controls.Add(lblSearchResultsFont);

            var lblTypes = new Label { Text = "File types:", Left = 20, Top = y, Width = 92 };
            var btnTypeIcons = new Button { Text = "Icons by type...", Left = 115, Top = y - 3, Width = 140, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnTypeIcons.FlatAppearance.BorderSize = 0;
            btnTypeIcons.Click += (s, e) => { using (var ft = new FileTypesForm(FileTypesForm.Mode.Icons, FileTypes.DefaultFilePath)) ft.ShowDialog(this); };
            var btnTypeOpen = new Button { Text = "Open with by type...", Left = 260, Top = y - 3, Width = 150, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnTypeOpen.FlatAppearance.BorderSize = 0;
            btnTypeOpen.Click += (s, e) => { using (var ft = new FileTypesForm(FileTypesForm.Mode.OpenWith, FileTypes.DefaultFilePath)) ft.ShowDialog(this); };
            y += 36;

            chkMiniExplorer = new CheckBox { Text = "Ctrl+Click a folder opens Mini Explorer", Left = 20, Top = y, Width = 420, Checked = settings.MiniExplorerCtrlClick, ForeColor = textColor };
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

            btnBackup = new Button { Text = "Backup Settings", Left = 20, Top = y, Width = 130, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnBackup.FlatAppearance.BorderSize = 0;
            btnBackup.Click += BtnBackup_Click;

            btnRestore = new Button { Text = "Restore Settings", Left = 165, Top = y, Width = 130, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnRestore.FlatAppearance.BorderSize = 0;
            btnRestore.Click += BtnRestore_Click;

            y += 44;

            btnSave = new Button { Text = "Save", Left = 90, Top = y, Width = 90, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.FlatAppearance.MouseOverBackColor = hoverColor;
            btnSave.FlatAppearance.MouseDownBackColor = panelColor;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button { Text = "Cancel", Left = 200, Top = y, Width = 90, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = textColor };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.FlatAppearance.MouseOverBackColor = hoverColor;
            btnCancel.FlatAppearance.MouseDownBackColor = panelColor;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(lblSize);
            this.Controls.Add(numWidth);
            this.Controls.Add(lblMul);
            this.Controls.Add(numHeight);
            this.Controls.Add(lblPos);
            this.Controls.Add(numX);
            this.Controls.Add(lblComma);
            this.Controls.Add(numY);
            this.Controls.Add(lblTrans);
            this.Controls.Add(numGridTransparency);
            this.Controls.Add(lblCols);
            this.Controls.Add(numGridCols);
            this.Controls.Add(lblRows);
            this.Controls.Add(numGridRows);
            this.Controls.Add(lblDefSize);
            this.Controls.Add(numDefaultItemSize);
            this.Controls.Add(lblIconScale);
            this.Controls.Add(numIconScale);
            this.Controls.Add(chkMinimizeToTray);
            this.Controls.Add(lblFolders);
            this.Controls.Add(cmbFolders);
            this.Controls.Add(chkLightTheme);
            this.Controls.Add(chkMiniExplorer);
            this.Controls.Add(numFolderExit);
            this.Controls.Add(lblExit);
            this.Controls.Add(lblSection);
            this.Controls.Add(chkAutoStart);
            this.Controls.Add(chkAutoStartMin);
            this.Controls.Add(chkTrayAlways);
            this.Controls.Add(chkKeepTab);
            this.Controls.Add(lblLang);
            this.Controls.Add(cmbLang);
            this.Controls.Add(lblHotkey);
            this.Controls.Add(cmbHotkey);
            this.Controls.Add(lblTypes);
            this.Controls.Add(btnTypeIcons);
            this.Controls.Add(btnTypeOpen);
            this.Controls.Add(btnBackup);
            this.Controls.Add(btnRestore);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            // Bottom info: the real window position/size — shown only when it differs
            // from the values entered above (numbers only, no separate button).
            lblLive = new Label
            {
                Left = 20,
                Top = this.ClientSize.Height - 44,
                Width = 390,
                Height = 20,
                ForeColor = settings.IsLightTheme ? Color.FromArgb(90, 90, 90) : Color.FromArgb(170, 170, 170)
            };
            this.Controls.Add(lblLive);
            numWidth.ValueChanged += (s2, e2) => UpdateLiveLabel();
            numHeight.ValueChanged += (s2, e2) => UpdateLiveLabel();
            numX.ValueChanged += (s2, e2) => UpdateLiveLabel();
            numY.ValueChanged += (s2, e2) => UpdateLiveLabel();
            Loc.Walk(this);
            UpdateLiveLabel();
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
                lblLive.Text = "Current window: " + liveRect.Width + " x " + liveRect.Height +
                               " at (" + liveRect.X + ", " + liveRect.Y + ")";
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

            this.Controls.Add(lbl);
            this.Controls.Add(numSize);
            this.Controls.Add(colorBtn);
            this.Controls.Add(combo);
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
            settings.FolderAutoExitSeconds = (int)numFolderExit.Value;
            settings.SearchFuzzyLevel = (int)numFuzzy.Value;
            settings.SearchInMeta = chkSearchMeta.Checked;
            settings.SearchInPaths = chkSearchPaths.Checked;
            settings.SearchInDesc = chkSearchDesc.Checked;
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

        private void BtnRestore_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "INI Files (*.ini)|*.ini|All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.Copy(ofd.FileName, settingsPath, true);
                        MessageBox.Show("Settings restored successfully! They will take effect when you close this window.", "Restore");

                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to restore settings: " + ex.Message, "Error");
                    }
                }
            }
        }
    }
}
