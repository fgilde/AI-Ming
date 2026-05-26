using SharpDX.XInput;

namespace PowerAim.InputLogic.Contracts;

public interface IGamepadReader: IDisposable
{
    Controller Controller { get; }
    State State { get; }
    bool IsConnected { get; }
    event EventHandler<GamepadEventArgs> ButtonEvent;
    bool IsPressed(string button);
}