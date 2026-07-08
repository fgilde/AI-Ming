using SharpDX.XInput;

namespace PowerAim.InputLogic.Contracts;

public interface IGamepadSender: IDisposable
{
    bool CanWork { get; }
    string LastError => "";

    /// <summary>
    ///     Mirror a physical pad (any transport) into the virtual one. Pass null for standalone use —
    ///     the sender still drains direct SetButton/SetAxis calls from the aim pipeline.
    /// </summary>
    IGamepadSender SyncWith(IGamepadStateSource? source);
    IGamepadSender StopSync();
    IGamepadSender PauseSync(GamepadButton button);
    IGamepadSender PauseSync(GamepadSlider slider);
    IGamepadSender PauseSync(GamepadAxis axis);
    IGamepadSender ResumeSync();
    IGamepadSender ResumeSync(GamepadButton button);
    IGamepadSender ResumeSync(GamepadSlider slider);
    IGamepadSender ResumeSync(GamepadAxis axis);
    IGamepadSender SetButtonState(GamepadButton button, bool pressed, GamepadSyncState gamepadSyncState = GamepadSyncState.None);
    IGamepadSender SetSliderValue(GamepadSlider slider, byte value, GamepadSyncState gamepadSyncState = GamepadSyncState.None);
    IGamepadSender SetAxisValue(GamepadAxis axis, short value, GamepadSyncState gamepadSyncState = GamepadSyncState.None);
}
