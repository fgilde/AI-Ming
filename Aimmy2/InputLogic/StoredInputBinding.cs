using Aimmy2.InputLogic.Contracts;
using System.Windows.Forms;

namespace Aimmy2.InputLogic;

public struct StoredInputBinding
{
    public bool Equals(StoredInputBinding other)
    {
        if(!IsValid && !other.IsValid)
            return true;
        return Key == other.Key && 
               (Is<GamepadEventArgs>() && other.Is<GamepadEventArgs>() || Is<MouseEventArgs>() && other.Is<MouseEventArgs>() || Is<KeyEventArgs>() && other.Is<KeyEventArgs>());
    }

    public override bool Equals(object? obj)
    {
        return obj is StoredInputBinding other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Key, MouseEventArgs, KeyEventArgs, GamepadEventArgs);
    }

    public string Key { get; set; }

    public MouseEventArgs? MouseEventArgs { get; set; }
    public KeyEventArgs? KeyEventArgs { get; set; }
    public GamepadEventArgs? GamepadEventArgs { get; set; }

    public bool Is<T>() where T : EventArgs => typeof(T) switch
    {
        not null when typeof(T) == typeof(MouseEventArgs) => MouseEventArgs != null,
        not null when typeof(T) == typeof(KeyEventArgs) => KeyEventArgs != null,
        not null when typeof(T) == typeof(GamepadEventArgs) => GamepadEventArgs != null,
        _ => false
    };

    public bool Is(GamepadButton button) => Is<GamepadEventArgs>() && GamepadEventArgs.GamepadButton == button;
    public bool Is(GamepadSlider slider) => Is<GamepadEventArgs>() && GamepadEventArgs.GamepadSlider == slider;
    public bool Is(GamepadAxis axis) => Is<GamepadEventArgs>() && GamepadEventArgs.GamepadAxis == axis;
    public bool Is(MouseButtons button) => Is<MouseEventArgs>() && MouseEventArgs.Button == button;
    public bool Is(Keys key) => Is<KeyEventArgs>() && KeyEventArgs.KeyCode == key;


    public bool Matches(GamepadEventArgs data) => Is<GamepadEventArgs>() && (GamepadEventArgs.Code == data.Code);
    public bool Matches(MouseEventArgs data) => Is<MouseEventArgs>() && MouseEventArgs.Button == data.Button;
    public bool Matches(KeyEventArgs data) => Is<KeyEventArgs>() && KeyEventArgs.KeyCode.ToString() == data.KeyCode.ToString();

    public bool IsValid => !string.IsNullOrWhiteSpace(Key) && Key.ToLower() != "none" && (MouseEventArgs != null || KeyEventArgs != null || GamepadEventArgs != null);
    public static StoredInputBinding Empty => new();
    public string DeviceName => MouseEventArgs != null ? "Mouse" : KeyEventArgs != null ? "Keyboard" : GamepadEventArgs != null ? "Gamepad" : "Unknown";

    public StoredInputBinding()
    {}

    public StoredInputBinding(KeyEventArgs data)
    {
        Key = data.KeyCode.ToString();
        KeyEventArgs = data;
    }

    public StoredInputBinding(MouseEventArgs data)
    {
        Key = data.Button.ToString();
        MouseEventArgs = data;
    }

    public StoredInputBinding(GamepadEventArgs data)
    {
        Key = data.Code;
        GamepadEventArgs = data;
    }

    public StoredInputBinding(GamepadSlider slider) : this(new GamepadEventArgs(slider))
    {}

    public StoredInputBinding(MouseButtons mouseButtons) : this(new MouseEventArgs(mouseButtons, 0, 0, 0, 0))
    {}

    public StoredInputBinding(Keys keys) : this(new KeyEventArgs(keys))
    {}

    public StoredInputBinding(GamepadButton gamepadButton) : this(new GamepadEventArgs(gamepadButton))
    {}

    public StoredInputBinding(GamepadAxis gamepadAxis): this(new GamepadEventArgs(gamepadAxis))
    {}

    public static implicit operator StoredInputBinding(Keys a) => new(a);
    public static implicit operator StoredInputBinding(MouseButtons a) => new(a);
    public static implicit operator StoredInputBinding(GamepadButton a) => new(a);
    public static implicit operator StoredInputBinding(GamepadSlider a) => new(a);
    public static implicit operator StoredInputBinding(GamepadAxis a) => new(a);
    
}