using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Shapes;
using PowerAim.Class.Native;
using PowerAim.Config;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;

namespace PowerAim.Visuality;

/// <summary>
///     Topmost click-through window that draws a configurable crosshair on the primary screen
///     centre. Shape, colour, size, thickness, gap and outline are all bound to
///     <see cref="CrosshairSettings"/> and re-render live when the user moves a slider.
///     <para>
///     Singleton via <see cref="ShowOrHide"/>; the toggle is bound to
///     <see cref="ToggleState.ShowCrosshairOverlay"/>. Settings changes invoke
///     <see cref="Render"/> through a PropertyChanged subscription.
///     </para>
/// </summary>
public partial class CrosshairOverlay : Window
{
    private static CrosshairOverlay? _instance;

    [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    public CrosshairOverlay()
    {
        InitializeComponent();
        // Live-update on settings change.
        var settings = AppConfig.Current?.CrosshairSettings;
        if (settings is not null) settings.PropertyChanged += (_, _) => Dispatcher.BeginInvoke(new Action(Render));
        Loaded += (_, _) => { PositionOnPrimaryCenter(); Render(); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        this.HideForCaptureIfEnabled();
    }

    private void PositionOnPrimaryCenter()
    {
        var screen = Screen.PrimaryScreen;
        if (screen is null) return;
        // Convert physical screen pixels to WPF DIPs.
        var src = PresentationSource.FromVisual(this);
        double dpi = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var b = screen.Bounds;
        Width = 200 / dpi;
        Height = 200 / dpi;
        Left = (b.X + b.Width  / 2.0) / dpi - Width  / 2;
        Top  = (b.Y + b.Height / 2.0) / dpi - Height / 2;
    }

    private void Render()
    {
        CrosshairCanvas.Children.Clear();
        var s = AppConfig.Current?.CrosshairSettings;
        if (s is null) return;

        double w = CrosshairCanvas.Width;
        double cx = w / 2;
        double cy = w / 2;
        double size = s.Size;
        double half = size / 2.0;
        double thick = s.Thickness;
        double gap = s.Gap;
        Brush fill = s.ColorBrush;
        Brush outlineBrush = s.OutlineBrush;
        double outline = s.OutlineThickness;

        switch (s.Shape)
        {
            case CrosshairShape.Dot:
                AddCircle(cx, cy, thick * 1.5, fill, outlineBrush, outline, filled: true);
                break;

            case CrosshairShape.Cross:
                // Full cross — no gap
                AddLine(cx - half, cy, cx + half, cy, thick, fill, outlineBrush, outline);
                AddLine(cx, cy - half, cx, cy + half, thick, fill, outlineBrush, outline);
                break;

            case CrosshairShape.Plus:
                // Cross with center gap
                AddLine(cx - half, cy, cx - gap, cy, thick, fill, outlineBrush, outline);
                AddLine(cx + gap, cy, cx + half, cy, thick, fill, outlineBrush, outline);
                AddLine(cx, cy - half, cx, cy - gap, thick, fill, outlineBrush, outline);
                AddLine(cx, cy + gap, cx, cy + half, thick, fill, outlineBrush, outline);
                break;

            case CrosshairShape.Circle:
                AddCircle(cx, cy, half, fill, outlineBrush, outline, filled: false, strokeThickness: thick);
                break;

            case CrosshairShape.CircleDot:
                AddCircle(cx, cy, half, fill, outlineBrush, outline, filled: false, strokeThickness: thick);
                AddCircle(cx, cy, thick * 1.4, fill, outlineBrush, outline, filled: true);
                break;

            case CrosshairShape.T:
                AddLine(cx - half, cy, cx + half, cy, thick, fill, outlineBrush, outline);
                AddLine(cx, cy, cx, cy + half, thick, fill, outlineBrush, outline);
                break;
        }
    }

    private void AddLine(double x1, double y1, double x2, double y2, double thickness,
        Brush fill, Brush outlineBrush, double outline)
    {
        if (outline > 0)
        {
            var bg = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = outlineBrush,
                StrokeThickness = thickness + outline * 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            CrosshairCanvas.Children.Add(bg);
        }
        var fg = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = fill,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        CrosshairCanvas.Children.Add(fg);
    }

    private void AddCircle(double cx, double cy, double r, Brush fill, Brush outlineBrush,
        double outline, bool filled, double strokeThickness = 1)
    {
        if (filled)
        {
            if (outline > 0)
            {
                var bg = new Ellipse
                {
                    Width = r * 2 + outline * 2,
                    Height = r * 2 + outline * 2,
                    Fill = outlineBrush
                };
                Canvas.SetLeft(bg, cx - r - outline);
                Canvas.SetTop(bg, cy - r - outline);
                CrosshairCanvas.Children.Add(bg);
            }
            var fg = new Ellipse { Width = r * 2, Height = r * 2, Fill = fill };
            Canvas.SetLeft(fg, cx - r);
            Canvas.SetTop(fg, cy - r);
            CrosshairCanvas.Children.Add(fg);
        }
        else
        {
            if (outline > 0)
            {
                var bg = new Ellipse
                {
                    Width = r * 2,
                    Height = r * 2,
                    Stroke = outlineBrush,
                    StrokeThickness = strokeThickness + outline * 2
                };
                Canvas.SetLeft(bg, cx - r);
                Canvas.SetTop(bg, cy - r);
                CrosshairCanvas.Children.Add(bg);
            }
            var fg = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = fill,
                StrokeThickness = strokeThickness
            };
            Canvas.SetLeft(fg, cx - r);
            Canvas.SetTop(fg, cy - r);
            CrosshairCanvas.Children.Add(fg);
        }
    }

    public static void ShowOrHide(bool visible)
    {
        if (visible)
        {
            if (_instance is null)
            {
                _instance = new CrosshairOverlay();
                _instance.Closed += (_, _) => _instance = null;
            }
            if (!_instance.IsVisible) _instance.Show();
        }
        else
        {
            if (_instance is not null)
            {
                _instance.Close();
                _instance = null;
            }
        }
    }
}
