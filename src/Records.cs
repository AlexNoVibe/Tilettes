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
        public int Size { get; set; } // Cell size for grid layout: 1 (1x1), 2 (2x2), 3 (3x3), 4 (4x4)
        public bool IsFolder { get; set; }
        public string CustomIconPath { get; set; }
        public List<ShortcutItem> Children { get; set; }

        public ShortcutItem()
        {
            Size = 2;
            Children = new List<ShortcutItem>();
        }
    }

    public class TabData
    {
        public string Name { get; set; }
        public bool IsGridLayout { get; set; } // True = 16x20 Grid, False = Free layout
        public List<ShortcutItem> Items { get; set; }

        public TabData()
        {
            Items = new List<ShortcutItem>();
        }
    }

    public class Records
    {
        public List<TabData> Tabs { get; set; }

        public Records()
        {
            Tabs = new List<TabData>();
        }

        public static Records Load(string path)
        {
            if (!File.Exists(path))
                return GetDefaultRecords();

            try
            {
                var serializer = new XmlSerializer(typeof(Records));
                using (var fs = new FileStream(path, FileMode.Open))
                {
                    var r = (Records)serializer.Deserialize(fs);
                    if (r.Tabs == null || r.Tabs.Count == 0) return GetDefaultRecords();
                    return r;
                }
            }
            catch
            {
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
                var serializer = new XmlSerializer(typeof(Records));
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    serializer.Serialize(fs, this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving records: " + ex.Message);
            }
        }
    }
}
