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
        public bool IsFolder { get; set; }
        public string CustomIconPath { get; set; }
        public List<ShortcutItem> Children { get; set; }

        public ShortcutItem()
        {
            Children = new List<ShortcutItem>();
        }
    }

    public class TabData
    {
        public string Name { get; set; }
        public List<ShortcutItem> Items { get; set; }

        public TabData()
        {
            Items = new List<ShortcutItem>();
        }
    }

    public class Settings
    {
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public bool MinimizeToTray { get; set; }
        public int IconSize { get; set; }
        public List<TabData> Tabs { get; set; }

        public Settings()
        {
            WindowWidth = 200;
            WindowHeight = 300;
            WindowX = 100;
            WindowY = 100;
            MinimizeToTray = true;
            IconSize = 32;
            Tabs = new List<TabData>();
        }

        public static Settings Load(string path)
        {
            if (!File.Exists(path))
                return GetDefaultSettings();

            try
            {
                var serializer = new XmlSerializer(typeof(Settings));
                using (var fs = new FileStream(path, FileMode.Open))
                {
                    var s = (Settings)serializer.Deserialize(fs);
                    if (s.Tabs == null || s.Tabs.Count == 0) return GetDefaultSettings(s);
                    return s;
                }
            }
            catch
            {
                return GetDefaultSettings();
            }
        }

        private static Settings GetDefaultSettings(Settings baseSettings = null)
        {
            var s = baseSettings ?? new Settings();
            if (s.Tabs.Count == 0)
            {
                s.Tabs.Add(new TabData { Name = "Main" });
            }
            return s;
        }

        public void Save(string path)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(Settings));
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    serializer.Serialize(fs, this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving settings: " + ex.Message);
            }
        }
    }
}
