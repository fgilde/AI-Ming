using Nextended.Core.Helper;

namespace Aimmy2.InputLogic.Contracts;

public class GamepadEventArgs : EventArgs
{
    private const string Prefix = "GP | ";

    public static bool TryParse(string code, out GamepadEventArgs? args)
    {
        args = null;
        if(IsGamepadKey(code)) 
        {
            args = new GamepadEventArgs
            {
                Button = GetButtonName(code),
            };
            if (code.Contains("|"))
            {
                var value = code.Split("|").Last();
                if (float.TryParse(value, out var floatValue))
                {
                    args.Value = floatValue;
                    args.IsPressed = floatValue > 0;
                }
            }
            return true;
        }
        return false;
    }

    public static bool IsGamepadKey(string key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith(Prefix))
            return false;
        key = GetButtonName(key);
        return ExistsInEnum<GamepadButton>(key) || ExistsInEnum<GamepadSlider>(key) || ExistsInEnum<GamepadAxis>(key);
    }

    public static string GetButtonName(string key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith(Prefix))
            return string.Empty;
        var keyAndMaybeValue = key[Prefix.Length..];
        return keyAndMaybeValue.Contains("|") ? keyAndMaybeValue.Split("|")[0] : keyAndMaybeValue;
    }

    public bool IsStickEvent { get; set; }
    public string Button { get; set; }
    public bool? IsPressed { get; set; }
    public float? Value { get; set; }
    public string CodeWithoutValue => $"{Prefix}{Button}";
    public string Code => ToString();

    public GamepadButton? GamepadButton => Enum.GetValues<GamepadButton>().FirstOrDefault(b => b.ToDescriptionString() == Button) is var match && match.ToDescriptionString() == Button ? match : null;
    public GamepadSlider? GamepadSlider => Enum.GetValues<GamepadSlider>().FirstOrDefault(b => b.ToDescriptionString() == Button) is var match && match.ToDescriptionString() == Button ? match : null;
    public GamepadAxis? GamepadAxis => Enum.GetValues<GamepadAxis>().FirstOrDefault(b => b.ToDescriptionString() == Button) is var match && match.ToDescriptionString() == Button ? match : null;

    private static bool ExistsInEnum<T>(string s) where T : struct, Enum
    {
        return Enum.GetValues<T>().FirstOrDefault(b => b.ToDescriptionString() == s) is var match && match.ToDescriptionString() == s;
    }

    public override string ToString()
    {
        return $"{Prefix}{Button}|{GetValue()}";
    }

    private string GetValue()
    {
        if (Value == null && IsPressed == null)
            return string.Empty;
        var fl = Value ?? (IsPressed == true ? 1f : 0f);
        return fl.ToString();
    }
}