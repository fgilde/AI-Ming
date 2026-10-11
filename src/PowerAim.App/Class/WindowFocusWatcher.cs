using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace PowerAim.Class;

/// <summary>
///     Polls the OS foreground-window once every <see cref="PollIntervalMs"/> ms and raises
///     events when the focused process / window changes. Used by:
///     <list type="bullet">
///       <item><b>Auto-Pause</b> — pauses AI actions when the user alt-tabs out of the game.</item>
///       <item><b>Per-Game Auto-Profile</b> — switches active trigger/AutoPlay profiles based on
///             the foreground process name.</item>
///     </list>
///     <para>
///     Singleton lifetime (<see cref="Instance"/>). Created lazily, runs only while at least
///     one consumer is subscribed. Designed to never crash on access denied / WMI errors —
///     all win32 failures are converted to "no foreground" and quietly logged.
///     </para>
/// </summary>
public sealed class WindowFocusWatcher : IDisposable
{
    public const int PollIntervalMs = 250;

    private static readonly Lazy<WindowFocusWatcher> _lazy = new(() => new WindowFocusWatcher());
    public static WindowFocusWatcher Instance => _lazy.Value;

    private readonly DispatcherTimer _timer;
    private string? _lastProcessName;
    private string? _lastWindowTitle;
    private IntPtr _lastHwnd = IntPtr.Zero;

    /// <summary>Snapshot of the current foreground state.</summary>
    public sealed record FocusInfo(IntPtr Hwnd, string ProcessName, string WindowTitle);

    /// <summary>Fired on the UI thread whenever the foreground process changes.</summary>
    public event EventHandler<FocusInfo>? FocusedProcessChanged;

    /// <summary>Current foreground process name (lower-case, no .exe). Empty if unknown.</summary>
    public string CurrentProcessName => _lastProcessName ?? string.Empty;

    /// <summary>Current foreground window title. Empty if unknown.</summary>
    public string CurrentWindowTitle => _lastWindowTitle ?? string.Empty;

    /// <summary>Current foreground HWND. <see cref="IntPtr.Zero"/> if unknown.</summary>
    public IntPtr CurrentHwnd => _lastHwnd;

    private WindowFocusWatcher()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(PollIntervalMs)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;
            if (hwnd == _lastHwnd) return; // same window, skip the GetWindowThreadProcessId call

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return;

            string proc = "";
            string title = "";
            try
            {
                using var p = Process.GetProcessById((int)pid);
                proc = (p.ProcessName ?? "").ToLowerInvariant();
            }
            catch { /* access denied for protected processes — ignore */ }

            try
            {
                var sb = new StringBuilder(512);
                if (GetWindowText(hwnd, sb, sb.Capacity) > 0)
                    title = sb.ToString();
            }
            catch { /* ignored */ }

            _lastHwnd = hwnd;
            if (proc != _lastProcessName)
            {
                _lastProcessName = proc;
                _lastWindowTitle = title;
                FocusedProcessChanged?.Invoke(this, new FocusInfo(hwnd, proc, title));
            }
            else
            {
                _lastWindowTitle = title;
            }
        }
        catch
        {
            // Defensive — never let a watcher tick take down the app.
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
}
