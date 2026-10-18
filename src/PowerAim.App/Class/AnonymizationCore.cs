using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace PowerAim.Class;

/// <summary>
///     Central anonymization helpers. Provides: window identity spoofing,
///     process-tree parent spoofing, PEB ImagePath spoof, human-timing
///     distributions, and network header randomization. All methods are
///     safe to call on the UI thread and from background threads.
/// </summary>
public static class AnonymizationCore
{
    // ─── Window / Process Identity ─────────────────────────────────────────────

    private const int GWL_EXSTYLE        = -20;
    private const long WS_EX_APPWINDOW  = 0x00040000L;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;

    // Use *Ptr variants — plain Get/SetWindowLong truncates the upper 32 bits on x64
    // and can corrupt extended styles (window becomes unmanageable / appears "gone").
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        => IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, dwNewLong);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
    private const int SW_SHOWNOACTIVATE = 4;
    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;

    private static readonly string[] _windowTitlePool =
    [
        "Settings",
        "Microsoft Edge",
        "System",
        "Host Process for Windows Services",
        "Runtime Broker",
        "Windows Security",
        "Device Manager",
        "Windows Update",
        "Microsoft Store",
        "Task Manager",
    ];

    /// <summary>
    ///     Soft identity spoof for the main UI: randomizes the native window title only.
    ///     Does NOT strip APPWINDOW / force TOOLWINDOW — that removed the window from the
    ///     taskbar and Alt-Tab and made the GUI appear "missing" while the process stayed alive.
    ///     Call after HWND is valid (Loaded / SourceInitialized).
    /// </summary>
    public static void SpoofWindowIdentity(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            // Title only — keep shell visibility so the user can still find the window.
            SetWindowText(hwnd, PickRandom(_windowTitlePool));

            // Ensure the window is actually shown (defensive against prior style corruption).
            if (window.Visibility != Visibility.Visible)
                window.Visibility = Visibility.Visible;
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            window.ShowInTaskbar = true;
            ShowWindow(hwnd, SW_SHOW);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Anon] SpoofWindowIdentity: {ex.Message}");
        }
    }

    /// <summary>
    ///     Optional stronger hide (taskbar + Alt-Tab). Not used for the main window.
    /// </summary>
    public static void HideFromShell(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            style &= ~WS_EX_APPWINDOW;
            style |= WS_EX_TOOLWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
            window.ShowInTaskbar = false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Anon] HideFromShell: {ex.Message}");
        }
    }

    // ─── PEB ImagePath Spoof ────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr Reserved3;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle, int processInformationClass,
        out PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(
        IntPtr hProcess, IntPtr lpBaseAddress,
        byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess, IntPtr lpBaseAddress,
        byte[] lpBuffer, int nSize, out int lpNumberOfBytesRead);

    private static readonly string[] _fakeExePaths =
    [
        @"C:\Windows\System32\svchost.exe",
        @"C:\Windows\System32\RuntimeBroker.exe",
        @"C:\Windows\System32\taskhostw.exe",
        @"C:\Program Files\WindowsApps\Microsoft.WindowsStore_0.0.0.0__8wekyb3d8bbwe\WinStore.App.exe",
        @"C:\Windows\System32\SearchHost.exe",
    ];

    /// <summary>
    ///     Overwrites this process's PEB ProcessParameters.ImagePathName and
    ///     CommandLine UNICODE_STRING buffers so tools reading NtQueryInformationProcess
    ///     see a spoofed path instead of the real exe name.
    ///     <para>
    ///     Writes to the process's own memory — no elevation required.
    ///     Safe to call once at startup.
    ///     </para>
    /// </summary>
    public static void SpoofPebImagePath()
    {
        try
        {
            SpoofPebImagePathCore(PickRandom(_fakeExePaths));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Anon] SpoofPebImagePath: {ex.Message}");
        }
    }

    private static unsafe void SpoofPebImagePathCore(string fakePath)
    {
        var hProcess = Process.GetCurrentProcess().Handle;

        // Step 1 — get PEB base address
        NtQueryInformationProcess(hProcess, 0,
            out var pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _);

        IntPtr pebBase = pbi.PebBaseAddress;
        if (pebBase == IntPtr.Zero) return;

        // Step 2 — read ProcessParameters pointer from PEB (offset 0x20 on x64)
        var ptrBuf = new byte[8];
        ReadProcessMemory(hProcess, pebBase + 0x20, ptrBuf, 8, out _);
        IntPtr procParams = (IntPtr)BitConverter.ToInt64(ptrBuf, 0);
        if (procParams == IntPtr.Zero) return;

        // Step 3 — read ImagePathName UNICODE_STRING at ProcessParameters+0x60 (x64)
        var usBuf = new byte[16]; // sizeof UNICODE_STRING = 16 on x64 (2+2+4pad+8)
        ReadProcessMemory(hProcess, procParams + 0x60, usBuf, 16, out _);
        IntPtr strBuffer = (IntPtr)BitConverter.ToInt64(usBuf, 8);
        ushort maxLen   = BitConverter.ToUInt16(usBuf, 2);

        // Step 4 — write the fake path into the existing buffer (capped to MaximumLength)
        var fakeBytes = Encoding.Unicode.GetBytes(fakePath);
        int writeLen  = Math.Min(fakeBytes.Length, maxLen - 2); // -2 for null terminator
        var padded    = new byte[writeLen + 2]; // null-terminate
        Array.Copy(fakeBytes, padded, writeLen);

        WriteProcessMemory(hProcess, strBuffer, padded, padded.Length, out _);

        // Step 5 — update the Length field to match
        var newLen = BitConverter.GetBytes((ushort)writeLen);
        WriteProcessMemory(hProcess, procParams + 0x60, newLen, 2, out _);
    }

    // ─── Human Timing Distributions ────────────────────────────────────────────

    /// <summary>
    ///     Log-normal reaction time sample matching measured human FPS-game distributions.
    ///     Mean ≈ 220ms, σ ≈ 50ms; clamped to [60ms, 600ms].
    /// </summary>
    public static TimeSpan SampleReactionTime()
    {
        const double mu    = 5.393; // ln(220)
        const double sigma = 0.22;
        double z  = BoxMullerZ();
        double ms = Math.Exp(mu + sigma * z);
        return TimeSpan.FromMilliseconds(Math.Clamp(ms, 60, 600));
    }

    /// <summary>
    ///     Gaussian jitter with σ ≈ 4ms, clamped to ±12ms. Added to the AI loop's
    ///     fixed inter-frame delay so the timing is never sub-millisecond consistent.
    /// </summary>
    public static int SampleTimingJitterMs()
    {
        double z = BoxMullerZ();
        return (int)Math.Clamp(z * 4, -12, 12);
    }

    /// <summary>
    ///     Jitter factor for network request intervals: uniform [0.78, 1.22].
    ///     Multiply by the configured interval in seconds.
    /// </summary>
    public static double SampleNetworkIntervalFactor()
        => 0.78 + _rng.NextDouble() * 0.44;

    private static double BoxMullerZ()
    {
        double u1 = 1.0 - _rng.NextDouble();
        double u2 = 1.0 - _rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    // ─── Mouse Path Humanization ────────────────────────────────────────────────

    /// <summary>
    ///     Subdivide a raw (dx, dy) delta into N Bézier sub-steps with a random
    ///     perpendicular wobble on the control point. Returns the sub-step sequence
    ///     as (dx, dy) integer pairs. Each step is intended to be sent with 1-3ms
    ///     inter-step delay by the caller.
    /// </summary>
    public static IReadOnlyList<(int dx, int dy)> HumanizeMouseDelta(int dx, int dy)
    {
        int steps   = _rng.Next(3, 7);
        double wobX = ((_rng.NextDouble() - 0.5) * 10);
        double wobY = ((_rng.NextDouble() - 0.5) * 10);

        // Control point halfway + perpendicular wobble
        double cx = dx * 0.5 + wobX;
        double cy = dy * 0.5 + wobY;

        var result = new List<(int, int)>(steps);
        double prevX = 0, prevY = 0;

        for (int i = 1; i <= steps; i++)
        {
            double t  = (double)i / steps;
            double u  = 1 - t;
            // Quadratic Bézier: P = (1-t)^2·P0 + 2(1-t)t·P1 + t^2·P2
            double bx = u * u * 0 + 2 * u * t * cx + t * t * dx;
            double by = u * u * 0 + 2 * u * t * cy + t * t * dy;

            int stepDx = (int)Math.Round(bx - prevX);
            int stepDy = (int)Math.Round(by - prevY);
            result.Add((stepDx, stepDy));
            prevX = bx;
            prevY = by;
        }

        return result;
    }

    // ─── Network Header Anonymization ──────────────────────────────────────────

    private static readonly string[] _userAgentPool =
    [
        "Microsoft-CryptoAPI/10.0",
        "Windows-Update-Agent/10.0.19041.1",
        "WinHTTP/1.0",
        "Microsoft Office/16.0 (Windows NT 10.0; Microsoft Outlook 16.0.15000; Pro)",
        "Dalvik/2.1.0 (Linux; U; Android 11; SM-G998U Build/RP1A.200720.012)",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
        "Microsoft-Symbol-Server/6.13.0009.1140",
    ];

    /// <summary>Returns a random plausible User-Agent string per session.</summary>
    public static string RandomUserAgent() => PickRandom(_userAgentPool);

    // ─── Parent Process Spoofing (Launcher) ────────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int    cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int    dwX, dwY, dwXSize, dwYSize;
        public int    dwXCountChars, dwYCountChars;
        public int    dwFillAttribute;
        public int    dwFlags;
        public short  wShowWindow;
        public short  cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int    dwProcessId, dwThreadId;
    }

    private const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const int PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = 0x00020000;
    private const uint PROCESS_CREATE_PROCESS = 0x0080;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute,
        ref IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName, string lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
        bool bInheritHandles, int dwCreationFlags,
        IntPtr lpEnvironment, string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    ///     Launch <paramref name="exePath"/> with explorer.exe as its parent process so the
    ///     process tree appears clean to any observer reading PROCESSENTRY32.th32ParentProcessID.
    /// </summary>
    /// <returns>True on success.</returns>
    public static bool LaunchWithSpoofedParent(string exePath, string? workingDir = null)
    {
        int explorerPid = 0;
        try
        {
            explorerPid = Process.GetProcessesByName("explorer").FirstOrDefault()?.Id ?? 0;
        }
        catch { /* fall through — will use direct launch */ }

        if (explorerPid == 0)
        {
            // No explorer → plain launch
            Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                WorkingDirectory = workingDir ?? Path.GetDirectoryName(exePath) ?? ""
            });
            return true;
        }

        IntPtr explorerHandle = OpenProcess(PROCESS_CREATE_PROCESS, false, explorerPid);
        if (explorerHandle == IntPtr.Zero)
        {
            Debug.WriteLine("[Anon] OpenProcess explorer failed, falling back to direct launch.");
            Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                WorkingDirectory = workingDir ?? Path.GetDirectoryName(exePath) ?? ""
            });
            return true;
        }

        IntPtr attrList = IntPtr.Zero;
        try
        {
            // Compute required buffer size
            IntPtr lpSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
            attrList = Marshal.AllocHGlobal(lpSize);
            if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref lpSize))
                return FallbackLaunch(exePath, workingDir);

            IntPtr parentHandle = explorerHandle;
            if (!UpdateProcThreadAttribute(attrList, 0,
                    (IntPtr)PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                    ref parentHandle, (IntPtr)IntPtr.Size,
                    IntPtr.Zero, IntPtr.Zero))
                return FallbackLaunch(exePath, workingDir);

            var si = new STARTUPINFOEX();
            si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
            si.lpAttributeList = attrList;

            bool ok = CreateProcess(
                null, $"\"{exePath}\"",
                IntPtr.Zero, IntPtr.Zero,
                false,
                EXTENDED_STARTUPINFO_PRESENT,
                IntPtr.Zero,
                workingDir ?? Path.GetDirectoryName(exePath),
                ref si, out var pi);

            if (ok)
            {
                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);
            }
            return ok;
        }
        finally
        {
            if (attrList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
            }
            CloseHandle(explorerHandle);
        }
    }

    private static bool FallbackLaunch(string exePath, string? workingDir)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                WorkingDirectory = workingDir ?? Path.GetDirectoryName(exePath) ?? ""
            });
            return true;
        }
        catch { return false; }
    }

    // ─── Shared utilities ──────────────────────────────────────────────────────

    // Thread-local to avoid lock contention on the hot path
    [ThreadStatic]
    private static Random? _rngLocal;
    private static Random _rng => _rngLocal ??= new Random(
        (int)(DateTime.UtcNow.Ticks ^ (long)RuntimeHelpers.GetHashCode(Thread.CurrentThread)));

    private static T PickRandom<T>(IReadOnlyList<T> pool)
        => pool[_rng.Next(pool.Count)];
}
