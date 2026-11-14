using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace STS2LongDistancePlay;

public static class JobObjectManager
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob,
        JobObjectInfoClass infoType,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private enum JobObjectInfoClass
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryLimit;
        public UIntPtr PeakJobMemoryLimit;
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    private static IntPtr _jobHandle = IntPtr.Zero;
    private static readonly object SyncLock = new();

    public static void Initialize()
    {
        if (!OperatingSystem.IsWindows()) return;

        lock (SyncLock)
        {
            if (_jobHandle != IntPtr.Zero) return;

            try
            {
                _jobHandle = CreateJobObject(IntPtr.Zero, null);
                if (_jobHandle == IntPtr.Zero)
                {
                    ModLog.Warn($"CreateJobObject failed. Error: {Marshal.GetLastWin32Error()}");
                    return;
                }

                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    }
                };

                int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(info, extendedInfoPtr, false);
                    if (!SetInformationJobObject(_jobHandle, JobObjectInfoClass.ExtendedLimitInformation, extendedInfoPtr, (uint)length))
                    {
                        ModLog.Warn($"SetInformationJobObject failed. Error: {Marshal.GetLastWin32Error()}");
                    }
                    else
                    {
                        ModLog.Info("Win32 Job Object initialized (KILL_ON_JOB_CLOSE).");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(extendedInfoPtr);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn($"Failed to create Win32 Job Object: {ex.Message}");
            }
        }
    }

    public static bool AddProcess(Process process)
    {
        if (!OperatingSystem.IsWindows()) return false;

        lock (SyncLock)
        {
            if (_jobHandle == IntPtr.Zero)
            {
                Initialize();
            }

            if (_jobHandle == IntPtr.Zero) return false;

            try
            {
                if (process.HasExited) return false;
                bool ok = AssignProcessToJobObject(_jobHandle, process.Handle);
                if (!ok)
                {
                    ModLog.Warn($"AssignProcessToJobObject failed. Error: {Marshal.GetLastWin32Error()}");
                }
                return ok;
            }
            catch (Exception ex)
            {
                ModLog.Warn($"Failed to assign process to Job Object: {ex.Message}");
                return false;
            }
        }
    }
}
