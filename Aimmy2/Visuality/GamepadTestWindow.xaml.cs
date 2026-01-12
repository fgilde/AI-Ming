using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SharpDX.XInput;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.Contracts;
using Aimmy2.InputLogic.Gamepad.Interaction;

namespace Aimmy2.Visuality;

public partial class GamepadTestWindow : Window
{
    private const double TARGET_FPS = 60;
    private readonly DispatcherTimer _updateTimer;
    private Controller? _controller;
    private readonly SolidColorBrush _pressedBrush = new SolidColorBrush(Colors.LimeGreen);
    private readonly SolidColorBrush _releasedBrush = new SolidColorBrush(Colors.DimGray);
    private CancellationTokenSource? _sequenceCts;
    private bool _isSequenceRunning;

    public GamepadTestWindow()
    {
        InitializeComponent();

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / (long)TARGET_FPS) // Exact 60 FPS
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += GamepadTestWindow_Loaded;
        Closing += GamepadTestWindow_Closing;
        
        // Wire up timeout slider
        TimeoutSlider.ValueChanged += (s, e) => TimeoutValue.Text = $"{(int)TimeoutSlider.Value}s";
    }

    private void GamepadTestWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeController();
        _updateTimer.Start();
    }

    private void GamepadTestWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _updateTimer.Stop();
        _sequenceCts?.Cancel();
    }

    private void InitializeController()
    {
        // Try to use the controller from GamepadManager if available
        if (GamepadManager.GamepadReader != null && 
            GamepadManager.GamepadReader.IsConnected &&
            GamepadManager.GamepadReader.Controller != null)
        {
            _controller = GamepadManager.GamepadReader.Controller;
            ConnectionStatus.Text = "Connected";
            ConnectionStatus.Foreground = _pressedBrush;
        }
        else
        {
            // Try to connect to any available controller
            for (var i = UserIndex.One; i <= UserIndex.Four; i++)
            {
                var controller = new Controller(i);
                if (controller.IsConnected)
                {
                    _controller = controller;
                    var controllerNumber = i - UserIndex.One + 1; // Convert enum to 1-based number
                    ConnectionStatus.Text = $"Connected (Controller {controllerNumber})";
                    ConnectionStatus.Foreground = _pressedBrush;
                    return;
                }
            }
            
            ConnectionStatus.Text = "Disconnected";
            ConnectionStatus.Foreground = new SolidColorBrush(Colors.Red);
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_controller == null || !_controller.IsConnected)
        {
            ConnectionStatus.Text = "Disconnected";
            ConnectionStatus.Foreground = new SolidColorBrush(Colors.Red);
            return;
        }

        State state;

        // Check if we're using Internal mode and can get virtual state
        if (GamepadManager.GamepadSender is GamepadSenderInternal internalSender)
        {
            state = internalSender.CurrentVirtualState;
        }
        else
        {
            state = _controller.GetState();
        }

        UpdateButtonStates(state);
        UpdateTriggerStates(state);
        UpdateThumbstickStates(state);
    }

    private void UpdateButtonStates(State state)
    {
        var buttons = state.Gamepad.Buttons;

        // Face buttons
        UpdateButton(BtnA, buttons.HasFlag(GamepadButtonFlags.A));
        UpdateButton(BtnB, buttons.HasFlag(GamepadButtonFlags.B));
        UpdateButton(BtnX, buttons.HasFlag(GamepadButtonFlags.X));
        UpdateButton(BtnY, buttons.HasFlag(GamepadButtonFlags.Y));

        // D-Pad
        UpdateButton(BtnUp, buttons.HasFlag(GamepadButtonFlags.DPadUp));
        UpdateButton(BtnDown, buttons.HasFlag(GamepadButtonFlags.DPadDown));
        UpdateButton(BtnLeft, buttons.HasFlag(GamepadButtonFlags.DPadLeft));
        UpdateButton(BtnRight, buttons.HasFlag(GamepadButtonFlags.DPadRight));

        // Shoulders
        UpdateButton(BtnLB, buttons.HasFlag(GamepadButtonFlags.LeftShoulder));
        UpdateButton(BtnRB, buttons.HasFlag(GamepadButtonFlags.RightShoulder));

        // Center buttons
        UpdateButton(BtnStart, buttons.HasFlag(GamepadButtonFlags.Start));
        UpdateButton(BtnBack, buttons.HasFlag(GamepadButtonFlags.Back));

        // Thumbstick buttons
        UpdateButton(BtnLS, buttons.HasFlag(GamepadButtonFlags.LeftThumb));
        UpdateButton(BtnRS, buttons.HasFlag(GamepadButtonFlags.RightThumb));
    }

    private void UpdateButton(System.Windows.Controls.Border button, bool isPressed)
    {
        button.Background = isPressed ? _pressedBrush : _releasedBrush;
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
        var leftX = state.Gamepad.LeftThumbX;
        var leftY = state.Gamepad.LeftThumbY;
        var rightX = state.Gamepad.RightThumbX;
        var rightY = state.Gamepad.RightThumbY;

        LeftThumbstickValue.Text = $"X: {leftX}, Y: {leftY}";
        RightThumbstickValue.Text = $"X: {rightX}, Y: {rightY}";
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        InitializeController();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
            SequenceStatus.Foreground = new SolidColorBrush(Colors.Red);
            return;
        }

        if (_isSequenceRunning)
            return;

        _isSequenceRunning = true;
        StartSequenceButton.IsEnabled = false;
        StopSequenceButton.IsEnabled = true;
        _sequenceCts = new CancellationTokenSource();

        var timeout = (int)TimeoutSlider.Value;
        var sequenceTag = (SequenceSelector.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DPad_Circle";

        try
        {
            // Countdown
            for (int i = timeout; i > 0; i--)
            {
                SequenceStatus.Text = $"Starting in {i}...";
                SequenceStatus.Foreground = new SolidColorBrush(Colors.Yellow);
                await Task.Delay(1000, _sequenceCts.Token);
            }

            SequenceStatus.Text = "Running sequence...";
            SequenceStatus.Foreground = new SolidColorBrush(Colors.LimeGreen);

            // Execute the selected sequence
            await ExecuteSequence(sequenceTag, _sequenceCts.Token);

            SequenceStatus.Text = "Sequence completed!";
            SequenceStatus.Foreground = new SolidColorBrush(Colors.LimeGreen);
        }
        catch (OperationCanceledException)
        {
            SequenceStatus.Text = "Sequence stopped.";
            SequenceStatus.Foreground = new SolidColorBrush(Colors.Orange);
        }
        catch (Exception ex)
        {
            SequenceStatus.Text = $"Error: {ex.Message}";
            SequenceStatus.Foreground = new SolidColorBrush(Colors.Red);
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

    private async Task PressButton(IGamepadSender sender, GamepadButton button, int durationMs, CancellationToken ct)
    {
        sender.PauseSync(button).SetButtonState(button, true);
        await Task.Delay(durationMs, ct);
        sender.SetButtonState(button, false).ResumeSync(button);
    }

    private async Task MoveStickCircle(IGamepadSender sender, GamepadAxis axisX, GamepadAxis axisY, int durationMs, CancellationToken ct)
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
