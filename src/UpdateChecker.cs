using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;

namespace WinPanel
{
    // Update check (stub): the app asks the public GitHub Releases API for the
    // latest tag and, when it is newer than AppInfo.AppVersion, shows the
    // "Update" plate in the top-right corner. Nothing is downloaded or
    // installed yet — the plate only opens the releases page in the browser.
    public static class UpdateChecker
    {
        public static void ScheduleCheck(MainForm owner, Settings settings)
        {
            try
            {
                if (owner == null || settings == null || !settings.UpdateCheckEnabled) return;
                int days = Math.Max(1, settings.UpdateCheckDays);
                if (!string.IsNullOrEmpty(settings.LastUpdateCheck))
                {
                    DateTime last;
                    if (DateTime.TryParseExact(settings.LastUpdateCheck, "yyyyMMddHHmmss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out last))
                    {
                        if ((DateTime.Now - last).TotalDays < days) return;
                    }
                }
                var t = new Thread(() => CheckInBackground(owner, settings));
                t.IsBackground = true;
                t.Start();
            }
            catch { }
        }

        private static void CheckInBackground(MainForm owner, Settings settings)
        {
            string latest = null;
            try
            {
                // The handle may not exist yet when the check was scheduled from
                // the MainForm constructor; wait briefly for it.
                for (int i = 0; i < 100 && !owner.IsHandleCreated; i++) Thread.Sleep(100);
                if (!owner.IsHandleCreated) return;

                try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
                var req = (HttpWebRequest)WebRequest.Create(AppInfo.ReleasesApiUrl);
                req.UserAgent = "Tilettes";
                req.Timeout = 6000;
                req.ReadWriteTimeout = 6000;
                using (var resp = req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                {
                    latest = ExtractTag(sr.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                try { AppLog.Write("Update check", ex); } catch { }
            }

            // All settings mutations happen back on the UI thread.
            try { owner.BeginInvoke((Action)(() => OnChecked(owner, settings, latest))); }
            catch { }
        }

        private static void OnChecked(MainForm owner, Settings settings, string latest)
        {
            try
            {
                settings.LastUpdateCheck = DateTime.Now.ToString("yyyyMMddHHmmss");
                settings.Save(owner.SettingsFilePath);
                if (latest != null && IsNewer(AppInfo.AppVersion, latest))
                    owner.ShowUpdatePlate(latest);
            }
            catch (Exception ex)
            {
                try { AppLog.Write("Update check apply", ex); } catch { }
            }
        }

        // Pulls the value of "tag_name":"..." out of the release JSON without
        // pulling in a JSON parser.
        private static string ExtractTag(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int key = json.IndexOf("\"tag_name\"");
            if (key < 0) return null;
            int q1 = json.IndexOf('"', key + 10);
            if (q1 < 0) return null;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            string tag = json.Substring(q1 + 1, q2 - q1 - 1);
            return tag.Length > 0 ? tag : null;
        }

        // "0.5" vs "v0.6": numeric segment comparison, longer version wins ties.
        private static bool IsNewer(string current, string tag)
        {
            try
            {
                int[] a = Parse(current), b = Parse(tag);
                if (a == null || b == null) return false;
                for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
                {
                    int x = i < a.Length ? a[i] : 0;
                    int y = i < b.Length ? b[i] : 0;
                    if (y != x) return y > x;
                }
            }
            catch { }
            return false;
        }

        private static int[] Parse(string version)
        {
            if (string.IsNullOrEmpty(version)) return null;
            string v = version.Trim().TrimStart('v', 'V');
            var parts = v.Split('.');
            var nums = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                // Keep only the leading digits ("5" from "5-beta").
                int end = 0;
                while (end < p.Length && p[end] >= '0' && p[end] <= '9') end++;
                if (end == 0 || !int.TryParse(p.Substring(0, end), out nums[i])) return null;
            }
            return nums;
        }
    }
}
