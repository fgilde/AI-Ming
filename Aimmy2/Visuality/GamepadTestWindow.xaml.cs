using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SharpDX.XInput;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.Gamepad.Interaction;

namespace Aimmy2.Visuality;

public partial class GamepadTestWindow : Window
{
    private readonly DispatcherTimer _updateTimer;
    private Controller? _controller;
    private readonly SolidColorBrush _pressedBrush = new SolidColorBrush(Colors.LimeGreen);
    private readonly SolidColorBrush _releasedBrush = new SolidColorBrush(Colors.DimGray);

    public GamepadTestWindow()
    {
        InitializeComponent();

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16.67) // 60 FPS
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += GamepadTestWindow_Loaded;
        Closing += GamepadTestWindow_Closing;
    }

    private void GamepadTestWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeController();
        _updateTimer.Start();
    }

    private void GamepadTestWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _updateTimer.Stop();
    }

    private void InitializeController()
    {
        // Try to use the controller from GamepadManager if available
        if (GamepadManager.GamepadReader != null && GamepadManager.GamepadReader.IsConnected)
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
                    ConnectionStatus.Text = $"Connected (Controller {(int)i + 1})";
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
}
