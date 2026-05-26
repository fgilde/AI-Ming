using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Contracts;
using PowerAim;
using SharpDX.XInput;

namespace PowerAim.UILibrary;

/// <summary>
///     Inline gamepad-diagnostics panel — sits directly on the Gamepad-Settings page so the user
///     sees what's happening to their pad pipeline without opening a dialog. Auto-refreshes via a
///     dispatcher timer so live changes (RT pulses, plugging the pad in/out) show up.
///     <para>
///     Diagnostics covered:
///     <list type="bullet">
///       <item>All 4 XInput slots with connected state + live RT/LT/buttons read</item>
///       <item>PowerAim sender mode + CanWork + relevant toggles</item>
///       <item>A "Disable physical device" button per detected gaming-HID, using stock Windows
///             <see cref="DeviceHide"/> as a HidHide alternative when running elevated.</item>
///       <item>A "Fire test RT pulse" button to verify the synthesised path actually reaches a
///             slot a game would be reading from.</item>
///     </list>
///     </para>
/// </summary>
public partial class GamepadDiagnosticsPanel : UserControl
{
    private readonly DispatcherTimer _refresh;

    public GamepadDiagnosticsPanel()
    {
        InitializeComponent();
        _refresh = new() { Interval = TimeSpan.FromMilliseconds(700) };
        _refresh.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { Refresh(); _refresh.Start(); };
        Unloaded += (_, _) => _refresh.Stop();
    }

    private void Refresh()
    {
        // ---- XInput slots ----
        SlotsPanel.Children.Clear();
        int firstConnected = -1;
        for (int i = 0; i < 4; i++)
        {
            var ctrl = new Controller((UserIndex)i);
            bool connected = false;
            string detail = Locale.EmptyParen;
            try
            {
                connected = ctrl.IsConnected;
                if (connected)
                {
                    var state = ctrl.GetState();
                    detail = $"RT={state.Gamepad.RightTrigger}  LT={state.Gamepad.LeftTrigger}  Btns=0x{(int)state.Gamepad.Buttons:X4}";
                    if (firstConnected < 0) firstConnected = i;
                }
            }
            catch (Exception ex) { detail = string.Format(Locale.ErrorLowerFormat, ex.Message); }

            SlotsPanel.Children.Add(BuildSlotRow(i, connected, detail));
        }

        // ---- Detected HID gamepads (summary only — full list lives on HiddenControllersPage) ----
        try
        {
            var devices = HidGamepadEnumerator.Enumerate();
            int hidden = devices.Count(d => !d.Enabled);
            DeviceSummary.Text = devices.Count switch
            {
                0 => Locale.NoHidDevicesDetectedShort,
                _ => string.Format(Locale.DevicesSummaryFormat, devices.Count, hidden),
            };
        }
        catch (Exception ex)
        {
            DeviceSummary.Text = string.Format(Locale.DeviceEnumerationFailedFormat, ex.Message);
        }

        // Mode-conditional buttons: only show 'Launch HidHide' when the binary exists.
        var hidPathForBtn = PowerAim.InputLogic.HidHide.HidHideHelper.GetHidHidePath();
        LaunchHidHideBtn.Visibility = (!string.IsNullOrEmpty(hidPathForBtn) && System.IO.File.Exists(hidPathForBtn))
            ? Visibility.Visible
            : Visibility.Collapsed;

        // ---- Sender ----
        SenderPanel.Children.Clear();
        var sender = GamepadManager.GamepadSender;
        var mode = AppConfig.Current?.DropdownState?.GamepadSendMode.ToString() ?? Locale.UnknownParen;
        SenderPanel.Children.Add(MakeKv(Locale.DiagSendMode, mode));
        SenderPanel.Children.Add(MakeKv(Locale.DiagSenderInstance, sender?.GetType().Name ?? Locale.NullParen));
        SenderPanel.Children.Add(MakeKv(Locale.DiagSenderCanWork,
            sender == null ? "false" : sender.CanWork.ToString(),
            warn: sender == null || !sender.CanWork));
        SenderPanel.Children.Add(MakeKv(Locale.DiagAutoHideController, AppConfig.Current?.ToggleState?.AutoHideController.ToString() ?? "?"));
        SenderPanel.Children.Add(MakeKv(Locale.DiagUseControllerForAim, AppConfig.Current?.ToggleState?.UseControllerForAim.ToString() ?? "?"));
        SenderPanel.Children.Add(MakeKv(Locale.DiagPowerAimElevated, DeviceHide.IsElevated().ToString()));
        var hidPath = PowerAim.InputLogic.HidHide.HidHideHelper.GetHidHidePath();
        SenderPanel.Children.Add(MakeKv(Locale.DiagHidHideInstalled,
            (!string.IsNullOrEmpty(hidPath) && System.IO.File.Exists(hidPath)).ToString()));

        // ---- Suggestion ----
        SuggestionText.Text = BuildSuggestion(sender, firstConnected, mode);
    }

    private static string BuildSuggestion(IGamepadSender? sender, int firstConnected, string mode)
    {
        if (sender == null && mode == "ViGEm")
            return Locale.SuggestVigemMissing;
        if (sender == null)
            return Locale.SuggestNoSender;
        if (!sender.CanWork && mode == "ViGEm")
            return Locale.SuggestVigemCantWork;
        if (!sender.CanWork)
            return string.Format(Locale.SuggestSenderCantWork, mode);
        if (mode == "ViGEm" && firstConnected >= 0)
            return string.Format(Locale.SuggestViGEmSlotConflict, firstConnected);
        if (mode == "XInputHook")
            return Locale.SuggestXInputHook;
        if (mode == "ViGEm")
            return Locale.SuggestViGEmNoPad;
        return Locale.SuggestGenericSenderAlive;
    }

    // ============================================================================ UI BUILDERS ====

    private FrameworkElement BuildSlotRow(int index, bool connected, string detail)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 10, Height = 10,
            Fill = connected
                ? (TryFindResource("FluentAccent") as Brush ?? Brushes.LimeGreen)
                : new SolidColorBrush(Color.FromArgb(80, 200, 200, 200)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(dot, 0);
        grid.Children.Add(dot);

        var label = new TextBlock
        {
            Text = string.Format(Locale.SlotFormat, index),
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        var details = new TextBlock
        {
            Text = detail,
            FontFamily = new FontFamily("Cascadia Mono,Consolas"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        details.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");
        Grid.SetColumn(details, 2);
        grid.Children.Add(details);
        return grid;
    }

    private FrameworkElement MakeKv(string key, string value, bool warn = false)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var k = new TextBlock
        {
            Text = key,
            FontFamily = new FontFamily("Segoe UI Variable Text"), FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        };
        k.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");
        Grid.SetColumn(k, 0);
        grid.Children.Add(k);

        var v = new TextBlock
        {
            Text = value,
            FontFamily = new FontFamily("Cascadia Mono,Consolas"), FontSize = 12,
            FontWeight = warn ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        v.SetResourceReference(TextBlock.ForegroundProperty, warn ? "FluentAccent" : "FluentTextPrimary");
        Grid.SetColumn(v, 1);
        grid.Children.Add(v);
        return grid;
    }

    // ============================================================================ HANDLERS ====

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void ReconnectVirtual_Click(object sender, RoutedEventArgs e)
    {
        // Force a USB re-enumeration of the virtual pad. This is the trick that made
        // VirtualGamepadHost appear to "work better" — being created later in the lifecycle
        // produces a fresh plug-event that XInput notices. Disconnect+reconnect on the existing
        // sender achieves the same thing without parallel ViGEmClients fighting for slots.
        if (GamepadManager.GamepadSender is global::GamepadSenderViGEm vigem)
        {
            bool ok = vigem.Reconnect();
            SuggestionText.Text = ok
                ? Locale.VirtualPadReconnected
                : Locale.VirtualPadReconnectFailed;
        }
        else
        {
            SuggestionText.Text = Locale.VirtualPadReconnectNotViGEm;
        }
        Refresh();
    }

    private void ResetHidHide_Click(object sender, RoutedEventArgs e)
    {
        // Nukes HidHide's cloak state. The user might have accidentally cloaked their own
        // virtual pads in past sessions (every PowerAim instance adds itself to HidHide's
        // app whitelist + may cloak devices via the AutoHideController toggle). Resetting
        // gives them a clean slate.
        bool ok = PowerAim.InputLogic.HidHide.HidHideHelper.ResetAll();
        SuggestionText.Text = ok
            ? Locale.HidHideResetOk
            : Locale.HidHideResetFailed;
        Refresh();
    }

    private void LaunchHidHide_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var cli = PowerAim.InputLogic.HidHide.HidHideHelper.GetHidHidePath();
            if (string.IsNullOrEmpty(cli) || !System.IO.File.Exists(cli))
            {
                SuggestionText.Text = Locale.HidHideNotInstalled;
                return;
            }
            var ui = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(cli) ?? "", "HidHideClient.exe");
            if (System.IO.File.Exists(ui))
                System.Diagnostics.Process.Start(ui);
            else
                SuggestionText.Text = Locale.HidHideClientNotFound;
        }
        catch (Exception ex) { SuggestionText.Text = string.Format(Locale.LaunchHidHideUiFailedFormat, ex.Message); }
    }

    private void OpenGamepadTester_Click(object sender, RoutedEventArgs e)
    {
        try { MainWindow.Instance?.OpenGamepadTester(); }
        catch (Exception ex) { SuggestionText.Text = string.Format(Locale.NavigationFailedFormat, ex.Message); }
    }

    private void OpenJoyCpl_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c joy.cpl",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch (Exception ex) { SuggestionText.Text = string.Format(Locale.OpenJoyCplFailedFormat, ex.Message); }
    }

    private void OpenHiddenControllers_Click(object sender, RoutedEventArgs e)
    {
        try { MainWindow.Instance?.OpenHiddenControllersPage(); }
        catch (Exception ex) { SuggestionText.Text = string.Format(Locale.NavigationFailedFormat, ex.Message); }
    }

    private async void TestPulse_Click(object sender, RoutedEventArgs e)
    {
        var s = GamepadManager.GamepadSender;
        if (s == null || !s.CanWork)
        {
            SuggestionText.Text = Locale.SenderUnavailable;
            return;
        }
        try
        {
            s.SetSliderValue(GamepadSlider.RightTrigger, 255, GamepadSyncState.Paused);
            SuggestionText.Text = Locale.TestPulseFiredHelp;
            await Task.Delay(250);
            s.SetSliderValue(GamepadSlider.RightTrigger, 0, GamepadSyncState.Resume);
        }
        catch (Exception ex)
        {
            SuggestionText.Text = string.Format(Locale.TestPulseFailedFormat, ex.Message);
        }
    }
}
