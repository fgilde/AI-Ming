using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Forms;
using PowerAim.Config;
using PowerAim.InputLogic.Mapping;
using PowerAim.Extensions; // AddToggleWithKeyBind / BindTo extensions
// System.Windows.Forms shadows several WPF types with the same names; pin the WPF ones explicitly.
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using UserControl = System.Windows.Controls.UserControl;
using ComboBox = System.Windows.Controls.ComboBox;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using RadioButton = System.Windows.Controls.RadioButton;

namespace PowerAim.Visuality;

/// <summary>
///     Mapping editor. Two diagrams (Xbox controller + keyboard) plus a profile picker, FPS-preset
///     shortcut, runtime direction switch, and a list of current mappings. Click a button on
///     either diagram to "arm" it, then click the target on the other diagram — the mapping is
///     created. Removing happens through the list.
///     <para>
///     The engine (<see cref="MappingEngine"/>) reads from <see cref="AppConfig"/> directly and
///     hot-reloads when the active profile or its mappings change, so this UI is purely an
///     editor — no engine talking required from here.
///     </para>
/// </summary>
public partial class MappingPage : UserControl
{
    // Source-armed state — what the user clicked first and is waiting to pair with a target.
    private (MappingInputKind kind, int code, string label)? _armed;

    // Profile we're currently editing.
    private ControllerMappingProfile? _editing;

    // Mapped lookups so we can render "(mapped to X)" labels on the diagrams.
    private readonly Dictionary<(MappingInputKind, int), string> _sourceLabels = new();
    private readonly Dictionary<(MappingInputKind, int), string> _targetLabels = new();

    public MappingPage()
    {
        InitializeComponent();
        BuildProfileHeader();
        BuildActiveToggle(); // after ProfileHeader build because ProfileHeader.Clear() in BuildProfileHeader would wipe it
        BuildDirectionPicker();
        BuildControllerCanvas();
        BuildKeyboardGrid();
        SubscribeConfig();
        RefreshFromActiveProfile();
        Loaded += (_, _) =>
        {
            // KB-level ESC support to cancel an armed selection.
            if (Window.GetWindow(this) is Window w) w.PreviewKeyDown += OnWindowPreviewKeyDown;
        };
        Unloaded += (_, _) =>
        {
            if (Window.GetWindow(this) is Window w) w.PreviewKeyDown -= OnWindowPreviewKeyDown;
        };
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _armed != null)
        {
            _armed = null;
            RefreshDiagramHighlights();
            UpdateStatus();
        }
    }

    // ============================================================================ PROFILE HEADER ====

    private ComboBox? _profileCombo;
    private System.Windows.Controls.CheckBox? _enabledBox;
    private System.Windows.Controls.TextBox? _matchProcessBox;

    /// <summary>
    ///     Inject the master "Mapping active" toggle (with a keybind, like Global Active / Auto
    ///     Trigger / Anti Recoil) at the top of the profile header — so the user can hotkey
    ///     mapping on/off from inside the game. Uses the same AddToggleWithKeyBind helper as the
    ///     other master toggles so the binding-manager plumbing is identical.
    /// </summary>
    private void BuildActiveToggle()
    {
        // Reuse the existing global BindingManager — the same instance the rest of the app uses
        // so hotkeys collide-detect correctly across pages.
        var bm = MainWindow.Instance?.BindingManager;
        if (bm == null) return;

        var holder = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        var toggle = holder.AddToggleWithKeyBind("Mapping active", "MappingActive", bm);
        toggle.BindTo(() => AppConfig.Current.ToggleState.MappingActive);
        toggle.ToolTip = "Master switch for the controller↔keyboard mapping engine. Off = nothing fires. Bind a hotkey on the right to flip from inside any game.";
        // Stick this in ProfileHeader as the first child so it reads as the top-level switch.
        ProfileHeader.Children.Insert(0, holder);
    }

    /// <summary>
    ///     Combine both built-in FPS presets into a single profile so the user gets bindings for
    ///     both directions out of the box; the runtime direction picker decides which set fires.
    /// </summary>
    private static ControllerMappingProfile MergeFpsPresets()
    {
        var kbToPad = MappingPresets.NewFpsKbToPad();
        var padToKb = MappingPresets.NewFpsPadToKb();
        var combined = new ControllerMappingProfile
        {
            Name = "FPS preset",
            Enabled = true,
            MouseToStickSensitivity = kbToPad.MouseToStickSensitivity,
            StickToMouseSensitivity = padToKb.StickToMouseSensitivity,
        };
        foreach (var m in kbToPad.Mappings) combined.Mappings.Add(m);
        foreach (var m in padToKb.Mappings) combined.Mappings.Add(m);
        return combined;
    }

    private void BuildDirectionPicker()
    {
        DirectionStack.Children.Clear();
        var options = new (string label, MappingDirection dir, string tip)[]
        {
            ("Both ↔",                MappingDirection.Both,                 "Both directions fire — KB+M drives the virtual controller AND your physical controller drives KB+M. Use when you want everything wired up."),
            ("Keyboard → Controller", MappingDirection.KeyboardToController, "Only KB+M-sourced mappings fire. Use when you want to play a gamepad-only game with keyboard + mouse."),
            ("Controller → Keyboard", MappingDirection.ControllerToKeyboard, "Only controller-sourced mappings fire. Use when you want to play a KB+M-only game with a controller."),
        };
        foreach (var opt in options)
        {
            var b = new RadioButton
            {
                Content = opt.label,
                GroupName = "MappingDirection",
                Margin = new Thickness(0, 0, 14, 0),
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 13,
                IsChecked = AppConfig.Current?.MappingDirection == opt.dir,
                ToolTip = opt.tip,
                Tag = opt.dir,
            };
            b.SetResourceReference(RadioButton.ForegroundProperty, "FluentTextPrimary");
            b.Checked += (_, _) =>
            {
                if (b.Tag is MappingDirection d && AppConfig.Current != null)
                    AppConfig.Current.MappingDirection = d;
                UpdateStatus();
            };
            DirectionStack.Children.Add(b);
        }
    }

    private void BuildProfileHeader()
    {
        ProfileHeader.Children.Clear();
        // Row 1: profile combo + action buttons
        var row1 = new Grid();
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var profileLabel = new TextBlock
        {
            Text = "Profile",
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
            Margin = new Thickness(0, 0, 10, 0),
        };
        profileLabel.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");
        Grid.SetColumn(profileLabel, 0);
        row1.Children.Add(profileLabel);

        _profileCombo = new ComboBox
        {
            MinHeight = 34,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
        };
        _profileCombo.SelectionChanged += (_, _) => OnProfileSelected();
        Grid.SetColumn(_profileCombo, 1);
        row1.Children.Add(_profileCombo);

        var newBtn = new Button
        {
            Content = "+ New",
            MinHeight = 34,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(14, 4, 14, 4),
            ToolTip = "Create a blank profile.",
        };
        newBtn.SetResourceReference(StyleProperty, "FluentAccentButton");
        newBtn.Click += (_, _) => AddBlankProfile();
        Grid.SetColumn(newBtn, 2);
        row1.Children.Add(newBtn);

        // One FPS preset button that creates either kind based on direction. The user picks
        // direction before/after — both halves can coexist in the same profile thanks to the
        // runtime direction filter.
        var fpsBtn = new Button
        {
            Content = "+ FPS preset",
            MinHeight = 34,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(14, 4, 14, 4),
            ToolTip = "Adds a starter profile with the standard FPS layout (WASD ↔ Left Stick, mouse ↔ Right Stick, LMB/RMB ↔ RT/LT, etc.). Both directions populated — flip the Active direction picker to choose which side drives.",
        };
        fpsBtn.SetResourceReference(StyleProperty, "FluentStandardButton");
        fpsBtn.Click += (_, _) => AddPreset(MergeFpsPresets());
        Grid.SetColumn(fpsBtn, 3);
        row1.Children.Add(fpsBtn);

        ProfileHeader.Children.Add(row1);

        // Row 2: enabled, match-process, delete
        var row2 = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _enabledBox = new System.Windows.Controls.CheckBox
        {
            Content = "Profile active",
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
        };
        _enabledBox.SetResourceReference(System.Windows.Controls.CheckBox.ForegroundProperty, "FluentTextPrimary");
        _enabledBox.Checked   += (_, _) => { if (_editing != null) _editing.Enabled = true; UpdateStatus(); };
        _enabledBox.Unchecked += (_, _) => { if (_editing != null) _editing.Enabled = false; UpdateStatus(); };
        Grid.SetColumn(_enabledBox, 0);
        row2.Children.Add(_enabledBox);

        var mpStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 0, 0) };
        var mpLabel = new TextBlock
        {
            Text = "Match process (optional): ",
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Small"), FontSize = 12,
        };
        mpLabel.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");
        mpStack.Children.Add(mpLabel);

        _matchProcessBox = new System.Windows.Controls.TextBox
        {
            MinWidth = 240,
            MinHeight = 28,
            FontFamily = new FontFamily("Segoe UI Variable Text"), FontSize = 13,
        };
        _matchProcessBox.TextChanged += (_, _) =>
        {
            if (_editing != null) _editing.MatchProcess = _matchProcessBox.Text;
        };
        mpStack.Children.Add(_matchProcessBox);
        Grid.SetColumn(mpStack, 1);
        row2.Children.Add(mpStack);

        var delBtn = new Button
        {
            Content = "Delete profile",
            MinHeight = 28,
            Padding = new Thickness(10, 4, 10, 4),
        };
        delBtn.SetResourceReference(StyleProperty, "FluentStandardButton");
        delBtn.Click += (_, _) => DeleteCurrentProfile();
        Grid.SetColumn(delBtn, 2);
        row2.Children.Add(delBtn);

        ProfileHeader.Children.Add(row2);
    }

    private void SubscribeConfig()
    {
        var profiles = AppConfig.Current?.ControllerMappingProfiles;
        if (profiles == null) return;
        profiles.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(new Action(RefreshFromActiveProfile));
    }

    private void RefreshFromActiveProfile()
    {
        if (_profileCombo == null) return;
        var profiles = AppConfig.Current?.ControllerMappingProfiles;
        if (profiles == null) return;

        var keepSelection = _editing;
        _profileCombo.Items.Clear();
        foreach (var p in profiles)
            _profileCombo.Items.Add(p);
        _profileCombo.DisplayMemberPath = "Name";

        if (keepSelection != null && profiles.Contains(keepSelection))
            _profileCombo.SelectedItem = keepSelection;
        else if (profiles.Count > 0)
            _profileCombo.SelectedItem = profiles[0];
        else
            _editing = null;

        OnProfileSelected();
    }

    private void OnProfileSelected()
    {
        _editing = _profileCombo?.SelectedItem as ControllerMappingProfile;
        if (_enabledBox != null) _enabledBox.IsChecked = _editing?.Enabled ?? false;
        if (_matchProcessBox != null) _matchProcessBox.Text = _editing?.MatchProcess ?? "";
        RebuildMappingsList();
        RefreshDiagramHighlights();
        UpdateStatus();
    }

    private void AddBlankProfile()
    {
        var p = new ControllerMappingProfile { Name = $"Profile {AppConfig.Current.ControllerMappingProfiles.Count + 1}" };
        AppConfig.Current.ControllerMappingProfiles.Add(p);
        _editing = p;
        RefreshFromActiveProfile();
    }

    private void AddPreset(ControllerMappingProfile preset)
    {
        AppConfig.Current.ControllerMappingProfiles.Add(preset);
        _editing = preset;
        RefreshFromActiveProfile();
    }

    private void DeleteCurrentProfile()
    {
        if (_editing == null) return;
        AppConfig.Current.ControllerMappingProfiles.Remove(_editing);
        _editing = null;
        RefreshFromActiveProfile();
    }

    // ============================================================================ STATUS ====

    private void UpdateStatus()
    {
        if (_editing == null)
        {
            StatusText.Text = "No profile selected. Add one with the buttons above.";
            EngineStatusText.Text = "";
            return;
        }
        if (_armed != null)
            StatusText.Text = $"Armed: {_armed.Value.label} — click the target on the other diagram to create the mapping (ESC to cancel).";
        else
            StatusText.Text = "Click a button on the controller or a key on the keyboard to start a mapping.";

        var engineStatus = MappingEngine.Instance.Status;
        var activeName = MappingEngine.Instance.ActiveProfile?.Name ?? "(none)";
        EngineStatusText.Text = $"Engine: {engineStatus} · Active: {activeName}";
    }

    // ============================================================================ MAPPING LIST ====

    private void RebuildMappingsList()
    {
        MappingItems.Items.Clear();
        if (_editing == null || _editing.Mappings.Count == 0)
        {
            MappingEmpty.Visibility = Visibility.Visible;
            BuildLookups();
            return;
        }
        MappingEmpty.Visibility = Visibility.Collapsed;
        foreach (var m in _editing.Mappings)
        {
            MappingItems.Items.Add(BuildMappingRow(m));
        }
        BuildLookups();
    }

    private FrameworkElement BuildMappingRow(InputMapping m)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 0, 4),
        };
        border.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        border.SetResourceReference(Border.BackgroundProperty, "FluentSurface3");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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

        var sourceLabel = LabelForCode(m.SourceKind, m.SourceCode);
        var targetLabel = LabelForCode(m.TargetKind, m.TargetCode);
        var txt = new TextBlock
        {
            Text = $"{sourceLabel}    →    {targetLabel}",
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"), FontSize = 13,
        };
        txt.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        Grid.SetColumn(txt, 1);
        grid.Children.Add(txt);

        var removeBtn = new Button
        {
            Content = "Remove",
            MinHeight = 26,
            Padding = new Thickness(8, 2, 8, 2),
        };
        removeBtn.SetResourceReference(StyleProperty, "FluentStandardButton");
        removeBtn.Click += (_, _) =>
        {
            _editing?.Mappings.Remove(m);
            RebuildMappingsList();
            RefreshDiagramHighlights();
        };
        Grid.SetColumn(removeBtn, 2);
        grid.Children.Add(removeBtn);

        border.Child = grid;
        return border;
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null) return;
        _editing.Mappings.Clear();
        RebuildMappingsList();
        RefreshDiagramHighlights();
    }

    private void BuildLookups()
    {
        _sourceLabels.Clear();
        _targetLabels.Clear();
        if (_editing == null) return;
        foreach (var m in _editing.Mappings)
        {
            _sourceLabels[(m.SourceKind, m.SourceCode)] = LabelForCode(m.TargetKind, m.TargetCode);
            _targetLabels[(m.TargetKind, m.TargetCode)] = LabelForCode(m.SourceKind, m.SourceCode);
        }
    }

    private static string LabelForCode(MappingInputKind kind, int code) => kind switch
    {
        MappingInputKind.KeyboardKey       => $"⌨ {(Keys)code}",
        MappingInputKind.MouseButton       => code == 0xFFFF ? "🖱 motion" : $"🖱 {(MouseButtons)code}",
        MappingInputKind.GamepadButton     => $"🎮 {((XboxButtonId)code)}",
        MappingInputKind.GamepadTrigger    => code == 0 ? "🎮 LT" : "🎮 RT",
        MappingInputKind.GamepadStickDirection => $"🎮 {((GamepadStickDirection)code)}",
        _ => "?",
    };

    // ============================================================================ CONTROLLER DIAGRAM ====

    private readonly Dictionary<(MappingInputKind, int), Border> _controllerHitboxes = new();

    private void BuildControllerCanvas()
    {
        ControllerCanvas.Children.Clear();
        _controllerHitboxes.Clear();
        var accent = TryFindResource("FluentAccent") as Brush ?? Brushes.MediumPurple;

        // Body — rough Xbox-controller silhouette: two big lobes + crossbar.
        var body = new Path
        {
            Data = Geometry.Parse(
                "M 60,140 " +
                "Q 60,40 200,40 " +
                "L 460,40 " +
                "Q 600,40 600,140 " +
                "Q 600,260 500,260 " +
                "Q 440,260 420,220 " +
                "L 240,220 " +
                "Q 220,260 160,260 " +
                "Q 60,260 60,140 Z"),
            StrokeThickness = 2,
        };
        body.SetResourceReference(Shape.StrokeProperty, "FluentStroke");
        body.SetResourceReference(Shape.FillProperty, "FluentSurface3");
        ControllerCanvas.Children.Add(body);

        // Shoulders + Triggers (top)
        AddCtrlButton("LB",  120, 30,  72, 24, MappingInputKind.GamepadButton, (int)XboxButtonId.LeftShoulder);
        AddCtrlButton("RB",  470, 30,  72, 24, MappingInputKind.GamepadButton, (int)XboxButtonId.RightShoulder);
        AddCtrlButton("LT",  120, 0,   72, 24, MappingInputKind.GamepadTrigger, 0);
        AddCtrlButton("RT",  470, 0,   72, 24, MappingInputKind.GamepadTrigger, 1);

        // Left stick
        AddCtrlCircle("LS",  120, 110, 56, MappingInputKind.GamepadButton, (int)XboxButtonId.LeftThumb);
        // Stick directions as small arrow hotspots around the stick
        AddCtrlButton("↑", 140, 70,  32, 20, MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.LeftStickUp);
        AddCtrlButton("↓", 140, 174, 32, 20, MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.LeftStickDown);
        AddCtrlButton("←", 80,  120, 24, 28, MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.LeftStickLeft);
        AddCtrlButton("→", 184, 120, 24, 28, MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.LeftStickRight);

        // DPad
        AddCtrlButton("↑",  240, 130, 32, 24, MappingInputKind.GamepadButton, (int)XboxButtonId.Up);
        AddCtrlButton("↓",  240, 184, 32, 24, MappingInputKind.GamepadButton, (int)XboxButtonId.Down);
        AddCtrlButton("←",  210, 158, 28, 24, MappingInputKind.GamepadButton, (int)XboxButtonId.Left);
        AddCtrlButton("→",  274, 158, 28, 24, MappingInputKind.GamepadButton, (int)XboxButtonId.Right);

        // Centre buttons
        AddCtrlButton("Back",   270, 80, 50, 22, MappingInputKind.GamepadButton, (int)XboxButtonId.Back);
        AddCtrlButton("Start",  340, 80, 50, 22, MappingInputKind.GamepadButton, (int)XboxButtonId.Start);

        // Face buttons (Y top, A bottom, X left, B right)
        AddCtrlCircle("Y", 470, 80,  28, MappingInputKind.GamepadButton, (int)XboxButtonId.Y);
        AddCtrlCircle("A", 470, 200, 28, MappingInputKind.GamepadButton, (int)XboxButtonId.A);
        AddCtrlCircle("X", 410, 140, 28, MappingInputKind.GamepadButton, (int)XboxButtonId.X);
        AddCtrlCircle("B", 530, 140, 28, MappingInputKind.GamepadButton, (int)XboxButtonId.B);

        // Right stick
        AddCtrlCircle("RS", 530, 200, 50, MappingInputKind.GamepadButton, (int)XboxButtonId.RightThumb);
        AddCtrlButton("↑", 546, 165, 32, 20, MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.RightStickUp);
        AddCtrlButton("↓", 546, 252, 32, 20, MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.RightStickDown);
        AddCtrlButton("←", 490, 210, 24, 28, MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.RightStickLeft);
        AddCtrlButton("→", 594, 210, 24, 28, MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.RightStickRight);
    }

    private void AddCtrlButton(string label, double x, double y, double w, double h,
        MappingInputKind kind, int code)
    {
        var box = new Border
        {
            Width = w, Height = h,
            CornerRadius = new CornerRadius(4),
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
        box.MouseLeftButtonDown += (_, e) => { OnDiagramClicked(kind, code, label); e.Handled = true; };
        ControllerCanvas.Children.Add(box);
        _controllerHitboxes[(kind, code)] = box;
    }

    private void AddCtrlCircle(string label, double cx, double cy, double diameter,
        MappingInputKind kind, int code)
    {
        var ell = new Border
        {
            Width = diameter, Height = diameter,
            CornerRadius = new CornerRadius(diameter / 2),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
        };
        ell.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        ell.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
        var tb = new TextBlock
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 12, FontWeight = FontWeights.SemiBold,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        ell.Child = tb;
        Canvas.SetLeft(ell, cx - diameter / 2);
        Canvas.SetTop(ell, cy - diameter / 2);
        ell.MouseLeftButtonDown += (_, e) => { OnDiagramClicked(kind, code, label); e.Handled = true; };
        ControllerCanvas.Children.Add(ell);
        _controllerHitboxes[(kind, code)] = ell;
    }

    // ============================================================================ KEYBOARD DIAGRAM ====

    private readonly Dictionary<(MappingInputKind, int), Border> _keyboardHitboxes = new();

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
                var keyBox = new Border
                {
                    Width = unit * widthUnits - 4,
                    Height = unit - 4,
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(2),
                };
                keyBox.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
                keyBox.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
                var tb = new TextBlock
                {
                    Text = label,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Segoe UI Variable Text"),
                    FontSize = 11,
                };
                tb.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
                keyBox.Child = tb;
                var captured = key;
                keyBox.MouseLeftButtonDown += (_, e) =>
                {
                    OnDiagramClicked(MappingInputKind.KeyboardKey, (int)captured, captured.ToString());
                    e.Handled = true;
                };
                sp.Children.Add(keyBox);
                _keyboardHitboxes[(MappingInputKind.KeyboardKey, (int)key)] = keyBox;
            }
            KeyboardStack.Children.Add(sp);
        }

        // Mouse row underneath.
        var mouseRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        mouseRow.Children.Add(MakeMouseBox("LMB", MouseButtons.Left));
        mouseRow.Children.Add(MakeMouseBox("RMB", MouseButtons.Right));
        mouseRow.Children.Add(MakeMouseBox("MMB", MouseButtons.Middle));
        mouseRow.Children.Add(MakeMouseBox("X1",  MouseButtons.XButton1));
        mouseRow.Children.Add(MakeMouseBox("X2",  MouseButtons.XButton2));
        // Mouse motion (used as the sentinel for stick-to-mouse / mouse-to-stick).
        var motionBox = MakeMouseBox("Mouse Motion", (MouseButtons)0xFFFF, widthUnits: 4);
        mouseRow.Children.Add(motionBox);
        KeyboardStack.Children.Add(mouseRow);
    }

    private Border MakeMouseBox(string label, MouseButtons btn, double widthUnits = 1.5)
    {
        const double unit = 38;
        var box = new Border
        {
            Width = unit * widthUnits - 4, Height = unit - 4,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand, Margin = new Thickness(2),
        };
        box.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        box.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
        var tb = new TextBlock
        {
            Text = label, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"), FontSize = 11,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        box.Child = tb;
        box.MouseLeftButtonDown += (_, e) =>
        {
            OnDiagramClicked(MappingInputKind.MouseButton, (int)btn, label);
            e.Handled = true;
        };
        _keyboardHitboxes[(MappingInputKind.MouseButton, (int)btn)] = box;
        return box;
    }

    // ============================================================================ INTERACTION ====

    private void OnDiagramClicked(MappingInputKind kind, int code, string label)
    {
        if (_editing == null) return;

        if (_armed == null)
        {
            _armed = (kind, code, label);
            RefreshDiagramHighlights();
            UpdateStatus();
            return;
        }

        // We have an armed source — the just-clicked element is the target. Disallow same-kind
        // (KB↔KB or Pad↔Pad in the same direction) because it makes no sense in this UI.
        var src = _armed.Value;
        bool srcIsGamepad = IsGamepadKind(src.kind);
        bool tgtIsGamepad = IsGamepadKind(kind);
        if (srcIsGamepad == tgtIsGamepad)
        {
            // User clicked two of the same family — swap the arming to the new one.
            _armed = (kind, code, label);
            RefreshDiagramHighlights();
            UpdateStatus();
            return;
        }

        // Direction normalisation: store the user-controlled side as the source. Gamepad source
        // means we read the physical pad → write KB/M. KB source means we read keyboard/mouse →
        // write virtual pad. Both are accepted as "source first".
        var mapping = new InputMapping
        {
            SourceKind = src.kind, SourceCode = src.code,
            TargetKind = kind, TargetCode = code,
            Enabled = true,
        };
        _editing.Mappings.Add(mapping);
        _armed = null;
        RebuildMappingsList();
        RefreshDiagramHighlights();
        UpdateStatus();
    }

    private static bool IsGamepadKind(MappingInputKind k)
        => k == MappingInputKind.GamepadButton
        || k == MappingInputKind.GamepadTrigger
        || k == MappingInputKind.GamepadStickDirection;

    private void RefreshDiagramHighlights()
    {
        var accent = TryFindResource("FluentAccent") as Brush ?? Brushes.MediumPurple;
        var stroke = TryFindResource("FluentStroke") as Brush ?? Brushes.DimGray;
        var mappedBg = new SolidColorBrush(Color.FromArgb(60,
            (accent as SolidColorBrush)?.Color.R ?? 139,
            (accent as SolidColorBrush)?.Color.G ?? 92,
            (accent as SolidColorBrush)?.Color.B ?? 246));

        foreach (var kv in _controllerHitboxes)   ApplyHighlight(kv.Key, kv.Value, accent, stroke, mappedBg);
        foreach (var kv in _keyboardHitboxes)     ApplyHighlight(kv.Key, kv.Value, accent, stroke, mappedBg);
    }

    private void ApplyHighlight((MappingInputKind kind, int code) key, Border box, Brush accent, Brush stroke, Brush mappedBg)
    {
        bool armed = _armed != null && _armed.Value.kind == key.kind && _armed.Value.code == key.code;
        bool mapped = _sourceLabels.ContainsKey(key) || _targetLabels.ContainsKey(key);
        if (armed)
        {
            box.BorderBrush = accent;
            box.BorderThickness = new Thickness(2);
            box.Background = mappedBg;
        }
        else if (mapped)
        {
            box.BorderBrush = accent;
            box.BorderThickness = new Thickness(1);
            box.Background = mappedBg;
        }
        else
        {
            box.BorderBrush = stroke;
            box.BorderThickness = new Thickness(1);
            box.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
        }
    }
}
