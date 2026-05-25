namespace PowerAim.InputLogic.Mapping;

/// <summary>
///     The 8 cardinal stick directions that <see cref="PowerAim.Config.MappingInputKind.GamepadStickDirection"/>
///     bindings reference. The engine treats each as a digital "pressed when stick deflected past
///     a threshold in this direction" signal.
/// </summary>
public enum GamepadStickDirection
{
    LeftStickUp = 0,
    LeftStickDown = 1,
    LeftStickLeft = 2,
    LeftStickRight = 3,
    RightStickUp = 4,
    RightStickDown = 5,
    RightStickLeft = 6,
    RightStickRight = 7,
}
