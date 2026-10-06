using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WinPanel
{
    // "Copy Start tiles": copies the user's pinned Windows 10 Start tiles
    // (groups, sizes, positions) into a new ordinary panel tab. The layout
    // comes from one PowerShell call (Export-StartLayout + Get-StartApps ->
    // CSV in a single process - Process is expensive, 1-2 s) with a hard
    // 20 s timeout, because Export-StartLayout hangs forever on systems
    // with a broken Start layer (observed). The XML is parsed by local name
    // (namespace-agnostic; the double-nested LayoutModificationTemplate is
    // the norm), every tile target is resolved to something the panel can
    // already launch and icon (native .lnk, or shell:AppsFolder\<AUMID>
    // handled by the existing UWP branch), and the grid layout stacks the
    // groups in their original order, keeping Row/Column 1:1 with collisions
    // moved to the nearest free cell (the panel renders overlaps as-is, so
    // the importer must hand out non-overlapping coordinates).
    public static class StartLayoutImport
    {
        // The tab is rebuilt under this name on every run (Kind stays null:
        // tabs with Kind "startmenu" belong to StartMenuSync). Terminology
        // per the author: the system Start Menu mirror is "Пуск" (Start),
        // this copy of the user's pinned tiles is "Пользовательский Пуск"
        // (User Start).
        public const string TabNameEn = "User Start";
        public const string TabNameRu = "\u041F\u043E\u043B\u044C\u0437\u043E\u0432\u0430\u0442\u0435\u043B\u044C\u0441\u043A\u0438\u0439 \u041F\u0443\u0441\u043A";   // Пользовательский Пуск

        public class Result
        {
            public TabData Tab;                 // null = no tiles parsed
            public int Added;                   // tiles placed into the tab
            public int Skipped;                 // SecondaryTile / FolderTile / unknown
            public int Unresolved;              // placed, but target missing on this machine
            public readonly List<string> SkippedNames = new List<string>();
            public readonly List<string> UnresolvedNames = new List<string>();
            public readonly List<string> Lines = new List<string>();   // detailed report
        }

        // ---------- export (PowerShell, one process, 20 s timeout) ----------

        // Runs the export, returns the layout XML and the Unicode name CSV
        // (Get-StartApps), and deletes the temp files. False on timeout,
        // powershell failure or a missing layout file.
        public static bool ExportLayout(out string layoutXml, out string nameCsv)
        {
            layoutXml = null;
            nameCsv = null;
            string xmlPath = Path.Combine(Path.GetTempPath(), "tilettes_layout.xml");
            string csvPath = Path.Combine(Path.GetTempPath(), "tilettes_apps.csv");
            try { if (File.Exists(xmlPath)) File.Delete(xmlPath); } catch { }
            try { if (File.Exists(csvPath)) File.Delete(csvPath); } catch { }

            AppLog.Write("Start import: exporting Start layout (powershell)");
            var psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            // Minimal and clean on purpose (the project keeps its antivirus
            // surface small). -Encoding Unicode keeps Cyrillic names intact.
            psi.Arguments = "-NoProfile -Command \"Export-StartLayout -Path '" + xmlPath +
                            "'; Get-StartApps | Export-Csv -Path '" + csvPath +
                            "' -NoTypeInformation -Encoding Unicode\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            try
            {
                using (Process p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        AppLog.Write("Start import: powershell did not start");
                        return false;
                    }
                    // Export-StartLayout can hang forever - never wait without
                    // a timeout.
                    if (!p.WaitForExit(20000))
                    {
                        try { p.Kill(); } catch { }
                        try { p.WaitForExit(2000); } catch { }
                        AppLog.Write("Start import: powershell timed out after 20 s, killed");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("Start import: powershell", ex);
                return false;
            }

            try
            {
                if (File.Exists(xmlPath)) layoutXml = File.ReadAllText(xmlPath);
                if (File.Exists(csvPath)) nameCsv = File.ReadAllText(csvPath, Encoding.Unicode);
            }
            catch (Exception ex)
            {
                AppLog.Write("Start import: read export", ex);
            }
            finally
            {
                try { if (File.Exists(xmlPath)) File.Delete(xmlPath); } catch { }
                try { if (File.Exists(csvPath)) File.Delete(csvPath); } catch { }
            }
            bool ok = !string.IsNullOrEmpty(layoutXml);
            AppLog.Write("Start import: export " + (ok
                ? "ok (xml " + layoutXml.Length + " chars, csv " + (nameCsv == null ? 0 : nameCsv.Length) + " chars)"
                : "produced no layout xml"));
            return ok;
        }

        // ---------- name map (Get-StartApps CSV, "Name","AppID") ----------

        public static Dictionary<string, string> ParseNameCsv(string csv)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(csv)) return map;
            string[] lines = csv.Replace("\r\n", "\n").Split('\n');
            for (int i = 1; i < lines.Length; i++)   // skip the header line
            {
                Match m = Regex.Match(lines[i],
                    "^\"(?<n>(?:[^\"]|\"\")*)\"\\s*,\\s*\"(?<a>(?:[^\"]|\"\")*)\"\\s*$");
                if (!m.Success) continue;
                string n = m.Groups["n"].Value.Replace("\"\"", "\"").Trim();
                string a = m.Groups["a"].Value.Replace("\"\"", "\"").Trim();
                if (a.Length > 0 && n.Length > 0 && !map.ContainsKey(a)) map[a] = n;
            }
            return map;
        }

        // ---------- local AUMID -> .lnk index (Start Menu folders) ----------

        // Desktop apps pinned by AUMID resolve to a native .lnk when one
        // carries the same System.AppUserModel.ID. Built from the user's and
        // the all-users Start Menu; empty (never throws) when COM is out of
        // reach - the tiles then fall back to shell:AppsFolder paths.
        public static Dictionary<string, string[]> BuildLnkIndex()
        {
            // value: { aumid, lnkPath, name }
            var index = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return index;
                object sh = Activator.CreateInstance(t);
                string[] roots =
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
                };
                foreach (string root in roots)
                {
                    if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                    string[] files;
                    try { files = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories); }
                    catch { continue; }
                    foreach (string file in files)
                    {
                        try
                        {
                            object sc = sh.GetType().InvokeMember("CreateShortcut",
                                BindingFlags.InvokeMethod, null, sh, new object[] { file });
                            if (sc == null) continue;
                            object id = sc.GetType().InvokeMember("ExtendedProperty",
                                BindingFlags.InvokeMethod, null, sc,
                                new object[] { "System.AppUserModel.ID" });
                            string aumid = id as string;
                            if (string.IsNullOrEmpty(aumid) || index.ContainsKey(aumid)) continue;
                            index[aumid] = new[] { aumid, file, Path.GetFileNameWithoutExtension(file) };
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex) { AppLog.Write("Start import: lnk index", ex); }
            AppLog.Write("Start import: lnk index: " + index.Count + " entries");
            return index;
        }

        // ---------- the import itself ----------

        private class WTile
        {
            public string RawSize;
            public int W, H;
            public int Col, Row;
            public string UwpAppId, DeskAppId, LinkPath, FolderId;
            public bool IsSecondary, IsFolderTile;
        }

        private class WGroup { public string Name; public List<WTile> Tiles = new List<WTile>(); }

        private class Tile
        {
            public string Name, Target;
            public int Size, GridX, GridY;
            public bool IsUwp;
        }

        public static Result BuildTab(string layoutXml, Dictionary<string, string> namesByAumid,
                                      Dictionary<string, string[]> lnkByAumid, int gridColumns)
        {
            var res = new Result();
            int cols = gridColumns < 4 ? 16 : gridColumns;
            Log(res, "grid columns: " + cols);

            // ---- parse the Start layout (local-name based) ----
            XDocument doc = XDocument.Parse(layoutXml);
            var groups = new List<WGroup>();
            WGroup orphan = null;
            foreach (XElement el in doc.Descendants())
            {
                string ln = el.Name.LocalName;
                if (ln == "StartLayoutGroup" || ln == "StartGroup" || ln == "Group")
                {
                    var g = new WGroup { Name = (string)el.Attribute("Name") };
                    groups.Add(g);
                    continue;
                }
                WTile t = ParseTile(el);
                if (t == null) continue;
                if (groups.Count == 0)
                {
                    if (orphan == null) orphan = new WGroup { Name = "(no group)" };
                    orphan.Tiles.Add(t);
                }
                else groups[groups.Count - 1].Tiles.Add(t);
            }
            if (orphan != null && orphan.Tiles.Count > 0) groups.Add(orphan);
            int totalTiles = 0;
            foreach (WGroup g in groups) totalTiles += g.Tiles.Count;
            Log(res, "groups: " + groups.Count + ", tiles: " + totalTiles);
            if (totalTiles == 0)
            {
                Log(res, "no tiles parsed - Windows 11 or a damaged Start layer");
                return res;   // Tab stays null
            }

            // ---- resolve targets ----
            var placedGroups = new List<List<Tile>>();
            foreach (WGroup g in groups)
            {
                var list = new List<Tile>();
                foreach (WTile t in g.Tiles) Resolve(t, list, res, namesByAumid, lnkByAumid);
                if (list.Count > 0) placedGroups.Add(list);
            }

            // ---- layout: groups stacked vertically, 1 empty row between ----
            // (pinned tile folders are not exported by Windows at all, so no
            // folder tile is fabricated here - they are just reported).
            var occ = new bool[4096, cols];
            int yBase = 0;
            foreach (List<Tile> list in placedGroups)
            {
                int groupH = 0;
                foreach (Tile t in list)
                {
                    int x = t.GridX, y = yBase + t.GridY;
                    x = Math.Max(0, Math.Min(cols - t.Size, x));
                    int[] cell = FindFree(occ, cols, x, y, t.Size);
                    t.GridX = cell[0];
                    t.GridY = cell[1];
                    Mark(occ, cols, t.GridX, t.GridY, t.Size);
                    groupH = Math.Max(groupH, t.GridY - yBase + t.Size);
                }
                yBase += groupH + 1;
            }

            // ---- assemble the tab (every field explicit: XmlSerializer skips
            // nulls and a missing element reads back as 0, not as the
            // constructor default - GridX would become 0 instead of -1) ----
            var tab = new TabData
            {
                Name = Loc.S(TabNameEn, TabNameRu),
                IsGridLayout = true,
                Kind = null,
                Items = new List<ShortcutItem>()
            };
            foreach (List<Tile> list in placedGroups)
                foreach (Tile t in list)
                {
                    tab.Items.Add(new ShortcutItem
                    {
                        Path = t.Target,
                        Name = t.Name,
                        X = 0,
                        Y = 0,
                        GridX = t.GridX,
                        GridY = t.GridY,
                        Size = t.Size,
                        IsFolder = false,
                        CustomIconPath = null,
                        ShortDescription = null,
                        Src = null,   // normal user item; no sync ever touches this tab
                        IsUwp = t.IsUwp,
                        AuraColor = null,
                        AuraAlpha = 0,
                        Children = new List<ShortcutItem>()
                    });
                }
            tab.Items.Sort(delegate(ShortcutItem a, ShortcutItem b)
            {
                if (a.GridY != b.GridY) return a.GridY - b.GridY;
                return a.GridX - b.GridX;
            });
            res.Tab = tab;
            res.Added = tab.Items.Count;
            Log(res, "added: " + res.Added + ", skipped: " + res.Skipped + ", unresolved: " + res.Unresolved);
            return res;
        }

        // ---------- parsing ----------

        private static WTile ParseTile(XElement el)
        {
            string ln = el.Name.LocalName;
            if (ln != "Tile" && ln != "DesktopApplicationTile" && ln != "SecondaryTile" && ln != "FolderTile") return null;
            var t = new WTile { RawSize = (string)el.Attribute("Size") ?? "2x2" };
            int xi = t.RawSize.IndexOf('x');
            int w, h;
            if (xi <= 0 || !int.TryParse(t.RawSize.Substring(0, xi), out w)) w = 2;
            if (!int.TryParse(t.RawSize.Substring(xi + 1), out h)) h = w;
            t.W = Math.Max(1, w);
            t.H = Math.Max(1, h);
            int v;
            int.TryParse((string)el.Attribute("Column") ?? "0", out v); t.Col = v;
            int.TryParse((string)el.Attribute("Row") ?? "0", out v); t.Row = v;
            t.UwpAppId = (string)el.Attribute("AppID");
            t.DeskAppId = (string)el.Attribute("DesktopApplicationID");
            t.LinkPath = (string)el.Attribute("DesktopApplicationLinkPath");
            t.FolderId = (string)el.Attribute("FolderID");
            t.IsSecondary = ln == "SecondaryTile";
            t.IsFolderTile = ln == "FolderTile";
            return t;
        }

        private static void Resolve(WTile t, List<Tile> list, Result res,
                                    Dictionary<string, string> namesByAumid,
                                    Dictionary<string, string[]> lnkByAumid)
        {
            if (t.IsSecondary)
            {
                res.Skipped++;
                res.SkippedNames.Add(t.UwpAppId ?? "?");
                Log(res, "SKIPPED SecondaryTile (pinned web/site tile): " + (t.UwpAppId ?? "?"));
                return;
            }
            if (t.IsFolderTile)
            {
                res.Skipped++;
                res.SkippedNames.Add(t.FolderId ?? "?");
                Log(res, "SKIPPED FolderTile (tile folder): " + (t.FolderId ?? "?"));
                return;
            }

            // Square approximation of the Windows tile shapes (user-approved):
            // small 1x1 -> 1, medium 2x2 -> 2, wide 4x2 -> 3, large 2x4 -> 4,
            // anything else -> 2.
            int s;
            if (t.W <= 1 && t.H <= 1) s = 1;
            else if (t.W == 4 && t.H == 2) s = 3;
            else if (t.W == 2 && t.H == 4) s = 4;
            else s = 2;

            // UWP tile: <Tile AppID="PFN!App">
            if (!string.IsNullOrEmpty(t.UwpAppId) && t.UwpAppId.IndexOf('!') >= 0)
            {
                string name = MapName(namesByAumid, t.UwpAppId);
                list.Add(new Tile
                {
                    Name = name,
                    Target = "shell:AppsFolder\\" + t.UwpAppId,
                    Size = s,
                    GridX = t.Col,
                    GridY = t.Row,
                    IsUwp = true
                });
                return;
            }

            // desktop app pinned by AUMID
            if (!string.IsNullOrEmpty(t.DeskAppId))
            {
                string aumid = t.DeskAppId;
                string[] lnk = null;
                if (lnkByAumid != null) lnkByAumid.TryGetValue(aumid, out lnk);
                if (lnk != null && lnk.Length > 1 && File.Exists(lnk[1]))
                {
                    string lnkName = lnk.Length > 2 ? lnk[2] : null;
                    if (string.IsNullOrEmpty(lnkName)) lnkName = MapName(namesByAumid, aumid);
                    list.Add(new Tile
                    {
                        Name = string.IsNullOrEmpty(lnkName) ? aumid : lnkName,
                        Target = lnk[1],
                        Size = s,
                        GridX = t.Col,
                        GridY = t.Row,
                        IsUwp = false
                    });
                    Log(res, "DESKTOP-AUMID->LNK " + t.RawSize + ": " + aumid + " -> " + lnk[1]);
                    return;
                }
                list.Add(new Tile
                {
                    Name = MapName(namesByAumid, aumid),
                    Target = "shell:AppsFolder\\" + aumid,
                    Size = s,
                    GridX = t.Col,
                    GridY = t.Row,
                    IsUwp = true
                });
                Log(res, "DESKTOP-AUMID->SHELL " + t.RawSize + ": " + aumid);
                return;
            }

            // desktop app pinned as a .lnk path
            if (!string.IsNullOrEmpty(t.LinkPath))
            {
                string raw = t.LinkPath;
                string expanded = Environment.ExpandEnvironmentVariables(raw);
                string target = null;
                if (File.Exists(expanded)) target = expanded;
                else
                {
                    // the path names a file on the source PC; try the same
                    // .lnk name in the local Start Menu folders before giving up
                    string local = FindLocalLnk(Path.GetFileName(expanded));
                    if (local != null) target = local;
                }
                string lnkName = Path.GetFileNameWithoutExtension(expanded);
                if (target != null)
                {
                    list.Add(new Tile { Name = lnkName, Target = target, Size = s, GridX = t.Col, GridY = t.Row, IsUwp = false });
                    Log(res, "LNK " + t.RawSize + ": " + target);
                }
                else
                {
                    res.Unresolved++;
                    res.UnresolvedNames.Add(lnkName);
                    list.Add(new Tile { Name = lnkName, Target = expanded, Size = s, GridX = t.Col, GridY = t.Row, IsUwp = false });
                    Log(res, "LNK-MISSING-LOCAL " + t.RawSize + " (" + raw + ")");
                }
                return;
            }

            res.Skipped++;
            res.SkippedNames.Add("?");
            Log(res, "SKIPPED unknown tile kind: " + t.RawSize);
        }

        // The display name for an AUMID: the Get-StartApps map, else the raw id.
        private static string MapName(Dictionary<string, string> namesByAumid, string aumid)
        {
            if (namesByAumid != null)
            {
                string n;
                if (namesByAumid.TryGetValue(aumid, out n)) return n;
            }
            return aumid;
        }

        private static string FindLocalLnk(string fileName)
        {
            string[] roots =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Windows", "Start Menu"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Microsoft", "Windows", "Start Menu"),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                try
                {
                    foreach (string hit in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories))
                        return hit;
                }
                catch { }
            }
            return null;
        }

        // ---------- grid helpers ----------

        private static bool Fits(bool[,] occ, int cols, int x, int y, int s)
        {
            if (x < 0 || y < 0 || x + s > cols || y + s > occ.GetLength(0)) return false;
            for (int dy = 0; dy < s; dy++)
                for (int dx = 0; dx < s; dx++)
                    if (occ[y + dy, x + dx]) return false;
            return true;
        }

        // Nearest free cell to (prefX, prefY): Manhattan-spiral, topmost wins
        // inside a ring.
        private static int[] FindFree(bool[,] occ, int cols, int prefX, int prefY, int s)
        {
            for (int d = 0; d < 2048; d++)
            {
                int[] best = null;
                for (int y = Math.Max(0, prefY - d); y <= prefY + d; y++)
                {
                    int rem = d - Math.Abs(y - prefY);
                    for (int side = 0; side < 2; side++)
                    {
                        int x = side == 0 ? prefX - rem : prefX + rem;
                        if (x < 0 || x + s > cols) continue;
                        if (!Fits(occ, cols, x, y, s)) continue;
                        if (best == null || y < best[1]) best = new[] { x, y };
                    }
                }
                if (best != null) return best;
            }
            return new[] { 0, 0 };
        }

        private static void Mark(bool[,] occ, int cols, int x, int y, int s)
        {
            int rows = occ.GetLength(0);
            for (int dy = 0; dy < s; dy++)
            {
                if (y + dy >= rows) break;
                for (int dx = 0; dx < s; dx++)
                    if (x + dx < cols) occ[y + dy, x + dx] = true;
            }
        }

        private static void Log(Result res, string s)
        {
            res.Lines.Add(s);
        }
    }
}
