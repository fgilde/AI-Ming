using System.Runtime.InteropServices;
using PowerAim.Config;

namespace PowerAim.InputLogic.Tools;

/// <summary>
///     Keeps the session from being flagged idle: while running, it nudges the mouse a single pixel and
///     immediately back on a fixed interval, which resets the OS idle timer and registers as activity in
///     most games (so you don't get AFK-kicked or dropped from a queue). The net cursor movement is zero.
///     Toggled by the built-in Anti-AFK tool; the interval comes from <see cref="SliderSettings.AntiAfkIntervalSeconds"/>.
/// </summary>
public static class AntiAfkService
{
    private static System.Timers.Timer? _timer;

    public static bool IsRunning => _timer != null;

    public static void Toggle()
    {
        if (IsRunning) Stop();
        else Start();
    }

    public static void Start()
    {
        StopTimer();
        var sec = AppConfig.Current?.SliderSettings?.AntiAfkIntervalSeconds ?? 45;
        if (sec < 5) sec = 5;   // a too-short interval just wastes cycles and risks feeling like input lag
        _timer = new System.Timers.Timer(sec * 1000) { AutoReset = true };
        _timer.Elapsed += (_, _) => Nudge();
        _timer.Start();
        Notifier.Notify(Locale.ToolAntiAfk, true);
    }

    public static void Stop()
    {
        if (!IsRunning) return;
        StopTimer();
        Notifier.Notify(Locale.ToolAntiAfk, false);
    }

    private static void StopTimer()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private static void Nudge()
    {
        try
        {
            // Two relative moves that cancel out — the OS still sees activity, the cursor doesn't drift.
            mouse_event(MOUSEEVENTF_MOVE, 1, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_MOVE, unchecked((uint)-1), 0, 0, UIntPtr.Zero);
        }
        catch { /* nudge is best-effort */ }
    }

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    private const uint MOUSEEVENTF_MOVE = 0x0001;
}
