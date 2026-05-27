using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PowerAim.UILibrary;

/// <summary>
///     A self-contained Fluent HSV colour picker: a saturation/value plane, a hue bar, an alpha
///     slider, a hex field and an optional row of preset swatches. It is intentionally free of any
///     WinForms dependency (the old <c>System.Windows.Forms.ColorDialog</c>) so it can live inside a
///     <see cref="System.Windows.Controls.Primitives.Popup"/> and update its host live as you drag.
/// </summary>
public partial class ColorPickerControl : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
        nameof(SelectedColor), typeof(Color), typeof(ColorPickerControl),
        new FrameworkPropertyMetadata(Colors.Magenta,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

    public static readonly DependencyProperty ShowAlphaProperty = DependencyProperty.Register(
        nameof(ShowAlpha), typeof(bool), typeof(ColorPickerControl),
        new PropertyMetadata(true, OnShowAlphaChanged));

    private double _h;   // 0..360
    private double _s;   // 0..1
    private double _v;   // 0..1
    private double _a = 1.0; // 0..1
    private bool _suppress;

    /// <summary>Raised on every change (drag, hex edit, swatch) so a host can apply live.</summary>
    public event EventHandler<Color>? ColorChanged;

    public ColorPickerControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyFromColor(SelectedColor);
            RefreshVisuals();
        };
        SizeChanged += (_, _) => RefreshThumbs();
    }

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public bool ShowAlpha
    {
        get => (bool)GetValue(ShowAlphaProperty);
        set => SetValue(ShowAlphaProperty, value);
    }

    /// <summary>Preset colours rendered as quick-fill swatches below the picker.</summary>
    public void SetSwatches(IEnumerable<Color> colors) => SwatchList.ItemsSource = new List<Color>(colors);

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (ColorPickerControl)d;
        if (picker._suppress) return;
        picker.ApplyFromColor((Color)e.NewValue);
        picker.RefreshVisuals();
    }

    private static void OnShowAlphaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (ColorPickerControl)d;
        picker.AlphaBar.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- Interaction -----------------------------------------------------------------------

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.LeftButton != MouseButtonState.Pressed || _dragTarget is null) return;
        UpdateFromPointer(_dragTarget, e.GetPosition(_dragTarget));
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragTarget is null) return;
        _dragTarget = null;
        ReleaseMouseCapture();
    }

    private FrameworkElement? _dragTarget;

    private void BeginDrag(FrameworkElement target, MouseButtonEventArgs e)
    {
        _dragTarget = target;
        CaptureMouse();
        UpdateFromPointer(target, e.GetPosition(target));
        e.Handled = true;
    }

    private void UpdateFromPointer(FrameworkElement target, Point p)
    {
        if (ReferenceEquals(target, SvBox))
        {
            _s = Clamp01(p.X / Math.Max(1, SvBox.ActualWidth));
            _v = 1.0 - Clamp01(p.Y / Math.Max(1, SvBox.ActualHeight));
        }
        else if (ReferenceEquals(target, HueBar))
        {
            _h = Clamp01(p.Y / Math.Max(1, HueBar.ActualHeight)) * 360.0;
        }
        else if (ReferenceEquals(target, AlphaBar))
        {
            _a = Clamp01(p.X / Math.Max(1, AlphaBar.ActualWidth));
        }

        CommitColor();
        RefreshVisuals();
    }

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyHex();
    }

    private void HexBox_LostFocus(object sender, RoutedEventArgs e) => ApplyHex();

    private void ApplyHex()
    {
        if (TryParseHex(HexBox.Text, out var c))
        {
            ApplyFromColor(c);
            CommitColor();
            RefreshVisuals();
        }
        else
        {
            // revert the text to the current value
            HexBox.Text = ToHex(SelectedColor);
        }
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Color c })
        {
            ApplyFromColor(ShowAlpha ? c : Color.FromArgb(255, c.R, c.G, c.B));
            CommitColor();
            RefreshVisuals();
        }
    }

    // ---- Color plumbing --------------------------------------------------------------------

    private void CommitColor()
    {
        var color = CurrentColor();
        _suppress = true;
        SelectedColor = color;
        _suppress = false;
        ColorChanged?.Invoke(this, color);
    }

    private Color CurrentColor()
    {
        var (r, g, b) = HsvToRgb(_h, _s, _v);
        byte a = ShowAlpha ? (byte)Math.Round(_a * 255.0) : (byte)255;
        return Color.FromArgb(a, r, g, b);
    }

    private void ApplyFromColor(Color c)
    {
        (_h, _s, _v) = RgbToHsv(c.R, c.G, c.B);
        _a = ShowAlpha ? c.A / 255.0 : 1.0;
    }

    private void RefreshVisuals()
    {
        var (hr, hg, hb) = HsvToRgb(_h, 1, 1);
        var hueColor = Color.FromRgb(hr, hg, hb);
        SvHueFill.Fill = new SolidColorBrush(hueColor);

        var current = CurrentColor();
        var opaque = Color.FromRgb(current.R, current.G, current.B);
        PreviewFill.Fill = new SolidColorBrush(current);
        AlphaGrad.Fill = new LinearGradientBrush(Color.FromArgb(0, opaque.R, opaque.G, opaque.B), opaque, 0);
        ((SolidColorBrush)SvThumb.Fill).Color = opaque;

        if (!HexBox.IsFocused) HexBox.Text = ToHex(current);
        RefreshThumbs();
    }

    private void RefreshThumbs()
    {
        if (SvBox.ActualWidth > 0)
        {
            Canvas.SetLeft(SvThumb, _s * SvBox.ActualWidth - SvThumb.Width / 2);
            Canvas.SetTop(SvThumb, (1 - _v) * SvBox.ActualHeight - SvThumb.Height / 2);
        }

        if (HueBar.ActualHeight > 0)
            Canvas.SetTop(HueThumb, _h / 360.0 * HueBar.ActualHeight - HueThumb.Height / 2);

        if (AlphaBar.ActualWidth > 0)
            Canvas.SetLeft(AlphaThumb, _a * AlphaBar.ActualWidth - AlphaThumb.Width / 2);
    }

    private void SvBox_MouseDown(object sender, MouseButtonEventArgs e) => BeginDrag(SvBox, e);
    private void HueBar_MouseDown(object sender, MouseButtonEventArgs e) => BeginDrag(HueBar, e);
    private void AlphaBar_MouseDown(object sender, MouseButtonEventArgs e) => BeginDrag(AlphaBar, e);

    // ---- Static helpers --------------------------------------------------------------------

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

    public static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    public static bool TryParseHex(string? text, out Color color)
    {
        color = Colors.Magenta;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().TrimStart('#');
        try
        {
            switch (s.Length)
            {
                case 6:
                    color = Color.FromRgb(Hex(s, 0), Hex(s, 2), Hex(s, 4));
                    return true;
                case 8:
                    color = Color.FromArgb(Hex(s, 0), Hex(s, 2), Hex(s, 4), Hex(s, 6));
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }

        static byte Hex(string str, int i) => byte.Parse(str.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    public static (double h, double s, double v) RgbToHsv(byte r, byte g, byte b)
    {
        double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double d = max - min;
        double h = 0;
        if (d > 0)
        {
            if (max == rd) h = ((gd - bd) / d) % 6;
            else if (max == gd) h = (bd - rd) / d + 2;
            else h = (rd - gd) / d + 4;
            h *= 60;
            if (h < 0) h += 360;
        }

        double s = max <= 0 ? 0 : d / max;
        return (h, s, max);
    }

    public static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
        double m = v - c;
        double r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; }
        else if (h < 120) { r = x; g = c; }
        else if (h < 180) { g = c; b = x; }
        else if (h < 240) { g = x; b = c; }
        else if (h < 300) { r = x; b = c; }
        else { r = c; b = x; }

        return ((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
