using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PowerAim.Config;
using PowerAim.InputLogic.Mapping;

namespace PowerAim.UILibrary;

/// <summary>
///     A reasonably faithful top-down Xbox 360 controller diagram. Each interactive zone is a
///     <see cref="Border"/> (hit-test friendly) overlaid on a hand-drawn silhouette. The host
///     subscribes to <see cref="HotspotClicked"/> to learn which logical input the user clicked.
///     <para>
///     Layout follows the real controller:
///     <list type="bullet">
///       <item>Top: LB/RB bumpers + LT/RT triggers (drawn as a curved bar above)</item>
///       <item>Upper-left lobe: Left Stick (LS)</item>
///       <item>Lower-left lobe: D-Pad</item>
///       <item>Centre: Back, Guide (Xbox), Start</item>
///       <item>Upper-right lobe: ABXY diamond (Y top, X left, B right, A bottom)</item>
///       <item>Lower-right lobe: Right Stick (RS)</item>
///     </list>
///     The hit-zones around each stick are 4 small directional pills so the user can pick
///     LeftStickUp/Down/Left/Right etc. as logical sources without us having to invent a
///     drag-affordance.
///     </para>
/// </summary>
public partial class Xbox360ControllerCanvas : UserControl
{
    public record Hotspot(MappingInputKind Kind, int Code, string Label);

    /// <summary>Fired when the user clicks any hotspot on the controller diagram.</summary>
    public event EventHandler<Hotspot>? HotspotClicked;

    private readonly Dictionary<(MappingInputKind, int), Border> _hitboxes = new();

    public Xbox360ControllerCanvas()
    {
        InitializeComponent();
        Loaded += (_, _) => Build();
    }

    /// <summary>
    ///     Force a re-highlight of all hotspots based on a "is this code currently bound?" predicate
    ///     (used by the editor to dim or accent buttons that already have a mapping).
    /// </summary>
    public void RefreshHighlights(Func<MappingInputKind, int, bool> isMapped, MappingInputKind? armedKind, int armedCode)
    {
        var accent = TryFindResource("FluentAccent") as Brush ?? Brushes.MediumPurple;
        var stroke = TryFindResource("FluentStroke") as Brush ?? Brushes.DimGray;
        var mappedBg = new SolidColorBrush(Color.FromArgb(70,
            (accent as SolidColorBrush)?.Color.R ?? 139,
            (accent as SolidColorBrush)?.Color.G ?? 92,
            (accent as SolidColorBrush)?.Color.B ?? 246));
        foreach (var kv in _hitboxes)
        {
            bool armed = armedKind.HasValue && armedKind.Value == kv.Key.Item1 && armedCode == kv.Key.Item2;
            bool mapped = isMapped(kv.Key.Item1, kv.Key.Item2);
            if (armed)
            {
                kv.Value.BorderBrush = accent;
                kv.Value.BorderThickness = new Thickness(2);
                kv.Value.Background = mappedBg;
            }
            else if (mapped)
            {
                kv.Value.BorderBrush = accent;
                kv.Value.BorderThickness = new Thickness(1);
                kv.Value.Background = mappedBg;
            }
            else
            {
                kv.Value.BorderBrush = stroke;
                kv.Value.BorderThickness = new Thickness(1);
                kv.Value.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
            }
        }
    }

    private void Build()
    {
        if (_hitboxes.Count > 0) return; // already built
        HostCanvas.Children.Clear();
        _hitboxes.Clear();

        // ---- Body silhouette ---------------------------------------------------------
        // A wide rounded body with two lobes — characteristic Xbox 360 outline.
        var body = new Path
        {
            Data = Geometry.Parse(
                "M 70,170 " +
                "Q 70,70 220,68 " +    // upper-left lobe arc
                "L 500,68 " +          // top of crossbar
                "Q 650,70 650,170 " +  // upper-right arc
                "Q 650,300 555,300 " + // right hand grip lobe
                "Q 480,300 460,250 " + // back to crossbar
                "L 260,250 " +
                "Q 240,300 165,300 " + // left hand grip lobe
                "Q 70,300 70,170 Z"),
            StrokeThickness = 2,
        };
        body.SetResourceReference(Shape.StrokeProperty, "FluentStroke");
        body.SetResourceReference(Shape.FillProperty, "FluentSurface3");
        HostCanvas.Children.Add(body);

        // Top curved bar that visually houses the triggers (decoration only).
        var trigBar = new Path
        {
            Data = Geometry.Parse("M 130,28 Q 360,4 590,28 L 580,52 Q 360,32 140,52 Z"),
            StrokeThickness = 1,
        };
        trigBar.SetResourceReference(Shape.StrokeProperty, "FluentStroke");
        trigBar.SetResourceReference(Shape.FillProperty, "FluentSurface2");
        HostCanvas.Children.Add(trigBar);

        // ---- Triggers (LT / RT) -----------------------------------------------------
        AddRect("LT", 138, 8,  80, 30, MappingInputKind.GamepadTrigger, 0);
        AddRect("RT", 502, 8,  80, 30, MappingInputKind.GamepadTrigger, 1);

        // ---- Bumpers (LB / RB) ------------------------------------------------------
        AddRoundRect("LB", 138, 56, 110, 24, 10, MappingInputKind.GamepadButton, (int)XboxButtonId.LeftShoulder);
        AddRoundRect("RB", 472, 56, 110, 24, 10, MappingInputKind.GamepadButton, (int)XboxButtonId.RightShoulder);

        // ---- Left Stick (upper-left lobe) -------------------------------------------
        AddCircle("LS", 175, 145, 64, MappingInputKind.GamepadButton, (int)XboxButtonId.LeftThumb);
        AddDirectionPads(stickCx: 175, stickCy: 145, stickRadius: 32,
            up: GamepadStickDirection.LeftStickUp,
            down: GamepadStickDirection.LeftStickDown,
            left: GamepadStickDirection.LeftStickLeft,
            right: GamepadStickDirection.LeftStickRight);

        // ---- D-Pad (lower-left lobe) ------------------------------------------------
        // Plus-shaped: up/down/left/right small squares centred at (280,205).
        AddRoundRect("▲", 264, 175, 32, 24, 4, MappingInputKind.GamepadButton, (int)XboxButtonId.Up);
        AddRoundRect("▼", 264, 215, 32, 24, 4, MappingInputKind.GamepadButton, (int)XboxButtonId.Down);
        AddRoundRect("◀", 232, 195, 28, 24, 4, MappingInputKind.GamepadButton, (int)XboxButtonId.Left);
        AddRoundRect("▶", 300, 195, 28, 24, 4, MappingInputKind.GamepadButton, (int)XboxButtonId.Right);

        // ---- Centre row: Back · Guide · Start ---------------------------------------
        AddRoundRect("Back",  295, 130, 50, 22, 11, MappingInputKind.GamepadButton, (int)XboxButtonId.Back);
        // Guide button — decorative round badge, not mappable as a logical source (XInput doesn't expose it cleanly).
        var guide = new Border
        {
            Width = 38, Height = 38,
            CornerRadius = new CornerRadius(19),
            BorderThickness = new Thickness(2),
        };
        guide.SetResourceReference(Border.BorderBrushProperty, "FluentAccent");
        guide.SetResourceReference(Border.BackgroundProperty, "FluentSurface1");
        var glog = new TextBlock
        {
            Text = "X",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Display"),
            FontWeight = FontWeights.Bold,
            FontSize = 16,
        };
        glog.SetResourceReference(TextBlock.ForegroundProperty, "FluentAccent");
        guide.Child = glog;
        Canvas.SetLeft(guide, 341);
        Canvas.SetTop(guide, 122);
        HostCanvas.Children.Add(guide);
        AddRoundRect("Start", 393, 130, 50, 22, 11, MappingInputKind.GamepadButton, (int)XboxButtonId.Start);

        // ---- ABXY diamond (upper-right lobe) ----------------------------------------
        // Y top, X left, B right, A bottom — centred at (540,145).
        AddColorCircle("Y", 540, 100, 36, "#7BC242", MappingInputKind.GamepadButton, (int)XboxButtonId.Y);
        AddColorCircle("X", 496, 145, 36, "#3B82F6", MappingInputKind.GamepadButton, (int)XboxButtonId.X);
        AddColorCircle("B", 584, 145, 36, "#EF4444", MappingInputKind.GamepadButton, (int)XboxButtonId.B);
        AddColorCircle("A", 540, 190, 36, "#F59E0B", MappingInputKind.GamepadButton, (int)XboxButtonId.A);

        // ---- Right Stick (lower-right lobe) -----------------------------------------
        AddCircle("RS", 445, 215, 60, MappingInputKind.GamepadButton, (int)XboxButtonId.RightThumb);
        AddDirectionPads(stickCx: 445, stickCy: 215, stickRadius: 30,
            up: GamepadStickDirection.RightStickUp,
            down: GamepadStickDirection.RightStickDown,
            left: GamepadStickDirection.RightStickLeft,
            right: GamepadStickDirection.RightStickRight);
    }

    private void AddRect(string label, double x, double y, double w, double h,
        MappingInputKind kind, int code)
        => AddRoundRect(label, x, y, w, h, 4, kind, code);

    private void AddRoundRect(string label, double x, double y, double w, double h, double radius,
        MappingInputKind kind, int code)
    {
        var box = new Border
        {
            Width = w, Height = h,
            CornerRadius = new CornerRadius(radius),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(2),
        };
        box.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        box.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
        var tb = new TextBlock
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 11,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        box.Child = tb;
        Canvas.SetLeft(box, x);
        Canvas.SetTop(box, y);
        box.MouseLeftButtonDown += (_, e) =>
        {
            HotspotClicked?.Invoke(this, new Hotspot(kind, code, label));
            e.Handled = true;
        };
        HostCanvas.Children.Add(box);
        _hitboxes[(kind, code)] = box;
    }

    private void AddCircle(string label, double cx, double cy, double diameter,
        MappingInputKind kind, int code)
    {
        var box = new Border
        {
            Width = diameter, Height = diameter,
            CornerRadius = new CornerRadius(diameter / 2),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
        };
        box.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        box.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
        var tb = new TextBlock
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 12, FontWeight = FontWeights.SemiBold,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        box.Child = tb;
        Canvas.SetLeft(box, cx - diameter / 2);
        Canvas.SetTop(box, cy - diameter / 2);
        box.MouseLeftButtonDown += (_, e) =>
        {
            HotspotClicked?.Invoke(this, new Hotspot(kind, code, label));
            e.Handled = true;
        };
        HostCanvas.Children.Add(box);
        _hitboxes[(kind, code)] = box;
    }

    /// <summary>Coloured face-button circle (Y green / X blue / B red / A yellow).</summary>
    private void AddColorCircle(string label, double cx, double cy, double diameter, string hex,
        MappingInputKind kind, int code)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        var box = new Border
        {
            Width = diameter, Height = diameter,
            CornerRadius = new CornerRadius(diameter / 2),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Background = new SolidColorBrush(Color.FromArgb(80, c.R, c.G, c.B)),
            BorderBrush = new SolidColorBrush(c),
        };
        var tb = new TextBlock
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Display"),
            FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(c),
        };
        box.Child = tb;
        Canvas.SetLeft(box, cx - diameter / 2);
        Canvas.SetTop(box, cy - diameter / 2);
        box.MouseLeftButtonDown += (_, e) =>
        {
            HotspotClicked?.Invoke(this, new Hotspot(kind, code, label));
            e.Handled = true;
        };
        HostCanvas.Children.Add(box);
        _hitboxes[(kind, code)] = box;
    }

    /// <summary>Four arrow hotspots around a stick centre to select stick directions.</summary>
    private void AddDirectionPads(double stickCx, double stickCy, double stickRadius,
        GamepadStickDirection up, GamepadStickDirection down,
        GamepadStickDirection left, GamepadStickDirection right)
    {
        AddRoundRect("↑", stickCx - 14, stickCy - stickRadius - 22, 28, 18, 4, MappingInputKind.GamepadStickDirection, (int)up);
        AddRoundRect("↓", stickCx - 14, stickCy + stickRadius + 4,  28, 18, 4, MappingInputKind.GamepadStickDirection, (int)down);
        AddRoundRect("←", stickCx - stickRadius - 22, stickCy - 11, 18, 22, 4, MappingInputKind.GamepadStickDirection, (int)left);
        AddRoundRect("→", stickCx + stickRadius + 4,  stickCy - 11, 18, 22, 4, MappingInputKind.GamepadStickDirection, (int)right);
    }
}
