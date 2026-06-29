using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Tools;
using PowerAim.Types;
using PowerAim.Visuality;

namespace PowerAim.UILibrary;

/// <summary>
///     The unified Tools page list. Shows the two fixed built-ins (Magnifier, HWID spoofer) plus the
///     user's custom tools as a single list of expandable cards. Every row has a start keybind
///     (persisted under USER_TOOL_&lt;Id&gt; via <see cref="AKeyChanger"/>), a start button and an
///     enable toggle; only custom tools get edit/delete. The expandable panel hosts the built-in's
///     bespoke controls or a custom tool's option-value inputs. Modelled on AutoPlayProfileList.
/// </summary>
public partial class ToolsList : UserControl
{
    private readonly MagnifierTool _magnifier = new();
    private readonly CrosshairTool _crosshair = new();
    private readonly AntiAfkTool _antiAfk = new();
    private readonly HwidSpooferTool _hwid = new();

    public ToolsList()
    {
        InitializeComponent();
    }

    /// <summary>The persisted custom tools (bound to <c>AppConfig.Current.UserTools</c>).</summary>
    public ObservableCollection<CustomTool> UserTools
    {
        get => (ObservableCollection<CustomTool>)GetValue(UserToolsProperty);
        set => SetValue(UserToolsProperty, value);
    }

    public static readonly DependencyProperty UserToolsProperty =
        DependencyProperty.Register(nameof(UserTools), typeof(ObservableCollection<CustomTool>), typeof(ToolsList),
            new PropertyMetadata(null, OnUserToolsChanged));

    /// <summary>Display list = built-ins first, then the custom tools. The ItemsControl binds here.</summary>
    public ObservableCollection<ToolDefinition> AllTools { get; } = new();

    private static void OnUserToolsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ToolsList)d;
        if (e.OldValue is ObservableCollection<CustomTool> oldC)
            oldC.CollectionChanged -= self.OnUserToolsCollectionChanged;
        if (e.NewValue is ObservableCollection<CustomTool> newC)
            newC.CollectionChanged += self.OnUserToolsCollectionChanged;
        self.RebuildAll();
    }

    private void RebuildAll()
    {
        AllTools.Clear();
        AllTools.Add(_magnifier);
        AllTools.Add(_crosshair);
        AllTools.Add(_antiAfk);
        AllTools.Add(_hwid);
        if (UserTools != null)
            foreach (var t in UserTools)
                AllTools.Add(t);
    }

    // Keep built-in rows stable; only add/remove the user-tool tail so their keybind controls aren't
    // needlessly regenerated (a full rebuild only on Reset / move).
    private void OnUserToolsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null:
                foreach (CustomTool t in e.NewItems) AllTools.Add(t);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                foreach (CustomTool t in e.OldItems) AllTools.Remove(t);
                break;
            default:
                RebuildAll();
                break;
        }
    }

    private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void Header_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ToolDefinition tool)
            tool.IsExpanded = !tool.IsExpanded;
    }

    // ── Start ────────────────────────────────────────────────────────────────────────────────────

    private void StartTool_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ToolDefinition tool)
            ToolLauncher.Launch(tool);
    }

    /// <summary>Per-tool start keybind fired globally → run the tool once.</summary>
    private void StartToolKey(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
    {
        if (e.Value.Sender.Tag is ToolDefinition tool)
        {
            // Swallow duplicate events from a double-subscribed changer (same guard the other lists use).
            if (!KeybindToggleGuard.ShouldHandle(tool)) return;
            ToolLauncher.Launch(tool);
        }
    }

    // ── Add / edit / delete (custom tools only) ────────────────────────────────────────────────────

    private void AddTool_Click(object sender, RoutedEventArgs e)
    {
        var draft = new CustomTool { Name = Locale.ToolNewName };
        MainWindow.Instance?.OpenToolEditor(draft, isNew: true, commit: saved =>
        {
            if (saved != null) UserTools.Add(saved);
        });
    }

    private void EditTool_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not CustomTool tool) return;

        // Edit a deep clone, not the live tool: Cancel must be a true no-op, but Nextended's
        // BeginEdit/CancelEdit only rolls back scalar properties — in-place edits to the Options /
        // Actions collection CONTENTS would otherwise survive Cancel. On Save we swap the edited clone
        // back into UserTools in place (Replace -> RebuildAll rebuilds the row with a fresh panel +
        // subtitle). The clone keeps the same Id, so the per-tool keybind still matches.
        var clone = CloneTool(tool);
        MainWindow.Instance?.OpenToolEditor(clone, isNew: false, commit: saved =>
        {
            if (saved == null) return;
            var idx = UserTools.IndexOf(tool);
            if (idx >= 0) UserTools[idx] = saved;
        });
    }

    // Deep clone via the same System.Text.Json round-trip the config uses — the polymorphic ToolAction
    // list round-trips through its $type discriminator, so the clone is fully independent of the original.
    private static CustomTool CloneTool(CustomTool t) =>
        System.Text.Json.JsonSerializer.Deserialize<CustomTool>(System.Text.Json.JsonSerializer.Serialize(t))!;

    private void DeleteTool_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is CustomTool tool &&
            MessageDialog.Show(string.Format(Locale.ToolConfirmDeleteFormat, tool.Name), Locale.ToolDelete,
                MessageDialog.DialogButtons.YesNo, MessageDialog.DialogIcon.Question,
                owner: Window.GetWindow(this), defaultResult: MessageDialog.DialogResult.No) == MessageDialog.DialogResult.Yes)
        {
            ToolLauncher.Stop(tool.Id);
            UserTools.Remove(tool);
        }
    }

    // ── Per-tool expandable panel ────────────────────────────────────────────────────────────────

    private void Panel_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ContentControl { Content: null, Tag: ToolDefinition tool } cc) return;
        // Defense in depth: a malformed custom tool (bad option bounds etc.) must never crash the whole
        // app from this Loaded handler. Fall back to a quiet error line instead.
        try
        {
            cc.Content = BuildPanel(tool);
        }
        catch (Exception ex)
        {
            cc.Content = new TextBlock
            {
                Text = ex.Message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = UIElementExtensions.LookupBrush("FluentTextSecondary", ApplicationConstants.Foreground)
            };
        }
    }

    private FrameworkElement BuildPanel(ToolDefinition tool) => tool switch
    {
        MagnifierTool => BuildMagnifierPanel(),
        CrosshairTool => BuildCrosshairPanel(),
        AntiAfkTool => BuildAntiAfkPanel(),
        HwidSpooferTool => BuildHwidPanel(),
        CustomTool c => BuildCustomPanel(c),
        _ => new StackPanel()
    };

    private FrameworkElement BuildAntiAfkPanel()
    {
        var p = new StackPanel();
        p.AddSlider(Locale.AntiAfkInterval, Locale.Seconds, 1, 5, 5, 300)
            .BindTo(() => AppConfig.Current.SliderSettings.AntiAfkIntervalSeconds);
        p.Add<TextBlock>(t =>
        {
            t.Text = Locale.AntiAfkHelp;
            t.TextWrapping = TextWrapping.Wrap;
            t.FontSize = 11;
            t.Foreground = UIElementExtensions.LookupBrush("FluentTextSecondary", ApplicationConstants.Foreground);
        });
        return p;
    }

    // The custom-crosshair appearance settings, moved here from the Overlays settings card (the
    // crosshair is really a tool). The tool's start key/button toggles ShowCrosshairOverlay; this
    // panel just holds the appearance settings — the crosshair is toggled on/off by the tool's start
    // button / start key (RunAsync flips ShowCrosshairOverlay), so a separate "Show crosshair" toggle
    // here would be redundant.
    private FrameworkElement BuildCrosshairPanel()
    {
        var p = new StackPanel();

        p.AddDropdown(Locale.CrosshairShape, AppConfig.Current.CrosshairSettings.Shape,
            v => AppConfig.Current.CrosshairSettings.Shape = v);
        p.AddSlider(Locale.CrosshairSize, Locale.Pixels, 1, 1, 4, 80).BindTo(() => AppConfig.Current.CrosshairSettings.Size);
        p.AddSlider(Locale.CrosshairThickness, Locale.Pixels, 1, 1, 1, 10).BindTo(() => AppConfig.Current.CrosshairSettings.Thickness);
        p.AddSlider(Locale.CrosshairGap, Locale.Pixels, 1, 1, 0, 30).BindTo(() => AppConfig.Current.CrosshairSettings.Gap);
        p.AddSlider(Locale.CrosshairOutline, Locale.Pixels, 1, 1, 0, 4).BindTo(() => AppConfig.Current.CrosshairSettings.OutlineThickness);
        p.AddColorChanger(Locale.CrosshairColor).BindTo(() => AppConfig.Current.CrosshairSettings.ColorValue);
        p.AddColorChanger(Locale.CrosshairOutlineColor).BindTo(() => AppConfig.Current.CrosshairSettings.OutlineColorValue);

        // Detection-flash cue + its dependent picker/duration that hide when the toggle is off.
        p.AddToggle(Locale.DetectionFlash)
            .InitWith(t => t.ToolTip = Locale.DetectionFlashHelp)
            .BindTo(() => AppConfig.Current.CrosshairSettings.DetectionFlashEnabled);
        var flashColor = p.AddColorChanger(Locale.DetectionFlashColor);
        flashColor.BindTo(() => AppConfig.Current.CrosshairSettings.DetectionFlashColorValue);
        var flashDuration = p.AddSlider(Locale.DetectionFlashDuration, Locale.Milliseconds, 10, 10, 50, 1000)
            .BindTo(() => AppConfig.Current.CrosshairSettings.DetectionFlashMs);
        void UpdateFlashVis()
        {
            var on = AppConfig.Current.CrosshairSettings.DetectionFlashEnabled;
            flashColor.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            flashDuration.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }
        AppConfig.Current.CrosshairSettings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CrosshairSettings.DetectionFlashEnabled))
                Dispatcher.Invoke(UpdateFlashVis);
        };
        UpdateFlashVis();
        return p;
    }

    private bool _buildingMagPanel;

    private FrameworkElement BuildMagnifierPanel()
    {
        var bm = MainWindow.Instance?.BindingManager;
        var p = new StackPanel();
        _buildingMagPanel = true;
        try
        {
            p.AddSlider(Locale.MagnificationValue, Locale.ZoomFactor, 0.1, 0.1, ApplicationConstants.MinMagnificationFactor, ApplicationConstants.MaxMagnificationFactor)
                .BindTo(() => AppConfig.Current.SliderSettings.MagnificationFactor);
            // Scaling quality: None (sharp pixels) / SmoothHQ (native bilinear) / Enhanced (custom bicubic).
            // _buildingMagPanel ignores the combo's spurious auto-select-first onSelect during populate.
            p.AddDropdown(Locale.MagnifierScaling, AppConfig.Current.SliderSettings.MagnifierScaling,
                new[] { MagnifierScalingMode.None, MagnifierScalingMode.SmoothHQ, MagnifierScalingMode.Enhanced },
                v => { if (!_buildingMagPanel) AppConfig.Current.SliderSettings.MagnifierScaling = v; },
                toStringFn: ScalingLabel);
            p.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.MagnifierZoomInKeybind), () => AppConfig.Current.BindingSettings.MagnifierZoomInKeybind, bm);
            p.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.MagnifierZoomOutKeybind), () => AppConfig.Current.BindingSettings.MagnifierZoomOutKeybind, bm);
            p.AddSlider(Locale.ZoomStep, Locale.Step, 0.1, 0.1, 0.1, 4).BindTo(() => AppConfig.Current.SliderSettings.MagnificationStepFactor);
            p.AddSlider(Locale.WindowSizeWidth, Locale.Width, 1, 10, 50, 1500).BindTo(() => AppConfig.Current.SliderSettings.MagnifierWindowWidth);
            p.AddSlider(Locale.WindowSizeHeight, Locale.Height, 1, 10, 50, 1500).BindTo(() => AppConfig.Current.SliderSettings.MagnifierWindowHeight);
        }
        finally { _buildingMagPanel = false; }
        return p;
    }

    private static string ScalingLabel(MagnifierScalingMode m) => m switch
    {
        MagnifierScalingMode.None => Locale.MagnifierScalingNone,
        MagnifierScalingMode.SmoothHQ => Locale.MagnifierScalingSmooth,
        MagnifierScalingMode.Enhanced => Locale.MagnifierScalingEnhanced,
        _ => m.ToString()
    };

    private FrameworkElement BuildHwidPanel()
    {
        var p = new StackPanel();
        p.Add<TextBlock>(t =>
        {
            t.Text = Locale.HwidSpooferHelp;
            t.TextWrapping = TextWrapping.Wrap;
            t.FontSize = 11;
            t.Foreground = UIElementExtensions.LookupBrush("FluentTextSecondary", ApplicationConstants.Foreground);
        });
        return p;
    }

    private FrameworkElement BuildCustomPanel(CustomTool tool)
    {
        var p = new StackPanel();
        if (tool.Options.Count == 0)
        {
            p.Add<TextBlock>(t =>
            {
                t.Text = Locale.ToolNoOptionsHint;
                t.TextWrapping = TextWrapping.Wrap;
                t.FontSize = 11;
                t.Foreground = UIElementExtensions.LookupBrush("FluentTextSecondary", ApplicationConstants.Foreground);
            });
            return p;
        }

        foreach (var opt in tool.Options)
            AddOptionControl(p, opt);
        return p;
    }

    // Renders a live value-input for one custom-tool option (so the user can set the {token} value
    // before starting). The editor defines the option (name/type/default); this edits its value.
    private void AddOptionControl(StackPanel p, ToolOption opt)
    {
        switch (opt.Type)
        {
            case ToolOptionType.Bool:
            {
                var tgl = p.AddToggle(opt.Name);
                tgl.Checked = opt.EffectiveValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                DependencyPropertyDescriptor.FromProperty(AToggle.CheckedProperty, typeof(AToggle))
                    .AddValueChanged(tgl, (_, _) => opt.Value = tgl.Checked ? "true" : "false");
                break;
            }
            case ToolOptionType.Number:
            {
                // Normalize bounds: the editor lets the user type Min/Max/Step freely, so an inverted
                // range (Min > Max) or Step <= 0 is possible. Math.Clamp throws if min > max, so swap.
                var lo = Math.Min(opt.Min, opt.Max);
                var hi = Math.Max(opt.Min, opt.Max);
                var step = opt.Step > 0 ? opt.Step : 1;
                var slider = p.AddSlider(opt.Name, "", step, step, lo, hi);
                if (double.TryParse(opt.EffectiveValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    slider.Slider.Value = Math.Clamp(v, lo, hi);
                slider.Slider.ValueChanged += (_, e) => opt.Value = e.NewValue.ToString(CultureInfo.InvariantCulture);
                break;
            }
            case ToolOptionType.Enum:
            {
                p.AddDropdown(opt.Name, opt.EffectiveValue, opt.EnumValues, v => opt.Value = v);
                break;
            }
            default: // String / Path
            {
                p.Add<Label>(l =>
                {
                    l.Content = opt.Name;
                    l.FontSize = 13;
                    l.Padding = new Thickness(0);
                    l.Margin = new Thickness(2, 4, 0, 2);
                    l.Foreground = UIElementExtensions.LookupBrush("FluentTextPrimary", Colors.White);
                });

                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (opt.Type == ToolOptionType.Path)
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var tb = new TextBox
                {
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6, 5, 6, 5),
                    Background = Brushes.Transparent,
                    BorderBrush = UIElementExtensions.LookupBrush("FluentStroke", Colors.Gray),
                    Foreground = UIElementExtensions.LookupBrush("FluentTextPrimary", Colors.White)
                };
                tb.SetBinding(TextBox.TextProperty, new Binding(nameof(ToolOption.Value))
                {
                    Source = opt,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
                Grid.SetColumn(tb, 0);
                row.Children.Add(tb);

                if (opt.Type == ToolOptionType.Path)
                {
                    var browse = new APButton("…") { Margin = new Thickness(6, 0, 0, 0), MinWidth = 40 };
                    browse.Reader.Click += (_, _) =>
                    {
                        var dlg = new OpenFileDialog { CheckFileExists = true };
                        if (dlg.ShowDialog() == true) opt.Value = dlg.FileName;
                    };
                    Grid.SetColumn(browse, 1);
                    row.Children.Add(browse);
                }

                p.Children.Add(row);
                break;
            }
        }
    }
}
