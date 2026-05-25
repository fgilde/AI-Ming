namespace PowerAim.Config;

/// <summary>
///     What kind of input the mapping reads or writes. The engine uses this to route reads to
///     the right device driver (XInput, keyboard hook, mouse hook) and writes to the right
///     synthesizer (ViGEm virtual controller, SendInput).
/// </summary>
public enum MappingInputKind
{
    KeyboardKey = 0,
    MouseButton = 1,
    GamepadButton = 2,
    GamepadTrigger = 3,    // LT / RT (slider, treated as button when > threshold)
    GamepadStickDirection = 4, // LStickUp / LStickDown / RStickLeft / …
}

/// <summary>
///     One mapping inside a <see cref="ControllerMappingProfile"/>. Reads <see cref="SourceKind"/>
///     / <see cref="SourceCode"/> and writes <see cref="TargetKind"/> / <see cref="TargetCode"/>
///     while the profile is enabled.
///     <para>
///     The "code" is a numeric identifier whose meaning depends on the Kind: for
///     <see cref="MappingInputKind.KeyboardKey"/> it's a <c>System.Windows.Forms.Keys</c> value;
///     for <see cref="MappingInputKind.MouseButton"/> a <c>MouseButtons</c> value; for the gamepad
///     variants it's a <c>GamepadButton</c> / <c>GamepadSlider</c> / <c>GamepadStickDirection</c>
///     enum value (see <c>PowerAim.InputLogic.Mapping</c>).
///     </para>
/// </summary>
public class InputMapping : BaseSettings
{
    private MappingInputKind _sourceKind;
    private int _sourceCode;
    private MappingInputKind _targetKind;
    private int _targetCode;
    private bool _enabled = true;

    public MappingInputKind SourceKind
    {
        get => _sourceKind;
        set => SetField(ref _sourceKind, value);
    }

    public int SourceCode
    {
        get => _sourceCode;
        set => SetField(ref _sourceCode, value);
    }

    public MappingInputKind TargetKind
    {
        get => _targetKind;
        set => SetField(ref _targetKind, value);
    }

    public int TargetCode
    {
        get => _targetCode;
        set => SetField(ref _targetCode, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }
}
