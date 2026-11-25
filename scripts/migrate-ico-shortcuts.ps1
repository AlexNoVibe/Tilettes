<#
.SYNOPSIS
    Scans Tilettes' ico\ folder for legacy byte-copied .lnk files, compares them
    against original .lnk shortcuts on the system, reports which shortcuts can
    be converted to redirect links (.lnk -> .lnk) and shows any parameter diffs.

.DESCRIPTION
    By default this script is 100% READ-ONLY (preview mode): it only inspects
    shortcuts and prints:
      1) Which .lnk files in ico\ can be converted to link-to-link redirects.
      2) Explicit field-by-field differences for shortcuts whose parameters
         in ico\ differ from the matched original .lnk.

    Conversion only happens when -Apply is explicitly passed AND only after
    interactive user confirmation. Backups are always created before any write.

.PARAMETER TilettesDir
    Path to the Tilettes directory containing ico\. Defaults to parent of scripts\
    (or current directory if ico\ exists there).

.PARAMETER IcoDir
    Explicit path to ico\ directory (overrides -TilettesDir\ico).

.PARAMETER SearchDirs
    Optional extra directories to search recursively for original .lnk files,
    in addition to standard Desktop, Start Menu, and Quick Launch locations.

.PARAMETER ShowAll
    Also list already-converted redirects, non-.lnk files, and unmatched files
    in detail (by default unmatched/exact/diff summaries are shown).

.PARAMETER Apply
    Enable interactive migration mode. Still prints full report first and
    strictly requires user confirmation before modifying any files.
#>
[CmdletBinding()]
param(
    [string]$TilettesDir = "",
    [string]$IcoDir = "",
    [string[]]$SearchDirs = @(),
    [switch]$ShowAll,
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Resolve ico\ directory
if ([string]::IsNullOrWhiteSpace($IcoDir)) {
    if ([string]::IsNullOrWhiteSpace($TilettesDir)) {
        $scriptParent = Split-Path -Parent $PSScriptRoot
        if (Test-Path (Join-Path (Get-Location).Path "ico")) {
            $TilettesDir = (Get-Location).Path
        } elseif ($scriptParent -and (Test-Path (Join-Path $scriptParent "ico"))) {
            $TilettesDir = $scriptParent
        } else {
            $TilettesDir = (Get-Location).Path
        }
    }
    $IcoDir = Join-Path $TilettesDir "ico"
}

if (-not (Test-Path -LiteralPath $IcoDir -PathType Container)) {
    Write-Error "ico directory not found: $IcoDir"
    exit 1
}
$IcoDir = (Resolve-Path -LiteralPath $IcoDir).Path

# Inline C# helper for Unicode IShellLinkW reading/updating, MS-SHLLINK
# LinkInfo parsing (matches src/Program.cs), and redirect .lnk generation.
$csharp = @"
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

public sealed class LnkInfo
{
    public string FilePath;
    public bool IsRedirect;
    public string RawLinkInfoPath;
    public string TargetPath;
    public string Arguments;
    public string WorkingDirectory;
    public string Description;
    public string IconPath;
    public int IconIndex;
    public int ShowCmd;
    public short Hotkey;
    public bool RunAsUser;
    public string ReadError;
}

public static class LnkHelper
{
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCom { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        [PreserveSig] int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
        [PreserveSig] int GetIDList(out IntPtr ppidl);
        [PreserveSig] int SetIDList(IntPtr pidl);
        [PreserveSig] int GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        [PreserveSig] int GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        [PreserveSig] int GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        [PreserveSig] int GetHotkey(out short pwHotkey);
        [PreserveSig] int SetHotkey(short wHotkey);
        [PreserveSig] int GetShowCmd(out int piShowCmd);
        [PreserveSig] int SetShowCmd(int iShowCmd);
        [PreserveSig] int GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        [PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        [PreserveSig] int Resolve(IntPtr hwnd, int fFlags);
        [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    public static string ParseLnkLocalBasePath(string lnkPath)
    {
        try
        {
            byte[] b = File.ReadAllBytes(lnkPath);
            if (b.Length < 0x4C) return null;
            if (b[0] != 0x4C || b[1] != 0x00 || b[2] != 0x00 || b[3] != 0x00) return null;
            int flags = b[0x14] | (b[0x15] << 8) | (b[0x16] << 16) | (b[0x17] << 24);
            int off = 0x4C;
            if ((flags & 0x01) != 0)
            {
                if (off + 2 > b.Length) return null;
                int idListSize = b[off] | (b[off + 1] << 8);
                off += 2 + idListSize;
            }
            if ((flags & 0x02) == 0) return null;
            if (off + 0x1C > b.Length) return null;
            int linkInfoSize = b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24);
            int linkInfoHeaderSize = b[off + 0x04] | (b[off + 0x05] << 8) | (b[off + 0x06] << 16) | (b[off + 0x07] << 24);
            int linkInfoEnd = (linkInfoSize > 0x1C && linkInfoSize <= b.Length - off) ? off + linkInfoSize : b.Length;
            if (linkInfoHeaderSize >= 0x24 && linkInfoEnd - off >= 0x24)
            {
                int localBaseUniOff = b[off + 0x1C] | (b[off + 0x1D] << 8) | (b[off + 0x1E] << 16) | (b[off + 0x1F] << 24);
                int pu = off + localBaseUniOff;
                if (localBaseUniOff >= 0x24 && pu > off && pu < linkInfoEnd - 1)
                {
                    int endu = pu;
                    while (endu + 1 < linkInfoEnd && !(b[endu] == 0 && b[endu + 1] == 0)) endu += 2;
                    string uniPath = Encoding.Unicode.GetString(b, pu, endu - pu);
                    if (!string.IsNullOrEmpty(uniPath))
                    {
                        if (uniPath.EndsWith("\\", StringComparison.Ordinal))
                        {
                            int commonUniOff = b[off + 0x20] | (b[off + 0x21] << 8) | (b[off + 0x22] << 16) | (b[off + 0x23] << 24);
                            int qu = off + commonUniOff;
                            if (commonUniOff >= 0x24 && qu > off && qu < linkInfoEnd - 1)
                            {
                                int endu2 = qu;
                                while (endu2 + 1 < linkInfoEnd && !(b[endu2] == 0 && b[endu2 + 1] == 0)) endu2 += 2;
                                uniPath += Encoding.Unicode.GetString(b, qu, endu2 - qu);
                            }
                        }
                        return uniPath;
                    }
                }
            }
            int localBasePathOffset = b[off + 0x10] | (b[off + 0x11] << 8) | (b[off + 0x12] << 16) | (b[off + 0x13] << 24);
            int p = off + localBasePathOffset;
            if (p <= off || p >= b.Length) return null;
            int end = p;
            while (end < b.Length && b[end] != 0) end++;
            string path = Encoding.Default.GetString(b, p, end - p);
            if (!string.IsNullOrEmpty(path) && path.EndsWith("\\", StringComparison.Ordinal))
            {
                int commonOff = b[off + 0x18] | (b[off + 0x19] << 8) | (b[off + 0x1A] << 16) | (b[off + 0x1B] << 24);
                int q = off + commonOff;
                if (q > off && q < b.Length)
                {
                    int end2 = q;
                    while (end2 < b.Length && b[end2] != 0) end2++;
                    path += Encoding.Default.GetString(b, q, end2 - q);
                }
            }
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch { return null; }
    }

    private static bool ReadRunAsFlag(string lnkPath)
    {
        try
        {
            byte[] hdr = new byte[0x18];
            using (var fs = File.OpenRead(lnkPath))
            {
                if (fs.Read(hdr, 0, 0x18) < 0x18) return false;
            }
            uint flags = (uint)(hdr[0x14] | (hdr[0x15] << 8) | (hdr[0x16] << 16) | (hdr[0x17] << 24));
            return (flags & 0x2000u) != 0;
        }
        catch { return false; }
    }

    public static LnkInfo ReadLnk(string lnkPath)
    {
        var info = new LnkInfo();
        info.FilePath = lnkPath;
        info.ShowCmd = 1;
        info.RawLinkInfoPath = ParseLnkLocalBasePath(lnkPath) ?? "";
        info.IsRedirect = !string.IsNullOrEmpty(info.RawLinkInfoPath) &&
                          info.RawLinkInfoPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
        info.RunAsUser = ReadRunAsFlag(lnkPath);

        if (info.IsRedirect)
        {
            info.TargetPath = info.RawLinkInfoPath;
            return info;
        }

        IShellLinkW sl = null;
        try
        {
            sl = (IShellLinkW)new ShellLinkCom();
            ((IPersistFile)sl).Load(lnkPath, 0);

            var sb = new StringBuilder(2048);
            if (sl.GetPath(sb, sb.Capacity, IntPtr.Zero, 4) >= 0 && sb.Length > 0) // SLGP_RAWPATH
                info.TargetPath = sb.ToString();
            if (string.IsNullOrEmpty(info.TargetPath))
            {
                sb.Length = 0;
                if (sl.GetPath(sb, sb.Capacity, IntPtr.Zero, 0) >= 0 && sb.Length > 0)
                    info.TargetPath = sb.ToString();
            }
            if (string.IsNullOrEmpty(info.TargetPath))
                info.TargetPath = info.RawLinkInfoPath;
            info.TargetPath = info.TargetPath ?? "";

            sb.Length = 0;
            info.Arguments = (sl.GetArguments(sb, sb.Capacity) >= 0) ? sb.ToString() : "";

            sb.Length = 0;
            info.WorkingDirectory = (sl.GetWorkingDirectory(sb, sb.Capacity) >= 0) ? sb.ToString() : "";

            sb.Length = 0;
            info.Description = (sl.GetDescription(sb, sb.Capacity) >= 0) ? sb.ToString() : "";

            sb.Length = 0;
            int idx;
            if (sl.GetIconLocation(sb, sb.Capacity, out idx) >= 0)
            {
                info.IconPath = sb.ToString().Trim();
                info.IconIndex = idx;
            }
            else
            {
                info.IconPath = "";
                info.IconIndex = 0;
            }

            int sc;
            if (sl.GetShowCmd(out sc) >= 0 && (sc == 3 || sc == 7))
                info.ShowCmd = sc;
            else
                info.ShowCmd = 1;

            short hk;
            info.Hotkey = (sl.GetHotkey(out hk) >= 0) ? hk : (short)0;
        }
        catch (Exception ex)
        {
            info.ReadError = ex.Message;
            info.TargetPath = info.TargetPath ?? "";
            info.Arguments = info.Arguments ?? "";
            info.WorkingDirectory = info.WorkingDirectory ?? "";
            info.Description = info.Description ?? "";
            info.IconPath = info.IconPath ?? "";
        }
        finally
        {
            if (sl != null) try { Marshal.ReleaseComObject(sl); } catch { }
        }
        return info;
    }

    // Matches WriteRedirectLnkFile in src/Program.cs
    public static void WriteRedirectLnkFile(string destPath, string targetPath, string iconPath, int iconIndex, int showCmd)
    {
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write((uint)0x0000004C);
            bw.Write(new Guid("00021401-0000-0000-C000-000000000046").ToByteArray());
            uint flags = 0x02u | 0x80u;
            if (!string.IsNullOrEmpty(iconPath)) flags |= 0x40u;
            bw.Write(flags);
            bw.Write((uint)0x20);
            bw.Write((ulong)0);
            bw.Write((ulong)0);
            bw.Write((ulong)0);
            bw.Write((uint)0);
            bw.Write((int)iconIndex);
            bw.Write((uint)(showCmd > 0 ? showCmd : 1));
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((uint)0);
            bw.Write((uint)0);

            byte[] localBaseAnsi = Encoding.Default.GetBytes(targetPath + "\0");
            byte[] localBaseUni = Encoding.Unicode.GetBytes(targetPath + "\0");
            byte[] commonAnsi = new byte[] { 0 };
            byte[] commonUni = new byte[] { 0, 0 };
            const int volIdSize = 17;
            const int linkInfoHeaderSize = 0x24;
            int volIdOff = linkInfoHeaderSize;
            int localBaseOff = volIdOff + volIdSize;
            int commonOff = localBaseOff + localBaseAnsi.Length;
            int localBaseUniOff = commonOff + commonAnsi.Length;
            int commonUniOff = localBaseUniOff + localBaseUni.Length;
            int linkInfoSize = commonUniOff + commonUni.Length;

            bw.Write((uint)linkInfoSize);
            bw.Write((uint)linkInfoHeaderSize);
            bw.Write((uint)1);
            bw.Write((uint)volIdOff);
            bw.Write((uint)localBaseOff);
            bw.Write((uint)0);
            bw.Write((uint)commonOff);
            bw.Write((uint)localBaseUniOff);
            bw.Write((uint)commonUniOff);

            bw.Write((uint)volIdSize);
            bw.Write((uint)3);
            bw.Write((uint)0);
            bw.Write((uint)16);
            bw.Write((byte)0);

            bw.Write(localBaseAnsi);
            bw.Write(commonAnsi);
            bw.Write(localBaseUni);
            bw.Write(commonUni);

            if (!string.IsNullOrEmpty(iconPath))
            {
                char[] chars = iconPath.ToCharArray();
                bw.Write((ushort)chars.Length);
                bw.Write(Encoding.Unicode.GetBytes(chars));
            }
            bw.Write((uint)0);
            WriteBytesAtomic(destPath, ms.ToArray());
        }
    }

    private static void WriteBytesAtomic(string destPath, byte[] data)
    {
        string tmpPath = destPath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
        try
        {
            File.WriteAllBytes(tmpPath, data);
            if (File.Exists(destPath))
                File.Replace(tmpPath, destPath, null);
            else
                File.Move(tmpPath, destPath);
        }
        finally
        {
            if (File.Exists(tmpPath))
                try { File.Delete(tmpPath); } catch { }
        }
    }

    public static void CopyParamsToOriginal(LnkInfo sourceParams, string origLnkPath)
    {
        IShellLinkW sl = null;
        try
        {
            sl = (IShellLinkW)new ShellLinkCom();
            ((IPersistFile)sl).Load(origLnkPath, 0);
            if (!string.IsNullOrEmpty(sourceParams.TargetPath))
                sl.SetPath(sourceParams.TargetPath);
            sl.SetArguments(sourceParams.Arguments ?? "");
            sl.SetWorkingDirectory(sourceParams.WorkingDirectory ?? "");
            sl.SetDescription(sourceParams.Description ?? "");
            sl.SetIconLocation(sourceParams.IconPath ?? "", sourceParams.IconIndex);
            sl.SetShowCmd(sourceParams.ShowCmd > 0 ? sourceParams.ShowCmd : 1);
            sl.SetHotkey(sourceParams.Hotkey);
            ((IPersistFile)sl).Save(origLnkPath, true);
        }
        finally
        {
            if (sl != null) try { Marshal.ReleaseComObject(sl); } catch { }
        }

        // Preserve RunAsUser flag (bit 0x2000 in LinkFlags at offset 0x14)
        byte[] bytes = File.ReadAllBytes(origLnkPath);
        if (bytes.Length >= 0x18)
        {
            uint flags = (uint)(bytes[0x14] | (bytes[0x15] << 8) | (bytes[0x16] << 16) | (bytes[0x17] << 24));
            if (sourceParams.RunAsUser) flags |= 0x2000u;
            else flags &= ~0x2000u;
            bytes[0x14] = (byte)(flags & 0xFF);
            bytes[0x15] = (byte)((flags >> 8) & 0xFF);
            bytes[0x16] = (byte)((flags >> 16) & 0xFF);
            bytes[0x17] = (byte)((flags >> 24) & 0xFF);
            WriteBytesAtomic(origLnkPath, bytes);
        }
    }
}
"@

if (-not ("LnkHelper" -as [type])) {
    Add-Type -TypeDefinition $csharp -Language CSharp
}

function Normalize-PathStr([string]$p) {
    if ([string]::IsNullOrWhiteSpace($p)) { return "" }
    $expanded = [Environment]::ExpandEnvironmentVariables($p.Trim())
    return $expanded.TrimEnd('\').ToLowerInvariant()
}

function Format-ShowCmd([int]$sc) {
    switch ($sc) {
        3 { return "Maximized (3)" }
        7 { return "Minimized (7)" }
        default { return "Normal (1)" }
    }
}

function Format-Hotkey([int16]$hk) {
    if ($hk -eq 0) { return "" }
    $vk = $hk -band 0xFF
    $mod = ($hk -shr 8) -band 0xFF
    $parts = @()
    if ($mod -band 2) { $parts += "Ctrl" }
    if ($mod -band 1) { $parts += "Shift" }
    if ($mod -band 4) { $parts += "Alt" }
    if ($mod -band 8) { $parts += "Ext" }
    $parts += ([System.Windows.Forms.Keys]$vk).ToString()
    return ($parts -join "+")
}

function Get-LnkDiffs([LnkInfo]$ico, [LnkInfo]$orig) {
    $diffs = New-Object System.Collections.Generic.List[object]

    if ((Normalize-PathStr $ico.TargetPath) -ne (Normalize-PathStr $orig.TargetPath)) {
        $diffs.Add([pscustomobject]@{ Field = "TargetPath"; Ico = $ico.TargetPath; Orig = $orig.TargetPath })
    }
    if (($ico.Arguments) -ne ($orig.Arguments)) {
        $diffs.Add([pscustomobject]@{ Field = "Arguments"; Ico = $ico.Arguments; Orig = $orig.Arguments })
    }
    if ((Normalize-PathStr $ico.WorkingDirectory) -ne (Normalize-PathStr $orig.WorkingDirectory)) {
        $diffs.Add([pscustomobject]@{ Field = "WorkingDirectory"; Ico = $ico.WorkingDirectory; Orig = $orig.WorkingDirectory })
    }

    $icoIconNorm = (Normalize-PathStr $ico.IconPath) + "," + $ico.IconIndex
    $origIconNorm = (Normalize-PathStr $orig.IconPath) + "," + $orig.IconIndex
    if ($icoIconNorm -ne $origIconNorm) {
        $diffs.Add([pscustomobject]@{
            Field = "IconLocation"
            Ico   = "$($ico.IconPath),$($ico.IconIndex)"
            Orig  = "$($orig.IconPath),$($orig.IconIndex)"
        })
    }

    if ($ico.ShowCmd -ne $orig.ShowCmd) {
        $diffs.Add([pscustomobject]@{
            Field = "WindowStyle"
            Ico   = Format-ShowCmd $ico.ShowCmd
            Orig  = Format-ShowCmd $orig.ShowCmd
        })
    }
    if (($ico.Description) -ne ($orig.Description)) {
        $diffs.Add([pscustomobject]@{ Field = "Description"; Ico = $ico.Description; Orig = $orig.Description })
    }
    if ($ico.Hotkey -ne $orig.Hotkey) {
        $diffs.Add([pscustomobject]@{
            Field = "Hotkey"
            Ico   = Format-Hotkey $ico.Hotkey
            Orig  = Format-Hotkey $orig.Hotkey
        })
    }
    if ($ico.RunAsUser -ne $orig.RunAsUser) {
        $diffs.Add([pscustomobject]@{ Field = "RunAsAdmin"; Ico = $ico.RunAsUser; Orig = $orig.RunAsUser })
    }
    return $diffs
}

function Get-RedirectIconMeta([LnkInfo]$info) {
    $ip = $info.IconPath
    $idx = $info.IconIndex
    if ([string]::IsNullOrEmpty($ip) -and -not [string]::IsNullOrEmpty($info.TargetPath)) {
        $tp = [Environment]::ExpandEnvironmentVariables($info.TargetPath)
        if (($tp.EndsWith(".exe", "OrdinalIgnoreCase") -or
             $tp.EndsWith(".ico", "OrdinalIgnoreCase") -or
             $tp.EndsWith(".dll", "OrdinalIgnoreCase")) -and (Test-Path -LiteralPath $tp)) {
            $ip = $tp
            $idx = 0
        }
    }
    return @{ IconPath = $ip; IconIndex = $idx }
}

Add-Type -AssemblyName System.Windows.Forms

# Build search roots for original shortcuts (standard Windows locations + custom -SearchDirs)
$roots = New-Object System.Collections.Generic.List[string]
$stdDirs = @(
    [Environment]::GetFolderPath("Desktop"),
    [Environment]::GetFolderPath("CommonDesktopDirectory"),
    [Environment]::GetFolderPath("StartMenu"),
    [Environment]::GetFolderPath("CommonStartMenu"),
    (Join-Path $env:APPDATA "Microsoft\Internet Explorer\Quick Launch")
)
foreach ($d in ($stdDirs + $SearchDirs)) {
    if (-not [string]::IsNullOrWhiteSpace($d) -and (Test-Path -LiteralPath $d -PathType Container)) {
        $full = (Resolve-Path -LiteralPath $d).Path
        if (-not $roots.Contains($full)) { $roots.Add($full) }
    }
}

Write-Host "=== Tilettes ico\ Shortcut Migration Scanner ===" -ForegroundColor Cyan
Write-Host "IcoDir     : $IcoDir"
Write-Host "Mode       : $(if ($Apply) { 'INTERACTIVE APPLY (requires confirmation)' } else { 'READ-ONLY PREVIEW (no changes)' })"
Write-Host "SearchDirs :"
foreach ($r in $roots) { Write-Host "  - $r" }
Write-Host ""

# Index candidate original .lnk files by normalized base name (excluding IcoDir itself)
$candidatesByName = @{}
foreach ($r in $roots) {
    $files = Get-ChildItem -LiteralPath $r -Filter "*.lnk" -Recurse -File -ErrorAction SilentlyContinue
    foreach ($f in $files) {
        if ($f.FullName.StartsWith($IcoDir + "\", [StringComparison]::OrdinalIgnoreCase)) { continue }
        # Skip backup folders
        if ($f.FullName -match "\\_migration_backup_") { continue }
        $key = $f.BaseName.ToLowerInvariant()
        if (-not $candidatesByName.ContainsKey($key)) {
            $candidatesByName[$key] = New-Object System.Collections.Generic.List[string]
        }
        if (-not $candidatesByName[$key].Contains($f.FullName)) {
            $candidatesByName[$key].Add($f.FullName)
        }
    }
}

$icoEntries = @(Get-ChildItem -LiteralPath $IcoDir -File -ErrorAction Stop | Sort-Object Name)
$nonLnkCount = 0
$alreadyRedirect = New-Object System.Collections.Generic.List[object]
$readyExact = New-Object System.Collections.Generic.List[object]
$readyDiff = New-Object System.Collections.Generic.List[object]
$targetMismatch = New-Object System.Collections.Generic.List[object]
$ambiguous = New-Object System.Collections.Generic.List[object]
$notFound = New-Object System.Collections.Generic.List[object]
$skippedNoTarget = New-Object System.Collections.Generic.List[object]

foreach ($entry in $icoEntries) {
    if ($entry.Extension -ine ".lnk") {
        $nonLnkCount++
        continue
    }

    $icoInfo = [LnkHelper]::ReadLnk($entry.FullName)
    if ($icoInfo.IsRedirect) {
        $redirExists = $false
        try { $redirExists = Test-Path -LiteralPath $icoInfo.RawLinkInfoPath } catch { $redirExists = $false }
        $alreadyRedirect.Add([pscustomobject]@{
            Name   = $entry.Name
            Ico    = $icoInfo
            Exists = $redirExists
        })
        continue
    }

    if (-not [string]::IsNullOrWhiteSpace($icoInfo.ReadError)) {
        $skippedNoTarget.Add([pscustomobject]@{ Name = $entry.Name; Path = $entry.FullName; Reason = "Read error: $($icoInfo.ReadError)" })
        continue
    }

    if ([string]::IsNullOrWhiteSpace($icoInfo.TargetPath)) {
        $skippedNoTarget.Add([pscustomobject]@{ Name = $entry.Name; Path = $entry.FullName; Reason = "Empty TargetPath (UWP/shell/IDList-only shortcut)" })
        continue
    }

    # Match by exact name or stripped " (N)" dedup suffix
    $baseKey = $entry.BaseName.ToLowerInvariant()
    $strippedKey = ($entry.BaseName -replace '\s+\(\d+\)$', '').ToLowerInvariant()
    $rawCandidates = New-Object System.Collections.Generic.List[string]
    foreach ($k in @($baseKey, $strippedKey) | Select-Object -Unique) {
        if ($candidatesByName.ContainsKey($k)) {
            foreach ($p in $candidatesByName[$k]) {
                if (-not $rawCandidates.Contains($p)) { $rawCandidates.Add($p) }
            }
        }
    }

    if ($rawCandidates.Count -eq 0) {
        $notFound.Add([pscustomobject]@{ Name = $entry.Name; Ico = $icoInfo })
        continue
    }

    # Read candidate metadata and ignore any candidate that is itself a redirect
    $candInfos = New-Object System.Collections.Generic.List[LnkInfo]
    foreach ($cp in $rawCandidates) {
        $ci = [LnkHelper]::ReadLnk($cp)
        if (-not $ci.IsRedirect) { $candInfos.Add($ci) }
    }

    if ($candInfos.Count -eq 0) {
        $notFound.Add([pscustomobject]@{ Name = $entry.Name; Ico = $icoInfo })
        continue
    }

    # Narrow down candidates:
    # 1) Prefer candidates with matching TargetPath.
    # 2) Candidates with different TargetPath go to $targetMismatch (avoids false positives on Uninstall.lnk etc.).
    $icoTargetNorm = Normalize-PathStr $icoInfo.TargetPath
    [object[]]$sameTarget = @($candInfos | Where-Object { (Normalize-PathStr $_.TargetPath) -eq $icoTargetNorm })

    if ($sameTarget.Count -eq 0) {
        if ($candInfos.Count -eq 1) {
            $orig = $candInfos[0]
            [object[]]$diffs = @(Get-LnkDiffs $icoInfo $orig)
            $targetMismatch.Add([pscustomobject]@{
                Name  = $entry.Name
                Ico   = $icoInfo
                Orig  = $orig
                Diffs = $diffs
            })
        } else {
            $ambiguous.Add([pscustomobject]@{
                Name       = $entry.Name
                Ico        = $icoInfo
                Candidates = @($candInfos)
                Reason     = "Multiple candidates, none matching TargetPath"
            })
        }
        continue
    }

    # Exact match (0 diffs) among same-TargetPath candidates
    [object[]]$exactMatches = @($sameTarget | Where-Object { (@(Get-LnkDiffs $icoInfo $_)).Count -eq 0 })
    if ($exactMatches.Count -ge 1) {
        $readyExact.Add([pscustomobject]@{
            Name = $entry.Name
            Ico  = $icoInfo
            Orig = $exactMatches[0]
            Alts = @($exactMatches | Select-Object -Skip 1 -ExpandProperty FilePath)
        })
        continue
    }

    if ($sameTarget.Count -eq 1) {
        $orig = $sameTarget[0]
        [object[]]$diffs = @(Get-LnkDiffs $icoInfo $orig)
        $readyDiff.Add([pscustomobject]@{
            Name  = $entry.Name
            Ico   = $icoInfo
            Orig  = $orig
            Diffs = $diffs
        })
        continue
    }

    # Multiple same-TargetPath candidates with diffs - check if all candidates are identical
    [object[]]$sigs = @($sameTarget | ForEach-Object {
        "$(Normalize-PathStr $_.TargetPath)|$($_.Arguments)|$(Normalize-PathStr $_.WorkingDirectory)|$(Normalize-PathStr $_.IconPath),$($_.IconIndex)|$($_.ShowCmd)|$($_.Description)|$($_.Hotkey)|$($_.RunAsUser)"
    } | Select-Object -Unique)
    if ($sigs.Count -eq 1) {
        $orig = $sameTarget[0]
        [object[]]$diffs = @(Get-LnkDiffs $icoInfo $orig)
        $readyDiff.Add([pscustomobject]@{
            Name  = $entry.Name
            Ico   = $icoInfo
            Orig  = $orig
            Diffs = $diffs
        })
    } else {
        $ambiguous.Add([pscustomobject]@{
            Name       = $entry.Name
            Ico        = $icoInfo
            Candidates = $sameTarget
            Reason     = "Multiple same-TargetPath candidates with differing parameters"
        })
    }
}

# --- Report ---
Write-Host "--- Summary ---" -ForegroundColor Cyan
Write-Host ("Total files in ico\            : {0}" -f $icoEntries.Count)
Write-Host ("  Already redirect (.lnk->.lnk): {0}" -f $alreadyRedirect.Count)
Write-Host ("  Ready (exact match, no diffs): {0}" -f $readyExact.Count) -ForegroundColor Green
Write-Host ("  Ready WITH PARAMETER DIFFS   : {0}" -f $readyDiff.Count) -ForegroundColor Yellow
Write-Host ("  TargetPath mismatch (review) : {0}" -f $targetMismatch.Count) -ForegroundColor Magenta
Write-Host ("  Ambiguous (>1 candidate)     : {0}" -f $ambiguous.Count) -ForegroundColor DarkYellow
Write-Host ("  Not found in SearchDirs      : {0}" -f $notFound.Count) -ForegroundColor Gray
Write-Host ("  Skipped (UWP/no TargetPath)  : {0}" -f $skippedNoTarget.Count) -ForegroundColor Gray
Write-Host ("  Non-.lnk files (ico/png/etc) : {0}" -f $nonLnkCount) -ForegroundColor Gray
Write-Host ""

if ($readyExact.Count -gt 0) {
    Write-Host "=== [1] Ready to convert (EXACT match, parameters identical): $($readyExact.Count) ===" -ForegroundColor Green
    foreach ($item in $readyExact) {
        Write-Host "  [EXACT] $($item.Name)  -->  $($item.Orig.FilePath)"
    }
    Write-Host ""
}

if ($readyDiff.Count -gt 0) {
    Write-Host "=== [2] Same TargetPath, PARAMETERS CHANGED (review before converting!): $($readyDiff.Count) ===" -ForegroundColor Yellow
    foreach ($item in $readyDiff) {
        Write-Host "  [DIFF]  $($item.Name)  -->  $($item.Orig.FilePath)" -ForegroundColor Yellow
        foreach ($d in $item.Diffs) {
            $icoVal = if ([string]::IsNullOrEmpty("$($d.Ico)")) { "<empty>" } else { "`"$($d.Ico)`"" }
            $origVal = if ([string]::IsNullOrEmpty("$($d.Orig)")) { "<empty>" } else { "`"$($d.Orig)`"" }
            Write-Host ("          {0,-16}: ico={1}  |  orig={2}" -f $d.Field, $icoVal, $origVal)
        }
    }
    Write-Host ""
}

if ($ambiguous.Count -gt 0) {
    Write-Host "=== [3] Ambiguous (multiple differing originals found): $($ambiguous.Count) ===" -ForegroundColor DarkYellow
    foreach ($item in $ambiguous) {
        Write-Host "  [AMBIG] $($item.Name) ($($item.Reason))"
        foreach ($c in $item.Candidates) {
            Write-Host "            cand: $($c.FilePath)"
            foreach ($d in @(Get-LnkDiffs $item.Ico $c)) {
                $icoVal = if ([string]::IsNullOrEmpty("$($d.Ico)")) { "<empty>" } else { "`"$($d.Ico)`"" }
                $origVal = if ([string]::IsNullOrEmpty("$($d.Orig)")) { "<empty>" } else { "`"$($d.Orig)`"" }
                Write-Host ("                  {0,-16}: ico={1}  |  orig={2}" -f $d.Field, $icoVal, $origVal)
            }
        }
    }
    Write-Host ""
}

if ($targetMismatch.Count -gt 0) {
    Write-Host "=== [4] Same name, DIFFERENT TargetPath (likely different app or moved target - NOT auto-converted): $($targetMismatch.Count) ===" -ForegroundColor Magenta
    foreach ($item in $targetMismatch) {
        Write-Host "  [MISMATCH] $($item.Name)  vs  $($item.Orig.FilePath)" -ForegroundColor Magenta
        foreach ($d in $item.Diffs) {
            $icoVal = if ([string]::IsNullOrEmpty("$($d.Ico)")) { "<empty>" } else { "`"$($d.Ico)`"" }
            $origVal = if ([string]::IsNullOrEmpty("$($d.Orig)")) { "<empty>" } else { "`"$($d.Orig)`"" }
            Write-Host ("             {0,-16}: ico={1}  |  orig={2}" -f $d.Field, $icoVal, $origVal)
        }
    }
    Write-Host ""
}

if ($ShowAll -and $alreadyRedirect.Count -gt 0) {
    Write-Host "=== Already redirect (.lnk -> .lnk): $($alreadyRedirect.Count) ===" -ForegroundColor Cyan
    foreach ($item in $alreadyRedirect) {
        $st = if ($item.Exists) { "OK" } else { "BROKEN TARGET" }
        Write-Host "  [$st] $($item.Name)  -->  $($item.Ico.RawLinkInfoPath)"
    }
    Write-Host ""
}

if ($ShowAll -and $notFound.Count -gt 0) {
    Write-Host "=== Not found in searched directories: $($notFound.Count) ===" -ForegroundColor Gray
    foreach ($item in $notFound) {
        Write-Host "  [MISS] $($item.Name)  (Target=$($item.Ico.TargetPath))"
    }
    Write-Host ""
}

if ($ShowAll -and $skippedNoTarget.Count -gt 0) {
    Write-Host "=== Skipped (UWP / IDList-only / no TargetPath): $($skippedNoTarget.Count) ===" -ForegroundColor Gray
    foreach ($item in $skippedNoTarget) {
        Write-Host "  [SKIP] $($item.Name)  ($($item.Reason))"
    }
    Write-Host ""
}

if (-not $Apply) {
    Write-Host "Preview complete. No files were modified." -ForegroundColor Cyan
    if ($readyExact.Count -gt 0 -or $readyDiff.Count -gt 0) {
        Write-Host "To convert interactively (with confirmation before every change), run with -Apply" -ForegroundColor Cyan
    }
    exit 0
}

# --- Interactive Apply (strictly requires confirmation) ---
if ($readyExact.Count -eq 0 -and $readyDiff.Count -eq 0) {
    Write-Host "Nothing eligible to convert." -ForegroundColor Cyan
    exit 0
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupIcoDir = Join-Path $IcoDir ("_migration_backup_" + $stamp)
$backupOrigDir = Join-Path $backupIcoDir "originals"
$backupCreated = $false

function Ensure-BackupDir {
    if (-not $script:backupCreated) {
        New-Item -ItemType Directory -Path $script:backupIcoDir -Force | Out-Null
        $script:backupCreated = $true
    }
}

$converted = 0
$pushedAndConverted = 0
$skipped = 0
$origBackupSeq = 0

if ($readyExact.Count -gt 0) {
    $ans = Read-Host "Convert $($readyExact.Count) EXACT-match shortcut(s) to redirect links (originals untouched, backup in _migration_backup_$stamp)? [y/N]"
    if ($ans -match '^(y|yes|д|да)$') {
        Ensure-BackupDir
        foreach ($item in $readyExact) {
            $icoBackupPath = Join-Path $backupIcoDir $item.Name
            try {
                Copy-Item -LiteralPath $item.Ico.FilePath -Destination $icoBackupPath -Force
                $meta = Get-RedirectIconMeta $item.Orig
                [LnkHelper]::WriteRedirectLnkFile($item.Ico.FilePath, $item.Orig.FilePath, $meta.IconPath, $meta.IconIndex, $item.Orig.ShowCmd)
                Write-Host "  [CONVERTED] $($item.Name) -> $($item.Orig.FilePath)" -ForegroundColor Green
                $converted++
            } catch {
                Write-Warning "Failed to convert $($item.Name): $($_.Exception.Message). Restoring/skipping."
                if (Test-Path -LiteralPath $icoBackupPath) {
                    try { Copy-Item -LiteralPath $icoBackupPath -Destination $item.Ico.FilePath -Force } catch { }
                }
                $skipped++
            }
        }
    } else {
        Write-Host "Skipped exact-match batch." -ForegroundColor Gray
        $skipped += $readyExact.Count
    }
}

if ($readyDiff.Count -gt 0) {
    Write-Host ""
    Write-Host "Reviewing $($readyDiff.Count) shortcut(s) WITH PARAMETER DIFFS:" -ForegroundColor Yellow
    foreach ($item in $readyDiff) {
        Write-Host ""
        Write-Host "  Shortcut: $($item.Name)" -ForegroundColor Yellow
        Write-Host "  Original: $($item.Orig.FilePath)"
        foreach ($d in $item.Diffs) {
            $icoVal = if ([string]::IsNullOrEmpty("$($d.Ico)")) { "<empty>" } else { "`"$($d.Ico)`"" }
            $origVal = if ([string]::IsNullOrEmpty("$($d.Orig)")) { "<empty>" } else { "`"$($d.Orig)`"" }
            Write-Host ("    {0,-16}: ico={1}  |  orig={2}" -f $d.Field, $icoVal, $origVal)
        }
        $choice = Read-Host "  Action: [S]kip (default), [R]eplace ico with redirect (discard ico edits), [P]ush ico params -> original & convert"
        if ($choice -match '^(r|replace)$') {
            Ensure-BackupDir
            $icoBackupPath = Join-Path $backupIcoDir $item.Name
            try {
                Copy-Item -LiteralPath $item.Ico.FilePath -Destination $icoBackupPath -Force
                $meta = Get-RedirectIconMeta $item.Orig
                [LnkHelper]::WriteRedirectLnkFile($item.Ico.FilePath, $item.Orig.FilePath, $meta.IconPath, $meta.IconIndex, $item.Orig.ShowCmd)
                Write-Host "  [CONVERTED (replaced)] $($item.Name) -> $($item.Orig.FilePath)" -ForegroundColor Green
                $converted++
            } catch {
                Write-Warning "Failed to replace $($item.Name): $($_.Exception.Message). Restoring/skipping."
                if (Test-Path -LiteralPath $icoBackupPath) {
                    try { Copy-Item -LiteralPath $icoBackupPath -Destination $item.Ico.FilePath -Force } catch { }
                }
                $skipped++
            }
        } elseif ($choice -match '^(p|push)$') {
            Ensure-BackupDir
            if (-not (Test-Path -LiteralPath $backupOrigDir)) {
                New-Item -ItemType Directory -Path $backupOrigDir -Force | Out-Null
            }
            $origBackupSeq++
            $origBackupName = ("{0:D3}_{1}" -f $origBackupSeq, [System.IO.Path]::GetFileName($item.Orig.FilePath))
            $origBackupPath = Join-Path $backupOrigDir $origBackupName
            $icoBackupPath = Join-Path $backupIcoDir $item.Name
            try {
                Copy-Item -LiteralPath $item.Orig.FilePath -Destination $origBackupPath -Force
                Copy-Item -LiteralPath $item.Ico.FilePath -Destination $icoBackupPath -Force
                [LnkHelper]::CopyParamsToOriginal($item.Ico, $item.Orig.FilePath)
                $meta = Get-RedirectIconMeta $item.Ico
                [LnkHelper]::WriteRedirectLnkFile($item.Ico.FilePath, $item.Orig.FilePath, $meta.IconPath, $meta.IconIndex, $item.Ico.ShowCmd)
                Write-Host "  [PUSHED + CONVERTED] $($item.Name) -> $($item.Orig.FilePath)" -ForegroundColor Green
                $pushedAndConverted++
            } catch {
                Write-Warning "Failed to push/convert $($item.Name) ($($item.Orig.FilePath)): $($_.Exception.Message). Rolling back."
                if (Test-Path -LiteralPath $origBackupPath) {
                    try { Copy-Item -LiteralPath $origBackupPath -Destination $item.Orig.FilePath -Force } catch { }
                }
                if (Test-Path -LiteralPath $icoBackupPath) {
                    try { Copy-Item -LiteralPath $icoBackupPath -Destination $item.Ico.FilePath -Force } catch { }
                }
                $skipped++
            }
        } else {
            Write-Host "  [SKIPPED] $($item.Name)" -ForegroundColor Gray
            $skipped++
        }
    }
}

Write-Host ""
Write-Host "Done. Converted: $converted, Pushed+Converted: $pushedAndConverted, Skipped: $skipped." -ForegroundColor Cyan
if ($backupCreated) {
    Write-Host "Backup saved at: $backupIcoDir" -ForegroundColor Cyan
}
