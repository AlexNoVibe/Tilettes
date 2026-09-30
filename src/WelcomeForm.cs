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
    // permission, a support/donate block with crypto placeholders, and the two
    // exit buttons (plain close / close + generate example tiles).
    public class WelcomeForm : Form
    {
        private readonly Settings settings;
        public bool CreateExamples { get; private set; }

        private readonly Color bgColor, panelColor, hoverColor, textColor, dimColor;
        private readonly Font mainFont, titleFont, smallFont;
        private readonly ToolTip tips = new ToolTip();

        private Label lblGreet, lblBeta, lblIssuePrompt, lblHow, lblLang, lblSupport, lblCopyHint, lblCopied, lblDonatePrompt;
        private LinkLabel linkIssues, linkDonate;
        private CheckBox chkUpdates;
        private Button btnClose, btnExamples;
        private readonly List<FlagButton> flags = new List<FlagButton>();
        private readonly List<Control> walletRows = new List<Control>();
        private System.Windows.Forms.Timer copyResetTimer;

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        public WelcomeForm(Settings settings)
        {
            this.settings = settings;

            if (MainForm.CurrentSettings != null && MainForm.CurrentSettings.IsLightTheme)
            {
                bgColor = Color.FromArgb(232, 232, 234);
                panelColor = Color.FromArgb(214, 214, 218);
                hoverColor = Color.FromArgb(200, 200, 200);
                textColor = Color.Black;
            }
            else
            {
                bgColor = Color.FromArgb(24, 24, 28);
                panelColor = Color.FromArgb(45, 45, 48);
                hoverColor = Color.FromArgb(62, 62, 66);
                textColor = Color.White;
            }
            dimColor = Color.FromArgb(150, 150, 155);

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
            int x = 22, cw = w - 44;

            // ---- Title bar: painted logo, name + version, close button ----
            var titleBar = new Panel { Left = 0, Top = 0, Width = w, Height = 44, BackColor = panelColor };
            titleBar.Paint += (s, e) => PaintLogo(e.Graphics, new Rectangle(14, 10, 24, 24));
            var titleLbl = new Label
            {
                Text = Loc.S("Плиточки", "Плиточки · Tilettes") + "  v" + AppInfo.AppVersion,
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
            lblBeta = AddLabel(x, ref y, cw, lh + 4, mainFont);
            y += 4;
            lblIssuePrompt = AddLabel(x, ref y, cw, lh + 2, mainFont);
            linkIssues = AddLink(x, ref y, cw, lh + 4, "github.com/AlexNoVibe/Tilettes/issues", AppInfo.IssuesUrl);
            y += 6;

            // Painted mini-diagram: no binary assets, so the release stays small.
            var ill = new Panel { Left = x, Top = y, Width = cw, Height = 100, BackColor = bgColor };
            ill.Paint += (s, e) => DrawIllustration(e.Graphics, ill.ClientRectangle);
            this.Controls.Add(ill);
            y += 106;

            lblHow = AddLabel(x, ref y, cw, lh + 2, mainFont);
            y += 10;

            // ---- Language: painted flags, live re-translation ----
            lblLang = AddLabel(x, ref y, cw, lh + 2, mainFont);
            var flagRow = new Panel { Left = x, Top = y, Width = cw, Height = 38, BackColor = bgColor };
            int fx = 0;
            foreach (var code in new[] { "ru", "en" })
            {
                var fb = new FlagButton(code) { Left = fx, Top = 1 };
                fb.Click += (s, e) => SelectLanguage(code);
                tips.SetToolTip(fb, code == "ru" ? "Русский" : "English");
                flagRow.Controls.Add(fb);
                flags.Add(fb);
                fx += 64;
            }
            this.Controls.Add(flagRow);
            y += 44;

            chkUpdates = new CheckBox
            {
                Left = x,
                Top = y,
                Width = cw,
                Checked = settings.UpdateCheckEnabled,
                ForeColor = textColor,
                BackColor = bgColor,
                Font = mainFont
            };
            tips.SetToolTip(chkUpdates, Loc.S("The app periodically asks GitHub Releases for a newer version (no auto-download yet)",
                "Приложение периодически спрашивает GitHub Releases о новой версии (автозагрузки пока нет)"));
            chkUpdates.CheckedChanged += (s, e) => { settings.UpdateCheckEnabled = chkUpdates.Checked; };
            this.Controls.Add(chkUpdates);
            y += lh + 14;

            // ---- Support the author: real wallets, click to copy ----
            lblSupport = AddLabel(x, ref y, cw, th + 2, titleFont);
            lblCopyHint = AddLabel(x, ref y, cw, smallFont.Height + 2, smallFont);
            lblCopyHint.ForeColor = dimColor;
            foreach (var wlt in DonateWallets.All)
            {
                var wallet = wlt;
                var row = new LinkLabel
                {
                    Left = x,
                    Top = y,
                    Width = cw,
                    Height = smallFont.Height + 4,
                    BackColor = bgColor,
                    LinkColor = textColor,
                    ActiveLinkColor = Color.FromArgb(86, 156, 214),
                    LinkBehavior = LinkBehavior.HoverUnderline,
                    Font = smallFont,
                    AutoSize = false
                };
                row.Text = DonateWallets.Display(wallet);
                row.Links.Add(0, row.Text.Length, wallet);
                row.LinkClicked += (s, e) => CopyWallet(wallet);
                tips.SetToolTip(row, Loc.S("Click to copy the address", "Клик — скопировать адрес"));
                this.Controls.Add(row);
                walletRows.Add(row);
                y += smallFont.Height + 4;
            }
            lblCopied = AddLabel(x, ref y, cw, smallFont.Height + 2, smallFont);
            lblCopied.ForeColor = Color.FromArgb(46, 204, 113);
            y += 4;
            lblDonatePrompt = AddLabel(x, ref y, cw, lh + 2, mainFont);
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

            ApplyLanguage(); // sets all captions incl. button texts

            btnClose.Width = TextRenderer.MeasureText(btnClose.Text, mainFont).Width + 30;
            btnExamples.Width = TextRenderer.MeasureText(btnExamples.Text, mainFont).Width + 30;
            btnClose.Location = new Point(w - 22 - btnClose.Width, y);
            btnExamples.Location = new Point(btnClose.Left - 12 - btnExamples.Width, y);
            this.Controls.Add(btnClose);
            this.Controls.Add(btnExamples);
            y += bh + 16;

            this.ClientSize = new Size(w, y);
            this.MinimumSize = new Size(420, 300);
            this.MaximumSize = new Size(int.MaxValue, Screen.PrimaryScreen.WorkingArea.Height - 40);
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

        // Copies the wallet address and flashes a short confirmation line.
        private void CopyWallet(DonateWallets.Wallet wallet)
        {
            try { Clipboard.SetText(wallet.Address); } catch { }
            lblCopied.Text = wallet.Label + ": " + Loc.S("address copied to clipboard", "адрес скопирован в буфер");
            if (copyResetTimer == null)
            {
                copyResetTimer = new System.Windows.Forms.Timer { Interval = 2500 };
                copyResetTimer.Tick += (s, e) => { copyResetTimer.Stop(); lblCopied.Text = ""; };
            }
            copyResetTimer.Start();
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
        // the user flips a flag in the window. Wallet rows are language-neutral
        // (coin names and addresses) and are not touched here.
        private void ApplyLanguage()
        {
            lblGreet.Text = Loc.S("Thanks for trying Tilettes!", "Спасибо, что решили попробовать Плиточки!");
            lblBeta.Text = Loc.S("This is a beta version - bugs and rough edges are possible.",
                "Это бета-версия — возможны баги и недоделки.");
            lblIssuePrompt.Text = Loc.S("Found a bug or have an idea? Create an issue on GitHub:", "Нашли баг или есть идея? Создайте issue на GitHub:");
            lblHow.Text = Loc.S("Just drag a shortcut or a file onto the panel - it becomes a tile.",
                "Просто перетащите ярлык или файл на панель — появится плитка.");
            lblLang.Text = Loc.S("Language / Язык:", "Язык / Language:");
            chkUpdates.Text = Loc.S("Check for updates automatically", "Проверять обновления автоматически");
            lblSupport.Text = Loc.S("Support the author", "Поддержать автора");
            lblCopyHint.Text = Loc.S("Click an address to copy it", "Клик по адресу — скопирует его");
            lblDonatePrompt.Text = Loc.S("Wallet addresses and other ways to help live on GitHub:", "Адреса кошельков и другие способы помочь — на GitHub:");
            btnClose.Text = Loc.S("Close", "Закрыть");
            btnExamples.Text = Loc.S("Close & create example tiles", "Закрыть и создать примеры");
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

        // Mini-diagram: a .lnk card -> arrow -> grid with three tiles + a ghost
        // cell where the new tile lands (small cursor included).
        private void DrawIllustration(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = r.Y + r.Height / 2;

            var card = new Rectangle(r.Left + 14, cy - 26, 52, 52);
            using (var path = RoundRectPath(card, 8))
            using (var b = new SolidBrush(panelColor))
            using (var pen = new Pen(dimColor, 1f))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }
            using (var f = new Font("Segoe UI", 8f))
            using (var tb = new SolidBrush(textColor))
            {
                var sz = TextRenderer.MeasureText(".lnk", f);
                g.DrawString(".lnk", f, tb, card.Left + (card.Width - sz.Width) / 2f, card.Top + (card.Height - sz.Height) / 2f);
            }

            int ax1 = card.Right + 10, ax2 = card.Right + (r.Width / 2) - 30;
            using (var pen = new Pen(dimColor, 2f) { CustomEndCap = new AdjustableArrowCap(6, 7) })
                g.DrawLine(pen, ax1, cy, ax2, cy);

            int cell = 38, gap = 4;
            int gx = r.Right - 14 - (cell * 2 + gap), gy = cy - (cell * 2 + gap) / 2;
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
                Width = 56;
                Height = 34;
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
                    if (Code == "ru")
                    {
                        int bh = Height / 3;
                        using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, 0, 0, Width, bh);
                        using (var b = new SolidBrush(Color.FromArgb(0, 57, 166))) g.FillRectangle(b, 0, bh, Width, bh);
                        using (var b = new SolidBrush(Color.FromArgb(213, 43, 30))) g.FillRectangle(b, 0, 2 * bh, Width, Height - 2 * bh);
                    }
                    else
                    {
                        // Union Jack, simplified: navy field, white+red diagonals,
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
                    }
                    g.ResetClip();
                    using (var pen = new Pen(Selected ? Color.FromArgb(46, 204, 113) : Color.FromArgb(130, 130, 135), Selected ? 2f : 1f))
                        g.DrawPath(pen, path);
                }
                base.OnPaint(e);
            }
        }
    }
}
