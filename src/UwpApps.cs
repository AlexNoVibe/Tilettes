using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinPanel
{
    // Packaged (UWP / Store) apps: Calculator, Alarms & Clock and the rest of
    // shell:AppsFolder. Their exe lives under WindowsApps (ACL-locked, never
    // launched directly) and the AppsFolder view itself can enumerate as empty
    // on locked-down systems (Windows' own Get-StartApps comes back empty
    // there too), so launch goes through a three-step chain and the app list
    // can come from the registry.
    public static class UwpApps
    {
        // True for "shell:AppsFolder\<Family>!<AppId>" - a packaged app that
        // must be activated by the shell, never opened as a file.
        public static bool IsAppsFolderAumid(string path)
        {
            if (string.IsNullOrEmpty(path) || !ShellItemApi.IsShellPath(path)) return false;
            if (path.IndexOf("AppsFolder", StringComparison.OrdinalIgnoreCase) < 0) return false;
            return path.IndexOf('!') >= 0;
        }

        // Launches a packaged app by its AppsFolder parse name. Three attempts,
        // in order of preference, the outcome of each in log.txt (no single
        // route is reliable everywhere):
        //   1) IApplicationActivationManager - the designed desktop-to-packaged
        //      COM activation;
        //   2) ShellExecute of the parse name - the ordinary tile route;
        //   3) explorer.exe relay - explorer sits in the interactive session
        //      and resolves the AUMID even when our process cannot.
        public static bool LaunchAumid(string shellPath)
        {
            if (!IsAppsFolderAumid(shellPath)) return false;
            string aumid = shellPath;
            int slash = aumid.LastIndexOf('\\');
            if (slash >= 0) aumid = aumid.Substring(slash + 1);

            // 1) The activation manager.
            try
            {
                var mgr = (IApplicationActivationManager)new ApplicationActivationManager();
                uint pid;
                int hr = mgr.ActivateApplication(aumid, "", 0, out pid);
                if (hr == 0)
                {
                    AppLog.Write("UwpApps: activated " + aumid + " (activation manager, pid " + pid + ")");
                    return true;
                }
                AppLog.Write("UwpApps: activation manager failed for " + aumid + " (0x" + hr.ToString("X") + ")");
            }
            catch (Exception ex) { AppLog.Write("UwpApps: activation manager", ex); }

            // 2) Plain ShellExecute of the parse name.
            try
            {
                using (Process.Start(new ProcessStartInfo
                {
                    FileName = shellPath,
                    UseShellExecute = true
                }))
                {
                    // ShellExecute may hand the work to an existing host and
                    // return no process - a null return is still a success.
                }
                AppLog.Write("UwpApps: launched " + aumid + " (shellexecute)");
                return true;
            }
            catch (Exception ex) { AppLog.Write("UwpApps: shellexecute failed for " + aumid, ex); }

            // 3) The explorer relay.
            try
            {
                Process.Start("explorer.exe", shellPath);
                AppLog.Write("UwpApps: launched " + aumid + " (explorer relay)");
                return true;
            }
            catch (Exception ex) { AppLog.Write("UwpApps: explorer relay failed for " + aumid, ex); }
            return false;
        }

        // Looks up a package by the start of its full name (for example
        // "Microsoft.WindowsCalculator_") in the per-user package repository
        // and returns "<Family>!<AppId>" - the AUMID suffix the launch and the
        // built-in tiles need. Pure registry, no shell involvement, so it works
        // even on systems where the AppsFolder view is empty. Null when the app
        // is not registered for this user.
        public static string FindAumid(string namePrefix)
        {
            try
            {
                using (var root = Registry.CurrentUser.OpenSubKey(PackagesKey))
                {
                    if (root == null) return null;
                    string bestFull = null, bestVer = null;
                    foreach (var full in root.GetSubKeyNames())
                    {
                        if (!full.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                        if (full.IndexOf("_split.", StringComparison.OrdinalIgnoreCase) >= 0) continue; // resource packages
                        string ver = VersionPart(full.Substring(namePrefix.Length));
                        if (bestVer == null || CompareVersions(ver, bestVer) > 0) { bestFull = full; bestVer = ver; }
                    }
                    if (bestFull == null) return null;
                    string family = FamilyOf(bestFull);
                    if (family == null) return null;
                    // AppId: the Applications\<id> subkey, conventionally "App".
                    string appId = "App";
                    using (var apps = root.OpenSubKey(bestFull + @"\Applications"))
                    {
                        var ids = apps == null ? null : apps.GetSubKeyNames();
                        if (ids != null && ids.Length > 0) appId = ids[0];
                    }
                    return family + "!" + appId;
                }
            }
            catch (Exception ex) { AppLog.Write("UwpApps.FindAumid " + namePrefix, ex); return null; }
        }

        // The per-user package repository: every installed packaged app
        // registers here. Shared with the registry fallback enumerator in
        // ShellItemApi.
        internal const string PackagesKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

        // "Name_Version_Arch__PublisherHash" -> "Name_PublisherHash" (the
        // package family; note the double underscore in the full name).
        internal static string FamilyOf(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            int nameEnd = fullName.IndexOf('_');
            int hashStart = fullName.LastIndexOf("__", StringComparison.Ordinal);
            if (nameEnd <= 0 || hashStart <= nameEnd) return null;
            return fullName.Substring(0, nameEnd) + fullName.Substring(hashStart + 1);
        }

        // The version is the segment right after the package name.
        private static string VersionPart(string tail)
        {
            int end = tail.IndexOf('_');
            return end > 0 ? tail.Substring(0, end) : tail;
        }

        private static int CompareVersions(string a, string b)
        {
            try
            {
                var pa = a.Split('.');
                var pb = b.Split('.');
                for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
                {
                    int va = i < pa.Length ? int.Parse(pa[i]) : 0;
                    int vb = i < pb.Length ? int.Parse(pb[i]) : 0;
                    if (va != vb) return va.CompareTo(vb);
                }
            }
            catch { }
            return string.CompareOrdinal(a, b);
        }

        // CLSID ApplicationActivationManager (Windows 8+).
        [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
        private class ApplicationActivationManager { }

        // IApplicationActivationManager; the two ActivateFor* methods only
        // keep the vtable slots in the right order.
        [ComImport, Guid("2e941141-7f97-4072-b6a2-144f42eaf446"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationActivationManager
        {
            [PreserveSig] int ActivateApplication(string appUserModelId, string arguments, uint options, out uint processId);
            [PreserveSig] int ActivateForFile(string appUserModelId, IntPtr pItemArray, string verb, out uint processId);
            [PreserveSig] int ActivateForProtocol(string appUserModelId, IntPtr pItemArray, out uint processId);
        }
    }
}
