using SharpDX.XInput;

namespace PowerAim.InputLogic.Contracts;

/// <summary>
///     Transport-neutral source of a gamepad state snapshot for the sender mirror loops. It decouples
///     the senders from the XInput <i>device/slot</i>: any reader — an XInput slot (<see cref="GamepadReader"/>)
///     or a DirectInput/HID pad such as a PS5 DualSense (<c>DirectInputGamepadReader</c>) — can supply the
///     snapshot, so a non-XInput pad can drive the virtual controller too.
///     <para>
///     <see cref="SharpDX.XInput.State"/> is deliberately reused as the interchange format: the whole
///     sender layer (all seven senders) already speaks it, so their battle-tested mirror math stays
///     untouched. Neutrality is w.r.t. the transport, not the struct — a DirectInput reader simply fills
///     a <see cref="State"/> with the values it read.
///     </para>
/// </summary>
public interface IGamepadStateSource
{
    /// <summary>True while a real pad is present and being read.</summary>
    bool IsConnected { get; }

    /// <summary>Current snapshot. For XInput this reads the device fresh; for DirectInput it returns the
    /// latest polled + mapped state. Returns <c>default</c> when not connected.</summary>
    State GetState();
}
