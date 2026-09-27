using System;
using System.Drawing;
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
        private Button btnSave;
        private Button btnCancel;
        
        private Color bgColor = Color.FromArgb(30, 30, 30);
        private Color panelColor = Color.FromArgb(45, 45, 48);
        private Color hoverColor = Color.FromArgb(62, 62, 66);
        private Color textColor = Color.White;

        public SettingsForm(Settings settings, string settingsPath)
        {
            this.settings = settings;
            this.settingsPath = settingsPath;

            this.Text = "Settings";
            this.Width = 320;
            this.Height = 280;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;

            this.BackColor = bgColor;
            this.ForeColor = textColor;
            this.Font = new Font("Segoe UI", 9f);

            var lblWidth = new Label { Text = "Startup Width:", Left = 20, Top = 20, Width = 120 };
            numWidth = new NumericUpDown { Left = 150, Top = 18, Width = 120, Maximum = 2000, Minimum = 100, Value = settings.WindowWidth, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            
            var lblHeight = new Label { Text = "Startup Height:", Left = 20, Top = 50, Width = 120 };
            numHeight = new NumericUpDown { Left = 150, Top = 48, Width = 120, Maximum = 2000, Minimum = 100, Value = settings.WindowHeight, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };

            var lblX = new Label { Text = "Startup X:", Left = 20, Top = 80, Width = 120 };
            numX = new NumericUpDown { Left = 150, Top = 78, Width = 120, Maximum = 4000, Minimum = -4000, Value = settings.WindowX, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };

            var lblY = new Label { Text = "Startup Y:", Left = 20, Top = 110, Width = 120 };
            numY = new NumericUpDown { Left = 150, Top = 108, Width = 120, Maximum = 4000, Minimum = -4000, Value = settings.WindowY, BackColor = panelColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };

            chkMinimizeToTray = new CheckBox { Text = "Minimize instead of close", Left = 20, Top = 140, Width = 250, Checked = settings.MinimizeToTray, FlatStyle = FlatStyle.Flat };

            btnSave = new Button { Text = "Save", Left = 50, Top = 190, Width = 90, FlatStyle = FlatStyle.Flat, BackColor = panelColor };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.FlatAppearance.MouseOverBackColor = hoverColor;
            btnSave.FlatAppearance.MouseDownBackColor = panelColor;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button { Text = "Cancel", Left = 160, Top = 190, Width = 90, FlatStyle = FlatStyle.Flat, BackColor = panelColor };
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
            this.Controls.Add(chkMinimizeToTray);
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
            settings.Save(settingsPath);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
