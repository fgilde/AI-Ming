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
    None = 255, // sentinel — used for "no modifier"
}

/// <summary>
///     reWASD-style activator types. Controls when a mapping actually fires the target.
/// </summary>
public enum MappingActivator
{
    /// <summary>Fire on key/button press, release when source releases. The default.</summary>
    Press = 0,
    /// <summary>Fire only after the source has been held for <see cref="InputMapping.LongPressMs"/> milliseconds.</summary>
    LongPress = 1,
    /// <summary>Fire on a quick double-tap (two presses within ~300 ms).</summary>
    DoubleTap = 2,
    /// <summary>Each press toggles the target on/off (latching).</summary>
    Toggle = 3,
    /// <summary>Fire one short pulse on press; auto-release after <see cref="InputMapping.LongPressMs"/> (used as pulse duration here).</summary>
    Pulse = 4,
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
///     <para>
///     reWASD parity: an optional <see cref="ModifierKind"/>/<see cref="ModifierCode"/> source-side
///     gate lets a mapping fire only while a second source is held (e.g. "LB+A → Y" — A maps to Y
///     only while LB is held). A <see cref="MappingActivator"/> per mapping picks press-style
///     (press / long-press / double-tap / toggle / pulse) so a single button can drive multiple
///     mappings with different timings.
///     </para>
/// </summary>
public class InputMapping : BaseSettings
{
    private MappingInputKind _sourceKind;
    private int _sourceCode;
    private MappingInputKind _targetKind;
    private int _targetCode;
    private bool _enabled = true;
    private MappingActivator _activator = MappingActivator.Press;
    private int _longPressMs = 350;
    private MappingInputKind _modifierKind = MappingInputKind.None;
    private int _modifierCode;

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

    /// <summary>Press style — see <see cref="MappingActivator"/>.</summary>
    public MappingActivator Activator
    {
        get => _activator;
        set => SetField(ref _activator, value);
    }

    /// <summary>
    ///     For <see cref="MappingActivator.LongPress"/>: hold time in ms.
    ///     For <see cref="MappingActivator.Pulse"/>: pulse length in ms.
    ///     Ignored for other activators.
    /// </summary>
    public int LongPressMs
    {
        get => _longPressMs;
        set => SetField(ref _longPressMs, value);
    }

    /// <summary>
    ///     Optional second source that must be held for this mapping to fire. Lets you build
    ///     "shift layers" — e.g. binding LB + A → Different Action so the same A drives two targets
    ///     depending on whether LB is held.
    ///     Set to <see cref="MappingInputKind.None"/> to disable the modifier gate (the default).
    /// </summary>
    public MappingInputKind ModifierKind
    {
        get => _modifierKind;
        set => SetField(ref _modifierKind, value);
    }

    public int ModifierCode
    {
        get => _modifierCode;
        set => SetField(ref _modifierCode, value);
    }
}
