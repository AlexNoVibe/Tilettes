using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace WinPanel
{
    public class ShortcutItem
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int Size { get; set; } // Cell size for grid layout: 1 (1x1), 2 (2x2), 3 (3x3), 4 (4x4)
        public bool IsFolder { get; set; }
        public string CustomIconPath { get; set; }
        // Free-form user description; used by the panel search ("Search in descriptions").
        public string ShortDescription { get; set; }
        // Sync bookkeeping: where a mirrored Start Menu item came from ("file:C:\...",
        // "dir:...", "uwp:shell:AppsFolder\..."). Empty for normal user items.
        public string Src { get; set; }
        // UWP / Store app (launched through shell:AppsFolder, icon via the shell).
        public bool IsUwp { get; set; }
        // Per-tile background color ("aura"): "RRGGBB" hex (legacy records may
        // still carry "AARRGGBB"); empty = no aura. AuraAlpha is the tile's own
        // transparency override (alpha 30..255); 0 = follow the global setting.
        public string AuraColor { get; set; }
        public int AuraAlpha { get; set; }
        public List<ShortcutItem> Children { get; set; }

        public ShortcutItem()
        {
            Size = 2;
            GridX = -1;
            GridY = -1;
            Children = new List<ShortcutItem>();
        }
    }

    // A named rectangular region on a grid tab that visually holds the tiles
    // dropped inside it ("grouping, not folders"): the tiles keep their own
    // grid cells, the group is only a container drawn under them. X/Y/W/H are
    // cells of the current rect; FixedW/FixedH pin an axis to a user-set size
    // (0 = auto: the axis follows the bounding box of the member tiles).
    public class TileGroup
    {
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public int FixedW { get; set; }
        public int FixedH { get; set; }

        public TileGroup()
        {
            Name = "";
            W = 8;
            H = 3;
        }
    }

    public class TabData
    {
        public string Name { get; set; }
        public bool IsGridLayout { get; set; } // True = Grid, False = Free layout
        public int Row { get; set; } // Tab bar row; tabs are placed into rows manually by dragging
        // Special tab type ("startmenu" = mirrored system Start Menu; null = normal).
        public string Kind { get; set; }
        public List<ShortcutItem> Items { get; set; }
        // Tile groups (grid tabs only). XmlSerializer does not run constructors,
        // so records saved before groups existed deserialize with a null list.
        public List<TileGroup> Groups { get; set; }

        public TabData()
        {
            Items = new List<ShortcutItem>();
            Row = 0;
        }

        public List<TileGroup> EnsureGroups()
        {
            if (Groups == null) Groups = new List<TileGroup>();
            return Groups;
        }
    }

    public class Records
    {
        public List<TabData> Tabs { get; set; }

        public Records()
        {
            Tabs = new List<TabData>();
        }

        // One shared serializer: constructing XmlSerializer is expensive and Save is
        // called after every drag/rename/move with a ~300 KB document on the UI thread.
        private static readonly XmlSerializer serializer = new XmlSerializer(typeof(Records));

        public static Records Load(string path)
        {
            if (!File.Exists(path))
                return GetDefaultRecords();

            try
            {
                using (var fs = new FileStream(path, FileMode.Open))
                {
                    var r = (Records)serializer.Deserialize(fs);
                    if (r.Tabs == null || r.Tabs.Count == 0) return GetDefaultRecords();
                    return r;
                }
            }
            catch (Exception ex)
            {
                // The damaged file is renamed aside (kept, not deleted), the
                // panel starts on defaults and the user is told once.
                DataGuard.OnReadFailure(path, "Records.Load", ex);
                return GetDefaultRecords();
            }
        }

        private static Records GetDefaultRecords()
        {
            var r = new Records();
            r.Tabs.Add(new TabData { Name = "Main", IsGridLayout = true });
            return r;
        }

        public void Save(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    serializer.Serialize(fs, this);
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("Records.Save", ex);
            }
        }
    }
}
