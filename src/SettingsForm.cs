using System;
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

        public SettingsForm(Settings settings, string settingsPath)
        {
            this.settings = settings;
            this.settingsPath = settingsPath;

            this.Text = "Settings";
            this.Width = 300;
            this.Height = 250;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;

            var lblWidth = new Label { Text = "Startup Width:", Left = 10, Top = 20, Width = 100 };
            numWidth = new NumericUpDown { Left = 120, Top = 18, Width = 100, Maximum = 2000, Minimum = 100, Value = settings.WindowWidth };
            
            var lblHeight = new Label { Text = "Startup Height:", Left = 10, Top = 50, Width = 100 };
            numHeight = new NumericUpDown { Left = 120, Top = 48, Width = 100, Maximum = 2000, Minimum = 100, Value = settings.WindowHeight };

            var lblX = new Label { Text = "Startup X:", Left = 10, Top = 80, Width = 100 };
            numX = new NumericUpDown { Left = 120, Top = 78, Width = 100, Maximum = 4000, Minimum = -4000, Value = settings.WindowX };

            var lblY = new Label { Text = "Startup Y:", Left = 10, Top = 110, Width = 100 };
            numY = new NumericUpDown { Left = 120, Top = 108, Width = 100, Maximum = 4000, Minimum = -4000, Value = settings.WindowY };

            chkMinimizeToTray = new CheckBox { Text = "Minimize instead of close (tray/taskbar)", Left = 10, Top = 140, Width = 250, Checked = settings.MinimizeToTray };

            btnSave = new Button { Text = "Save", Left = 50, Top = 170, Width = 80 };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button { Text = "Cancel", Left = 150, Top = 170, Width = 80 };
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
