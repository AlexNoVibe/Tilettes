using System;
using System.Drawing;
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
        
        private NumericUpDown numGridTransparency;
        private NumericUpDown numGridCols;
        private NumericUpDown numGridRows;
        private NumericUpDown numDefaultItemSize;
        private CheckBox chkLightTheme;

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

        public SettingsForm(Settings settings, string settingsPath)
        {
            this.settings = settings;
            this.settingsPath = settingsPath;

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

            this.Text = "Settings";
            this.Width = 320;
            this.Height = 520;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;

            this.BackColor = bgColor;
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

            int y = 40;

            var lblWidth = new Label { Text = "Startup Width:", Left = 20, Top = y, Width = 120 };
            numWidth = new NumericUpDown { Left = 150, Top = y-2, Width = 120, Maximum = 2000, Minimum = 100, Value = settings.WindowWidth, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;
            
            var lblHeight = new Label { Text = "Startup Height:", Left = 20, Top = y, Width = 120 };
            numHeight = new NumericUpDown { Left = 150, Top = y-2, Width = 120, Maximum = 2000, Minimum = 100, Value = settings.WindowHeight, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            var lblX = new Label { Text = "Startup X:", Left = 20, Top = y, Width = 120 };
            numX = new NumericUpDown { Left = 150, Top = y-2, Width = 120, Maximum = 4000, Minimum = -4000, Value = settings.WindowX, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            var lblY = new Label { Text = "Startup Y:", Left = 20, Top = y, Width = 120 };
            numY = new NumericUpDown { Left = 150, Top = y-2, Width = 120, Maximum = 4000, Minimum = -4000, Value = settings.WindowY, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            var lblTrans = new Label { Text = "Grid Transp. (0-255):", Left = 20, Top = y, Width = 120 };
            numGridTransparency = new NumericUpDown { Left = 150, Top = y-2, Width = 120, Maximum = 255, Minimum = 0, Value = settings.GridTransparency, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            var lblCols = new Label { Text = "Grid Columns:", Left = 20, Top = y, Width = 120 };
            numGridCols = new NumericUpDown { Left = 150, Top = y-2, Width = 120, Maximum = 100, Minimum = 1, Value = settings.GridColumns, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            var lblRows = new Label { Text = "Grid Rows:", Left = 20, Top = y, Width = 120 };
            numGridRows = new NumericUpDown { Left = 150, Top = y-2, Width = 120, Maximum = 100, Minimum = 1, Value = settings.GridRows, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            var lblSize = new Label { Text = "Def. Item Size:", Left = 20, Top = y, Width = 120 };
            numDefaultItemSize = new NumericUpDown { Left = 150, Top = y-2, Width = 120, Maximum = 4, Minimum = 1, Value = settings.DefaultItemSize, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            y += 30;

            chkMinimizeToTray = new CheckBox { Text = "Minimize instead of close", Left = 20, Top = y, Width = 250, Checked = settings.MinimizeToTray, FlatStyle = FlatStyle.Flat };
            y += 30;
            
            chkLightTheme = new CheckBox { Text = "Light Theme", Left = 20, Top = y, Width = 250, Checked = settings.IsLightTheme, FlatStyle = FlatStyle.Flat };
            y += 30;

            btnBackup = new Button { Text = "Backup Settings", Left = 20, Top = y, Width = 120, FlatStyle = FlatStyle.Flat, BackColor = panelColor };
            btnBackup.FlatAppearance.BorderSize = 0;
            btnBackup.Click += BtnBackup_Click;

            btnRestore = new Button { Text = "Restore Settings", Left = 150, Top = y, Width = 120, FlatStyle = FlatStyle.Flat, BackColor = panelColor };
            btnRestore.FlatAppearance.BorderSize = 0;
            btnRestore.Click += BtnRestore_Click;
            
            y += 50;

            btnSave = new Button { Text = "Save", Left = 50, Top = y, Width = 90, FlatStyle = FlatStyle.Flat, BackColor = panelColor };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.FlatAppearance.MouseOverBackColor = hoverColor;
            btnSave.FlatAppearance.MouseDownBackColor = panelColor;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button { Text = "Cancel", Left = 160, Top = y, Width = 90, FlatStyle = FlatStyle.Flat, BackColor = panelColor };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.FlatAppearance.MouseOverBackColor = hoverColor;
            btnCancel.FlatAppearance.MouseDownBackColor = panelColor;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(lblWidth);
            this.Controls.Add(numWidth);
            this.Controls.Add(lblHeight);
            this.Controls.Add(numHeight);
            this.Controls.Add(lblX);
            this.Controls.Add(numX);
            this.Controls.Add(lblY);
            this.Controls.Add(numY);
            this.Controls.Add(lblTrans);
            this.Controls.Add(numGridTransparency);
            this.Controls.Add(lblCols);
            this.Controls.Add(numGridCols);
            this.Controls.Add(lblRows);
            this.Controls.Add(numGridRows);
            this.Controls.Add(lblSize);
            this.Controls.Add(numDefaultItemSize);
            this.Controls.Add(chkMinimizeToTray);
            this.Controls.Add(chkLightTheme);
            this.Controls.Add(btnBackup);
            this.Controls.Add(btnRestore);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            settings.WindowWidth = (int)numWidth.Value;
            settings.WindowHeight = (int)numHeight.Value;
            settings.WindowX = (int)numX.Value;
            settings.WindowY = (int)numY.Value;
            settings.MinimizeToTray = chkMinimizeToTray.Checked;
            settings.GridTransparency = (int)numGridTransparency.Value;
            settings.GridColumns = (int)numGridCols.Value;
            settings.GridRows = (int)numGridRows.Value;
            settings.DefaultItemSize = (int)numDefaultItemSize.Value;
            settings.IsLightTheme = chkLightTheme.Checked;
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
