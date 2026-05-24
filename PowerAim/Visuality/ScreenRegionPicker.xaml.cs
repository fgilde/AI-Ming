using PowerAim.Class.Native;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using Point = System.Windows.Point;

namespace PowerAim.Visuality;

/// <summary>
///     Topmost, transparent overlay that spans every connected monitor and lets the user drag a
///     selection rectangle in *screen-pixel coordinates*. Used by the OCR-region dialog to pick a
///     HUD area without typing X/Y/W/H by hand. Returns the picked rect (or <c>null</c> on cancel)
///     via the static <see cref="Pick"/> entry point — modal blocking call.
///     <para>
///     The window is intentionally a tiny GDI window first that we expand to cover the virtual
///     screen on <see cref="Window.SourceInitialized"/> — instantiating with the full-screen size
///     before <c>Owner</c>/<c>HideForCapture</c> is set behaves badly on multi-monitor setups.
///     </para>
/// </summary>
public partial class ScreenRegionPicker : Window
{
    [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private bool _drawing;
    private Point _start;
    private System.Drawing.Rectangle? _result;
    private double _dpi = 1.0;

    public ScreenRegionPicker()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

        // Span the entire virtual screen so the picker works across multi-monitor setups.
        var src = PresentationSource.FromVisual(this);
        _dpi = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var vs = SystemInformation.VirtualScreen;
        Left = vs.Left / _dpi;
        Top  = vs.Top  / _dpi;
        Width  = vs.Width  / _dpi;
        Height = vs.Height / _dpi;
        // Make sure the canvas takes its sizing cue from the window now.
        RootCanvas.Width = Width;
        RootCanvas.Height = Height;
        this.HideForCaptureIfEnabled();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _result = null;
            Close();
        }
    }

    private void Root_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(RootCanvas);
        _drawing = true;
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        Canvas.SetLeft(SelectionRect, _start.X);
        Canvas.SetTop(SelectionRect, _start.Y);
        SelectionRect.Visibility = Visibility.Visible;
        RootCanvas.CaptureMouse();
    }

    private void Root_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_drawing) return;
        var p = e.GetPosition(RootCanvas);
        double x = Math.Min(_start.X, p.X);
        double y = Math.Min(_start.Y, p.Y);
        double w = Math.Abs(p.X - _start.X);
        double h = Math.Abs(p.Y - _start.Y);
        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;
        // Coords as screen pixels (account for DPI and virtual-screen offset).
        var vs = SystemInformation.VirtualScreen;
        int sx = (int)Math.Round(x * _dpi + vs.Left);
        int sy = (int)Math.Round(y * _dpi + vs.Top);
        int sw = (int)Math.Round(w * _dpi);
        int sh = (int)Math.Round(h * _dpi);
        CoordsText.Text = $"X={sx}  Y={sy}  W={sw}  H={sh}";
    }

    private void Root_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing) return;
        _drawing = false;
        if (RootCanvas.IsMouseCaptured) RootCanvas.ReleaseMouseCapture();

        double x = Canvas.GetLeft(SelectionRect);
        double y = Canvas.GetTop(SelectionRect);
        double w = SelectionRect.Width;
        double h = SelectionRect.Height;
        if (w < 6 || h < 6)
        {
            // Too small — treat as cancel so we don't accidentally write a 1×1 region.
            _result = null;
        }
        else
        {
            var vs = SystemInformation.VirtualScreen;
            _result = new System.Drawing.Rectangle(
                (int)Math.Round(x * _dpi + vs.Left),
                (int)Math.Round(y * _dpi + vs.Top),
                (int)Math.Round(w * _dpi),
                (int)Math.Round(h * _dpi));
        }
        Close();
    }

    /// <summary>
    ///     Show the picker modally and return the selected screen-pixel rectangle, or <c>null</c>
    ///     if the user pressed ESC / drew nothing.
    /// </summary>
    public static System.Drawing.Rectangle? Pick(Window? owner = null)
    {
        var picker = new ScreenRegionPicker();
        if (owner != null) picker.Owner = owner;
        // We're explicitly not hiding the owner — the picker has a slight tint so the user can
        // still see what they're aiming at through it.
        picker.ShowDialog();
        return picker._result;
    }
}
