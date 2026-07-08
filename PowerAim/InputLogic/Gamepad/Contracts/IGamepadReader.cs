using SharpDX.XInput;

namespace PowerAim.InputLogic.Contracts;

/// <summary>
///     Transport-neutral gamepad reader: turns a physical pad's input into the app's normalized string
///     vocabulary ("A"/"B"/…/"RSX"/"LT") via <see cref="ButtonEvent"/>. Implemented by the XInput reader
///     (<see cref="IXInputGamepadReader"/>) and by a DirectInput/HID reader (e.g. for a PS5 DualSense),
///     so consumers (InputBindingManager, actions) never need to know the transport.
/// </summary>
public interface IGamepadReader: IDisposable
{
    bool IsConnected { get; }

    event EventHandler<GamepadEventArgs> ButtonEvent;
    bool IsPressed(string button);
}

/// <summary>
///     A reader backed by an XInput slot. Only this variant exposes the XInput <see cref="Controller"/>
///     and slot switching; callers that need those must first check <c>reader is IXInputGamepadReader</c>
///     so a DirectInput-backed reader (which has no XInput slot) is handled gracefully.
/// </summary>
public interface IXInputGamepadReader : IGamepadReader
{
    Controller Controller { get; }
    State State { get; }

    /// <summary>The XInput slot currently being read as the sync source.</summary>
    UserIndex CurrentSlot { get; }

    /// <summary>Switch the physical source to a specific XInput slot at runtime (no teardown needed).</summary>
    void UseSlot(UserIndex slot);
}
