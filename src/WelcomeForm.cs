using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinPanel
{
    // First-start welcome window: thanks + beta note + issue link, a painted
    // "drag a shortcut -> a tile appears" mini-diagram, language choice via
    // painted flags (SMP emoji glyphs render as tofu in GDI), the update-check
    // permission, a support-the-author block (one link to the GitHub donate
    // section), and the two exit buttons (plain close / close + generate
    // example tiles).
    public class WelcomeForm : Form
    {
        private readonly Settings settings;
        public bool CreateExamples { get; private set; }
        // The "close & sync user Start" answer: the panel copies the user's
        // pinned Start tiles into the "User Start" tab right after closing.
        public bool SyncUserStart { get; private set; }

        private readonly Color bgColor, panelColor, hoverColor, textColor, dimColor;
        private readonly Font mainFont, titleFont, smallFont;
        private readonly ToolTip tips = new ToolTip();

        private Label lblGreet, lblNote, lblIssuePrompt, lblHow, lblEditHint, lblGroupHint, lblLang, lblSupport;
        private LinkLabel linkIssues, linkDonate;
        private CheckBox chkUpdates;
        private Button btnClose, btnExamples, btnSyncUser;
        private readonly List<FlagButton> flags = new List<FlagButton>();
        private Panel ill, flagRow;
        private int winW;

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        public WelcomeForm(Settings settings)
        {
            this.settings = settings;

            // UiPalette: follows the active skin, falls back to the classic
            // light/dark colors without one (used to be hard-coded classic).
            bgColor = UiPalette.Bg;
            panelColor = UiPalette.Panel;
            hoverColor = UiPalette.Hover;
            textColor = UiPalette.Text;
            dimColor = UiPalette.Dim;

            mainFont = Settings.MakeFont(this.settings.FontUiName, this.settings.FontUiSize);
            titleFont = Settings.MakeFont(this.settings.FontUiName, Math.Max(6, this.settings.FontUiSize + 2), FontStyle.Bold);
            smallFont = Settings.MakeFont(this.settings.FontUiName, Math.Max(7, this.settings.FontUiSize - 1));
            this.Font = mainFont;

            this.Text = "Tilettes";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.BackColor = bgColor;
            this.ForeColor = textColor;
            this.AutoScroll = true;
            this.DoubleBuffered = true;
            this.AcceptButton = null;
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { CreateExamples = false; this.Close(); } };

            BuildLayout();

            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));
            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(120, textColor)))
                    e.Graphics.DrawPath(pen, RoundRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 15));
            };
            this.Resize += (s, e) =>
            {
                try { this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15)); } catch { }
            };
        }

        private void BuildLayout()
        {
            int lh = mainFont.Height;          // one line of body text
            int th = titleFont.Height;         // heading line
            int w = Math.Min(580, Screen.PrimaryScreen.WorkingArea.Width - 40);
            winW = w;
            int x = 22, cw = w - 44;

            // ---- Title bar: painted logo, name + version, close button ----
            var titleBar = new Panel { Left = 0, Top = 0, Width = w, Height = 44, BackColor = panelColor };
            titleBar.Paint += (s, e) => PaintLogo(e.Graphics, new Rectangle(14, 10, 24, 24));
            var titleLbl = new Label
            {
                Text = Loc.S("Tilettes", "Плиточки · Tilettes") + "  v" + AppInfo.AppVersion,
                Left = 46,
                Top = (44 - titleFont.Height) / 2,
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = textColor,
                Font = titleFont
            };
            titleBar.Controls.Add(titleLbl);
            var closeX = new Button
            {
                Text = "✕",
                Width = 40,
                Height = 44,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10f)
            };
            closeX.FlatAppearance.BorderSize = 0;
            closeX.FlatAppearance.MouseOverBackColor = Color.Red;
            closeX.Click += (s, e) => { CreateExamples = false; this.Close(); };
            titleBar.Controls.Add(closeX);
            titleBar.MouseDown += TitleBarDrag;
            titleLbl.MouseDown += TitleBarDrag;
            this.Controls.Add(titleBar);

            int y = 44 + 12;

            lblGreet = AddLabel(x, ref y, cw, th + 4, titleFont);
            lblNote = AddLabel(x, ref y, cw, lh + 4, mainFont);
            y += 4;
            lblIssuePrompt = AddLabel(x, ref y, cw, lh + 2, mainFont);
            linkIssues = AddLink(x, ref y, cw, lh + 4, "github.com/AlexNoVibe/Tilettes/issues", AppInfo.IssuesUrl);
            y += 6;

            // Painted mini-diagram: no binary assets, so the release stays small.
            ill = new Panel { Left = x, Top = y, Width = cw, Height = 100, BackColor = bgColor };
            ill.Paint += (s, e) => DrawIllustration(e.Graphics, ill.ClientRectangle);
            this.Controls.Add(ill);
            y += 106;

            lblHow = AddLabel(x, ref y, cw, lh + 2, mainFont);
            // The corner checkmark is the single most asked "how do I add tiles"
            // question - spell out what it toggles right under the how-to line.
            lblEditHint = AddLabel(x, ref y, cw, mainFont.Height + 2, mainFont);
            lblEditHint.ForeColor = dimColor;
            // Groups answer the second most asked question: dragging a group
            // does nothing outside the red multi-select state.
            lblGroupHint = AddLabel(x, ref y, cw, mainFont.Height + 2, mainFont);
            lblGroupHint.ForeColor = dimColor;
            y += 10;

            // ---- Language: painted flags (one per supported language, 5 x 2) ----
            lblLang = AddLabel(x, ref y, cw, lh + 2, mainFont);
            flagRow = new Panel { Left = x, Top = y, Width = cw, Height = 74, BackColor = bgColor };
            int fi = 0;
            foreach (var code in Loc.Languages)
            {
                var fb = new FlagButton(code)
                {
                    Left = (fi % 5) * (52 + 12),
                    Top = (fi / 5) * (30 + 12)
                };
                fb.Click += (s, e) => SelectLanguage(code);
                tips.SetToolTip(fb, Loc.NativeName(code));
                flagRow.Controls.Add(fb);
                flags.Add(fb);
                fi++;
            }
            this.Controls.Add(flagRow);
            y += 80;

            chkUpdates = new CheckBox
            {
                Left = x,
                Top = y,
                Width = cw,
                // Explicit height: the auto height clips larger UI fonts.
                Height = mainFont.Height + 10,
                Checked = settings.UpdateCheckEnabled,
                ForeColor = textColor,
                BackColor = bgColor,
                Font = mainFont
            };
            tips.SetToolTip(chkUpdates, Loc.S("The app periodically asks GitHub Releases for a newer version (no auto-download yet)",
                "Приложение периодически спрашивает GitHub Releases о новой версии (автозагрузки пока нет)"));
            chkUpdates.CheckedChanged += (s, e) => { settings.UpdateCheckEnabled = chkUpdates.Checked; };
            this.Controls.Add(chkUpdates);
            y += mainFont.Height + 20;

            // ---- Support the author: one link to the GitHub donate section ----
            lblSupport = AddLabel(x, ref y, cw, th + 2, titleFont);
            linkDonate = AddLink(x, ref y, cw, lh + 4, Loc.S("Support section on GitHub (README)", "Раздел поддержки на GitHub (README)"), AppInfo.DonateUrl);
            y += 12;

            // ---- Exit buttons ----
            int bh = Math.Max(32, lh + 8);
            btnClose = new Button
            {
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Height = bh,
                Font = mainFont
            };
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.FlatAppearance.BorderColor = panelColor;
            btnClose.FlatAppearance.MouseOverBackColor = hoverColor;
            btnClose.Click += (s, e) => { CreateExamples = false; this.Close(); };

            btnExamples = new Button
            {
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Height = bh,
                Font = mainFont
            };
            btnExamples.FlatAppearance.BorderSize = 0;
            btnExamples.FlatAppearance.MouseOverBackColor = hoverColor;
            btnExamples.Click += (s, e) => { CreateExamples = true; this.Close(); };

            btnSyncUser = new Button
            {
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = textColor,
                Cursor = Cursors.Hand,
                Height = bh,
                Font = mainFont
            };
            btnSyncUser.FlatAppearance.BorderSize = 1;
            btnSyncUser.FlatAppearance.BorderColor = panelColor;
            btnSyncUser.FlatAppearance.MouseOverBackColor = hoverColor;
            btnSyncUser.Click += (s, e) => { SyncUserStart = true; this.Close(); };

            this.Controls.Add(btnClose);
            this.Controls.Add(btnExamples);
            this.Controls.Add(btnSyncUser);

            ApplyLanguage(); // sets all captions, then flows the whole layout
            this.MinimumSize = new Size(420, 300);
            this.MaximumSize = new Size(int.MaxValue, Screen.PrimaryScreen.WorkingArea.Height - 40);
        }

        // Vertical layout pass. Every text is measured with word wrap because
        // localizations (and larger UI fonts) often need more than one line -
        // the fixed one-line heights clipped the ends of the longest strings.
        // Runs on every language switch (captions change live).
        private void LayoutContents()
        {
            int lh = mainFont.Height;          // one line of body text
            int w = winW, x = 22, cw = w - 44;
            int y = 44 + 12;

            y = FlowLabel(lblGreet, x, y, cw, titleFont, 4);
            y = FlowLabel(lblNote, x, y, cw, mainFont, 4);
            y += 4;
            y = FlowLabel(lblIssuePrompt, x, y, cw, mainFont, 2);
            y = FlowLabel(linkIssues, x, y, cw, mainFont, 4);
            y += 6;

            ill.Left = x; ill.Top = y;
            y += ill.Height + 6;

            y = FlowLabel(lblHow, x, y, cw, mainFont, 2);
            y = FlowLabel(lblEditHint, x, y, cw, mainFont, 2);
            y = FlowLabel(lblGroupHint, x, y, cw, mainFont, 2);
            y += 10;

            y = FlowLabel(lblLang, x, y, cw, mainFont, 2);
            flagRow.Left = x; flagRow.Top = y;
            y += flagRow.Height + 6;

            // The text sits next to the box glyph, so measure it narrower.
            int chkH = Math.Max(lh + 10, WrapHeight(chkUpdates.Text, mainFont, cw - 24) + 8);
            chkUpdates.SetBounds(x, y, cw, chkH);
            y += chkH + 10;

            y = FlowLabel(lblSupport, x, y, cw, titleFont, 2);
            y = FlowLabel(linkDonate, x, y, cw, mainFont, 4);
            y += 12;

            // Exit buttons: re-measured per language (caption widths differ).
            int bh = Math.Max(32, lh + 8);
            btnClose.Height = bh;
            btnExamples.Height = bh;
            btnSyncUser.Height = bh;
            btnClose.Width = TextRenderer.MeasureText(btnClose.Text, mainFont).Width + 30;
            btnExamples.Width = TextRenderer.MeasureText(btnExamples.Text, mainFont).Width + 30;
            btnSyncUser.Width = TextRenderer.MeasureText(btnSyncUser.Text, mainFont).Width + 30;
            btnClose.Location = new Point(w - 22 - btnClose.Width, y);
            btnExamples.Location = new Point(btnClose.Left - 12 - btnExamples.Width, y);
            // The sync button gets its own row below the exit pair: the three
            // captions together are wider than the window in several languages.
            btnSyncUser.Location = new Point(w - 22 - btnSyncUser.Width, y + bh + 8);
            y += bh + 8 + bh + 16;

            this.ClientSize = new Size(w, y);
        }

        // Positions a flowing control, gives it the measured wrapped height
        // plus padding, and returns the next y.
        private static int FlowLabel(Control c, int x, int y, int width, Font font, int pad)
        {
            c.Left = x; c.Top = y; c.Width = width;
            c.Height = WrapHeight(c.Text, font, width) + pad;
            return y + c.Height;
        }

        private static int WrapHeight(string text, Font font, int width)
        {
            if (string.IsNullOrEmpty(text)) return font.Height;
            return TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height;
        }

        // Adds a themed label; returns it (text is filled by ApplyLanguage).
        private Label AddLabel(int x, ref int y, int width, int height, Font font)
        {
            var lbl = new Label
            {
                Left = x,
                Top = y,
                Width = width,
                Height = height,
                BackColor = bgColor,
                ForeColor = textColor,
                Font = font
            };
            this.Controls.Add(lbl);
            y += height;
            return lbl;
        }

        private LinkLabel AddLink(int x, ref int y, int width, int height, string caption, string url)
        {
            var link = new LinkLabel
            {
                Left = x,
                Top = y,
                Width = width,
                Height = height,
                BackColor = bgColor,
                LinkColor = Color.FromArgb(86, 156, 214),
                ActiveLinkColor = Color.FromArgb(120, 180, 240),
                LinkBehavior = LinkBehavior.HoverUnderline,
                Font = mainFont,
                AutoSize = false
            };
            link.Text = caption;
            link.Links.Add(0, caption.Length, url);
            link.LinkClicked += (s, e) => OpenUrl(e.Link.LinkData as string);
            this.Controls.Add(link);
            y += height;
            return link;
        }

        private void OpenUrl(string url)
        {
            try { Process.Start(url); }
            catch { }
        }

        private void TitleBarDrag(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        private void SelectLanguage(string code)
        {
            settings.Language = code;
            Loc.Lang = code;
            foreach (var f in flags) { f.Selected = f.Code == code; f.Invalidate(); }
            ApplyLanguage();
        }

        // Live (re)translation of every caption — used both on build and when
        // the user flips a flag in the window.
        private void ApplyLanguage()
        {
            lblGreet.Text = Loc.S("Thanks for trying Tilettes!", "Спасибо, что решили попробовать Плиточки!");
            lblNote.Text = Loc.S("Bugs and rough edges are possible.", "Возможны баги и недоделки.");
            lblIssuePrompt.Text = Loc.S("Found a bug or have an idea? Create an issue on GitHub:", "Нашли баг или есть идея? Создайте issue на GitHub:");
            lblHow.Text = Loc.S("Just drag a shortcut or a file onto the panel - it becomes a tile.",
                "Просто перетащите ярлык или файл на панель — появится плитка.");
            lblEditHint.Text = Loc.S("✅ - the corner checkmark enables adding and editing tiles",
                "✅ — галочка в углу панели включает добавление и редактирование плиток");
            lblGroupHint.Text = Loc.S("The red checkmark (a second click on the corner checkmark) enables groups: they can only be moved and resized in that mode",
                "Красная галочка (второй клик по галочке в углу) включает группы: двигать и менять их размер можно только в этом режиме");
            lblLang.Text = Loc.S("Language / Язык:", "Язык / Language:");
            chkUpdates.Text = Loc.S("Check for updates automatically", "Проверять обновления автоматически");
            lblSupport.Text = Loc.S("Support the author", "Поддержать автора");
            btnClose.Text = Loc.S("Close", "Закрыть");
            btnExamples.Text = Loc.S("Close & create example tiles", "Закрыть и создать примеры");
            btnSyncUser.Text = Loc.S("Close & sync user Start", "Закрыть и синхронизировать пользовательский Пуск");
            LayoutContents();
        }

        // ---- Painted graphics (logo, flags, diagram) — zero file-size cost ----

        private static readonly Color[] LogoColors =
        {
            Color.FromArgb(46, 204, 113),   // mint
            Color.FromArgb(241, 196, 15),   // amber
            Color.FromArgb(231, 76, 60),    // coral
            Color.FromArgb(52, 152, 219)    // blue
        };

        private static void PaintLogo(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int gap = 2, cell = (r.Width - gap) / 2;
            for (int i = 0; i < 4; i++)
            {
                var c = new Rectangle(r.X + (i % 2) * (cell + gap), r.Y + (i / 2) * (cell + gap), cell, cell);
                using (var path = RoundRectPath(c, 4))
                using (var b = new SolidBrush(LogoColors[i]))
                    g.FillPath(b, path);
            }
        }

        // Mini-diagram: three source cards (a folder - amber, an .exe - green,
        // a .lnk - neutral) -> arrow -> grid with three tiles + a ghost cell
        // where the new tile lands (small cursor included). Anything you can
        // drag becomes a tile, not just shortcuts.
        private void DrawIllustration(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = r.Y + r.Height / 2;

            int cardW = 56, cardH = 26, cardGap = 7;
            int cardsH = cardH * 3 + cardGap * 2;
            int top = cy - cardsH / 2;
            var folder = new Rectangle(r.Left + 10, top, cardW, cardH);
            var exe = new Rectangle(r.Left + 10, top + cardH + cardGap, cardW, cardH);
            var lnk = new Rectangle(r.Left + 10, top + (cardH + cardGap) * 2, cardW, cardH);

            using (var amber = new SolidBrush(Color.FromArgb(241, 196, 15)))
            using (var path = RoundRectPath(folder, 6))
                g.FillPath(amber, path);
            DrawFolderGlyph(g, folder);

            using (var green = new SolidBrush(Color.FromArgb(46, 204, 113)))
            using (var path = RoundRectPath(exe, 6))
                g.FillPath(green, path);
            using (var f = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var white = new SolidBrush(Color.White))
            {
                var sz = TextRenderer.MeasureText(".exe", f);
                g.DrawString(".exe", f, white, exe.Left + (exe.Width - sz.Width) / 2f, exe.Top + (exe.Height - sz.Height) / 2f);
            }

            using (var back = new SolidBrush(panelColor))
            using (var pen = new Pen(dimColor, 1f))
            using (var path = RoundRectPath(lnk, 6))
            {
                g.FillPath(back, path);
                g.DrawPath(pen, path);
            }
            using (var f = new Font("Segoe UI", 8.5f))
            using (var tb = new SolidBrush(textColor))
            {
                var sz = TextRenderer.MeasureText(".lnk", f);
                g.DrawString(".lnk", f, tb, lnk.Left + (lnk.Width - sz.Width) / 2f, lnk.Top + (lnk.Height - sz.Height) / 2f);
            }

            int cell = 38, gap = 4;
            int gx = r.Right - 14 - (cell * 2 + gap), gy = cy - (cell * 2 + gap) / 2;
            using (var pen = new Pen(dimColor, 2f) { CustomEndCap = new AdjustableArrowCap(6, 7) })
                g.DrawLine(pen, r.Left + 10 + cardW + 10, cy, gx - 10, cy);

            for (int i = 0; i < 3; i++)
            {
                var c = new Rectangle(gx + (i % 2) * (cell + gap), gy + (i / 2) * (cell + gap), cell, cell);
                using (var path = RoundRectPath(c, 7))
                using (var b = new SolidBrush(LogoColors[i]))
                    g.FillPath(b, path);
            }
            var ghost = new Rectangle(gx + cell + gap, gy + cell + gap, cell, cell);
            using (var path = RoundRectPath(ghost, 7))
            using (var dash = new Pen(dimColor, 1.5f) { DashStyle = DashStyle.Dash })
                g.DrawPath(dash, path);
            using (var pf = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var tb = new SolidBrush(textColor))
            {
                var sz = TextRenderer.MeasureText("+", pf);
                g.DrawString("+", pf, tb, ghost.Left + (ghost.Width - sz.Width) / 2f, ghost.Top + (ghost.Height - sz.Height) / 2f);
            }
            DrawCursor(g, ghost.Left - 5, ghost.Top - 4);
        }

        // Folder pictogram on the amber source card: tab + body silhouette.
        private static void DrawFolderGlyph(Graphics g, Rectangle card)
        {
            int w = 17, h = 11;
            var body = new Rectangle(card.Left + (card.Width - w) / 2, card.Top + (card.Height - h) / 2 + 2, w, h);
            var tab = new Rectangle(body.X, body.Y - 3, 7, 4);
            using (var b = new SolidBrush(Color.FromArgb(93, 64, 55)))
            {
                g.FillRectangle(b, tab);
                using (var path = RoundRectPath(body, 2)) g.FillPath(b, path);
            }
        }

        private static void DrawCursor(Graphics g, int x, int y)
        {
            var pts = new[]
            {
                new Point(0, 0), new Point(0, 13), new Point(3, 10), new Point(5, 14),
                new Point(7, 13), new Point(5, 9), new Point(9, 9)
            };
            var moved = Array.ConvertAll(pts, p => new Point(p.X + x, p.Y + y));
            using (var wb = new SolidBrush(Color.White)) g.FillPolygon(wb, moved);
            using (var bp = new Pen(Color.Black)) g.DrawPolygon(bp, moved);
        }

        internal static GraphicsPath RoundRectPath(Rectangle b, int rad)
        {
            var path = new GraphicsPath();
            int d = rad * 2;
            if (b.Width < d || b.Height < d) { path.AddRectangle(b); return path; }
            var arc = new Rectangle(b.Location, new Size(d, d));
            path.AddArc(arc, 180, 90);
            arc.X = b.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = b.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = b.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Small painted flag button (RU tricolor / UK flag). No emoji: SMP
        // glyphs render as tofu in GDI buttons.
        private class FlagButton : Control
        {
            public readonly string Code;
            public bool Selected;

            public FlagButton(string code)
            {
                Code = code;
                Width = 52;
                Height = 30;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = WelcomeForm.RoundRectPath(r, 6))
                {
                    g.SetClip(path);
                    switch (Code)
                    {
                        case "ru":
                            {
                                int bh = Height / 3;
                                using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, 0, 0, Width, bh);
                                using (var b = new SolidBrush(Color.FromArgb(0, 57, 166))) g.FillRectangle(b, 0, bh, Width, bh);
                                using (var b = new SolidBrush(Color.FromArgb(213, 43, 30))) g.FillRectangle(b, 0, 2 * bh, Width, Height - 2 * bh);
                                break;
                            }
                        case "de":
                            {
                                int bh = Height / 3;
                                using (var b = new SolidBrush(Color.FromArgb(20, 20, 20))) g.FillRectangle(b, 0, 0, Width, bh);
                                using (var b = new SolidBrush(Color.FromArgb(221, 0, 0))) g.FillRectangle(b, 0, bh, Width, bh);
                                using (var b = new SolidBrush(Color.FromArgb(255, 206, 0))) g.FillRectangle(b, 0, 2 * bh, Width, Height - 2 * bh);
                                break;
                            }
                        case "fr":
                            {
                                int bw = Width / 3;
                                using (var b = new SolidBrush(Color.FromArgb(0, 85, 164))) g.FillRectangle(b, 0, 0, bw, Height);
                                using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, bw, 0, bw, Height);
                                using (var b = new SolidBrush(Color.FromArgb(239, 65, 53))) g.FillRectangle(b, 2 * bw, 0, Width - 2 * bw, Height);
                                break;
                            }
                        case "it":
                            {
                                int bw = Width / 3;
                                using (var b = new SolidBrush(Color.FromArgb(0, 146, 70))) g.FillRectangle(b, 0, 0, bw, Height);
                                using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, bw, 0, bw, Height);
                                using (var b = new SolidBrush(Color.FromArgb(205, 33, 42))) g.FillRectangle(b, 2 * bw, 0, Width - 2 * bw, Height);
                                break;
                            }
                        case "es":
                            {
                                // red / yellow (double height) / red
                                int bh = Height / 4;
                                using (var b = new SolidBrush(Color.FromArgb(170, 21, 27))) g.FillRectangle(b, 0, 0, Width, bh);
                                using (var b = new SolidBrush(Color.FromArgb(255, 196, 0))) g.FillRectangle(b, 0, bh, Width, 2 * bh);
                                using (var b = new SolidBrush(Color.FromArgb(170, 21, 27))) g.FillRectangle(b, 0, 3 * bh, Width, Height - 3 * bh);
                                break;
                            }
                        case "pt":
                            {
                                // green field, yellow diamond, blue globe (simplified)
                                int gw = Width * 2 / 5;
                                using (var b = new SolidBrush(Color.FromArgb(0, 102, 0))) g.FillRectangle(b, 0, 0, gw, Height);
                                using (var b = new SolidBrush(Color.FromArgb(239, 65, 53))) g.FillRectangle(b, gw, 0, Width - gw, Height);
                                var pts = new[] { new Point(Width / 2, 3), new Point(Width - 8, Height / 2), new Point(Width / 2, Height - 3), new Point(8, Height / 2) };
                                using (var b = new SolidBrush(Color.FromArgb(255, 223, 0))) g.FillPolygon(b, pts);
                                using (var b = new SolidBrush(Color.FromArgb(0, 39, 118)))
                                    g.FillEllipse(b, Width / 2 - 6, Height / 2 - 6, 12, 12);
                                break;
                            }
                        case "pl":
                            {
                                int bh = Height / 2;
                                using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, 0, 0, Width, bh);
                                using (var b = new SolidBrush(Color.FromArgb(220, 55, 75))) g.FillRectangle(b, 0, bh, Width, Height - bh);
                                break;
                            }
                        case "zh":
                            {
                                using (var b = new SolidBrush(Color.FromArgb(222, 30, 38))) g.FillRectangle(b, r);
                                using (var b = new SolidBrush(Color.FromArgb(255, 222, 0)))
                                {
                                    DrawStar(g, b, 10, 8, 6);
                                    DrawStar(g, b, 20, 3, 2);
                                    DrawStar(g, b, 23, 7, 2);
                                    DrawStar(g, b, 23, 12, 2);
                                    DrawStar(g, b, 20, 16, 2);
                                }
                                break;
                            }
                        case "ja":
                            {
                                using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, r);
                                using (var b = new SolidBrush(Color.FromArgb(188, 0, 45)))
                                    g.FillEllipse(b, Width / 2 - 7, Height / 2 - 7, 14, 14);
                                break;
                            }
                        default:
                            {
                                // en: Union Jack, simplified: navy field, white+red diagonals,
                                // white+red cross.
                                using (var b = new SolidBrush(Color.FromArgb(0, 36, 125))) g.FillRectangle(b, r);
                                using (var pw = new Pen(Color.White, Height / 5f))
                                {
                                    g.DrawLine(pw, 0, 0, Width, Height);
                                    g.DrawLine(pw, Width, 0, 0, Height);
                                }
                                using (var pr = new Pen(Color.FromArgb(200, 16, 35), Height / 12f))
                                {
                                    g.DrawLine(pr, 0, 0, Width, Height);
                                    g.DrawLine(pr, Width, 0, 0, Height);
                                }
                                using (var pw = new Pen(Color.White, Height / 3f))
                                {
                                    g.DrawLine(pw, Width / 2, 0, Width / 2, Height);
                                    g.DrawLine(pw, 0, Height / 2, Width, Height / 2);
                                }
                                using (var pr = new Pen(Color.FromArgb(200, 16, 35), Height / 5f))
                                {
                                    g.DrawLine(pr, Width / 2, 0, Width / 2, Height);
                                    g.DrawLine(pr, 0, Height / 2, Width, Height / 2);
                                }
                                break;
                            }
                    }
                    g.ResetClip();
                    using (var pen = new Pen(Selected ? Color.FromArgb(46, 204, 113) : Color.FromArgb(130, 130, 135), Selected ? 2f : 1f))
                        g.DrawPath(pen, path);
                }
                base.OnPaint(e);
            }

            // Small 5-point star (used by the zh flag).
            private static void DrawStar(Graphics g, Brush brush, float cx, float cy, float radius)
            {
                var pts = new PointF[10];
                for (int i = 0; i < 10; i++)
                {
                    double ang = -Math.PI / 2 + i * Math.PI / 5;
                    float rad = i % 2 == 0 ? radius : radius * 0.42f;
                    pts[i] = new PointF(cx + (float)(Math.Cos(ang) * rad), cy + (float)(Math.Sin(ang) * rad));
                }
                g.FillPolygon(brush, pts);
            }
        }
    }
}
