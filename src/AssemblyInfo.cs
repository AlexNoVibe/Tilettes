using System.Reflection;
using System.Runtime.InteropServices;

// Assembly metadata. csc turns these attributes into a proper VERSIONINFO
// resource inside Tilettes.exe (visible under Properties -> Details), which
// also helps security heuristics: an unsigned exe with no version info is a
// common false-positive pattern for Windows Defender.
// Keep AssemblyVersion in sync with AppInfo.AppVersion (src/apputil.cs).
[assembly: AssemblyTitle("Tilettes")]
[assembly: AssemblyProduct("Tilettes")]
[assembly: AssemblyDescription("Fast-launch desktop panel: tabs of tiles for apps, folders and files")]
[assembly: AssemblyCompany("AlexNoVibe")]
[assembly: AssemblyCopyright("Copyright (c) 2026 AlexNoVibe. MIT license.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyVersion("0.6.0.0")]
[assembly: AssemblyFileVersion("0.6.0.0")]
[assembly: ComVisible(false)]
