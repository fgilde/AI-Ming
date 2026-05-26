using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Contracts;
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
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
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
            string detail = "(empty)";
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
            catch (Exception ex) { detail = $"error: {ex.Message}"; }

            SlotsPanel.Children.Add(BuildSlotRow(i, connected, detail));
        }

        // ---- Detected HID gamepads (summary only — full list lives on HiddenControllersPage) ----
        try
        {
            var devices = HidGamepadEnumerator.Enumerate();
            int hidden = devices.Count(d => !d.Enabled);
            DeviceSummary.Text = devices.Count switch
            {
                0 => "No HID gaming devices detected.",
                _ => $"{devices.Count} detected, {hidden} hidden. Click → for the full list with Hide / Show actions.",
            };
        }
        catch (Exception ex)
        {
            DeviceSummary.Text = $"Device enumeration failed: {ex.Message}";
        }

        // Mode-conditional buttons: only show 'Launch HidHide' when the binary exists.
        var hidPathForBtn = PowerAim.InputLogic.HidHide.HidHideHelper.GetHidHidePath();
        LaunchHidHideBtn.Visibility = (!string.IsNullOrEmpty(hidPathForBtn) && System.IO.File.Exists(hidPathForBtn))
            ? Visibility.Visible
            : Visibility.Collapsed;

        // ---- Sender ----
        SenderPanel.Children.Clear();
        var sender = GamepadManager.GamepadSender;
        var mode = AppConfig.Current?.DropdownState?.GamepadSendMode.ToString() ?? "(unknown)";
        SenderPanel.Children.Add(MakeKv("Send-mode", mode));
        SenderPanel.Children.Add(MakeKv("Sender instance", sender?.GetType().Name ?? "(null)"));
        SenderPanel.Children.Add(MakeKv("Sender.CanWork",
            sender == null ? "false" : sender.CanWork.ToString(),
            warn: sender == null || !sender.CanWork));
        SenderPanel.Children.Add(MakeKv("AutoHideController (HidHide)", AppConfig.Current?.ToggleState?.AutoHideController.ToString() ?? "?"));
        SenderPanel.Children.Add(MakeKv("UseControllerForAim", AppConfig.Current?.ToggleState?.UseControllerForAim.ToString() ?? "?"));
        SenderPanel.Children.Add(MakeKv("PowerAim elevated", DeviceHide.IsElevated().ToString()));
        var hidPath = PowerAim.InputLogic.HidHide.HidHideHelper.GetHidHidePath();
        SenderPanel.Children.Add(MakeKv("HidHide installed",
            (!string.IsNullOrEmpty(hidPath) && System.IO.File.Exists(hidPath)).ToString()));

        // ---- Suggestion ----
        SuggestionText.Text = BuildSuggestion(sender, firstConnected, mode);
    }

    private static string BuildSuggestion(IGamepadSender? sender, int firstConnected, string mode)
    {
        if (sender == null && mode == "ViGEm")
            return "⚠ ViGEmBus driver appears to be missing. PowerAim couldn't create the virtual controller — every gamepad-output feature (synth triggers, mapping engine, controller-aim) is dead until you fix this.\n\n" +
                   "1. Download ViGEmBus: https://github.com/nefarius/ViGEmBus/releases (pick the latest .exe installer).\n" +
                   "2. Run the installer (UAC prompt).\n" +
                   "3. Restart PowerAim.\n\n" +
                   "The diagnostic will then show Sender.CanWork=true and a virtual pad will appear in one of the XInput slots above.";
        if (sender == null)
            return "GamepadManager has no sender. Open Gamepad-Settings → Send-mode and pick one (ViGEm is the easy default; needs the ViGEmBus driver).";
        if (!sender.CanWork && mode == "ViGEm")
            return "⚠ ViGEm sender created but CanWork=false — the ViGEmBus driver is missing or broken. Install/repair from https://github.com/nefarius/ViGEmBus/releases and reboot.";
        if (!sender.CanWork)
            return $"⚠ Sender exists ({mode}) but CanWork=false. For XInputHook this is expected if the bundled XInputHook.dll is x86-only — fails for 64-bit games (CoD, Apex). Switch to ViGEm + slot swap.";
        if (mode == "ViGEm" && firstConnected >= 0)
            return "Your physical pad sits at XInput slot " + firstConnected + ", which is the slot games read from. The virtual pad lands at a higher slot — the game isn't seeing it.\n\n" +
                   "Pick ONE of these (in order of friction):\n" +
                   "• Easiest: unplug your physical pad, restart PowerAim, plug it back in. The virtual claims slot 0 first.\n" +
                   "• Install HidHide (https://github.com/ViGEm/HidHide) and enable 'AutoHideController' below.\n" +
                   "• Or run PowerAim as administrator and use the Disable buttons we'll add per device — disables the pad system-wide.";
        if (mode == "XInputHook")
            return "XInputHook injects EasyHook into the game. The bundled XInputHook.dll is 32-bit only — works for x86 games (rare today), fails for x64 games (Call of Duty, Apex Legends, etc.) with 'STATUS_INVALID_PARAMETER_5 64-Bit library does not exist'. Use ViGEm + slot swap instead.";
        if (mode == "ViGEm")
            return "ViGEm sender alive, no physical pad detected. Test the virtual pad with the button below — your game should read from slot 0.";
        return "Sender is alive. Fire the test pulse below — whichever slot lights up is where the virtual pad sits.";
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
            Text = $"Slot {index}",
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
                ? "Virtual pad disconnected + reconnected. Look at the slot list above — if the slot moved, your game now needs to re-read XInput (Alt-Tab COD or restart it to be sure)."
                : "Reconnect failed — see the log for details. Most commonly this means ViGEmBus has hit an error and needs a reboot.";
        }
        else
        {
            SuggestionText.Text = "Active sender isn't ViGEm — reconnect is only meaningful there. Switch send-mode to ViGEm first.";
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
            ? "HidHide reset: cloak off, device-hide-list cleared, app-whitelist cleared. If your virtual pad was being mistakenly hidden, it should now be visible to games."
            : "HidHide reset attempted but the CLI returned an error. HidHide might not be installed — that's also fine, in which case nothing was hidden in the first place.";
        Refresh();
    }

    private void LaunchHidHide_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var cli = PowerAim.InputLogic.HidHide.HidHideHelper.GetHidHidePath();
            if (string.IsNullOrEmpty(cli) || !System.IO.File.Exists(cli))
            {
                SuggestionText.Text = "HidHide isn't installed — install it from https://github.com/ViGEm/HidHide first.";
                return;
            }
            var ui = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(cli) ?? "", "HidHideClient.exe");
            if (System.IO.File.Exists(ui))
                System.Diagnostics.Process.Start(ui);
            else
                SuggestionText.Text = "HidHideClient.exe not found next to the CLI. Reinstall HidHide.";
        }
        catch (Exception ex) { SuggestionText.Text = $"Could not launch HidHide UI: {ex.Message}"; }
    }

    private void OpenGamepadTester_Click(object sender, RoutedEventArgs e)
    {
        try { MainWindow.Instance?.OpenGamepadTester(); }
        catch (Exception ex) { SuggestionText.Text = $"Navigation failed: {ex.Message}"; }
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
        catch (Exception ex) { SuggestionText.Text = $"Could not open joy.cpl: {ex.Message}"; }
    }

    private void OpenHiddenControllers_Click(object sender, RoutedEventArgs e)
    {
        try { MainWindow.Instance?.OpenHiddenControllersPage(); }
        catch (Exception ex) { SuggestionText.Text = $"Navigation failed: {ex.Message}"; }
    }

    private async void TestPulse_Click(object sender, RoutedEventArgs e)
    {
        var s = GamepadManager.GamepadSender;
        if (s == null || !s.CanWork)
        {
            SuggestionText.Text = "Sender unavailable — can't fire a pulse. See the diagnose box above.";
            return;
        }
        try
        {
            s.SetSliderValue(GamepadSlider.RightTrigger, 255, GamepadSyncState.Paused);
            SuggestionText.Text = "Test pulse fired (RT=255 for 250ms). Watch the slot rows above — whichever slot's RT spikes is where the virtual pad is. If your physical pad's slot also shows RT, you might be touching the trigger.";
            await Task.Delay(250);
            s.SetSliderValue(GamepadSlider.RightTrigger, 0, GamepadSyncState.Resume);
        }
        catch (Exception ex)
        {
            SuggestionText.Text = $"Test pulse failed: {ex.Message}";
        }
    }
}
