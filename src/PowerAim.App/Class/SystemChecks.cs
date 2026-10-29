using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace PowerAim.Class;

/// <summary>Start-up checks that explain slow or unstable sessions: the power plan, and crash dumps.</summary>
public static class SystemChecks
{
    private static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid PowerSaver = new("a1841308-3541-4fab-bc81-f71556f20b4a");

    /// <summary>Balanced and power-saver plans clock the CPU/GPU down between frames; custom plans are not judged.</summary>
    public static bool IsThrottlingPowerPlan(Guid plan) => plan == Balanced || plan == PowerSaver;

    /// <summary>The active power plan, or null when it can't be read.</summary>
    public static Guid? ActivePowerPlan()
    {
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out var ptr) != 0) return null;
            try { return Marshal.PtrToStructure<Guid>(ptr); }
            finally { LocalFree(ptr); }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Writes a small dump of <paramref name="process"/> (default: this one, for a crash) to
    ///     <paramref name="directory"/>; the file path, or null on failure.
    /// </summary>
    public static string? WriteMiniDump(string directory, Process? process = null)
    {
        try
        {
            Directory.CreateDirectory(directory);
            process ??= Process.GetCurrentProcess();
            var path = Path.Combine(directory, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}_{process.Id}.dmp");
            using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            // MiniDumpWithThreadInfo | MiniDumpWithIndirectlyReferencedMemory: stacks plus what they point at, still small.
            const int dumpType = 0x1000 | 0x40;
            return MiniDumpWriteDump(process.Handle, (uint)process.Id, file.SafeFileHandle.DangerousGetHandle(), dumpType,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    public static string CrashDumpDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerAim", "crash");

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    [DllImport("dbghelp.dll", SetLastError = true)]
    private static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId, IntPtr hFile, int dumpType,
        IntPtr exceptionParam, IntPtr userStreamParam, IntPtr callbackParam);
}
