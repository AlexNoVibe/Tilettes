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
        private Color bgColor, panelColor, hoverColor, textColor, dimColor, accentColor, listColor, consoleBg;

        // navigation state
        private string currentPath = "";
        private readonly List<string> history = new List<string>();
        private int historyIndex = -1;

        // controls
        private Label titleLbl, statusLbl;
        private Button btnBack, btnFwd, btnUp, btnRefresh, btnEditPath, btnBmAdd;
        private Button btnConsoleWin, btnConsoleRestart, btnConsoleClear, btnSaveCmd, btnRunCmd;
        private Panel crumbHost;
        private FlowLayoutPanel crumbFlow;
        private TextBox pathEdit, consoleIn;
        private ListBox bookmarksList, fileList;
        private RichTextBox consoleOut;

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
        private readonly HashSet<string> expandedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private class Row { public ExplorerBookmark Bm; public int Depth; }
        private readonly List<Row> bmRows = new List<Row>();

        // console
        private Process shell;
        private StreamWriter shellIn;
        private readonly List<string> cmdHistory = new List<string>();
        private int cmdHistoryPos = 0;
        private const string Sentinel = "__WPMARK__";

        public MiniExplorerForm(string startPath, Settings st)
        {
            settings = st != null ? st : new Settings();
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
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = true;
            this.KeyPreview = true;
            this.ClientSize = new Size(940, 640);
            this.BackColor = bgColor;
            this.ForeColor = textColor;
            this.Font = Settings.MakeFont(settings.FontUiName, settings.FontUiSize);
            this.DoubleBuffered = true;
            this.KeyDown += Mini_KeyDown;
            boldFont = new Font(this.Font, FontStyle.Bold);
            tip = new ToolTip();

            // ---------- title bar ----------
            var titleBar = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = panelColor };
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
            btnBack = NavButton("←", 8);
            btnFwd = NavButton("→", 46);
            btnUp = NavButton("↑", 84);
            btnRefresh = NavButton("↻", 122);
            btnBack.Click += (s, e) => GoBack();
            btnFwd.Click += (s, e) => GoForward();
            btnUp.Click += (s, e) => GoUp();
            btnRefresh.Click += (s, e) => LoadDir();
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

            btnEditPath = FlatButton("Edit", 762, 32, 44, 26);
            btnEditPath.Click += (s, e) => BeginPathEdit();
            this.Controls.Add(btnEditPath);

            statusLbl = new Label { Left = 812, Top = 37, Width = 120, Height = 18, ForeColor = dimColor, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true };
            this.Controls.Add(statusLbl);

            // ---------- bookmarks panel ----------
            var bmHeader = new Label { Text = "BOOKMARKS", Left = 10, Top = 70, Width = 150, Height = 16, ForeColor = dimColor };
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
                ItemHeight = 24,
                IntegralHeight = false
            };
            fileList.DrawItem += FileList_DrawItem;
            fileList.DoubleClick += (s, e) => OpenSelectedEntry();
            fileList.KeyDown += FileList_KeyDown;
            fileList.MouseDown += FileList_MouseDown;
            fileList.MouseMove += FileList_MouseMove;
            fileList.MouseLeave += (s, e) => { if (hoverFile != -1) { hoverFile = -1; fileList.Invalidate(); } };
            this.Controls.Add(fileList);

            // ---------- console ----------
            var lblConsole = new Label { Text = "Console", Left = 10, Top = 440, Width = 120, ForeColor = textColor };
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
            this.Controls.Add(consoleOut);

            var promptLbl = new Label { Text = "›", Left = 8, Top = 594, Width = 16, ForeColor = accentColor };
            this.Controls.Add(promptLbl);
            consoleIn = new TextBox { Left = 26, Top = 590, Width = 772, Height = 24, BackColor = listColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            consoleIn.KeyDown += ConsoleIn_KeyDown;
            this.Controls.Add(consoleIn);
            btnSaveCmd = FlatButton("+ Save", 806, 590, 64, 24);
            btnSaveCmd.Click += (s, e) => SaveCommandBookmark(consoleIn.Text);
            this.Controls.Add(btnSaveCmd);
            btnRunCmd = FlatButton("Run", 876, 590, 56, 24);
            btnRunCmd.Click += (s, e) => RunConsoleInput();
            this.Controls.Add(btnRunCmd);

            var hint = new Label
            {
                Text = "Ctrl+L or Edit - edit path · F5 - refresh · Backspace - up · Enter - open · double-click - open",
                Left = 8,
                Top = 618,
                Width = 924,
                Height = 16,
                ForeColor = dimColor
            };
            this.Controls.Add(hint);

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
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));

            Navigate(startPath);
            if (currentPath.Length == 0)
            {
                try { Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); } catch { }
            }
            if (currentPath.Length == 0) Navigate("C:\\");
        }

        private Font ConsoleFont()
        {
            try { return new Font("Consolas", 8.5f); }
            catch { }
            try { return new Font("Courier New", 9f); }
            catch { }
            return new Font(FontFamily.GenericMonospace, 9f);
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
                Height = 22,
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

            string stat = "folders: " + dirs + " · files: " + files;
            if (truncated) stat += " · first 800";
            if (denied) stat += " · access denied";
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
            int i = fileList.IndexFromPoint(e.Location);
            var m = new ContextMenu();
            if (i >= 0 && i < entries.Count && i < fileList.Items.Count)
            {
                var en = entries[i];
                fileList.SelectedIndex = i;
                m.MenuItems.Add("Open", (s2, e2) => OpenEntry(en));
                if (en.IsDir)
                {
                    m.MenuItems.Add("Add to bookmarks", (s2, e2) => AddFolderBookmark(en.FullPath, en.Name));
                    m.MenuItems.Add("Open in Explorer", (s2, e2) => OpenInExplorer(en.FullPath, false));
                }
                else
                {
                    m.MenuItems.Add("Show in Explorer", (s2, e2) => OpenInExplorer(en.FullPath, true));
                }
                m.MenuItems.Add("Copy path", (s2, e2) => CopyText(en.FullPath));
            }
            else
            {
                m.MenuItems.Add("Refresh", (s2, e2) => LoadDir());
                m.MenuItems.Add("Copy folder path", (s2, e2) => CopyText(currentPath));
                m.MenuItems.Add("Open in Explorer", (s2, e2) => OpenInExplorer(currentPath, false));
                m.MenuItems.Add("Add current folder to bookmarks", (s2, e2) => AddFolderBookmark(currentPath, FolderNameOf(currentPath)));
                m.MenuItems.Add("Open console window here", (s2, e2) => OpenRealConsole());
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
                TextRenderer.DrawText(g, b.Name, this.Font, new Point(x + 14, ty), textColor);
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
                m.MenuItems.Add("Rename...", (s2, e2) => RenameBookmark(b));
                m.MenuItems.Add("Remove", (s2, e2) => RemoveBookmark(b));
            }
            m.MenuItems.Add("Add current folder", (s2, e2) => AddFolderBookmark(currentPath, FolderNameOf(currentPath)));
            m.MenuItems.Add("Add group...", (s2, e2) => AddGroup());
            m.Show(bookmarksList, e.Location);
        }

        private void ShowBookmarksAddMenu()
        {
            var m = new ContextMenu();
            m.MenuItems.Add("Add current folder", (s2, e2) => AddFolderBookmark(currentPath, FolderNameOf(currentPath)));
            m.MenuItems.Add("Add group...", (s2, e2) => AddGroup());
            m.Show(btnBmAdd, new Point(0, btnBmAdd.Height));
        }

        private void AddGroup()
        {
            string name = Prompt.ShowDialog("Group name:", "Add bookmark group", "");
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
            string name = Prompt.ShowDialog("Bookmark name:", "Add folder bookmark", defaultName);
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
            string name = Prompt.ShowDialog("New name:", "Rename bookmark", b.Name);
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

        private void RemoveBookmark(ExplorerBookmark b)
        {
            int kids = b.Children != null ? b.Children.Count : 0;
            string msg = (b.Kind == "group" && kids > 0)
                ? "Remove group \"" + b.Name + "\" with " + kids + " entries?"
                : "Remove \"" + b.Name + "\"?";
            if (MessageBox.Show(this, msg, "Bookmarks", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
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
            if (cmd == null) return;
            cmd = cmd.Trim();
            if (cmd.Length == 0) return;
            string name = Prompt.ShowDialog("Command name:", "Save command", cmd);
            if (string.IsNullOrWhiteSpace(name)) return;
            string group = Prompt.ShowDialog("Group name (empty = top level):", "Save command", "");
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
            nb.Name = name.Trim();
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
                shell.Exited += (s, e) => Ui(delegate { AppendConsole("[console process exited - press Restart]", dimColor); });

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
            try
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    string l = line;
                    if (l.IndexOf(Sentinel, StringComparison.Ordinal) >= 0)
                    {
                        Ui(delegate { AppendConsole(PromptText(), accentColor); });
                        continue;
                    }
                    Ui(delegate { AppendConsole(l, textColor); });
                }
            }
            catch { }
        }

        private void SendCmd(string text)
        {
            try { if (shellIn != null) shellIn.WriteLine(text); } catch { }
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
    }
}
