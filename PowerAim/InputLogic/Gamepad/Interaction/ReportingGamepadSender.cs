using PowerAim.InputLogic.Contracts;
using SharpDX.XInput;

/// <summary>
///     Transparent decorator around the active <see cref="IGamepadSender"/> that mirrors every
///     emitted button / trigger / stick value to <see cref="PowerAim.InputLogic.InputEventBus"/> for
///     the debug input visualizer, then delegates to the wrapped sender.
///     <para>
///     Wrapping happens once in <c>GamepadManager</c>, so the visualizer works for <i>every</i>
///     send mode (ViGEm, vJoy, XInput-emu, internal) and every call path — the mapping engine, the
///     aim right-stick, and trigger/action <c>Send</c> (which itself funnels through the interface
///     methods below). Reporting is gated by <see cref="PowerAim.InputLogic.InputEventBus.Enabled"/>,
///     so it costs effectively nothing while the visualizer is closed.
///     </para>
/// </summary>
public sealed class ReportingGamepadSender(IGamepadSender inner) : IGamepadSender
{
    /// <summary>The wrapped sender — exposed so type-specific consumers (e.g. the gamepad tester) can unwrap.</summary>
    public IGamepadSender Inner => inner;

    public bool CanWork => inner.CanWork;

    public IGamepadSender SyncWith(IGamepadStateSource? source) { inner.SyncWith(source); return this; }
    public IGamepadSender StopSync() { inner.StopSync(); return this; }
    public IGamepadSender PauseSync(GamepadButton button) { inner.PauseSync(button); return this; }
    public IGamepadSender PauseSync(GamepadSlider slider) { inner.PauseSync(slider); return this; }
    public IGamepadSender PauseSync(GamepadAxis axis) { inner.PauseSync(axis); return this; }
    public IGamepadSender ResumeSync() { inner.ResumeSync(); return this; }
    public IGamepadSender ResumeSync(GamepadButton button) { inner.ResumeSync(button); return this; }
    public IGamepadSender ResumeSync(GamepadSlider slider) { inner.ResumeSync(slider); return this; }
    public IGamepadSender ResumeSync(GamepadAxis axis) { inner.ResumeSync(axis); return this; }

    public IGamepadSender SetButtonState(GamepadButton button, bool pressed, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (PowerAim.InputLogic.InputEventBus.Enabled)
            PowerAim.InputLogic.InputEventBus.GamepadButton(ToXboxButtonId(button), pressed);
        inner.SetButtonState(button, pressed, gamepadSyncState);
        return this;
    }

    public IGamepadSender SetSliderValue(GamepadSlider slider, byte value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (PowerAim.InputLogic.InputEventBus.Enabled)
            PowerAim.InputLogic.InputEventBus.GamepadTrigger(slider == GamepadSlider.LeftTrigger ? 0 : 1, value / 255.0);
        inner.SetSliderValue(slider, value, gamepadSyncState);
        return this;
    }

    public IGamepadSender SetAxisValue(GamepadAxis axis, short value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (PowerAim.InputLogic.InputEventBus.Enabled)
            PowerAim.InputLogic.InputEventBus.GamepadAxis((int)axis, value / 32767.0);
        inner.SetAxisValue(axis, value, gamepadSyncState);
        return this;
    }

    public void Dispose() => inner.Dispose();

    // PowerAim's GamepadButton and the canvas-facing XboxButtonId share names but not numeric
    // values, so the visualizer needs this explicit remap to light the correct hotspot.
    private static int ToXboxButtonId(GamepadButton b) => b switch
    {
        GamepadButton.Up => 0, GamepadButton.Down => 1, GamepadButton.Left => 2, GamepadButton.Right => 3,
        GamepadButton.Start => 4, GamepadButton.Back => 5,
        GamepadButton.LeftThumb => 6, GamepadButton.RightThumb => 7,
        GamepadButton.LeftShoulder => 8, GamepadButton.RightShoulder => 9,
        GamepadButton.A => 10, GamepadButton.B => 11, GamepadButton.X => 12, GamepadButton.Y => 13,
        _ => -1
    };
}
