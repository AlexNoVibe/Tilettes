using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace WinPanel
{
    // Update check: the app asks the public GitHub Releases API for the latest
    // tag and, when it is newer than AppInfo.AppVersion, shows the "Update"
    // plate in the top-right corner. Nothing is downloaded or installed yet —
    // the plate only opens the releases page.
    // Hard rule: a scheduled (automatic) check runs ONLY when the user allowed
    // it in settings ("Check for updates automatically"). CheckNow() is the
    // explicit "check now" button and runs regardless of that flag.
    // Second hard rule: a release younger than MinReleaseAgeHours is NOT
    // offered at all (plate and manual check alike). Freshly published builds
    // keep tripping antivirus false positives for a while; the day of delay
    // gives those reports time to be processed before anyone downloads.
    public static class UpdateChecker
    {
        // A release must sit on GitHub at least this long before the app offers it.
        private const double MinReleaseAgeHours = 24;

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
                var t = new Thread(() => CheckInBackground(owner, settings, false));
                t.IsBackground = true;
                t.Start();
            }
            catch { }
        }

        // The explicit "Check now" button: ignores the interval and the
        // auto-check flag (clicking it is explicit consent), reports the
        // outcome in a message box.
        public static void CheckNow(MainForm owner, Settings settings)
        {
            try
            {
                if (owner == null || settings == null) return;
                var t = new Thread(() => CheckInBackground(owner, settings, true));
                t.IsBackground = true;
                t.Start();
            }
            catch { }
        }

        private static void CheckInBackground(MainForm owner, Settings settings, bool manual)
        {
            string latest = null;
            DateTime? publishedUtc = null;
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
                    string json = sr.ReadToEnd();
                    latest = ExtractTag(json);
                    publishedUtc = ExtractPublishedUtc(json);
                }
            }
            catch (Exception ex)
            {
                try { AppLog.Write("Update check", ex); } catch { }
                if (manual)
                {
                    try { owner.BeginInvoke((Action)(() => ConfirmDialog.ShowInfo(owner,
                        Loc.S("Could not reach GitHub Releases - check the internet connection.", "Не удалось связаться с GitHub Releases — проверьте интернет.")))); } catch { }
                    return;
                }
            }

            // All settings mutations and UI happen back on the UI thread.
            try { owner.BeginInvoke((Action)(() => OnChecked(owner, settings, latest, publishedUtc, manual))); }
            catch { }
        }

        private static void OnChecked(MainForm owner, Settings settings, string latest, DateTime? publishedUtc, bool manual)
        {
            try
            {
                settings.LastUpdateCheck = DateTime.Now.ToString("yyyyMMddHHmmss");
                settings.Save(owner.SettingsFilePath);
                bool newer = latest != null && IsNewer(AppInfo.AppVersion, latest);
                // Age gate: a release that has been out for less than a day is
                // not offered yet (antivirus false positives settle). When the
                // timestamp is missing or unparsable the release is allowed —
                // a changed API format must not freeze updates.
                bool tooFresh = false;
                if (newer && !ReleaseOldEnough(publishedUtc, DateTime.UtcNow))
                {
                    tooFresh = true;
                    newer = false;
                }
                if (newer) owner.ShowUpdatePlate(latest);
                if (manual)
                {
                    if (tooFresh)
                    {
                        ConfirmDialog.ShowInfo(owner,
                            Loc.S("New version v", "Новая версия v") + latest +
                            Loc.S(" is published, but it is less than a day old. It will be offered after 24 hours - antivirus false positives on fresh builds usually settle within that time.",
                                  " уже опубликована, но ей меньше суток. Будет предложена через 24 часа — за это время обычно уходят ложные срабатывания антивирусов на свежих сборках."));
                    }
                    else
                    {
                        ConfirmDialog.ShowInfo(owner,
                            newer
                                ? Loc.S("New version available: v", "Доступна новая версия: v") + latest +
                                  Loc.S("\nThe green Update plate has appeared in the corner.", "\nЗелёная плашка «Обновить» появилась в углу.")
                                : Loc.S("You are on the latest version: v", "У вас последняя версия: v") + AppInfo.AppVersion);
                    }
                }
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

        // Pulls "published_at":"2026-10-03T09:00:00Z" (ISO 8601, UTC) out of the
        // release JSON; null when absent or unparsable.
        private static DateTime? ExtractPublishedUtc(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return null;
                int key = json.IndexOf("\"published_at\"");
                if (key < 0) return null;
                // key+13 is the closing quote of the field name itself; the
                // value's opening quote comes after the colon (key+14).
                int q1 = json.IndexOf('"', key + 14);
                if (q1 < 0) return null;
                int q2 = json.IndexOf('"', q1 + 1);
                if (q2 < 0) return null;
                string s = json.Substring(q1 + 1, q2 - q1 - 1);
                DateTime t;
                if (!DateTime.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out t)) return null;
                return t.ToUniversalTime();
            }
            catch { return null; }
        }

        // The age-gate decision, extracted for the unit test: is this release
        // (newer, published at the given UTC time) old enough to be offered?
        internal static bool ReleaseOldEnough(DateTime? publishedUtc, DateTime nowUtc)
        {
            if (!publishedUtc.HasValue) return true; // unknown age must not freeze updates
            return (nowUtc - publishedUtc.Value).TotalHours >= MinReleaseAgeHours;
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
