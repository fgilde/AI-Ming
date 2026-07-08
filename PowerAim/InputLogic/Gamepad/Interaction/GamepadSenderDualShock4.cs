using System.Collections.Concurrent;
using PowerAim.InputLogic.Contracts;
using SharpDX.XInput;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;

/// <summary>
///     Emulates a Sony DualShock 4 through ViGEm — the PlayStation counterpart to
///     <see cref="GamepadSenderViGEm"/>. Structure and threading are identical (a single background
///     loop drains a queue of direct SetButton/SetAxis calls and mirrors an optional physical pad), with
///     two DS4-specific twists: the D-pad is a single 8-way direction (not four buttons), so the four
///     D-pad members are combined into a <see cref="DualShock4DPadDirection"/>; and DS4 thumb axes are
///     bytes (centre 128), so XInput's short axes are rescaled.
/// </summary>
public class GamepadSenderDualShock4 : IGamepadSender
{
    private IGamepadStateSource? _source;
    private IDualShock4Controller? _virtualController;
    private bool _isRunning;
    private ViGEmClient? _client;

    private readonly BlockingCollection<Action> _actions = new();
    private readonly HashSet<GamepadButton> _pausedButtons = new();
    private readonly HashSet<GamepadSlider> _pausedSliders = new();
    private readonly HashSet<GamepadAxis> _pausedAxes = new();

    // Current D-pad state (loop-thread only). Combined into a direction on every change.
    private bool _dpUp, _dpDown, _dpLeft, _dpRight;

    private bool _connected;
    public bool IsConnected => _connected;
    public string LastError { get; private set; } = "";

    public GamepadSenderDualShock4() => EnsureConnected();

    private bool EnsureConnected()
    {
        if (_connected) return true;
        try
        {
            _client = new ViGEmClient();
            _virtualController = _client.CreateDualShock4Controller(); // Sony DS4 VID/PID defaults
            _virtualController.Connect();
            _connected = true;
            LastError = "";
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GamepadSenderDualShock4] ViGEm DS4 init failed: {ex.Message}");
            _virtualController = null;
            _client = null;
            LastError = ex.Message;
            _connected = false;
            return false;
        }
    }

    public bool CanWork => _virtualController != null && _connected;

    public IGamepadSender SyncWith(IGamepadStateSource? source)
    {
        _source = source;
        if (_isRunning) return this;
        _isRunning = true;
        new Thread(SyncLoop) { IsBackground = true, Name = "GamepadSenderDualShock4-Loop" }.Start();
        return this;
    }

    public IGamepadSender StopSync() { _isRunning = false; return this; }

    public IGamepadSender PauseSync(GamepadButton button) { _actions.Add(() => _pausedButtons.Add(button)); return this; }
    public IGamepadSender PauseSync(GamepadSlider slider) { _actions.Add(() => _pausedSliders.Add(slider)); return this; }
    public IGamepadSender PauseSync(GamepadAxis axis) { _actions.Add(() => _pausedAxes.Add(axis)); return this; }

    public IGamepadSender ResumeSync()
    {
        _actions.Add(() => { _pausedButtons.Clear(); _pausedSliders.Clear(); _pausedAxes.Clear(); });
        return this;
    }

    public IGamepadSender ResumeSync(GamepadButton button) { _actions.Add(() => _pausedButtons.Remove(button)); return this; }
    public IGamepadSender ResumeSync(GamepadSlider slider) { _actions.Add(() => _pausedSliders.Remove(slider)); return this; }
    public IGamepadSender ResumeSync(GamepadAxis axis) { _actions.Add(() => _pausedAxes.Remove(axis)); return this; }

    public IGamepadSender SetButtonState(GamepadButton button, bool pressed, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused) PauseSync(button);
        _actions.Add(() =>
        {
            if (button.IsDPad()) { SetDPad(button, pressed); ApplyDPad(); }
            else _virtualController?.SetButtonState(button.ToDualShock4Button(), pressed);
        });
        if (gamepadSyncState == GamepadSyncState.Resume) ResumeSync(button);
        return this;
    }

    public IGamepadSender SetSliderValue(GamepadSlider slider, byte value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused) PauseSync(slider);
        _actions.Add(() => _virtualController?.SetSliderValue(slider.ToDualShock4Slider(), value));
        if (gamepadSyncState == GamepadSyncState.Resume) ResumeSync(slider);
        return this;
    }

    public IGamepadSender SetAxisValue(GamepadAxis axis, short value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused) PauseSync(axis);
        _actions.Add(() => _virtualController?.SetAxisValue(axis.ToDualShock4Axis(), GamepadEnumExtensions.ToDualShock4AxisByte(value)));
        if (gamepadSyncState == GamepadSyncState.Resume) ResumeSync(axis);
        return this;
    }

    private void SetDPad(GamepadButton button, bool pressed)
    {
        switch (button)
        {
            case GamepadButton.Up: _dpUp = pressed; break;
            case GamepadButton.Down: _dpDown = pressed; break;
            case GamepadButton.Left: _dpLeft = pressed; break;
            case GamepadButton.Right: _dpRight = pressed; break;
        }
    }

    private void ApplyDPad() =>
        _virtualController?.SetDPadDirection(GamepadEnumExtensions.ToDualShock4DPad(_dpUp, _dpDown, _dpLeft, _dpRight));

    private void SyncLoop()
    {
        while (_isRunning)
        {
            while (_actions.TryTake(out var action, 0))
            {
                try { action(); } catch { /* transient ViGEm failure — keep the loop alive */ }
            }

            if (_source == null || !_source.IsConnected) { Thread.Sleep(2); continue; }

            State state;
            try { state = _source.GetState(); } catch { Thread.Sleep(10); continue; }
            var b = state.Gamepad.Buttons;

            void Mirror(GamepadButton g, GamepadButtonFlags f)
            {
                if (_pausedButtons.Contains(g)) return;
                _virtualController?.SetButtonState(g.ToDualShock4Button(), b.HasFlag(f));
            }

            Mirror(GamepadButton.A, GamepadButtonFlags.A);
            Mirror(GamepadButton.B, GamepadButtonFlags.B);
            Mirror(GamepadButton.X, GamepadButtonFlags.X);
            Mirror(GamepadButton.Y, GamepadButtonFlags.Y);
            Mirror(GamepadButton.LeftShoulder, GamepadButtonFlags.LeftShoulder);
            Mirror(GamepadButton.RightShoulder, GamepadButtonFlags.RightShoulder);
            Mirror(GamepadButton.Back, GamepadButtonFlags.Back);
            Mirror(GamepadButton.Start, GamepadButtonFlags.Start);
            Mirror(GamepadButton.LeftThumb, GamepadButtonFlags.LeftThumb);
            Mirror(GamepadButton.RightThumb, GamepadButtonFlags.RightThumb);

            // D-pad: update only the non-paused directions from the physical pad, then combine.
            if (!_pausedButtons.Contains(GamepadButton.Up)) _dpUp = b.HasFlag(GamepadButtonFlags.DPadUp);
            if (!_pausedButtons.Contains(GamepadButton.Down)) _dpDown = b.HasFlag(GamepadButtonFlags.DPadDown);
            if (!_pausedButtons.Contains(GamepadButton.Left)) _dpLeft = b.HasFlag(GamepadButtonFlags.DPadLeft);
            if (!_pausedButtons.Contains(GamepadButton.Right)) _dpRight = b.HasFlag(GamepadButtonFlags.DPadRight);
            ApplyDPad();

            if (!_pausedSliders.Contains(GamepadSlider.LeftTrigger)) _virtualController?.SetSliderValue(DualShock4Slider.LeftTrigger, state.Gamepad.LeftTrigger);
            if (!_pausedSliders.Contains(GamepadSlider.RightTrigger)) _virtualController?.SetSliderValue(DualShock4Slider.RightTrigger, state.Gamepad.RightTrigger);

            // Straight short→byte for every axis — NO Y inversion, so the mirror matches the direct
            // SetAxisValue path the aim pipeline uses (they must agree on orientation). If a game turns
            // out to want DS4-inverted Y, that's a single deliberate change in both paths.
            if (!_pausedAxes.Contains(GamepadAxis.LeftThumbX)) _virtualController?.SetAxisValue(DualShock4Axis.LeftThumbX, GamepadEnumExtensions.ToDualShock4AxisByte(state.Gamepad.LeftThumbX));
            if (!_pausedAxes.Contains(GamepadAxis.LeftThumbY)) _virtualController?.SetAxisValue(DualShock4Axis.LeftThumbY, GamepadEnumExtensions.ToDualShock4AxisByte(state.Gamepad.LeftThumbY));
            if (!_pausedAxes.Contains(GamepadAxis.RightThumbX)) _virtualController?.SetAxisValue(DualShock4Axis.RightThumbX, GamepadEnumExtensions.ToDualShock4AxisByte(state.Gamepad.RightThumbX));
            if (!_pausedAxes.Contains(GamepadAxis.RightThumbY)) _virtualController?.SetAxisValue(DualShock4Axis.RightThumbY, GamepadEnumExtensions.ToDualShock4AxisByte(state.Gamepad.RightThumbY));

            Thread.Sleep(1);
        }
    }

    public void Dispose()
    {
        StopSync();
        try { _virtualController?.Disconnect(); } catch { /* already gone */ }
        _client?.Dispose();
    }
}
