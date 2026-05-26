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
    public MappingInputKind SourceKind
    {
        get;
        set => SetField(ref field, value);
    }

    public int SourceCode
    {
        get;
        set => SetField(ref field, value);
    }

    public MappingInputKind TargetKind
    {
        get;
        set => SetField(ref field, value);
    }

    public int TargetCode
    {
        get;
        set => SetField(ref field, value);
    }

    public bool Enabled
    {
        get;
        set => SetField(ref field, value);
    } = true;

    /// <summary>Press style — see <see cref="MappingActivator"/>.</summary>
    public MappingActivator Activator
    {
        get;
        set => SetField(ref field, value);
    } = MappingActivator.Press;

    /// <summary>
    ///     For <see cref="MappingActivator.LongPress"/>: hold time in ms.
    ///     For <see cref="MappingActivator.Pulse"/>: pulse length in ms.
    ///     Ignored for other activators.
    /// </summary>
    public int LongPressMs
    {
        get;
        set => SetField(ref field, value);
    } = 350;

    /// <summary>
    ///     Optional second source that must be held for this mapping to fire. Lets you build
    ///     "shift layers" — e.g. binding LB + A → Different Action so the same A drives two targets
    ///     depending on whether LB is held.
    ///     Set to <see cref="MappingInputKind.None"/> to disable the modifier gate (the default).
    /// </summary>
    public MappingInputKind ModifierKind
    {
        get;
        set => SetField(ref field, value);
    } = MappingInputKind.None;

    public int ModifierCode
    {
        get;
        set => SetField(ref field, value);
    }
}
