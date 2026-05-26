using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Mapping;
using PowerAim;
// Pin WPF types that collide with WinForms.
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace PowerAim.UILibrary;

/// <summary>
///     Mapping editor — hybrid design.
///     <list type="number">
///       <item>Editor is a row list. Each row uses two <see cref="AKeyChanger"/>s (source + target)
///             driven by the same global recorder PowerAim uses everywhere else (Triggers, Aim
///             keybinds, …), plus an Activator combo. Recording works for KB + mouse + gamepad
///             buttons + triggers out of the box because <see cref="StoredInputBinding"/> already
///             covers all of them.</item>
///       <item>Two stick-shaped specials (mouse ↔ stick motion, stick direction) that can't be
///             carried by <see cref="StoredInputBinding"/> show as a labelled chip with a Remove
///             button instead of an AKeyChanger.</item>
///       <item>The Xbox 360 visual stays as a read-only reference — buttons that have a mapping
///             are highlighted, but the diagram itself isn't an editor anymore.</item>
///     </list>
///     Persistence schema (<see cref="InputMapping"/>) is unchanged — the per-row
///     <see cref="MappingBindingConverter"/> handles the round-trip so the engine doesn't care.
/// </summary>
public partial class MappingEdit : UserControl
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile), typeof(ControllerMappingProfile), typeof(MappingEdit),
        new PropertyMetadata(null, OnProfileChanged));

    public ControllerMappingProfile? Profile
    {
        get => (ControllerMappingProfile?)GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    private NotifyCollectionChangedEventHandler? _mappingsHandler;

    public MappingEdit()
    {
        InitializeComponent();
        DataContext = this;

        BuildStickSettings();

        Loaded += (_, _) => RebuildAll();
    }

    private static void OnProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MappingEdit me) return;
        if (e.OldValue is ControllerMappingProfile oldP && me._mappingsHandler != null)
            oldP.Mappings.CollectionChanged -= me._mappingsHandler;

        if (e.NewValue is ControllerMappingProfile newP)
        {
            me._mappingsHandler = (_, _) => me.Dispatcher.BeginInvoke(new Action(me.RebuildAll));
            newP.Mappings.CollectionChanged += me._mappingsHandler;
        }
        me.RebuildAll();
    }

    private void RebuildAll()
    {
        BuildStickSettings();
        RebuildMappingsList();
    }

    // ============================================================================ STICK SETTINGS ====

    private void BuildStickSettings()
    {
        StickSettingsHost.Children.Clear();
        if (Profile == null) return;
        var p = Profile;

        StickSettingsHost.AddSlider(Locale.MouseToStickSensitivity, Locale.MultiplierUnit, 0.05, 0.05, 0.1, 5.0)
            .BindTo(() => p.MouseToStickSensitivity);
        StickSettingsHost.AddSlider(Locale.StickToMouseSensitivity, Locale.PxPerTickUnit, 1, 1, 1, 60)
            .BindTo(() => p.StickToMouseSensitivity);
        StickSettingsHost.AddSlider(Locale.DeadZone, Locale.FullDeflectionUnit, 0.01, 0.01, 0.0, 0.45)
            .BindTo(() => p.StickDeadzone);
        StickSettingsHost.AddSlider(Locale.AntiDeadZone, Locale.FullDeflectionUnit, 0.01, 0.01, 0.0, 0.45)
            .BindTo(() => p.StickAntiDeadzone);
        StickSettingsHost.AddSlider(Locale.MouseResponseCurve, Locale.ExponentUnit, 0.05, 0.05, 0.8, 3.0)
            .BindTo(() => p.StickMouseExponent);

        var invY = new System.Windows.Controls.CheckBox
        {
            Content = Locale.InvertYAxis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
            IsChecked = p.InvertMouseY,
        };
        invY.SetResourceReference(System.Windows.Controls.CheckBox.ForegroundProperty, "FluentTextPrimary");
        invY.Checked   += (_, _) => p.InvertMouseY = true;
        invY.Unchecked += (_, _) => p.InvertMouseY = false;
        StickSettingsHost.Children.Add(invY);
    }

    // ============================================================================ MAPPING ROWS ====

    private void RebuildMappingsList()
    {
        MappingItems.Items.Clear();
        if (Profile == null || Profile.Mappings.Count == 0)
        {
            MappingEmpty.Visibility = Visibility.Visible;
            return;
        }
        MappingEmpty.Visibility = Visibility.Collapsed;
        int idx = 0;
        foreach (var m in Profile.Mappings)
        {
            MappingItems.Items.Add(BuildRow(m, idx));
            idx++;
        }
    }

    private FrameworkElement BuildRow(InputMapping mapping, int index)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
        };
        border.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        border.SetResourceReference(Border.BackgroundProperty, "FluentSurface3");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });            // 0 enabled
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 1 source
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });            // 2 arrow
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 3 target
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });            // 4 activator
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });            // 5 remove

        // -- col 0: enabled checkbox
        var enabledBox = new System.Windows.Controls.CheckBox
        {
            IsChecked = mapping.Enabled,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        enabledBox.Checked   += (_, _) => mapping.Enabled = true;
        enabledBox.Unchecked += (_, _) => mapping.Enabled = false;
        Grid.SetColumn(enabledBox, 0);
        grid.Children.Add(enabledBox);

        // -- col 1: source
        var sourceHost = BuildEndpointPicker(mapping, isSource: true, index);
        Grid.SetColumn(sourceHost, 1);
        grid.Children.Add(sourceHost);

        // -- col 2: arrow
        var arrow = new TextBlock
        {
            Text = "→",
            Margin = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 16,
        };
        arrow.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextTertiary");
        Grid.SetColumn(arrow, 2);
        grid.Children.Add(arrow);

        // -- col 3: target
        var targetHost = BuildEndpointPicker(mapping, isSource: false, index);
        Grid.SetColumn(targetHost, 3);
        grid.Children.Add(targetHost);

        // -- col 4: activator combo
        var act = new System.Windows.Controls.ComboBox
        {
            MinWidth = 130, MinHeight = 30,
            Margin = new Thickness(10, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 12,
            ToolTip = Locale.MappingActivatorTooltip,
        };
        foreach (MappingActivator a in Enum.GetValues<MappingActivator>())
            act.Items.Add(a);
        act.SelectedItem = mapping.Activator;
        act.SelectionChanged += (_, _) =>
        {
            if (act.SelectedItem is MappingActivator a) mapping.Activator = a;
        };
        Grid.SetColumn(act, 4);
        grid.Children.Add(act);

        // -- col 5: remove
        var rem = new Button
        {
            Content = "🗑",
            MinHeight = 30, MinWidth = 36,
            Padding = new Thickness(6, 2, 6, 2),
            ToolTip = Locale.RemoveMapping,
        };
        rem.SetResourceReference(StyleProperty, "FluentStandardButton");
        rem.Click += (_, _) => Profile?.Mappings.Remove(mapping);
        Grid.SetColumn(rem, 5);
        grid.Children.Add(rem);

        border.Child = grid;
        return border;
    }

    /// <summary>
    ///     Renders one endpoint slot for the row. The slot is always a two-column composite:
    ///     <c>[ AKeyChanger | "▾ Special" button ]</c> when the endpoint is a regular kb/mouse/pad
    ///     binding, OR <c>[ Special chip | "← Record" button ]</c> when the endpoint is a
    ///     stick-direction / mouse-motion special that can't be captured via the recorder. The
    ///     "Special" button opens a context menu listing all stick directions plus mouse-motion,
    ///     letting the user assemble e.g. "W → LStickUp" entirely by hand instead of relying on
    ///     the FPS preset.
    /// </summary>
    private FrameworkElement BuildEndpointPicker(InputMapping mapping, bool isSource, int rowIndex)
    {
        var host = new Grid();
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        MappingInputKind kind = isSource ? mapping.SourceKind : mapping.TargetKind;
        int code = isSource ? mapping.SourceCode : mapping.TargetCode;

        if (MappingBindingConverter.IsSpecial(kind, code))
        {
            // ----- Special chip (stick direction or mouse motion) -----
            var chip = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 6, 12, 6),
                MinHeight = 34,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
            };
            chip.SetResourceReference(Border.BorderBrushProperty, "FluentAccent");
            chip.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
            var t = new TextBlock
            {
                Text = MappingBindingConverter.SpecialLabel(kind, code),
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            t.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
            chip.Child = t;
            Grid.SetColumn(chip, 0);
            host.Children.Add(chip);

            // Swap-to-recorder button so user can flip back from a special to a normal capture.
            var recordBtn = new Button
            {
                Content = "↺",
                MinHeight = 30, MinWidth = 30,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(0),
                ToolTip = Locale.RecordNormalTooltip,
            };
            recordBtn.SetResourceReference(StyleProperty, "FluentStandardButton");
            recordBtn.Click += (_, _) =>
            {
                if (isSource) { mapping.SourceKind = MappingInputKind.KeyboardKey; mapping.SourceCode = 0; }
                else          { mapping.TargetKind = MappingInputKind.KeyboardKey; mapping.TargetCode = 0; }
                RebuildMappingsList();
            };
            Grid.SetColumn(recordBtn, 1);
            host.Children.Add(recordBtn);
            return host;
        }

        // ----- Normal slot: AKeyChanger + Special picker button -----
        string slotKey = $"MAP_{Profile?.Id ?? "x"}_{rowIndex}_{(isSource ? "S" : "T")}";
        var bm = MainWindow.Instance?.BindingManager;
        var initial = MappingBindingConverter.ToBinding(kind, code);

        var picker = new AKeyChanger
        {
            BindingManager = bm,
            KeyConfigName = slotKey,
            KeyConfigPrefix = "MAPPING_PICKER",
            ShowTitle = false,
            CanEditMinTime = false,
            WithBorder = true,
            KeyBind = initial,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 34,
        };
        picker.KeyBindChanged += (s, e) =>
        {
            var newBinding = e.Value.KeyBinding;
            if (newBinding is not { IsValid: true }) return;
            var dec = MappingBindingConverter.FromBinding(newBinding);
            if (dec == null) return;
            if (isSource)
            {
                mapping.SourceKind = dec.Value.Kind;
                mapping.SourceCode = dec.Value.Code;
            }
            else
            {
                mapping.TargetKind = dec.Value.Kind;
                mapping.TargetCode = dec.Value.Code;
            }
        };
        Grid.SetColumn(picker, 0);
        host.Children.Add(picker);

        var specialBtn = new Button
        {
            Content = "▾",
            MinHeight = 30, MinWidth = 30,
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(0),
            ToolTip = Locale.SpecialEndpointTooltip,
        };
        specialBtn.SetResourceReference(StyleProperty, "FluentStandardButton");
        specialBtn.Click += (_, _) => OpenSpecialMenu(specialBtn, mapping, isSource);
        Grid.SetColumn(specialBtn, 1);
        host.Children.Add(specialBtn);
        return host;
    }

    /// <summary>
    ///     Show the "pick a special endpoint" context menu — stick directions for both sticks plus
    ///     mouse-motion. Selecting an item rewrites the mapping's kind/code and triggers a list
    ///     rebuild so the slot flips from AKeyChanger to a special chip.
    /// </summary>
    private void OpenSpecialMenu(Button anchor, InputMapping mapping, bool isSource)
    {
        var menu = new System.Windows.Controls.ContextMenu();

        void Add(string header, MappingInputKind kind, int code)
        {
            var item = new System.Windows.Controls.MenuItem { Header = header };
            item.Click += (_, _) =>
            {
                if (isSource) { mapping.SourceKind = kind; mapping.SourceCode = code; }
                else          { mapping.TargetKind = kind; mapping.TargetCode = code; }
                RebuildMappingsList();
            };
            menu.Items.Add(item);
        }

        Add(Locale.MenuLeftStickUp,    MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.LeftStickUp);
        Add(Locale.MenuLeftStickDown,  MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.LeftStickDown);
        Add(Locale.MenuLeftStickLeft,  MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.LeftStickLeft);
        Add(Locale.MenuLeftStickRight, MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.LeftStickRight);
        menu.Items.Add(new System.Windows.Controls.Separator());
        Add(Locale.MenuRightStickUp,    MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.RightStickUp);
        Add(Locale.MenuRightStickDown,  MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.RightStickDown);
        Add(Locale.MenuRightStickLeft,  MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.RightStickLeft);
        Add(Locale.MenuRightStickRight, MappingInputKind.GamepadStickDirection, (int)GamepadStickDirection.RightStickRight);
        menu.Items.Add(new System.Windows.Controls.Separator());
        Add(Locale.MenuMouseMotionRelative, MappingInputKind.MouseButton, MappingBindingConverter.MouseMotionSentinel);

        menu.PlacementTarget = anchor;
        menu.IsOpen = true;
    }

    // ============================================================================ ADD BUTTONS ====

    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        if (Profile == null) return;
        // Start with a blank kb→kb row — user records both sides.
        Profile.Mappings.Add(new()
        {
            SourceKind = MappingInputKind.KeyboardKey,
            SourceCode = 0,
            TargetKind = MappingInputKind.KeyboardKey,
            TargetCode = 0,
            Enabled = true,
            Activator = MappingActivator.Press,
        });
    }

    private void AddStickToMouse_Click(object sender, RoutedEventArgs e)
    {
        if (Profile == null) return;
        if (Profile.Mappings.Any(m =>
            m.SourceKind == MappingInputKind.GamepadStickDirection
            && m.SourceCode == (int)GamepadStickDirection.RightStickRight
            && m.TargetKind == MappingInputKind.MouseButton
            && m.TargetCode == MappingBindingConverter.MouseMotionSentinel)) return;
        Profile.Mappings.Add(new()
        {
            SourceKind = MappingInputKind.GamepadStickDirection,
            SourceCode = (int)GamepadStickDirection.RightStickRight,
            TargetKind = MappingInputKind.MouseButton,
            TargetCode = MappingBindingConverter.MouseMotionSentinel,
            Enabled = true,
        });
    }

    private void AddMouseToStick_Click(object sender, RoutedEventArgs e)
    {
        if (Profile == null) return;
        if (Profile.Mappings.Any(m =>
            m.SourceKind == MappingInputKind.MouseButton
            && m.SourceCode == MappingBindingConverter.MouseMotionSentinel
            && m.TargetKind == MappingInputKind.GamepadStickDirection
            && m.TargetCode == (int)GamepadStickDirection.RightStickRight)) return;
        Profile.Mappings.Add(new()
        {
            SourceKind = MappingInputKind.MouseButton,
            SourceCode = MappingBindingConverter.MouseMotionSentinel,
            TargetKind = MappingInputKind.GamepadStickDirection,
            TargetCode = (int)GamepadStickDirection.RightStickRight,
            Enabled = true,
        });
    }

}
