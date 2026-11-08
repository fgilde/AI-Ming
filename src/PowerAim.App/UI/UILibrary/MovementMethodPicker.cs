using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Shell;
using Nextended.Core.Extensions;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.Input;
using PowerAim.Types;
using Nextended.Core.Helper;

namespace PowerAim.UI.UILibrary;

/// <summary>
///     Title-bar pill for the movement method (how the aim moves: mouse event, SendInput, …, controller stick):
///     icon, name and, for the controller, whether the virtual pad is ready. A click opens every method; picks go
///     through the host's callback. <see cref="Fill"/> builds the settings page's dropdown from the same list;
///     both follow the config, so a change in one shows in the other.
/// </summary>
public sealed class MovementMethodPicker : Button
{
    private const string IconFont = "Segoe Fluent Icons,Segoe MDL2 Assets";
    private readonly Func<MouseMovementMethod, Task> _onPick;
    private readonly TextBlock _icon, _label;
    private readonly Ellipse _status;
    private readonly Popup _popup;
    private readonly StackPanel _list;

    public MovementMethodPicker(Func<MouseMovementMethod, Task> onPick)
    {
        _onPick = onPick;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        Margin = new Thickness(8, 0, 0, 0);
        VerticalAlignment = VerticalAlignment.Center;
        Cursor = Cursors.Hand;
        Focusable = false;
        WindowChrome.SetIsHitTestVisibleInChrome(this, true);

        _icon = new TextBlock { FontFamily = new FontFamily(IconFont), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) };
        _label = new TextBlock { FontFamily = new FontFamily("Segoe UI Variable Small"), FontWeight = FontWeights.SemiBold, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center };
        _status = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var chevron = new TextBlock { Text = "", FontFamily = new FontFamily(IconFont), FontSize = 9, Margin = new Thickness(7, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_icon);
        row.Children.Add(_label);
        row.Children.Add(_status);
        row.Children.Add(chevron);

        var accent = Res("FluentAccent").Color;
        var pill = new Border
        {
            Child = row,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(10, 4, 10, 4),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(0x22, accent.R, accent.G, accent.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x70, accent.R, accent.G, accent.B)),
        };
        _icon.Foreground = new SolidColorBrush(accent);
        _label.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        chevron.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");
        pill.MouseEnter += (_, _) => pill.Background = new SolidColorBrush(Color.FromArgb(0x3A, accent.R, accent.G, accent.B));
        pill.MouseLeave += (_, _) => pill.Background = new SolidColorBrush(Color.FromArgb(0x22, accent.R, accent.G, accent.B));
        Template = PlainTemplate();
        Content = pill;

        _list = new StackPanel { Margin = new Thickness(4) };
        var card = new Border
        {
            Child = _list,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            MinWidth = 280,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.35 },
            Margin = new Thickness(0, 4, 12, 12),
        };
        card.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
        card.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        _popup = new Popup
        {
            Child = card, PlacementTarget = this, Placement = PlacementMode.Bottom, StaysOpen = false,
            AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade,
        };
        Click += (_, _) => Open();

        Follow(this, Refresh);
        Refresh();
    }

    /// <summary>Shown name, for tests.</summary>
    public string Label => _label.Text;

    public static string ShortName(MouseMovementMethod m) => m switch
    {
        MouseMovementMethod.MouseEvent => "Mouse Event",
        MouseMovementMethod.SendInput => "SendInput",
        MouseMovementMethod.LGHUB => "LG HUB",
        MouseMovementMethod.RazerSynapse => "Razer",
        MouseMovementMethod.ddxoft => "ddxoft",
        MouseMovementMethod.Gamepad => "Controller",
        _ => m.ToString(),
    };

    private static string Glyph(MouseMovementMethod m) => m == MouseMovementMethod.Gamepad ? "" : "";

    private static MouseMovementMethod Current => AppConfig.Current?.DropdownState?.MouseMovementMethod ?? MouseMovementMethod.MouseEvent;

    public void Pick(MouseMovementMethod m)
    {
        _popup.IsOpen = false;
        if (m != Current) _ = _onPick(m);
    }

    private void Refresh()
    {
        var m = Current;
        _icon.Text = Glyph(m);
        _label.Text = ShortName(m);
        bool pad = m == MouseMovementMethod.Gamepad;
        _status.Visibility = pad ? Visibility.Visible : Visibility.Collapsed;
        _status.Fill = InputSender.GamepadAimAvailable ? new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50)) : new SolidColorBrush(Color.FromRgb(0xE5, 0x53, 0x4B));
        ToolTip = $"{Locale.MouseMovementMethod}: {m.ToDescriptionString()}"
                  + (pad ? Environment.NewLine + (InputSender.GamepadAimAvailable ? Locale.GamepadReadyToSend : Locale.GamepadNotSending) : "");
    }

    private void Open()
    {
        Refresh();
        _list.Children.Clear();
        var title = new TextBlock { Text = Locale.MouseMovementMethod, FontSize = 11, Margin = new Thickness(10, 6, 10, 4), FontFamily = new FontFamily("Segoe UI Variable Small") };
        title.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");
        _list.Children.Add(title);
        foreach (var m in Enum.GetValues<MouseMovementMethod>())
            _list.Children.Add(Row(m, m == Current));
        _popup.IsOpen = true;
    }

    private Button Row(MouseMovementMethod m, bool current)
    {
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new TextBlock { Text = Glyph(m), FontFamily = new FontFamily(IconFont), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = m.ToDescriptionString(), FontSize = 13, FontFamily = new FontFamily("Segoe UI Variable Text"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) };
        var check = new TextBlock { Text = current ? "" : "", FontFamily = new FontFamily(IconFont), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(name, 1);
        Grid.SetColumn(check, 2);
        content.Children.Add(icon);
        content.Children.Add(name);
        content.Children.Add(check);
        icon.SetResourceReference(TextBlock.ForegroundProperty, current ? "FluentAccent" : "FluentTextSecondary");
        name.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        check.SetResourceReference(TextBlock.ForegroundProperty, "FluentAccent");
        if (current) name.FontWeight = FontWeights.SemiBold;

        var hover = Res("FluentSurface3");
        var row = new Button
        {
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 7, 10, 7),
            Background = current ? hover : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Template = PlainTemplate(6),
        };
        if (m == MouseMovementMethod.Gamepad) row.ToolTip = Locale.UseControllerForAimTooltip;
        if (!current)
        {
            row.MouseEnter += (_, _) => row.Background = hover;
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        }
        row.Click += (_, _) => Pick(m);
        return row;
    }

    /// <summary>
    ///     Fill a combo box (the settings page's dropdown) with every method, select the configured one without
    ///     firing a pick (the box selects its first item by itself while it is filled), and route real picks to
    ///     <paramref name="onPick"/>. It keeps following the config while it is shown.
    /// </summary>
    public static void Fill(ComboBox box, Func<MouseMovementMethod, Task> onPick)
    {
        bool syncing = true;
        box.Items.Clear();
        foreach (var m in Enum.GetValues<MouseMovementMethod>())
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock { Text = Glyph(m), FontFamily = new FontFamily(IconFont), FontSize = 13, Width = 20, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.85 });
            stack.Children.Add(new TextBlock { Text = m.ToDescriptionString(), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
            box.Items.Add(new ComboBoxItem { Content = stack, Tag = m, ToolTip = m == MouseMovementMethod.Gamepad ? Locale.UseControllerForAimTooltip : null });
        }

        void Select()
        {
            syncing = true;
            box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (MouseMovementMethod)i.Tag == Current);
            syncing = false;
        }
        Select();
        box.SelectionChanged += (_, _) =>
        {
            if (!syncing && box.SelectedItem is ComboBoxItem { Tag: MouseMovementMethod m } && m != Current) _ = onPick(m);
        };
        Follow(box, Select);
    }

    /// <summary>Run <paramref name="refresh"/> on every movement-method or send-mode change while <paramref name="owner"/> is loaded.</summary>
    private static void Follow(FrameworkElement owner, Action refresh)
    {
        DropdownState? watched = null;
        void OnChanged(object? s, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DropdownState.MouseMovementMethod) or nameof(DropdownState.GamepadSendMode) or null)
                OnUi();
        }
        void OnUi()
        {
            if (owner.Dispatcher.CheckAccess()) refresh();
            else owner.Dispatcher.BeginInvoke(refresh);
        }
        void Watch()
        {
            if (watched != null) watched.PropertyChanged -= OnChanged;
            watched = AppConfig.Current?.DropdownState;
            if (watched != null) watched.PropertyChanged += OnChanged;
        }
        void OnLoaded(object? s, EventArgs<AppConfig> e) { Watch(); OnUi(); }

        owner.Loaded += (_, _) =>
        {
            Watch();
            AppConfig.ConfigLoaded -= OnLoaded;
            AppConfig.ConfigLoaded += OnLoaded;
            refresh();
        };
        owner.Unloaded += (_, _) =>
        {
            if (watched != null) watched.PropertyChanged -= OnChanged;
            watched = null;
            AppConfig.ConfigLoaded -= OnLoaded;
        };
        Watch(); // also before the first Loaded (built off-screen)
    }

    private static ControlTemplate PlainTemplate(double radius = 0)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding(nameof(Padding)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        border.AppendChild(presenter);
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }

    private static SolidColorBrush Res(string key)
        => Application.Current?.TryFindResource(key) as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF));
}
