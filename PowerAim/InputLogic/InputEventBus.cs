namespace PowerAim.InputLogic;

/// <summary>Which kind of synthetic input PowerAim just emitted.</summary>
public enum InputChannel
{
    Key,            // Code = Win32 VK
    MouseButton,    // Code = 0:L 1:R 2:M 3:X1 4:X2
    MouseMove,      // X/Y = delta toward the target (coarse direction)
    GamepadButton,  // Code = XboxButtonId
    GamepadTrigger, // Code = 0:LT 1:RT, X = 0..1
    GamepadAxis     // Code = 0:LX 1:LY 2:RX 3:RY, X = -1..1
}

public readonly record struct InputEvent(InputChannel Channel, int Code, double X, double Y, bool Down);

/// <summary>
///     A tiny, allocation-light pub/sub for "PowerAim just sent this input". The debug overlay's
///     input visualizer subscribes; senders publish. <see cref="Enabled"/> is flipped on only while
///     the visualizer is showing so the hot input paths pay nothing in the common case.
/// </summary>
public static class InputEventBus
{
    /// <summary>Gate — set true only while something is listening (the visualizer).</summary>
    public static bool Enabled;

    public static event Action<InputEvent>? Sent;

    public static void Report(InputEvent e)
    {
        if (!Enabled) return;
        try { Sent?.Invoke(e); }
        catch { /* a flaky listener must never break input dispatch */ }
    }

    public static void Key(int vk, bool down) => Report(new(InputChannel.Key, vk, 0, 0, down));
    public static void MouseButton(int code, bool down) => Report(new(InputChannel.MouseButton, code, 0, 0, down));
    public static void MouseMove(double dx, double dy) => Report(new(InputChannel.MouseMove, 0, dx, dy, true));
    public static void GamepadButton(int xboxButtonId, bool down) => Report(new(InputChannel.GamepadButton, xboxButtonId, 0, 0, down));
    public static void GamepadTrigger(int index, double value) => Report(new(InputChannel.GamepadTrigger, index, value, 0, value > 0.02));
    public static void GamepadAxis(int index, double value) => Report(new(InputChannel.GamepadAxis, index, value, 0, true));
}
