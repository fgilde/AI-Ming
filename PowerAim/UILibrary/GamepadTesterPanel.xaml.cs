using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Contracts;
using PowerAim.InputLogic.Gamepad.Interaction;
using SharpDX.XInput;

namespace PowerAim.UILibrary;

public partial class GamepadTesterPanel : UserControl
{
    private const double TARGET_FPS = 60;
    private readonly DispatcherTimer _updateTimer;
    private Controller? _controller;
    private readonly SolidColorBrush _pressedBrush = new SolidColorBrush(Color.FromArgb(255, 73, 215, 132));
    private readonly SolidColorBrush _connectedDot = new SolidColorBrush(Color.FromArgb(255, 73, 215, 132));
    private readonly SolidColorBrush _disconnectedDot = new SolidColorBrush(Color.FromArgb(255, 232, 17, 35));
    private SolidColorBrush? _releasedBrush;
    private CancellationTokenSource? _sequenceCts;
    private bool _isSequenceRunning;

    public event EventHandler? BackRequested;

    public GamepadTesterPanel()
    {
        InitializeComponent();

        _updateTimer = new DispatcherTimer
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
                         ?? new SolidColorBrush(Color.FromArgb(255, 44, 44, 44));
        InitializeController();
        _updateTimer.Start();
    }

    private void GamepadTesterPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
        _sequenceCts?.Cancel();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void InitializeController()
    {
        if (GamepadManager.GamepadReader != null &&
            GamepadManager.GamepadReader.IsConnected &&
            GamepadManager.GamepadReader.Controller != null)
        {
            _controller = GamepadManager.GamepadReader.Controller;
            ConnectionStatus.Text = "Connected";
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
                    ConnectionStatus.Text = $"Connected (Controller {controllerNumber})";
                    StatusDot.Fill = _connectedDot;
                    return;
                }
            }

            ConnectionStatus.Text = "Disconnected";
            StatusDot.Fill = _disconnectedDot;
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_controller == null || !_controller.IsConnected)
        {
            ConnectionStatus.Text = "Disconnected";
            StatusDot.Fill = _disconnectedDot;
            return;
        }

        State state;
        if (GamepadManager.GamepadSender is GamepadSenderInternal internalSender)
            state = internalSender.CurrentVirtualState;
        else
            state = _controller.GetState();

        UpdateButtonStates(state);
        UpdateTriggerStates(state);
        UpdateThumbstickStates(state);
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
                "A_Button" => "Presses the A button for 500ms",
                "AB_Combo" => "Presses A and B buttons together for 1 second",
                "DPad_Circle" => "Simulates D-Pad presses in a circular pattern: Up → Right → Down → Left",
                "LStick_Circle" => "Moves left thumbstick in a circular motion",
                "Trigger_Press" => "Presses both triggers to maximum and releases",
                "Complex_Combo" => "Complex sequence: A → B → X → Y with D-Pad movements",
                _ => ""
            };
        }
    }

    private async void StartSequenceButton_Click(object sender, RoutedEventArgs e)
    {
        if (GamepadManager.GamepadSender == null)
        {
            SequenceStatus.Text = "Error: No gamepad sender available. Please select a gamepad mode.";
            SequenceStatus.Foreground = _disconnectedDot;
            return;
        }

        if (_isSequenceRunning) return;

        _isSequenceRunning = true;
        StartSequenceButton.IsEnabled = false;
        StopSequenceButton.IsEnabled = true;
        _sequenceCts = new CancellationTokenSource();

        var timeout = (int)TimeoutSlider.Value;
        var sequenceTag = (SequenceSelector.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DPad_Circle";

        try
        {
            for (int i = timeout; i > 0; i--)
            {
                SequenceStatus.Text = $"Starting in {i}…";
                SequenceStatus.Foreground = _connectedDot;
                await Task.Delay(1000, _sequenceCts.Token);
            }
            SequenceStatus.Text = "Running sequence…";
            SequenceStatus.Foreground = _connectedDot;
            await ExecuteSequence(sequenceTag, _sequenceCts.Token);
            SequenceStatus.Text = "Sequence completed.";
        }
        catch (OperationCanceledException)
        {
            SequenceStatus.Text = "Sequence stopped.";
        }
        catch (Exception ex)
        {
            SequenceStatus.Text = $"Error: {ex.Message}";
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
