using System.Runtime.InteropServices;

namespace PowerAim.Class.Native;

/// <summary>
///     Without this every Thread.Sleep / Task.Delay in the app waits a whole 15.6 ms timer tick: the stick mixer's
///     2 ms refresh, the virtual pad's 1 ms sync of the physical pad and the pad reader's poll all ran at ~64 Hz.
///     Windows 11 also drops a background process's timer request and moves it to efficiency mode (EcoQoS) —
///     and the app always runs behind the game — so it opts out of both.
/// </summary>
internal static class TimerResolution
{
    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottlingState info, int size);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerThrottlingState
    {
        public uint Version, ControlMask, StateMask;
    }

    private const int ProcessPowerThrottling = 4;
    private const uint ExecutionSpeed = 0x1, IgnoreTimerResolution = 0x4;

    public static void Raise()
    {
        try
        {
            timeBeginPeriod(1); // released by Windows when the process ends
            // ControlMask = what we decide, StateMask 0 = "don't throttle" for both.
            var state = new PowerThrottlingState { Version = 1, ControlMask = ExecutionSpeed | IgnoreTimerResolution, StateMask = 0 };
            SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref state, Marshal.SizeOf<PowerThrottlingState>());
        }
        catch { /* older Windows: the timer request alone still helps */ }
    }
}
