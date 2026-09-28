using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace WinPanel
{
    // One entry of the mini-explorer bookmarks panel (browser-style):
    //   Kind "group"  - a folder-like group holding child entries
    //   Kind "folder" - a saved folder; Value = full path (click = navigate there)
    //   Kind "cmd"    - a saved console command; Value = the command line (click = run)
    public class ExplorerBookmark
    {
        public string Kind { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public List<ExplorerBookmark> Children { get; set; }

        public ExplorerBookmark()
        {
            Kind = "folder";
            Children = new List<ExplorerBookmark>();
        }
    }

    // Load/save of the bookmarks file (bookmarks.xml next to the exe).
    // On the first run a small "Docker" group with typical commands is seeded.
    public static class BookmarksStore
    {
        public static List<ExplorerBookmark> Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var serializer = new XmlSerializer(typeof(List<ExplorerBookmark>));
                    using (var fs = new FileStream(path, FileMode.Open))
                    {
                        var list = (List<ExplorerBookmark>)serializer.Deserialize(fs);
                        if (list != null) return list;
                    }
                    return new List<ExplorerBookmark>();
                }
            }
            catch
            {
                return new List<ExplorerBookmark>();
            }

            var seeded = CreateDefault();
            try { Save(path, seeded); }
            catch { }
            return seeded;
        }

        public static void Save(string path, List<ExplorerBookmark> list)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(List<ExplorerBookmark>));
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    serializer.Serialize(fs, list != null ? list : new List<ExplorerBookmark>());
                }
            }
            catch
            {
            }
        }

        private static ExplorerBookmark Command(string name, string cmd)
        {
            var b = new ExplorerBookmark();
            b.Kind = "cmd";
            b.Name = name;
            b.Value = cmd;
            return b;
        }

        private static List<ExplorerBookmark> CreateDefault()
        {
            var docker = new ExplorerBookmark();
            docker.Kind = "group";
            docker.Name = "Docker";
            docker.Children.Add(Command("docker ps", "docker ps"));
            docker.Children.Add(Command("docker ps -a", "docker ps -a"));
            docker.Children.Add(Command("docker compose up -d", "docker compose up -d"));
            docker.Children.Add(Command("docker compose logs --tail 100 -f", "docker compose logs --tail 100 -f"));

            var list = new List<ExplorerBookmark>();
            list.Add(docker);
            return list;
        }
    }
}
