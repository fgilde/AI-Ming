using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using PowerAim.Class;
using PowerAim.Class.Native;

namespace PowerAim.Visuality;

/// <summary>
///     Topmost, click-through, transparent overlay that reads from
///     <see cref="SessionStats.Instance"/> and displays live inference / detection / strategic
///     stats in the corner of the screen. Useful for tuning models, watching for stalls, and
///     verifying that AutoPlay's intent layer is producing values.
///     <para>
///     Singleton via <see cref="ShowOrHide"/>; the toggle is bound to
///     <see cref="Config.ToggleState.ShowDebugOverlay"/>.
///     </para>
/// </summary>
public partial class DebugOverlay : Window
{
    private static DebugOverlay? _instance;
    private readonly DispatcherTimer _timer;

    [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    public DebugOverlay()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _timer.Tick += OnTick;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Click-through + no taskbar entry + no activation focus stealing.
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        _timer.Start();
        this.HideForCaptureIfEnabled();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        base.OnClosed(e);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var s = SessionStats.Instance;
        FpsText.Text     = s.InstantFps.ToString("0.0");
        MsText.Text      = s.LastInferenceMs.ToString("0.0") + " ms";
        DetText.Text     = s.LastDetectionCount.ToString();
        ShotsText.Text   = s.ShotsFired.ToString();
        IntentText.Text  = string.IsNullOrEmpty(s.LastIntent) ? "—" : s.LastIntent;
        ProfileText.Text = string.IsNullOrEmpty(s.ActiveProfileName) ? "—" : s.ActiveProfileName;
        ProcessText.Text = string.IsNullOrEmpty(WindowFocusWatcher.Instance.CurrentProcessName)
            ? "—"
            : WindowFocusWatcher.Instance.CurrentProcessName;
    }

    /// <summary>
    ///     Idempotent show/hide. Pass the visibility flag from the global toggle — when true the
    ///     overlay window is created (or focused) and ticks; when false it's closed and the
    ///     instance dropped so the next show creates a fresh one.
    /// </summary>
    public static void ShowOrHide(bool visible)
    {
        if (visible)
        {
            if (_instance == null)
            {
                _instance = new DebugOverlay();
                _instance.Closed += (_, _) => _instance = null;
            }
            if (!_instance.IsVisible) _instance.Show();
        }
        else
        {
            if (_instance != null)
            {
                _instance.Close();
                _instance = null;
            }
        }
    }
}
