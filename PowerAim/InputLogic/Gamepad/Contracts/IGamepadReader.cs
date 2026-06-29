using SharpDX.XInput;

namespace PowerAim.InputLogic.Contracts;

public interface IGamepadReader: IDisposable
{
    Controller Controller { get; }
    State State { get; }
    bool IsConnected { get; }

    /// <summary>The XInput slot currently being read as the sync source.</summary>
    UserIndex CurrentSlot { get; }

    /// <summary>Switch the physical source to a specific XInput slot at runtime (no teardown needed).</summary>
    void UseSlot(UserIndex slot);

    event EventHandler<GamepadEventArgs> ButtonEvent;
    bool IsPressed(string button);
}