using System;
using System.Runtime.InteropServices;

namespace WinPanel
{
    // System power actions behind the four built-in Start-section tiles (shut
    // down, restart, sleep, hibernate). Everything runs in-process via
    // ExitWindowsEx / SetSuspendState - spawning the shutdown.exe tool would
    // be exactly the external-tool pattern antivirus machine-learning
    // heuristics weigh.
    internal static class PowerActions
    {
        private const uint EWX_SHUTDOWN = 0x00000001;
        private const uint EWX_REBOOT = 0x00000002;
        private const uint EWX_POWEROFF = 0x00000008;
        // Application-initiated, planned shutdown reason for the event log.
        private const uint ShutdownReason =
            0x00040000 /* SHTDN_REASON_MAJOR_APPLICATION */ |
            0x80000000 /* SHTDN_REASON_FLAG_PLANNED */;

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const int SE_PRIVILEGE_ENABLED = 0x00000002;
        private const string SeShutdownPrivilege = "SeShutdownPrivilege";

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ExitWindowsEx(uint flags, uint reason);

        [DllImport("powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string systemName, string name, out long luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAllPrivileges, ref TOKEN_PRIVILEGES newState, int bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TOKEN_PRIVILEGES
        {
            public int PrivilegeCount;
            public long Luid;
            public int Attributes;
        }

        // Best effort: a failure here just means the privilege may stay
        // missing - the interactive user's token normally has it anyway.
        private static void EnableShutdownPrivilege()
        {
            IntPtr token;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token)) return;
            try
            {
                long luid;
                if (!LookupPrivilegeValue(null, SeShutdownPrivilege, out luid)) return;
                var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
                AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                // AdjustTokenPrivileges reports success even when the privilege
                // was not assigned; the outcome is deliberately ignored.
            }
            finally { CloseHandle(token); }
        }

        public static bool Shutdown()
        {
            EnableShutdownPrivilege();
            return ExitWindowsEx(EWX_SHUTDOWN | EWX_POWEROFF, ShutdownReason);
        }

        public static bool Restart()
        {
            EnableShutdownPrivilege();
            return ExitWindowsEx(EWX_REBOOT, ShutdownReason);
        }

        public static bool Sleep()
        {
            EnableShutdownPrivilege();
            return SetSuspendState(false, false, false);
        }

        public static bool Hibernate()
        {
            EnableShutdownPrivilege();
            return SetSuspendState(true, false, false);
        }
    }
}
