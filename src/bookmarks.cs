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
            if (File.Exists(path))
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(List<ExplorerBookmark>));
                    using (var fs = new FileStream(path, FileMode.Open))
                    {
                        var list = (List<ExplorerBookmark>)serializer.Deserialize(fs);
                        if (list != null)
                        {
                            MergeDefaults(list);
                            return list;
                        }
                    }
                    return new List<ExplorerBookmark>();
                }
                catch (Exception ex)
                {
                    DataGuard.OnReadFailure(path, "BookmarksStore.Load", ex);
                    return new List<ExplorerBookmark>();
                }
            }

            var seeded = CreateDefault();
            try { Save(path, seeded); }
            catch { }
            return seeded;
        }

        // Adds command groups that appeared after the user's bookmarks.xml was
        // created (docker/cmd/powershell/tar). Existing entries are never touched
        // or reordered; only missing groups/commands are appended.
        public static void MergeDefaults(List<ExplorerBookmark> list)
        {
            try
            {
                if (list == null) return;
                var defaults = CreateDefault();
                foreach (var group in defaults)
                {
                    ExplorerBookmark existing = null;
                    foreach (var b in list)
                        if (b.Kind == "group" && string.Equals(b.Name, group.Name, StringComparison.OrdinalIgnoreCase))
                        { existing = b; break; }

                    if (existing == null)
                    {
                        list.Add(group);
                        continue;
                    }
                    if (existing.Children == null) existing.Children = new List<ExplorerBookmark>();
                    foreach (var cmd in group.Children)
                    {
                        bool has = false;
                        foreach (var c in existing.Children)
                            if (c.Kind == "cmd" && string.Equals(c.Value, cmd.Value, StringComparison.OrdinalIgnoreCase))
                            { has = true; break; }
                        if (!has) existing.Children.Add(cmd);
                    }
                }
            }
            catch { }
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
            docker.Children.Add(Command("docker images", "docker images"));
            docker.Children.Add(Command("docker compose up -d", "docker compose up -d"));
            docker.Children.Add(Command("docker compose down", "docker compose down"));
            docker.Children.Add(Command("docker compose logs --tail 100 -f", "docker compose logs --tail 100 -f"));
            docker.Children.Add(Command("docker compose build", "docker compose build"));
            docker.Children.Add(Command("docker system df", "docker system df"));

            var cmd = new ExplorerBookmark();
            cmd.Kind = "group";
            cmd.Name = "CMD";
            cmd.Children.Add(Command("systeminfo", "systeminfo"));
            cmd.Children.Add(Command("ping 8.8.8.8 -n 5", "ping 8.8.8.8 -n 5"));
            cmd.Children.Add(Command("dir /a", "dir /a"));
            // %1 is substituted with the folder open in the mini explorer, so
            // Windows Terminal starts right there (wt ignores the caller's cwd
            // when the profile has its own starting directory).
            cmd.Children.Add(Command("Windows Terminal here", "wt -d \"%1\""));

            var ps = new ExplorerBookmark();
            ps.Kind = "group";
            ps.Name = "PowerShell";
            ps.Children.Add(Command("Disk free space", "powershell -NoProfile -Command \"Get-PSDrive -PSProvider FileSystem | Format-Table Name, Used, Free -AutoSize\""));
            ps.Children.Add(Command("Open PowerShell here (window)", "start powershell -NoExit -Command \"Set-Location -LiteralPath .\""));

            var list = new List<ExplorerBookmark>();
            list.Add(docker);
            list.Add(cmd);
            list.Add(ps);
            return list;
        }
    }
}
