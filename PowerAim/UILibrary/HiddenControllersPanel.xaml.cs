using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PowerAim.Class.Native;
using PowerAim;

namespace PowerAim.UILibrary;

/// <summary>
///     Stand-alone "Hidden controllers" panel — lists every HID gaming device Windows knows about,
///     with a Hide / Show toggle that drives stock <see cref="DeviceHide"/> (CM_Disable_DevNode).
///     <para>
///     Lives in its own UserControl so the diagnostic strip on the Gamepad page can stay focused
///     on telemetry, while a dedicated sub-page provides a more spacious surface to manage which
///     pads games actually see (a common reWASD/HidHide chore).
///     </para>
/// </summary>
public partial class HiddenControllersPanel : UserControl
{
    private readonly DispatcherTimer _refresh;

    public HiddenControllersPanel()
    {
        InitializeComponent();
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refresh.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { Refresh(); _refresh.Start(); };
        Unloaded += (_, _) => _refresh.Stop();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        bool elevated = DeviceHide.IsElevated();
        ElevationWarning.Visibility = elevated ? Visibility.Collapsed : Visibility.Visible;
        DevicesPanel.Children.Clear();
        try
        {
            var devices = HidGamepadEnumerator.Enumerate();
            if (devices.Count == 0)
            {
                DevicesPanel.Children.Add(new TextBlock
                {
                    Text = Locale.NoHidDevicesDetected,
                    FontFamily = new FontFamily("Segoe UI Variable Small"),
                    FontSize = 12,
                    Foreground = (Brush?)TryFindResource("FluentTextTertiary") ?? Brushes.Gray,
                    Margin = new Thickness(0, 4, 0, 0),
                });
                return;
            }
            foreach (var d in devices)
                DevicesPanel.Children.Add(BuildRow(d, elevated));
        }
        catch (Exception ex)
        {
            DevicesPanel.Children.Add(new TextBlock
            {
                Text = string.Format(Locale.DeviceEnumerationFailedFormat, ex.Message),
                Foreground = Brushes.Tomato,
                FontSize = 12,
            });
        }
    }

    private FrameworkElement BuildRow(DetectedGamepad d, bool elevated)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 6),
            Opacity = d.Enabled ? 1.0 : 0.55,
        };
        border.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
        border.SetResourceReference(Border.BackgroundProperty, "FluentSurface3");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel();
        var name = new TextBlock
        {
            Text = d.Enabled ? d.FriendlyName : string.Format(Locale.ControllerHiddenFormat, d.FriendlyName),
            FontFamily = new FontFamily("Segoe UI Variable Display"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty,
            d.Enabled ? "FluentTextPrimary" : "FluentTextTertiary");
        info.Children.Add(name);

        var sub = new TextBlock
        {
            Text = d.InstanceId,
            FontFamily = new FontFamily("Cascadia Mono,Consolas"),
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0),
        };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");
        info.Children.Add(sub);

        var hw = new TextBlock
        {
            Text = d.HardwareId,
            FontFamily = new FontFamily("Cascadia Mono,Consolas"),
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 1, 0, 0),
        };
        hw.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextTertiary");
        info.Children.Add(hw);

        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var btn = new Button
        {
            Content = d.Enabled ? Locale.HideFromGames : Locale.ShowAgain,
            MinHeight = 30,
            MinWidth = 140,
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(8, 0, 0, 0),
            IsEnabled = elevated,
            ToolTip = elevated
                ? (d.Enabled
                    ? Locale.HideDeviceTooltip
                    : Locale.ReEnableDeviceTooltip)
                : Locale.RequiresAdminTooltip,
        };
        btn.SetResourceReference(StyleProperty, d.Enabled ? "FluentStandardButton" : "FluentAccentButton");
        btn.Click += (_, _) =>
        {
            try
            {
                bool ok = d.Enabled
                    ? DeviceHide.TryDisable(d.InstanceId)
                    : DeviceHide.TryEnable(d.InstanceId);
                StatusText.Text = ok
                    ? string.Format(Locale.DeviceToggledFormat, d.FriendlyName, d.Enabled ? Locale.Hidden : Locale.Shown)
                    : string.Format(Locale.DeviceToggleFailedFormat, d.Enabled ? "disable" : "enable", d.FriendlyName, DeviceHide.LastError);
            }
            catch (Exception ex) { StatusText.Text = string.Format(Locale.OperationFailedFormat, ex.Message); }
            Refresh();
        };
        Grid.SetColumn(btn, 1);
        grid.Children.Add(btn);
        border.Child = grid;
        return border;
    }
}
