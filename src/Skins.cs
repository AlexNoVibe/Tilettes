using System;
using System.Collections.Generic;
using System.Drawing;

namespace WinPanel
{
    // Decorative skins for the main window: an outer border line plus an inner
    // color scheme. "None" keeps the classic theme behavior (no skin, no border).
    public class Skin
    {
        public string Id;
        public string NameEn;
        public string NameRu;
        public Color BgTop;
        public Color BgBottom;
        public Color Panel;
        public Color Hover;
        public Color Text;
        public Color Accent;
        public Color Border;
        public int BorderWidth;
        // Drives the classic dark/light branches scattered through the UI
        // (settings dialog, welcome, mini explorer, search accents): while a
        // skin is active, IsLightTheme is derived from this flag, so a light
        // skin never leaves those surfaces in the dark palette.
        public bool IsDark;

        public string DisplayName { get { return Loc.IsRu ? NameRu : NameEn; } }

        public static readonly Skin None = new Skin
        {
            Id = "", NameEn = "None", NameRu = "Отключено",
            BgTop = Color.Empty, BgBottom = Color.Empty,
            Panel = Color.Empty, Hover = Color.Empty, Text = Color.Empty,
            Accent = Color.Empty, Border = Color.Empty, BorderWidth = 0
        };

        // Android phone look: near-white surface, dark text, Material teal accent.
        public static readonly Skin Android = new Skin
        {
            Id = "android", NameEn = "Android", NameRu = "Android",
            BgTop = Color.FromArgb(250, 250, 250),
            BgBottom = Color.FromArgb(238, 240, 242),
            Panel = Color.FromArgb(228, 230, 233),
            Hover = Color.FromArgb(207, 212, 217),
            Text = Color.FromArgb(33, 33, 33),
            Accent = Color.FromArgb(0, 150, 136),
            Border = Color.FromArgb(176, 182, 188),
            BorderWidth = 2,
            IsDark = false
        };

        // Night city: deep blue gradient, soft violet accent.
        public static readonly Skin Night = new Skin
        {
            Id = "night", NameEn = "Night", NameRu = "Ночь",
            BgTop = Color.FromArgb(16, 18, 28),
            BgBottom = Color.FromArgb(28, 31, 48),
            Panel = Color.FromArgb(42, 46, 68),
            Hover = Color.FromArgb(58, 63, 92),
            Text = Color.FromArgb(232, 234, 246),
            Accent = Color.FromArgb(124, 105, 255),
            Border = Color.FromArgb(90, 96, 140),
            BorderWidth = 2,
            IsDark = true
        };

        public static readonly Skin Mint = new Skin
        {
            Id = "mint", NameEn = "Mint", NameRu = "Мята",
            BgTop = Color.FromArgb(243, 250, 246),
            BgBottom = Color.FromArgb(228, 242, 236),
            Panel = Color.FromArgb(213, 233, 224),
            Hover = Color.FromArgb(192, 220, 207),
            Text = Color.FromArgb(22, 50, 40),
            Accent = Color.FromArgb(46, 160, 110),
            Border = Color.FromArgb(150, 195, 175),
            BorderWidth = 2,
            IsDark = false
        };

        public static readonly List<Skin> All = new List<Skin> { None, Android, Night, Mint };

        public static Skin Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return None;
            foreach (var s in All)
                if (string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)) return s;
            return None;
        }

        public static bool IsActive(Skin s) { return s != null && !ReferenceEquals(s, None); }
    }
}
