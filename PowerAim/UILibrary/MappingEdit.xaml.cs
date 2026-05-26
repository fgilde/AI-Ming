using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic.Mapping;
// System.Windows.Forms shadows several WPF types — pin the WPF ones.
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Cursors = System.Windows.Input.Cursors;

namespace PowerAim.UILibrary;

/// <summary>
///     Full mapping editor that mirrors the TriggerEdit pattern: a UserControl bound to a single
///     <see cref="ControllerMappingProfile"/> via the <see cref="Profile"/> DependencyProperty.
///     Hosts the new Xbox-360 controller visual, a QWERTY keyboard + mouse strip, stick-tuning
///     sliders, and a mappings list (with activator picker per row).
/// </summary>
public partial class MappingEdit : UserControl
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile), typeof(ControllerMappingProfile), typeof(MappingEdit),
        new PropertyMetadata(null, OnProfileChanged));

    /// <summary>The profile being edited. Set this once when opening the editor.</summary>
    public ControllerMappingProfile? Profile
    {
        get => (ControllerMappingProfile?)GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    private (MappingInputKind kind, int code, string label)? _armed;
    private readonly Dictionary<(MappingInputKind, int), Border> _keyboardHitboxes = new();
    private readonly HashSet<(MappingInputKind, int)> _mappedSources = new();
    private readonly HashSet<(MappingInputKind, int)> _mappedTargets = new();

    private NotifyCollectionChangedEventHandler? _mappingsHandler;

    public MappingEdit()
    {
        InitializeComponent();
        DataContext = this;

        BuildStickSettings();
        BuildKeyboardGrid();

        Controller.HotspotClicked += (_, hot) => OnDiagramClicked(hot.Kind, hot.Code, hot.Label);

        Loaded += (_, _) =>
        {
            if (Window.GetWindow(this) is Window w) w.PreviewKeyDown += OnWindowPreviewKeyDown;
            RebuildAll();
        };
        Unloaded += (_, _) =>
        {
            if (Window.GetWindow(this) is Window w) w.PreviewKeyDown -= OnWindowPreviewKeyDown;
        };
    }

    private static void OnProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MappingEdit me) return;
        if (e.OldValue is ControllerMappingProfile oldP && me._mappingsHandler != null)
        {
            oldP.Mappings.CollectionChanged -= me._mappingsHandler;
        }
        if (e.NewValue is ControllerMappingProfile newP)
        {
            me._mappingsHandler = (_, _) => me.Dispatcher.BeginInvoke(new Action(me.RebuildAll));
            newP.Mappings.CollectionChanged += me._mappingsHandler;
        }
        me.RebuildAll();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _armed != null)
        {
            _armed = null;
            RefreshHighlights();
            UpdateStatus();
            e.Handled = true;
        }
    }

    private void RebuildAll()
    {
        // Rebuild stick-setting bindings against the new profile.
        BuildStickSettings();
        RebuildMappingsList();
        RefreshHighlights();
        UpdateStatus();
    }

    // ============================================================================ STICK SETTINGS ====

    private void BuildStickSettings()
    {
        StickSettingsHost.Children.Clear();
        if (Profile == null) return;
        var p = Profile;

        StickSettingsHost.AddSlider("Mouse → Stick sensitivity", "× scale", 0.05, 0.05, 0.1, 5.0)
            .BindTo(() => p.MouseToStickSensitivity);

        StickSettingsHost.AddSlider("Stick → Mouse sensitivity", "px per tick", 1, 1, 1, 60)
            .BindTo(() => p.StickToMouseSensitivity);

        StickSettingsHost.AddSlider("Dead-zone", "× full deflection", 0.01, 0.01, 0.0, 0.45)
            .BindTo(() => p.StickDeadzone);

        StickSettingsHost.AddSlider("Anti-dead-zone", "× full deflection", 0.01, 0.01, 0.0, 0.45)
            .BindTo(() => p.StickAntiDeadzone);

        StickSettingsHost.AddSlider("Mouse response curve", "exponent", 0.05, 0.05, 0.8, 3.0)
            .BindTo(() => p.StickMouseExponent);

        // Invert-Y toggle row.
        var toggleRow = new System.Windows.Controls.CheckBox
        {
            Content = "Invert Y axis (stick ↔ mouse)",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
            IsChecked = p.InvertMouseY,
        };
        toggleRow.SetResourceReference(System.Windows.Controls.CheckBox.ForegroundProperty, "FluentTextPrimary");
        toggleRow.Checked   += (_, _) => p.InvertMouseY = true;
        toggleRow.Unchecked += (_, _) => p.InvertMouseY = false;
        StickSettingsHost.Children.Add(toggleRow);
    }

    // ============================================================================ KEYBOARD GRID ====

    /// <summary>Hand-tuned QWERTY layout. Each row is a list of (label, Keys-value, width-units).</summary>
    private static readonly (string label, Keys key, double widthUnits)[][] _kbRows = new[]
    {
        new[]
        {
            ("Esc", Keys.Escape, 1.0), ("F1", Keys.F1, 1.0), ("F2", Keys.F2, 1.0), ("F3", Keys.F3, 1.0),
            ("F4", Keys.F4, 1.0), ("F5", Keys.F5, 1.0), ("F6", Keys.F6, 1.0), ("F7", Keys.F7, 1.0),
            ("F8", Keys.F8, 1.0), ("F9", Keys.F9, 1.0), ("F10", Keys.F10, 1.0), ("F11", Keys.F11, 1.0),
            ("F12", Keys.F12, 1.0),
        },
        new[]
        {
            ("`", Keys.Oemtilde, 1.0), ("1", Keys.D1, 1.0), ("2", Keys.D2, 1.0), ("3", Keys.D3, 1.0),
            ("4", Keys.D4, 1.0), ("5", Keys.D5, 1.0), ("6", Keys.D6, 1.0), ("7", Keys.D7, 1.0),
            ("8", Keys.D8, 1.0), ("9", Keys.D9, 1.0), ("0", Keys.D0, 1.0),
            ("-", Keys.OemMinus, 1.0), ("=", Keys.Oemplus, 1.0), ("⌫", Keys.Back, 2.0),
        },
        new[]
        {
            ("Tab", Keys.Tab, 1.5),
            ("Q", Keys.Q, 1.0), ("W", Keys.W, 1.0), ("E", Keys.E, 1.0), ("R", Keys.R, 1.0),
            ("T", Keys.T, 1.0), ("Y", Keys.Y, 1.0), ("U", Keys.U, 1.0), ("I", Keys.I, 1.0),
            ("O", Keys.O, 1.0), ("P", Keys.P, 1.0), ("[", Keys.OemOpenBrackets, 1.0),
            ("]", Keys.OemCloseBrackets, 1.0), ("\\", Keys.OemPipe, 1.5),
        },
        new[]
        {
            ("Caps", Keys.CapsLock, 1.75),
            ("A", Keys.A, 1.0), ("S", Keys.S, 1.0), ("D", Keys.D, 1.0), ("F", Keys.F, 1.0),
            ("G", Keys.G, 1.0), ("H", Keys.H, 1.0), ("J", Keys.J, 1.0), ("K", Keys.K, 1.0),
            ("L", Keys.L, 1.0), (";", Keys.OemSemicolon, 1.0), ("'", Keys.OemQuotes, 1.0),
            ("Enter", Keys.Enter, 2.25),
        },
        new[]
        {
            ("Shift", Keys.ShiftKey, 2.25),
            ("Z", Keys.Z, 1.0), ("X", Keys.X, 1.0), ("C", Keys.C, 1.0), ("V", Keys.V, 1.0),
            ("B", Keys.B, 1.0), ("N", Keys.N, 1.0), ("M", Keys.M, 1.0),
            (",", Keys.Oemcomma, 1.0), (".", Keys.OemPeriod, 1.0), ("/", Keys.OemQuestion, 1.0),
            ("Shift", Keys.RShiftKey, 2.75),
        },
        new[]
        {
            ("Ctrl", Keys.ControlKey, 1.5), ("Win", Keys.LWin, 1.0), ("Alt", Keys.Menu, 1.5),
            ("Space", Keys.Space, 6.0),
            ("Alt", Keys.RMenu, 1.5), ("Menu", Keys.Apps, 1.0), ("Ctrl", Keys.RControlKey, 1.5),
        },
    };

    private void BuildKeyboardGrid()
    {
        KeyboardStack.Children.Clear();
        _keyboardHitboxes.Clear();
        const double unit = 38;
        foreach (var row in _kbRows)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            foreach (var (label, key, widthUnits) in row)
            {
                var captured = key;
                var box = MakeBox(label, unit * widthUnits - 4, unit - 4,
                    () => OnDiagramClicked(MappingInputKind.KeyboardKey, (int)captured, captured.ToString()));
                sp.Children.Add(box);
                _keyboardHitboxes[(MappingInputKind.KeyboardKey, (int)key)] = box;
            }
            KeyboardStack.Children.Add(sp);
        }

        // Mouse row.
        var mouseRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        AddMouse(mouseRow, "LMB", MouseButtons.Left);
        AddMouse(mouseRow, "RMB", MouseButtons.Right);
        AddMouse(mouseRow, "MMB", MouseButtons.Middle);
        AddMouse(mouseRow, "X1",  MouseButtons.XButton1);
        AddMouse(mouseRow, "X2",  MouseButtons.XButton2);
        AddMouse(mouseRow, "🖱  Motion", (MouseButtons)0xFFFF, widthUnits: 4);
        KeyboardStack.Children.Add(mouseRow);
    }

    private void AddMouse(StackPanel row, string label, MouseButtons btn, double widthUnits = 1.5)
    {
        const double unit = 38;
        var box = MakeBox(label, unit * widthUnits - 4, unit - 4,
            () => OnDiagramClicked(MappingInputKind.MouseButton, (int)btn, label));
        row.Children.Add(box);
        _keyboardHitboxes[(MappingInputKind.MouseButton, (int)btn)] = box;
    }

    private Border MakeBox(string label, double width, double height, Action onClick)
    {
        var box = new Border
        {
            Width = width, Height = height,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Margin = new Thickness(2),
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
        box.MouseLeftButtonDown += (_, e) => { onClick(); e.Handled = true; };
        return box;
    }

    // ============================================================================ INTERACTION ====

    private void OnDiagramClicked(MappingInputKind kind, int code, string label)
    {
        if (Profile == null) return;
        if (_armed == null)
        {
            _armed = (kind, code, label);
            RefreshHighlights();
            UpdateStatus();
            return;
        }

        var src = _armed.Value;
        bool srcIsGamepad = IsGamepadKind(src.kind);
        bool tgtIsGamepad = IsGamepadKind(kind);
        if (srcIsGamepad == tgtIsGamepad)
        {
            // Re-arm — clicking two of the same family is interpreted as "I meant THIS one".
            _armed = (kind, code, label);
            RefreshHighlights();
            UpdateStatus();
            return;
        }

        Profile.Mappings.Add(new InputMapping
        {
            SourceKind = src.kind, SourceCode = src.code,
            TargetKind = kind, TargetCode = code,
            Enabled = true,
            Activator = MappingActivator.Press,
        });
        _armed = null;
        // Rebuild will be triggered by CollectionChanged.
    }

    private static bool IsGamepadKind(MappingInputKind k)
        => k == MappingInputKind.GamepadButton
        || k == MappingInputKind.GamepadTrigger
        || k == MappingInputKind.GamepadStickDirection;

    private void UpdateStatus()
    {
        if (Profile == null) { StatusText.Text = "(no profile)"; return; }
        StatusText.Text = _armed == null
            ? $"{Profile.Mappings.Count} mappings · click a hotspot to arm a source."
            : $"Armed: {_armed.Value.label} — click the OTHER side to pair (ESC to cancel).";
    }

    private void RefreshHighlights()
    {
        _mappedSources.Clear();
        _mappedTargets.Clear();
        if (Profile != null)
        {
            foreach (var m in Profile.Mappings)
            {
                _mappedSources.Add((m.SourceKind, m.SourceCode));
                _mappedTargets.Add((m.TargetKind, m.TargetCode));
            }
        }
        var armedKind = _armed?.kind;
        var armedCode = _armed?.code ?? -1;
        Controller.RefreshHighlights((k, c) => _mappedSources.Contains((k, c)) || _mappedTargets.Contains((k, c)),
            armedKind, armedCode);

        var accent = TryFindResource("FluentAccent") as Brush ?? Brushes.MediumPurple;
        var stroke = TryFindResource("FluentStroke") as Brush ?? Brushes.DimGray;
        var mappedBg = new SolidColorBrush(Color.FromArgb(70,
            (accent as SolidColorBrush)?.Color.R ?? 139,
            (accent as SolidColorBrush)?.Color.G ?? 92,
            (accent as SolidColorBrush)?.Color.B ?? 246));
        foreach (var kv in _keyboardHitboxes)
        {
            bool armed = armedKind.HasValue && armedKind.Value == kv.Key.Item1 && armedCode == kv.Key.Item2;
            bool mapped = _mappedSources.Contains(kv.Key) || _mappedTargets.Contains(kv.Key);
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

    // ============================================================================ MAPPINGS LIST ====

    private void RebuildMappingsList()
    {
        MappingItems.Items.Clear();
        if (Profile == null || Profile.Mappings.Count == 0)
        {
            MappingEmpty.Visibility = Visibility.Visible;
            return;
        }
        MappingEmpty.Visibility = Visibility.Collapsed;
        foreach (var m in Profile.Mappings)
            MappingItems.Items.Add(BuildMappingRow(m));
    }

    private FrameworkElement BuildMappingRow(InputMapping m)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 0, 6),
        };
        border.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        border.SetResourceReference(Border.BackgroundProperty, "FluentSurface3");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // 0 — enabled checkbox
        var enabledBox = new System.Windows.Controls.CheckBox
        {
            IsChecked = m.Enabled,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        enabledBox.Checked   += (_, _) => m.Enabled = true;
        enabledBox.Unchecked += (_, _) => m.Enabled = false;
        Grid.SetColumn(enabledBox, 0);
        grid.Children.Add(enabledBox);

        // 1 — source → target label
        var src = LabelForCode(m.SourceKind, m.SourceCode);
        var tgt = LabelForCode(m.TargetKind, m.TargetCode);
        var txt = new TextBlock
        {
            Text = $"{src}    →    {tgt}",
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
        };
        txt.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        Grid.SetColumn(txt, 1);
        grid.Children.Add(txt);

        // 2 — activator picker (combobox)
        var act = new System.Windows.Controls.ComboBox
        {
            MinWidth = 130, MinHeight = 28,
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 12,
            ToolTip = "Press style: Press = while held, LongPress = after holding, DoubleTap = quick two presses, Toggle = latching, Pulse = brief fire-and-release.",
        };
        foreach (MappingActivator a in Enum.GetValues<MappingActivator>())
            act.Items.Add(a);
        act.SelectedItem = m.Activator;
        act.SelectionChanged += (_, _) =>
        {
            if (act.SelectedItem is MappingActivator a) m.Activator = a;
        };
        Grid.SetColumn(act, 2);
        grid.Children.Add(act);

        // 3 — remove button
        var rem = new Button
        {
            Content = "Remove",
            MinHeight = 28,
            Padding = new Thickness(10, 2, 10, 2),
        };
        rem.SetResourceReference(StyleProperty, "FluentStandardButton");
        rem.Click += (_, _) => Profile?.Mappings.Remove(m);
        Grid.SetColumn(rem, 3);
        grid.Children.Add(rem);

        border.Child = grid;
        return border;
    }

    private static string LabelForCode(MappingInputKind kind, int code) => kind switch
    {
        MappingInputKind.KeyboardKey       => $"⌨ {(Keys)code}",
        MappingInputKind.MouseButton       => code == 0xFFFF ? "🖱 Motion" : $"🖱 {(MouseButtons)code}",
        MappingInputKind.GamepadButton     => $"🎮 {((XboxButtonId)code)}",
        MappingInputKind.GamepadTrigger    => code == 0 ? "🎮 LT" : "🎮 RT",
        MappingInputKind.GamepadStickDirection => $"🎮 {((GamepadStickDirection)code)}",
        _ => "?",
    };

    // ============================================================================ STICK ↔ MOUSE SHORTCUTS ====

    private void AddStickToMouse_Click(object sender, RoutedEventArgs e)
    {
        if (Profile == null) return;
        // Sentinel mapping the engine recognises: source = RightStickRight, target = MouseButton 0xFFFF.
        if (Profile.Mappings.Any(m =>
            m.SourceKind == MappingInputKind.GamepadStickDirection
            && m.SourceCode == (int)GamepadStickDirection.RightStickRight
            && m.TargetKind == MappingInputKind.MouseButton
            && m.TargetCode == 0xFFFF))
        {
            return; // already exists
        }
        Profile.Mappings.Add(new InputMapping
        {
            SourceKind = MappingInputKind.GamepadStickDirection,
            SourceCode = (int)GamepadStickDirection.RightStickRight,
            TargetKind = MappingInputKind.MouseButton,
            TargetCode = 0xFFFF,
            Enabled = true,
        });
    }

    private void AddMouseToStick_Click(object sender, RoutedEventArgs e)
    {
        if (Profile == null) return;
        if (Profile.Mappings.Any(m =>
            m.SourceKind == MappingInputKind.MouseButton
            && m.SourceCode == 0xFFFF
            && m.TargetKind == MappingInputKind.GamepadStickDirection
            && m.TargetCode == (int)GamepadStickDirection.RightStickRight))
        {
            return; // already exists
        }
        Profile.Mappings.Add(new InputMapping
        {
            SourceKind = MappingInputKind.MouseButton,
            SourceCode = 0xFFFF,
            TargetKind = MappingInputKind.GamepadStickDirection,
            TargetCode = (int)GamepadStickDirection.RightStickRight,
            Enabled = true,
        });
    }
}
