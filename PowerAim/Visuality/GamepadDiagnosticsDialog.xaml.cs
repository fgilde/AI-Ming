using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim.Config;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Contracts;
using SharpDX.XInput;

namespace PowerAim.Visuality;

/// <summary>
///     Live snapshot of the gamepad pipeline. Built because "send a trigger pulse, nothing happens
///     in the game" can have several causes (ViGEm bus missing, wrong send-mode, the game reading
///     from the real pad's lower-indexed XInput slot, etc.) and guessing is wasteful.
///     <para>
///     The diagnostic walks XInput slots 0-3, reports each one's connected/disconnected state and
///     button-press fingerprint so the user can correlate "the pad I'm physically holding" with
///     the slot it occupies. Combined with sender status it produces a concrete next-step
///     suggestion instead of leaving the user to guess.
///     </para>
/// </summary>
public partial class GamepadDiagnosticsDialog
{
    public GamepadDiagnosticsDialog()
    {
        InitializeComponent();
        Refresh();
    }

    private void Refresh()
    {
        // ---- XInput slots ----
        SlotsPanel.Children.Clear();
        int realSlot = -1;
        int virtualSlot = -1;
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
                    var caps = ctrl.GetCapabilities(DeviceQueryType.Any);
                    string device = caps.Type switch
                    {
                        DeviceType.Gamepad => "Gamepad",
                        _ => caps.Type.ToString(),
                    };
                    detail = $"{device}  ·  RT={state.Gamepad.RightTrigger}  LT={state.Gamepad.LeftTrigger}  Btns=0x{(int)state.Gamepad.Buttons:X4}";
                    // The virtual pad we create has VID 045E / PID 028E (Xbox 360 wired). XInput
                    // doesn't expose VID/PID directly, but the pad responds to our writes — we
                    // can identify it by checking whether the GamepadSender's last test-pulse
                    // produced the RT spike we read here.
                }
            }
            catch (Exception ex)
            {
                detail = $"error: {ex.Message}";
            }

            var row = BuildSlotRow(i, connected, detail);
            SlotsPanel.Children.Add(row);

            if (connected && realSlot < 0) realSlot = i;
        }

        // ---- Sender status ----
        SenderPanel.Children.Clear();
        var sender = GamepadManager.GamepadSender;
        var mode = AppConfig.Current?.DropdownState?.GamepadSendMode.ToString() ?? "(unknown)";
        SenderPanel.Children.Add(MakeKv("Active send-mode", mode));
        SenderPanel.Children.Add(MakeKv("Sender instance", sender?.GetType().Name ?? "(null — Gamepad off?)"));
        SenderPanel.Children.Add(MakeKv("Sender.CanWork",
            sender == null ? "false" : sender.CanWork.ToString(),
            warn: sender == null || !sender.CanWork));
        SenderPanel.Children.Add(MakeKv("AutoHideController", AppConfig.Current?.ToggleState?.AutoHideController.ToString() ?? "?"));
        SenderPanel.Children.Add(MakeKv("UseControllerForAim", AppConfig.Current?.ToggleState?.UseControllerForAim.ToString() ?? "?"));
        var hidPath = PowerAim.InputLogic.HidHide.HidHideHelper.GetHidHidePath();
        SenderPanel.Children.Add(MakeKv("HidHide installed",
            (!string.IsNullOrEmpty(hidPath) && System.IO.File.Exists(hidPath)).ToString()));

        // ---- Suggestion ----
        SuggestionText.Text = BuildSuggestion(sender, realSlot, mode);
    }

    private static string BuildSuggestion(IGamepadSender? sender, int realSlot, string mode)
    {
        if (sender == null)
            return "GamepadManager has no sender. Initialise it via Settings → Gamepad → 'Send-mode' (ViGEm is the default).";
        if (!sender.CanWork)
            return "Sender exists but CanWork=false — the ViGEm bus driver is probably not installed. Get it from https://github.com/ViGEm/ViGEmBus/releases and reboot.";
        if (mode == "ViGEm" && realSlot == 0)
            return "Your real pad is at XInput slot 0, which is the slot most games (CoD, Apex, etc.) read from. PowerAim's virtual pad lands at slot 1 — the game won't see it.\n\n" +
                   "Three options:\n" +
                   "1. Switch send-mode to 'XInputHook' (Settings → Gamepad → Send-mode). This injects a DLL into the game and redirects XInput calls — no HidHide, no slot games. Best for most users.\n\n" +
                   "2. Enable 'Hide physical controller from games' on the Aim Tools card. Requires HidHide installed.\n\n" +
                   "3. Unplug your physical controller, restart PowerAim, then plug it back in. The virtual pad claims slot 0 first.";
        if (mode == "ViGEm")
            return "Your real pad's slot suggests the virtual pad has slot 0 — that's the configuration you want. If the game still doesn't react, hit the 'Fire test RT pulse' button below and watch the slot-0 row for an RT spike.";
        if (mode == "XInputHook")
            return "XInputHook mode is active. This injects into the running game process — make sure the game is running and the target-process name is set in Settings → Gamepad → 'Gamepad process'.";
        return "Sender is alive. If your game still doesn't react, fire the test pulse below and watch which slot lights up to identify which pad the game is reading from.";
    }

    // ============================================================================ UI BUILDERS ====

    private FrameworkElement BuildSlotRow(int index, bool connected, string detail)
    {
        var dot = new Ellipse_ { Diameter = 8 };
        dot.Color = connected
            ? (TryFindResource("FluentAccent") as Brush ?? Brushes.LimeGreen)
            : new SolidColorBrush(Color.FromArgb(80, 200, 200, 200));

        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dotHost = new System.Windows.Shapes.Ellipse
        {
            Width = 10, Height = 10,
            Fill = dot.Color,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(dotHost, 0);
        grid.Children.Add(dotHost);

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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
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

    // Placeholder for a coloured dot — kept inline for brevity.
    private class Ellipse_ { public Brush Color = Brushes.Transparent; public double Diameter; }

    // ============================================================================ HANDLERS ====

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private async void TestPulse_Click(object sender, RoutedEventArgs e)
    {
        var s = GamepadManager.GamepadSender;
        if (s == null || !s.CanWork)
        {
            SuggestionText.Text = "Sender unavailable — can't fire a pulse. See the suggestions above.";
            return;
        }
        // Send a 200ms RT spike with explicit pause/resume around it so the SyncLoop doesn't
        // immediately overwrite the value with the physical pad's resting state.
        try
        {
            s.SetSliderValue(GamepadSlider.RightTrigger, 255, GamepadSyncState.Paused);
            await Task.Delay(200);
            s.SetSliderValue(GamepadSlider.RightTrigger, 0, GamepadSyncState.Resume);
            SuggestionText.Text = "Test pulse fired. Refresh the slot snapshot above — whichever slot showed RT spike around 255 is where your game is looking when reading the virtual pad.";
        }
        catch (Exception ex)
        {
            SuggestionText.Text = $"Test pulse failed: {ex.Message}";
        }
        // Refresh after a short delay so the user can see the snapshot AFTER the pulse settled.
        await Task.Delay(50);
        Refresh();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
