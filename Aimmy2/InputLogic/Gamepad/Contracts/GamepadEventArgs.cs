using Nextended.Core.Helper;

namespace Aimmy2.InputLogic.Contracts;

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

    public GamepadButton? GamepadButton => Enum.GetValues<GamepadButton>().FirstOrDefault(b => b.ToDescriptionString() == Button) is var match && match.ToDescriptionString() == Button ? match : null;
    public GamepadSlider? GamepadSlider => Enum.GetValues<GamepadSlider>().FirstOrDefault(b => b.ToDescriptionString() == Button) is var match && match.ToDescriptionString() == Button ? match : null;
    public GamepadAxis? GamepadAxis => Enum.GetValues<GamepadAxis>().FirstOrDefault(b => b.ToDescriptionString() == Button) is var match && match.ToDescriptionString() == Button ? match : null;
    
    public override string ToString()
    {
        return Button;
    }

}