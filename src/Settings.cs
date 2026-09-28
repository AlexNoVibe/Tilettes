using System;
using System.IO;

namespace WinPanel
{
    public class Settings
    {
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public bool MinimizeToTray { get; set; }
        
        public int GridTransparency { get; set; }
        public int GridColumns { get; set; }
        public int GridRows { get; set; }
        public int DefaultItemSize { get; set; }
        public bool IsLightTheme { get; set; }

        public Settings()
        {
            WindowWidth = 800;
            WindowHeight = 600;
            WindowX = 100;
            WindowY = 100;
            MinimizeToTray = true;
            
            GridTransparency = 50;
            GridColumns = 16;
            GridRows = 16;
            DefaultItemSize = 2;
            IsLightTheme = false;
        }

        public static Settings Load(string path)
        {
            var s = new Settings();
            if (!File.Exists(path))
            {
                return s;
            }

            try
            {
                var ini = new IniFile(path);
                int w, h, x, y, gt, gc, gr, dis;
                bool m, lt;
                if (int.TryParse(ini.Read("WindowWidth"), out w)) s.WindowWidth = w;
                if (int.TryParse(ini.Read("WindowHeight"), out h)) s.WindowHeight = h;
                if (int.TryParse(ini.Read("WindowX"), out x)) s.WindowX = x;
                if (int.TryParse(ini.Read("WindowY"), out y)) s.WindowY = y;
                if (bool.TryParse(ini.Read("MinimizeToTray"), out m)) s.MinimizeToTray = m;
                
                if (int.TryParse(ini.Read("GridTransparency"), out gt)) s.GridTransparency = gt;
                if (int.TryParse(ini.Read("GridColumns"), out gc)) s.GridColumns = gc;
                if (int.TryParse(ini.Read("GridRows"), out gr)) s.GridRows = gr;
                if (int.TryParse(ini.Read("DefaultItemSize"), out dis)) s.DefaultItemSize = dis;
                if (bool.TryParse(ini.Read("IsLightTheme"), out lt)) s.IsLightTheme = lt;
            }
            catch
            {
                // fallback to defaults
            }
            return s;
        }

        public void Save(string path)
        {
            try
            {
                var ini = new IniFile(path);
                ini.Write("WindowWidth", WindowWidth.ToString());
                ini.Write("WindowHeight", WindowHeight.ToString());
                ini.Write("WindowX", WindowX.ToString());
                ini.Write("WindowY", WindowY.ToString());
                ini.Write("MinimizeToTray", MinimizeToTray.ToString());
                
                ini.Write("GridTransparency", GridTransparency.ToString());
                ini.Write("GridColumns", GridColumns.ToString());
                ini.Write("GridRows", GridRows.ToString());
                ini.Write("DefaultItemSize", DefaultItemSize.ToString());
                ini.Write("IsLightTheme", IsLightTheme.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving settings: " + ex.Message);
            }
        }
    }
}
