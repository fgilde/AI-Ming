using Nextended.UI.Input.Gamepad;

namespace PowerAim.Input;

/// <summary>
///     Exposes <see cref="GamepadManager"/>'s button-event facade as an <see cref="IGamepadReader"/>, so an
///     <see cref="Nextended.UI.Input.InputBindingManager"/> follows reader swaps (XInput ↔ DirectInput)
///     instead of opening a reader of its own. Disposing only unsubscribes; the manager owns the readers.
/// </summary>
internal sealed class GamepadManagerReader : IGamepadReader
{
    public GamepadManagerReader() => GamepadManager.ButtonEvent += Forward;

    public event EventHandler<GamepadEventArgs>? ButtonEvent;

    public bool IsConnected => GamepadManager.GamepadReader?.IsConnected ?? false;

    public bool IsPressed(string button) => GamepadManager.GamepadReader?.IsPressed(button) ?? false;

    private void Forward(object? sender, GamepadEventArgs e) => ButtonEvent?.Invoke(sender, e);

    public void Dispose() => GamepadManager.ButtonEvent -= Forward;
}
