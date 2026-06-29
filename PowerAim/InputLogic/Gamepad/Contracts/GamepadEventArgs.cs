using Nextended.Core.Helper;

namespace PowerAim.InputLogic.Contracts;

public class GamepadEventArgs : EventArgs
{
    public GamepadEventArgs(GamepadAxis button, float? value = null)
    {
        Button = button.ToDescriptionString();
        IsStickEvent = true;
        Value = value;
    }

    public GamepadEventArgs(GamepadSlider button, float? value = null)
    {
        Button = button.ToDescriptionString();
        IsStickEvent = false;
        Value = value;
        if(value != null)
            IsPressed = value > 0;
    }

    public GamepadEventArgs(GamepadButton button, bool? pressed = null)
    {
        Button = button.ToDescriptionString();
        IsStickEvent = false;
        IsPressed = pressed;
        if (pressed == true)
            Value = 1f;
        else if (pressed == false)
            Value = 0f;
    }

    public GamepadEventArgs()
    {}
    
    public bool IsStickEvent { get; set; }
    public string Button { get; set; }
    public bool? IsPressed { get; set; }
    public float? Value { get; set; }
    public string Code => ToString();

    public GamepadButton? GamepadButton => MatchEnum<GamepadButton>();
    public GamepadSlider? GamepadSlider => MatchEnum<GamepadSlider>();
    public GamepadAxis? GamepadAxis => MatchEnum<GamepadAxis>();

    // Resolve the enum value whose [Description] equals Button. FirstOrDefault yields default(T)
    // (the zero-valued member) when nothing matches, so re-check the description to distinguish a
    // genuine match at the zero index from "no match" — identical to the original per-property logic.
    private T? MatchEnum<T>() where T : struct, Enum =>
        Enum.GetValues<T>().FirstOrDefault(b => b.ToDescriptionString() == Button) is var match && match.ToDescriptionString() == Button ? match : null;
    
    public override string ToString()
    {
        return Button;
    }

}