using System.Collections.Concurrent;
using Aimmy2.InputLogic.Contracts;
using SharpDX.XInput;

namespace Aimmy2.InputLogic.Gamepad.Interaction;

public class GamepadSenderInternal : IGamepadSender
{
    private const int MAX_ACTIONS_PER_ITERATION = 100;
    private const int SYNC_LOOP_DELAY_MS = 1; // ~1000 Hz update rate
    
    private Controller? _physicalController;
    private bool _isRunning;
    private readonly HashSet<GamepadButton> _pausedButtons = new();
    private readonly HashSet<GamepadSlider> _pausedSliders = new();
    private readonly HashSet<GamepadAxis> _pausedAxes = new();
    private readonly BlockingCollection<Action> _actions = new();
    private readonly Gamepad _virtualGamepad = new(); // Reuse to avoid allocations

    // Virtual controller state
    private readonly Dictionary<GamepadButton, bool> _virtualButtonStates = new();
    private readonly Dictionary<GamepadSlider, byte> _virtualSliderStates = new();
    private readonly Dictionary<GamepadAxis, short> _virtualAxisStates = new();

    // Physical controller state snapshot
    private State _currentPhysicalState;
    private State _currentVirtualState;

    public GamepadSenderInternal()
    {
        // Initialize virtual states
        foreach (GamepadButton button in Enum.GetValues(typeof(GamepadButton)))
        {
            _virtualButtonStates[button] = false;
        }
        foreach (GamepadSlider slider in Enum.GetValues(typeof(GamepadSlider)))
        {
            _virtualSliderStates[slider] = 0;
        }
        foreach (GamepadAxis axis in Enum.GetValues(typeof(GamepadAxis)))
        {
            _virtualAxisStates[axis] = 0;
        }
    }

    public bool CanWork => true;

    public State CurrentVirtualState => _currentVirtualState;

    public IGamepadSender SyncWith(Controller physicalController)
    {
        _physicalController = physicalController;
        _isRunning = true;
        var thread = new Thread(SyncLoop);
        thread.Start();
        return this;
    }

    public IGamepadSender StopSync()
    {
        _isRunning = false;
        return this;
    }

    public IGamepadSender PauseSync(GamepadButton button)
    {
        _actions.Add(() => _pausedButtons.Add(button));
        return this;
    }

    public IGamepadSender PauseSync(GamepadSlider slider)
    {
        _actions.Add(() => _pausedSliders.Add(slider));
        return this;
    }

    public IGamepadSender PauseSync(GamepadAxis axis)
    {
        _actions.Add(() => _pausedAxes.Add(axis));
        return this;
    }

    public IGamepadSender ResumeSync()
    {
        _actions.Add(() =>
        {
            _pausedButtons.Clear();
            _pausedSliders.Clear();
            _pausedAxes.Clear();
        });
        return this;
    }

    public IGamepadSender ResumeSync(GamepadButton button)
    {
        _actions.Add(() => _pausedButtons.Remove(button));
        return this;
    }

    public IGamepadSender ResumeSync(GamepadSlider slider)
    {
        _actions.Add(() => _pausedSliders.Remove(slider));
        return this;
    }

    public IGamepadSender ResumeSync(GamepadAxis axis)
    {
        _actions.Add(() => _pausedAxes.Remove(axis));
        return this;
    }

    public IGamepadSender SetButtonState(GamepadButton button, bool pressed, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused)
            PauseSync(button);
        
        _actions.Add(() => _virtualButtonStates[button] = pressed);
        
        if (gamepadSyncState == GamepadSyncState.Resume)
            ResumeSync(button);
        
        return this;
    }

    public IGamepadSender SetSliderValue(GamepadSlider slider, byte value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused)
            PauseSync(slider);
        
        _actions.Add(() => _virtualSliderStates[slider] = value);
        
        if (gamepadSyncState == GamepadSyncState.Resume)
            ResumeSync(slider);
        
        return this;
    }

    public IGamepadSender SetAxisValue(GamepadAxis axis, short value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused)
            PauseSync(axis);
        
        _actions.Add(() => _virtualAxisStates[axis] = value);
        
        if (gamepadSyncState == GamepadSyncState.Resume)
            ResumeSync(axis);
        
        return this;
    }

    private void SyncLoop()
    {
        while (_isRunning)
        {
            var controller = _physicalController; // Cache for thread safety
            if (controller == null || !controller.IsConnected)
            {
                Thread.Sleep(100); // Wait longer if disconnected
                continue;
            }
            
            _currentPhysicalState = controller.GetState();

            // Process pending actions
            int actionsProcessed = 0;
            while (_actions.TryTake(out var action, 0) && actionsProcessed < MAX_ACTIONS_PER_ITERATION)
            {
                action();
                actionsProcessed++;
            }

            // Build virtual state from physical state and overrides
            _virtualGamepad.Buttons = BuildButtonFlags();

            // Sync triggers
            _virtualGamepad.LeftTrigger = !_pausedSliders.Contains(GamepadSlider.LeftTrigger)
                ? _currentPhysicalState.Gamepad.LeftTrigger
                : _virtualSliderStates[GamepadSlider.LeftTrigger];

            _virtualGamepad.RightTrigger = !_pausedSliders.Contains(GamepadSlider.RightTrigger)
                ? _currentPhysicalState.Gamepad.RightTrigger
                : _virtualSliderStates[GamepadSlider.RightTrigger];

            // Sync axes
            _virtualGamepad.LeftThumbX = !_pausedAxes.Contains(GamepadAxis.LeftThumbX)
                ? _currentPhysicalState.Gamepad.LeftThumbX
                : _virtualAxisStates[GamepadAxis.LeftThumbX];

            _virtualGamepad.LeftThumbY = !_pausedAxes.Contains(GamepadAxis.LeftThumbY)
                ? _currentPhysicalState.Gamepad.LeftThumbY
                : _virtualAxisStates[GamepadAxis.LeftThumbY];

            _virtualGamepad.RightThumbX = !_pausedAxes.Contains(GamepadAxis.RightThumbX)
                ? _currentPhysicalState.Gamepad.RightThumbX
                : _virtualAxisStates[GamepadAxis.RightThumbX];

            _virtualGamepad.RightThumbY = !_pausedAxes.Contains(GamepadAxis.RightThumbY)
                ? _currentPhysicalState.Gamepad.RightThumbY
                : _virtualAxisStates[GamepadAxis.RightThumbY];

            // Update current virtual state
            _currentVirtualState = new State
            {
                PacketNumber = _currentPhysicalState.PacketNumber,
                Gamepad = _virtualGamepad
            };

            Thread.Sleep(SYNC_LOOP_DELAY_MS);
        }
    }

    private GamepadButtonFlags BuildButtonFlags()
    {
        GamepadButtonFlags flags = 0;

        foreach (GamepadButton button in Enum.GetValues(typeof(GamepadButton)))
        {
            bool pressed;
            if (_pausedButtons.Contains(button))
            {
                // Use virtual state
                pressed = _virtualButtonStates[button];
            }
            else
            {
                // Use physical state
                pressed = IsPhysicalButtonPressed(button);
            }

            if (pressed)
            {
                flags |= button.ToGamepadButtonFlags();
            }
        }

        return flags;
    }

    private bool IsPhysicalButtonPressed(GamepadButton button)
    {
        var physicalFlags = _currentPhysicalState.Gamepad.Buttons;
        return physicalFlags.HasFlag(button.ToGamepadButtonFlags());
    }

    public void Dispose()
    {
        StopSync();
        _actions.Dispose();
    }
}
