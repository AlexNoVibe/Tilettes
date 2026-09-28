using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WinPanel
{
    // Editor for file-type rules (opened from the settings window).
    // Mode.Icons    - which icon to show for a file type on the panel.
    // Mode.OpenWith - which program to open such files with.
    public class FileTypesForm : Form
    {
        public enum Mode { Icons, OpenWith }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private readonly Mode mode;
        private readonly string filePath;
        private readonly List<FileTypeRule> work;
        private ListBox list;
        private Color bgColor;
        private Color panelColor;
        private Color hoverColor;
        private Color textColor;
        private Color dimColor;
        private readonly Dictionary<string, Image> previewCache = new Dictionary<string, Image>();

        public FileTypesForm(Mode mode, string filePath)
        {
            this.mode = mode;
            this.filePath = filePath;
            this.work = FileTypes.CloneRules();

            Settings settings = MainForm.CurrentSettings;
            bool light = settings != null && settings.IsLightTheme;
            bgColor = light ? Color.FromArgb(232, 232, 234) : Color.FromArgb(24, 24, 28);
            panelColor = light ? Color.FromArgb(212, 212, 216) : Color.FromArgb(45, 45, 48);
            hoverColor = light ? Color.FromArgb(196, 196, 202) : Color.FromArgb(62, 62, 66);
            textColor = light ? Color.Black : Color.White;
            dimColor = light ? Color.FromArgb(110, 110, 115) : Color.FromArgb(165, 165, 170);
            if (settings != null) this.Font = Settings.MakeFont(settings.FontUiName, settings.FontUiSize);

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.BackColor = bgColor;
            this.ForeColor = textColor;
            this.ClientSize = new Size(600, 470);
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            };

            var titleBar = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = panelColor };
            titleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
            var title = new Label
            {
                Text = mode == Mode.Icons ? "File Type Icons" : "Open With by File Type",
                ForeColor = textColor,
                AutoSize = true,
                Location = new Point(10, 7)
            };
            titleBar.Controls.Add(title);
            var closeBtn = new Button { Text = "X", Width = 30, Height = 30, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, ForeColor = textColor, BackColor = panelColor };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            titleBar.Controls.Add(closeBtn);
            this.Controls.Add(titleBar);

            var caption = new Label
            {
                Left = 16,
                Top = 36,
                Width = 566,
                Height = 60,
                ForeColor = dimColor,
                Text = mode == Mode.Icons
                    ? "Priority on tiles: item icon (Change Icon) > file type icon (set here) > standard Windows icon.\nPatterns: .txt - extension; readme.* or *.bak - mask; group: .jpg / .jpeg / .png.\nChosen icons are copied into the panel's \"ico\" folder."
                    : "Files of these types are opened by the chosen program (the file path is passed to it). Other types open the standard way.\nPatterns: .txt - extension; readme.* or *.bak - mask; group: .jpg / .jpeg / .png.\nShortcuts (.lnk) are not re-mapped."
            };
            this.Controls.Add(caption);

            list = new ListBox
            {
                Left = 16,
                Top = 98,
                Width = 568,
                Height = 282,
                BackColor = bgColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 30,
                IntegralHeight = false
            };
            list.DrawItem += List_DrawItem;
            list.DoubleClick += (s, e) => ChangeSelected();
            this.Controls.Add(list);

            var btnAdd = MakeButton("Add type...", 16, 392, 120);
            btnAdd.Click += (s, e) => AddNew();
            var btnChange = MakeButton("Change...", 144, 392, 110);
            btnChange.Click += (s, e) => ChangeSelected();
            var btnClear = MakeButton(mode == Mode.Icons ? "Remove icon" : "Open standard", 262, 392, 160);
            btnClear.Click += (s, e) => ClearSelected();

            var btnImport = MakeButton("Import...", 16, 428, 110);
            btnImport.Click += (s, e) => ImportRules();
            var btnExport = MakeButton("Export...", 134, 428, 110);
            btnExport.Click += (s, e) => ExportRules();

            var btnCancel = MakeButton("Cancel", 376, 428, 90);
            btnCancel.DialogResult = DialogResult.Cancel;
            var btnOk = MakeButton("OK", 474, 428, 90);
            btnOk.Click += (s, e) =>
            {
                FileTypes.ReplaceAll(work);
                FileTypes.Save(filePath);
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            Reload();

            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));
            this.FormClosed += (s, e) =>
            {
                foreach (var img in previewCache.Values)
                {
                    try { if (img != null) img.Dispose(); } catch { }
                }
                previewCache.Clear();
            };
        }

        private Button MakeButton(string text, int x, int y, int w)
        {
            var b = new Button
            {
                Text = text,
                Left = x,
                Top = y,
                Width = w,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = hoverColor;
            this.Controls.Add(b);
            return b;
        }

        private void Reload()
        {
            work.Sort(delegate(FileTypeRule a, FileTypeRule b)
            {
                return string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase);
            });
            list.Items.Clear();
            foreach (var r in work) list.Items.Add(r.Extension);
            list.Invalidate();
        }

        private static bool IsEmpty(FileTypeRule r)
        {
            return string.IsNullOrEmpty(r.IconPath) && string.IsNullOrEmpty(r.OpenWith);
        }

        private void List_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= work.Count) return;
            var r = work[e.Index];
            var g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var back = new SolidBrush(sel ? hoverColor : bgColor))
                g.FillRectangle(back, e.Bounds);

            int x = e.Bounds.Left + 8;
            if (mode == Mode.Icons)
            {
                Image img = GetPreview(r.IconPath);
                if (img != null)
                    g.DrawImage(img, new Rectangle(x, e.Bounds.Top + 7, 16, 16));
                x += 26;
            }
            TextRenderer.DrawText(g, r.Extension ?? "", this.Font, new Point(x, e.Bounds.Top + 7), textColor);

            string info;
            Color infoColor;
            if (mode == Mode.Icons)
            {
                bool has = !string.IsNullOrEmpty(r.IconPath);
                info = has ? Path.GetFileName(r.IconPath) : "(standard icon)";
                infoColor = has ? textColor : dimColor;
            }
            else
            {
                bool has = !string.IsNullOrEmpty(r.OpenWith);
                info = has ? r.OpenWith : "(standard)";
                infoColor = has ? textColor : dimColor;
            }
            var sz = TextRenderer.MeasureText(info, this.Font);
            int ix = e.Bounds.Right - sz.Width - 10;
            if (ix < x + 70) ix = x + 70;
            TextRenderer.DrawText(g, info, this.Font, new Point(ix, e.Bounds.Top + 7), infoColor);
        }

        private Image GetPreview(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Image cached;
            if (previewCache.TryGetValue(path, out cached)) return cached;
            Image img = null;
            try { if (File.Exists(path)) img = IconExtractor.LoadAny(path); } catch { }
            previewCache[path] = img;
            return img;
        }

        private FileTypeRule FindInWork(string ext)
        {
            string norm = FileTypes.NormalizePatternText(ext);
            if (norm == null) return null;
            foreach (var r in work)
                if (string.Equals(r.Extension, norm, StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        private void AddNew()
        {
            string ext = Prompt.ShowDialog("File type pattern (e.g. .txt, readme.*, .jpg / .png):", "Add file type", ".");
            string norm = FileTypes.NormalizePatternText(ext);
            if (norm == null)
            {
                if (!string.IsNullOrWhiteSpace(ext) && ext.Trim() != ".")
                    MessageBox.Show(this, "Invalid pattern. Examples: .txt, readme.*, .jpg / .png", "Add file type");
                return;
            }
            var rule = FindInWork(norm);
            bool created = false;
            if (rule == null)
            {
                rule = new FileTypeRule { Extension = norm };
                work.Add(rule);
                created = true;
            }
            if (!PickValueFor(rule) && created && IsEmpty(rule))
                work.Remove(rule);
            Reload();
        }

        private void ChangeSelected()
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= work.Count) return;
            PickValueFor(work[list.SelectedIndex]);
            Reload();
        }

        private bool PickValueFor(FileTypeRule rule)
        {
            if (mode == Mode.Icons)
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = "Choose icon for " + rule.Extension;
                    ofd.Filter = "Icons and images (*.ico;*.png;*.jpg;*.bmp)|*.ico;*.png;*.jpg;*.jpeg;*.bmp|Programs for icon (*.exe)|*.exe|All files (*.*)|*.*";
                    if (ofd.ShowDialog(this) != DialogResult.OK) return false;
                    rule.IconPath = MainForm.ConsolidateIconFile(ofd.FileName);
                    return true;
                }
            }
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Choose program for " + rule.Extension;
                ofd.Filter = "Programs (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All files (*.*)|*.*";
                if (ofd.ShowDialog(this) != DialogResult.OK) return false;
                rule.OpenWith = ofd.FileName;
                return true;
            }
        }

        private void ClearSelected()
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= work.Count) return;
            var r = work[list.SelectedIndex];
            if (mode == Mode.Icons) r.IconPath = null;
            else r.OpenWith = null;
            if (IsEmpty(r)) work.Remove(r);
            Reload();
        }

        // ---------- import / export ----------

        private void ImportRules()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Import file type rules";
                ofd.Filter = "WinPanel rules (*.xml)|*.xml|All files (*.*)|*.*";
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                var imported = FileTypes.ImportFrom(ofd.FileName);
                if (imported == null)
                {
                    MessageBox.Show(this, "Could not read the rules file.", "Import");
                    return;
                }
                foreach (var r in imported) MergeIntoWork(r);
                Reload();
            }
        }

        private void ExportRules()
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = "Export file type rules";
                sfd.Filter = "WinPanel rules (*.xml)|*.xml|All files (*.*)|*.*";
                sfd.FileName = "winpanel-filetypes.xml";
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                FileTypes.ExportTo(sfd.FileName, work);
            }
        }

        private void MergeIntoWork(FileTypeRule r)
        {
            var existing = FindInWork(r.Extension);
            if (existing == null)
            {
                work.Add(new FileTypeRule
                {
                    Extension = r.Extension,
                    IconPath = r.IconPath,
                    OpenWith = r.OpenWith,
                    OpenArgs = r.OpenArgs
                });
                return;
            }
            if (!string.IsNullOrEmpty(r.IconPath)) existing.IconPath = r.IconPath;
            if (!string.IsNullOrEmpty(r.OpenWith)) existing.OpenWith = r.OpenWith;
            if (!string.IsNullOrEmpty(r.OpenArgs)) existing.OpenArgs = r.OpenArgs;
        }
    }
}
