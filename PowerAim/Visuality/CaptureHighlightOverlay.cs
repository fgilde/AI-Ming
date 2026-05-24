using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace PowerAim.Visuality;

/// <summary>
///     Click-through, topmost, transparent window with a colored border. Used to highlight the actual
///     monitor or application window while the user is hovering a tile in the capture-source picker.
///     Single instance per process — call <see cref="ShowFor(System.Windows.Forms.Screen)"/> /
///     <see cref="ShowFor(IntPtr)"/> to move the overlay, <see cref="HideOverlay"/> to dismiss.
/// </summary>
internal sealed class CaptureHighlightOverlay : Window
{
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // Foreground helpers — used by ShowFor(hwnd, bringToFront:true) so the user can see the live
    // game window while hovering the picker tile / selecting in the ProcessPickerDialog.
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    private const int SW_RESTORE = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private static CaptureHighlightOverlay? _instance;

    private readonly System.Windows.Controls.Border _border;

    private CaptureHighlightOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        IsHitTestVisible = false;
        Focusable = false;

        _border = new System.Windows.Controls.Border
        {
            BorderThickness = new Thickness(4),
            CornerRadius = new CornerRadius(0),
            IsHitTestVisible = false
        };
        _border.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "FluentAccent");
        Content = _border;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    private static CaptureHighlightOverlay GetOrCreate()
    {
        if (_instance == null)
        {
            // Construct on the UI thread the Application is bound to.
            var owner = Application.Current?.MainWindow;
            _instance = new CaptureHighlightOverlay();
            if (owner != null) _instance.Owner = owner;
        }
        return _instance;
    }

    public static void ShowFor(Screen monitor)
    {
        var ov = GetOrCreate();
        // Bounds are in physical pixels; WPF coordinates are DIPs. Find the DPI of the target
        // monitor; if unavailable, assume the primary's DPI.
        double dpiScale = GetDpiScaleFor(monitor);
        var b = monitor.Bounds;
        ov.Left   = b.X / dpiScale;
        ov.Top    = b.Y / dpiScale;
        ov.Width  = b.Width / dpiScale;
        ov.Height = b.Height / dpiScale;
        if (!ov.IsVisible) ov.Show();
    }

    public static void ShowFor(IntPtr hwnd, bool bringToFront = false)
    {
        if (hwnd == IntPtr.Zero) return;
        if (bringToFront) BringToFront(hwnd);
        if (!GetWindowRect(hwnd, out var r)) return;
        int w = r.right - r.left;
        int h = r.bottom - r.top;
        if (w <= 4 || h <= 4) return;

        var ov = GetOrCreate();
        var screen = Screen.FromHandle(hwnd);
        double dpiScale = GetDpiScaleFor(screen);
        ov.Left = r.left / dpiScale;
        ov.Top = r.top / dpiScale;
        ov.Width = w / dpiScale;
        ov.Height = h / dpiScale;
        if (!ov.IsVisible) ov.Show();
    }

    /// <summary>
    ///     Restore (un-minimise) and foreground the target window. Safe to call on a zero handle.
    ///     Windows imposes restrictions on cross-process foreground attempts — they only succeed
    ///     when the calling process currently owns the foreground or has been granted permission;
    ///     since the user is actively clicking inside PowerAim when this fires, the call is
    ///     permitted and works reliably.
    /// </summary>
    public static void BringToFront(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
        catch { /* P/Invoke failures aren't actionable here — just don't crash the hover */ }
    }

    public static void HideOverlay()
    {
        if (_instance == null) return;
        if (_instance.IsVisible) _instance.Hide();
    }

    public static void DisposeOverlay()
    {
        if (_instance == null) return;
        try { _instance.Close(); } catch { /* ignored */ }
        _instance = null;
    }

    private static double GetDpiScaleFor(Screen screen)
    {
        // Best-effort: use the Application's main-window DPI. Mixed-DPI setups will be slightly
        // off but the overlay just has to roughly cover the target — pixel-perfect alignment isn't
        // required for a hover preview.
        var src = Application.Current?.MainWindow != null
            ? PresentationSource.FromVisual(Application.Current.MainWindow)
            : null;
        if (src?.CompositionTarget != null)
            return src.CompositionTarget.TransformToDevice.M11;
        return 1.0;
    }
}
