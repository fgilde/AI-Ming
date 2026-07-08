using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Contracts;
using PowerAim.InputLogic.Gamepad;
using PowerAim.InputLogic.Gamepad.Interaction;
using PowerAim;
using SharpDX.XInput;

namespace PowerAim.UILibrary;

public partial class GamepadTesterPanel : UserControl
{
    private const double TARGET_FPS = 60;
    private readonly DispatcherTimer _updateTimer;
    private Controller? _controller;
    // The controller the user picked to visualize (null = auto-detected default). Virtual + internal
    // send mode is read from the injected virtual state; everything else from its XInput slot.
    private ControllerInfo? _selected;
    private readonly SolidColorBrush _pressedBrush = new(Color.FromArgb(255, 73, 215, 132));
    private readonly SolidColorBrush _connectedDot = new(Color.FromArgb(255, 73, 215, 132));
    private readonly SolidColorBrush _disconnectedDot = new(Color.FromArgb(255, 232, 17, 35));
    private SolidColorBrush? _releasedBrush;
    private CancellationTokenSource? _sequenceCts;
    private bool _isSequenceRunning;

    public event EventHandler? BackRequested;

    public GamepadTesterPanel()
    {
        InitializeComponent();

        _updateTimer = new()
        {
            Interval = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / (long)TARGET_FPS)
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += GamepadTesterPanel_Loaded;
        Unloaded += GamepadTesterPanel_Unloaded;

        TimeoutSlider.ValueChanged += (s, e) => TimeoutValue.Text = $"{(int)TimeoutSlider.Value}s";
    }

    private void GamepadTesterPanel_Loaded(object sender, RoutedEventArgs e)
    {
        _releasedBrush = TryFindResource("FluentSurface3") as SolidColorBrush
                         ?? new(Color.FromArgb(255, 44, 44, 44));
        InitializeController();
        ControllerPicker.SelectionChanged += OnControllerPicked;
        _updateTimer.Start();
    }

    private void GamepadTesterPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
        _sequenceCts?.Cancel();
        ControllerPicker.SelectionChanged -= OnControllerPicked;
    }

    /// <summary>
    ///     The user picked a controller to visualize. A physical pad (or a ViGEm virtual with a known
    ///     slot) is read from its XInput slot; the virtual pad in internal send mode is read from the
    ///     injected virtual state in the tick. This is what lets you SEE that a test sequence lands on
    ///     the virtual pad (select it → buttons light up) but not on the physical one (select it → no
    ///     test effect, only your own input).
    /// </summary>
    private void OnControllerPicked(object? sender, ControllerInfo? info)
    {
        _selected = info;
        _controller = info?.Slot is int slot ? new Controller((UserIndex)slot) : null;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Open the tester in a stand-alone non-modal window so it stays visible while the user
    ///     navigates to other PowerAim pages — useful for verifying KB→Pad mapping while editing
    ///     the mapping profile, or watching live stick values while tuning sensitivities.
    /// </summary>
    private void PopOut_Click(object sender, RoutedEventArgs e)
    {
        // Re-use the existing GamepadTesterWindow if already open to avoid stacking copies.
        if (PowerAim.Visuality.GamepadTesterWindow.Current is { } existing)
        {
            existing.Activate();
            return;
        }
        var win = new PowerAim.Visuality.GamepadTesterWindow();
        win.Show();
    }

    private void InitializeController()
    {
        // The tester visualizes an XInput Controller. A DirectInput-backed reader (e.g. a raw DualSense)
        // has no XInput Controller, so fall through to the slot scan (which also only finds XInput pads).
        if (GamepadManager.GamepadReader is PowerAim.InputLogic.Contracts.IXInputGamepadReader xr &&
            xr.IsConnected)
        {
            _controller = xr.Controller;
            ConnectionStatus.Text = Locale.Connected;
            StatusDot.Fill = _connectedDot;
        }
        else
        {
            for (var i = UserIndex.One; i <= UserIndex.Four; i++)
            {
                var controller = new Controller(i);
                if (controller.IsConnected)
                {
                    _controller = controller;
                    var controllerNumber = i - UserIndex.One + 1;
                    ConnectionStatus.Text = string.Format(Locale.ConnectedControllerFormat, controllerNumber);
                    StatusDot.Fill = _connectedDot;
                    return;
                }
            }

            ConnectionStatus.Text = Locale.Disconnected;
            StatusDot.Fill = _disconnectedDot;
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        var state = ReadState();
        if (state == null)
        {
            ConnectionStatus.Text = Locale.Disconnected;
            StatusDot.Fill = _disconnectedDot;
            return;
        }

        StatusDot.Fill = _connectedDot;
        ConnectionStatus.Text = _selected?.Name ?? Locale.Connected;

        UpdateButtonStates(state.Value);
        UpdateTriggerStates(state.Value);
        UpdateThumbstickStates(state.Value);
    }

    /// <summary>
    ///     Live state of the currently-visualized source. The internal-mode virtual pad has no real
    ///     XInput device, so it's read from the injected virtual state; physical pads (and a ViGEm
    ///     virtual with a known slot) are read from their XInput slot. RawSender is the unwrapped
    ///     concrete sender (GamepadSender is the reporting decorator).
    /// </summary>
    private State? ReadState()
    {
        var internalSender = GamepadManager.RawSender as GamepadSenderInternal;

        if (_selected != null)
        {
            if (_selected.Kind == ControllerKind.Virtual && internalSender != null)
                return internalSender.CurrentVirtualState;
            return _controller is { IsConnected: true } ? _controller.GetState() : (State?)null;
        }

        // No explicit pick: internal mode shows the virtual state (nothing else to read), otherwise
        // the auto-detected physical reader from InitializeController.
        if (internalSender != null) return internalSender.CurrentVirtualState;
        return _controller is { IsConnected: true } ? _controller.GetState() : (State?)null;
    }

    private void UpdateButtonStates(State state)
    {
        var buttons = state.Gamepad.Buttons;
        UpdateButton(BtnA, buttons.HasFlag(GamepadButtonFlags.A));
        UpdateButton(BtnB, buttons.HasFlag(GamepadButtonFlags.B));
        UpdateButton(BtnX, buttons.HasFlag(GamepadButtonFlags.X));
        UpdateButton(BtnY, buttons.HasFlag(GamepadButtonFlags.Y));
        UpdateButton(BtnUp, buttons.HasFlag(GamepadButtonFlags.DPadUp));
        UpdateButton(BtnDown, buttons.HasFlag(GamepadButtonFlags.DPadDown));
        UpdateButton(BtnLeft, buttons.HasFlag(GamepadButtonFlags.DPadLeft));
        UpdateButton(BtnRight, buttons.HasFlag(GamepadButtonFlags.DPadRight));
        UpdateButton(BtnLB, buttons.HasFlag(GamepadButtonFlags.LeftShoulder));
        UpdateButton(BtnRB, buttons.HasFlag(GamepadButtonFlags.RightShoulder));
        UpdateButton(BtnStart, buttons.HasFlag(GamepadButtonFlags.Start));
        UpdateButton(BtnBack, buttons.HasFlag(GamepadButtonFlags.Back));
        UpdateButton(BtnLS, buttons.HasFlag(GamepadButtonFlags.LeftThumb));
        UpdateButton(BtnRS, buttons.HasFlag(GamepadButtonFlags.RightThumb));
    }

    private void UpdateButton(Border button, bool isPressed)
    {
        button.Background = isPressed ? _pressedBrush : (Brush?)_releasedBrush ?? button.Background;
    }

    private void UpdateTriggerStates(State state)
    {
        var leftTrigger = state.Gamepad.LeftTrigger;
        var rightTrigger = state.Gamepad.RightTrigger;
        LeftTriggerBar.Value = leftTrigger;
        LeftTriggerValue.Text = leftTrigger.ToString();
        RightTriggerBar.Value = rightTrigger;
        RightTriggerValue.Text = rightTrigger.ToString();
    }

    private void UpdateThumbstickStates(State state)
    {
        LeftThumbstickValue.Text = $"{state.Gamepad.LeftThumbX}, {state.Gamepad.LeftThumbY}";
        RightThumbstickValue.Text = $"{state.Gamepad.RightThumbX}, {state.Gamepad.RightThumbY}";
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        InitializeController();
    }

    private void SequenceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SequenceSelector.SelectedItem is ComboBoxItem item && SequenceDescription != null)
        {
            SequenceDescription.Text = item.Tag?.ToString() switch
            {
                "A_Button" => Locale.SeqDescAButton,
                "AB_Combo" => Locale.SeqDescABCombo,
                "DPad_Circle" => Locale.SeqDpadCircleDescription,
                "LStick_Circle" => Locale.SeqDescLStickCircle,
                "Trigger_Press" => Locale.SeqDescTriggerPress,
                "Complex_Combo" => Locale.SeqDescComplexCombo,
                _ => ""
            };
        }
    }

    private async void StartSequenceButton_Click(object sender, RoutedEventArgs e)
    {
        if (GamepadManager.GamepadSender == null)
        {
            SequenceStatus.Text = Locale.NoGamepadSenderError;
            SequenceStatus.Foreground = _disconnectedDot;
            return;
        }

        if (_isSequenceRunning) return;

        _isSequenceRunning = true;
        StartSequenceButton.IsEnabled = false;
        StopSequenceButton.IsEnabled = true;
        _sequenceCts = new();

        var timeout = (int)TimeoutSlider.Value;
        var sequenceTag = (SequenceSelector.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DPad_Circle";

        try
        {
            for (int i = timeout; i > 0; i--)
            {
                SequenceStatus.Text = string.Format(Locale.StartingInFormat, i);
                SequenceStatus.Foreground = _connectedDot;
                await Task.Delay(1000, _sequenceCts.Token);
            }
            SequenceStatus.Text = Locale.RunningSequence;
            SequenceStatus.Foreground = _connectedDot;
            await ExecuteSequence(sequenceTag, _sequenceCts.Token);
            SequenceStatus.Text = Locale.SequenceCompleted;
        }
        catch (OperationCanceledException)
        {
            SequenceStatus.Text = Locale.SequenceStopped;
        }
        catch (Exception ex)
        {
            SequenceStatus.Text = string.Format(Locale.ErrorFormat, ex.Message);
            SequenceStatus.Foreground = _disconnectedDot;
        }
        finally
        {
            _isSequenceRunning = false;
            StartSequenceButton.IsEnabled = true;
            StopSequenceButton.IsEnabled = false;
            _sequenceCts?.Dispose();
            _sequenceCts = null;
        }
    }

    private void StopSequenceButton_Click(object sender, RoutedEventArgs e)
    {
        _sequenceCts?.Cancel();
    }

    private async Task ExecuteSequence(string sequenceTag, CancellationToken ct)
    {
        var sender = GamepadManager.GamepadSender;
        if (sender == null) return;

        switch (sequenceTag)
        {
            case "A_Button":
                await PressButton(sender, GamepadButton.A, 500, ct);
                break;
            case "AB_Combo":
                sender.PauseSync(GamepadButton.A).SetButtonState(GamepadButton.A, true);
                sender.PauseSync(GamepadButton.B).SetButtonState(GamepadButton.B, true);
                await Task.Delay(1000, ct);
                sender.SetButtonState(GamepadButton.A, false).ResumeSync(GamepadButton.A);
                sender.SetButtonState(GamepadButton.B, false).ResumeSync(GamepadButton.B);
                break;
            case "DPad_Circle":
                await PressButton(sender, GamepadButton.Up, 300, ct);
                await Task.Delay(100, ct);
                await PressButton(sender, GamepadButton.Right, 300, ct);
                await Task.Delay(100, ct);
                await PressButton(sender, GamepadButton.Down, 300, ct);
                await Task.Delay(100, ct);
                await PressButton(sender, GamepadButton.Left, 300, ct);
                break;
            case "LStick_Circle":
                await MoveStickCircle(sender, GamepadAxis.LeftThumbX, GamepadAxis.LeftThumbY, 2000, ct);
                break;
            case "Trigger_Press":
                sender.PauseSync(GamepadSlider.LeftTrigger).SetSliderValue(GamepadSlider.LeftTrigger, 255);
                sender.PauseSync(GamepadSlider.RightTrigger).SetSliderValue(GamepadSlider.RightTrigger, 255);
                await Task.Delay(1000, ct);
                sender.SetSliderValue(GamepadSlider.LeftTrigger, 0).ResumeSync(GamepadSlider.LeftTrigger);
                sender.SetSliderValue(GamepadSlider.RightTrigger, 0).ResumeSync(GamepadSlider.RightTrigger);
                break;
            case "Complex_Combo":
                await PressButton(sender, GamepadButton.A, 200, ct);
                await Task.Delay(100, ct);
                await PressButton(sender, GamepadButton.B, 200, ct);
                await Task.Delay(100, ct);
                await PressButton(sender, GamepadButton.Up, 300, ct);
                await Task.Delay(100, ct);
                await PressButton(sender, GamepadButton.X, 200, ct);
                await Task.Delay(100, ct);
                await PressButton(sender, GamepadButton.Y, 200, ct);
                await Task.Delay(100, ct);
                await PressButton(sender, GamepadButton.Down, 300, ct);
                break;
        }
    }

    private static async Task PressButton(IGamepadSender sender, GamepadButton button, int durationMs, CancellationToken ct)
    {
        sender.PauseSync(button).SetButtonState(button, true);
        await Task.Delay(durationMs, ct);
        sender.SetButtonState(button, false).ResumeSync(button);
    }

    private static async Task MoveStickCircle(IGamepadSender sender, GamepadAxis axisX, GamepadAxis axisY, int durationMs, CancellationToken ct)
    {
        sender.PauseSync(axisX);
        sender.PauseSync(axisY);
        int steps = 50;
        int delayPerStep = durationMs / steps;
        const short maxValue = 32767;
        for (int i = 0; i <= steps; i++)
        {
            if (ct.IsCancellationRequested) break;
            double angle = 2 * Math.PI * i / steps;
            short x = (short)(Math.Cos(angle) * maxValue);
            short y = (short)(Math.Sin(angle) * maxValue);
            sender.SetAxisValue(axisX, x);
            sender.SetAxisValue(axisY, y);
            await Task.Delay(delayPerStep, ct);
        }
        sender.SetAxisValue(axisX, 0);
        sender.SetAxisValue(axisY, 0);
        sender.ResumeSync(axisX);
        sender.ResumeSync(axisY);
    }
}
