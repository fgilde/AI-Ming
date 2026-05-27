using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PowerAim.Config;
using PowerAim.InputLogic;

namespace PowerAim.UILibrary;

/// <summary>
///     Live visualization of the input PowerAim is sending: a compact keyboard + mouse diagram on
///     the left and the reused <see cref="Xbox360ControllerCanvas"/> on the right. Keys / buttons
///     glow while held (and briefly after a tap), triggers fill, sticks light their direction, and
///     the mouse shows a short aim-direction arrow. Fed by <see cref="InputEventBus"/>.
/// </summary>
public partial class InputVisualizerPanel : UserControl
{
    private const double Threshold = 0.35;     // stick deflection that counts as a direction
    private const int FlashMs = 170;           // residual glow after a tap / release
    private const int StaleMs = 220;           // axis/trigger/move values considered live within

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly object _lock = new();

    // (held, flashUntil) per code
    private readonly Dictionary<int, (bool held, DateTime flash)> _keyState = new();
    private readonly Dictionary<int, (bool held, DateTime flash)> _mouseState = new();
    private readonly Dictionary<int, (bool held, DateTime flash)> _padState = new();
    private readonly double[] _trigger = new double[2];
    private readonly DateTime[] _triggerStamp = new DateTime[2];
    private readonly double[] _axis = new double[4];
    private DateTime _axisStamp;
    private double _moveDx, _moveDy;
    private DateTime _moveStamp;

    // Visual caps
    private readonly List<(Border cap, int[] vks)> _keyCaps = new();
    private Border? _mouseL, _mouseR, _mouseM;
    private System.Windows.Shapes.Line? _moveArrow;
    private double _mouseCx, _mouseCy;

    private SolidColorBrush _accent = new(Color.FromRgb(0x8B, 0x5C, 0xF6));
    private SolidColorBrush _accentFaint = new(Color.FromArgb(80, 0x8B, 0x5C, 0xF6));

    public InputVisualizerPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _timer.Tick += OnTick;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (TryFindResource("FluentAccent") is SolidColorBrush a)
        {
            _accent = a;
            _accentFaint = new SolidColorBrush(Color.FromArgb(80, a.Color.R, a.Color.G, a.Color.B));
        }

        if (_keyCaps.Count == 0) BuildKeyboard();

        InputEventBus.Sent += OnInputSent;
        InputEventBus.Enabled = true;
        _timer.Start();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _timer.Stop();
        InputEventBus.Sent -= OnInputSent;
        InputEventBus.Enabled = false;
    }

    // ---- Event intake (any thread) ---------------------------------------------------------

    private void OnInputSent(InputEvent e)
    {
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            switch (e.Channel)
            {
                case InputChannel.Key: Upsert(_keyState, e.Code, e.Down, now); break;
                case InputChannel.MouseButton: Upsert(_mouseState, e.Code, e.Down, now); break;
                case InputChannel.GamepadButton: if (e.Code >= 0) Upsert(_padState, e.Code, e.Down, now); break;
                case InputChannel.GamepadTrigger:
                    if (e.Code is 0 or 1) { _trigger[e.Code] = e.X; _triggerStamp[e.Code] = now; }
                    break;
                case InputChannel.GamepadAxis:
                    if (e.Code is >= 0 and < 4) { _axis[e.Code] = e.X; _axisStamp = now; }
                    break;
                case InputChannel.MouseMove:
                    _moveDx = e.X; _moveDy = e.Y; _moveStamp = now;
                    break;
            }
        }
    }

    private static void Upsert(Dictionary<int, (bool held, DateTime flash)> map, int code, bool down, DateTime now)
        => map[code] = down ? (true, now) : (false, now.AddMilliseconds(FlashMs));

    private static bool IsLit(Dictionary<int, (bool held, DateTime flash)> map, int code, DateTime now)
        => map.TryGetValue(code, out var s) && (s.held || now < s.flash);

    // ---- Render (UI thread) ----------------------------------------------------------------

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;

        // Snapshot the bits we need under the lock; render outside it.
        double[] axis; double[] trig; DateTime axisStamp; DateTime[] trigStamp;
        double mdx, mdy; DateTime moveStamp;
        lock (_lock)
        {
            axis = (double[])_axis.Clone();
            trig = (double[])_trigger.Clone();
            trigStamp = (DateTime[])_triggerStamp.Clone();
            axisStamp = _axisStamp;
            mdx = _moveDx; mdy = _moveDy; moveStamp = _moveStamp;

            foreach (var (cap, vks) in _keyCaps)
                SetLit(cap, vks.Any(vk => IsLit(_keyState, vk, now)));

            if (_mouseL != null) SetLit(_mouseL, IsLit(_mouseState, 0, now));
            if (_mouseR != null) SetLit(_mouseR, IsLit(_mouseState, 1, now));
            if (_mouseM != null) SetLit(_mouseM, IsLit(_mouseState, 2, now));

            Pad.RefreshHighlights((kind, code) => IsPadActive(kind, code, axis, trig, trigStamp, axisStamp, now), null, 0);
        }

        // Mouse aim-direction arrow
        bool moveLive = (now - moveStamp).TotalMilliseconds < StaleMs && (Math.Abs(mdx) > 1 || Math.Abs(mdy) > 1);
        if (_moveArrow != null)
        {
            if (moveLive)
            {
                double len = Math.Min(22, Math.Sqrt(mdx * mdx + mdy * mdy) / 8.0 + 6);
                double mag = Math.Sqrt(mdx * mdx + mdy * mdy);
                double nx = mag > 0 ? mdx / mag : 0, ny = mag > 0 ? mdy / mag : 0;
                _moveArrow.X1 = _mouseCx; _moveArrow.Y1 = _mouseCy;
                _moveArrow.X2 = _mouseCx + nx * len; _moveArrow.Y2 = _mouseCy + ny * len;
                _moveArrow.Stroke = _accent;
                _moveArrow.Visibility = Visibility.Visible;
            }
            else
            {
                _moveArrow.Visibility = Visibility.Collapsed;
            }
        }
    }

    private bool IsPadActive(MappingInputKind kind, int code, double[] axis, double[] trig,
        DateTime[] trigStamp, DateTime axisStamp, DateTime now)
    {
        switch (kind)
        {
            case MappingInputKind.GamepadButton:
                return IsLit(_padState, code, now);
            case MappingInputKind.GamepadTrigger:
                return code is 0 or 1 && (now - trigStamp[code]).TotalMilliseconds < StaleMs && trig[code] > 0.05;
            case MappingInputKind.GamepadStickDirection:
                if ((now - axisStamp).TotalMilliseconds >= StaleMs) return false;
                return code switch
                {
                    0 => axis[1] > Threshold,   // LeftStickUp   (+Y up)
                    1 => axis[1] < -Threshold,  // LeftStickDown
                    2 => axis[0] < -Threshold,  // LeftStickLeft
                    3 => axis[0] > Threshold,   // LeftStickRight
                    4 => axis[3] > Threshold,   // RightStickUp
                    5 => axis[3] < -Threshold,  // RightStickDown
                    6 => axis[2] < -Threshold,  // RightStickLeft
                    7 => axis[2] > Threshold,   // RightStickRight
                    _ => false
                };
            default:
                return false;
        }
    }

    private void SetLit(Border cap, bool lit)
    {
        if (lit)
        {
            cap.BorderBrush = _accent;
            cap.Background = _accentFaint;
        }
        else
        {
            cap.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
            cap.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
        }
    }

    // ---- Keyboard / mouse diagram construction --------------------------------------------

    private void BuildKeyboard()
    {
        // Win32 VK codes (System.Windows.Forms.Keys values).
        const int Tab = 9, Q = 81, W = 87, E = 69, R = 82, D1 = 49, D2 = 50, D3 = 51;
        const int A = 65, S = 83, D = 68, F = 70, Space = 32, Z = 90, X = 88, C = 67, V = 86;
        const int Up = 38, Down = 40, Left = 37, Right = 39;
        int[] shift = [16, 160, 161];
        int[] ctrl = [17, 162, 163];

        AddCap("Tab", 0, 0, 44, Tab);
        AddCap("Q", 48, 0, 30, Q);
        AddCap("W", 82, 0, 30, W);
        AddCap("E", 116, 0, 30, E);
        AddCap("R", 150, 0, 30, R);
        AddCap("1", 192, 0, 26, D1);
        AddCap("2", 222, 0, 26, D2);
        AddCap("3", 252, 0, 26, D3);

        AddCap("Shift", 0, 30, 50, shift);
        AddCap("A", 54, 30, 30, A);
        AddCap("S", 88, 30, 30, S);
        AddCap("D", 122, 30, 30, D);
        AddCap("F", 156, 30, 30, F);

        AddCap("Ctrl", 0, 60, 44, ctrl);
        AddCap("Z", 48, 60, 30, Z);
        AddCap("X", 82, 60, 30, X);
        AddCap("C", 116, 60, 30, C);
        AddCap("V", 150, 60, 30, V);
        AddCap("Space", 48, 92, 132, Space);

        // Arrow cluster
        AddCap("↑", 116, 124, 30, Up);
        AddCap("←", 82, 152, 30, Left);
        AddCap("↓", 116, 152, 30, Down);
        AddCap("→", 150, 152, 30, Right);

        BuildMouse(222, 64);
    }

    private void AddCap(string label, double x, double y, double w, params int[] vks)
    {
        var cap = new Border
        {
            Width = w,
            Height = 26,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1)
        };
        cap.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        cap.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
        var tb = new TextBlock
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 11
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        cap.Child = tb;
        Canvas.SetLeft(cap, x);
        Canvas.SetTop(cap, y);
        KbCanvas.Children.Add(cap);
        _keyCaps.Add((cap, vks));
    }

    private void BuildMouse(double x, double y)
    {
        double bodyW = 56, bodyH = 92;
        var body = new Border
        {
            Width = bodyW,
            Height = bodyH,
            CornerRadius = new CornerRadius(26, 26, 22, 22),
            BorderThickness = new Thickness(1)
        };
        body.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        body.SetResourceReference(Border.BackgroundProperty, "FluentSurface3");
        Canvas.SetLeft(body, x);
        Canvas.SetTop(body, y);
        KbCanvas.Children.Add(body);

        _mouseL = MouseSegment(x + 2, y + 2, bodyW / 2 - 3, 34, new CornerRadius(24, 0, 0, 0));
        _mouseR = MouseSegment(x + bodyW / 2 + 1, y + 2, bodyW / 2 - 3, 34, new CornerRadius(0, 24, 0, 0));
        _mouseM = MouseSegment(x + bodyW / 2 - 5, y + 6, 10, 22, new CornerRadius(5));

        _mouseCx = x + bodyW / 2;
        _mouseCy = y + bodyH / 2 + 6;
        _moveArrow = new System.Windows.Shapes.Line { StrokeThickness = 3, StrokeEndLineCap = PenLineCap.Round, Visibility = Visibility.Collapsed };
        KbCanvas.Children.Add(_moveArrow);
    }

    private Border MouseSegment(double x, double y, double w, double h, CornerRadius radius)
    {
        var seg = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = radius,
            BorderThickness = new Thickness(1)
        };
        seg.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        seg.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
        Canvas.SetLeft(seg, x);
        Canvas.SetTop(seg, y);
        KbCanvas.Children.Add(seg);
        return seg;
    }
}
