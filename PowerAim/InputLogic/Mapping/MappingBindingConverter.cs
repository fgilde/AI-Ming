using System.Windows.Forms;
using PowerAim.Config;
using PowerAim.InputLogic.Contracts;
using PowerAim;

namespace PowerAim.InputLogic.Mapping;

/// <summary>
///     Bidirectional translation between the persistence-friendly <see cref="InputMapping"/>
///     <c>(MappingInputKind, int)</c> pair and PowerAim's unified <see cref="StoredInputBinding"/>
///     used by <see cref="UILibrary.AKeyChanger"/> / <see cref="UILibrary.MultiKeyChanger"/> in the
///     rest of the app. This lets the mapping editor share the global key-recording UX (KB + mouse
///     + gamepad button + trigger + axis all from one capture gesture) without disturbing the
///     engine's hot-path schema.
///     <para>
///     <b>Coverage</b>: keyboard keys, mouse buttons, gamepad buttons, gamepad triggers (LT/RT) all
///     round-trip cleanly. <b>Edge cases</b>: gamepad stick <i>direction</i> (LStickUp vs LStickDown
///     on the same axis) and the mouse-motion sentinel (0xFFFF) are NOT representable inside a
///     <see cref="StoredInputBinding"/> alone — the editor's row UI keeps an explicit "special
///     endpoint" dropdown for those cases instead of forcing them through this converter.
///     </para>
/// </summary>
public static class MappingBindingConverter
{
    /// <summary>Mouse-motion sentinel — also defined in the engine as <c>MouseMotionSentinel</c>.</summary>
    public const int MouseMotionSentinel = 0xFFFF;

    // ============================================================================ TO BINDING ====

    /// <summary>
    ///     Build a <see cref="StoredInputBinding"/> from a mapping endpoint. Returns
    ///     <see cref="StoredInputBinding.Empty"/> for the two unrepresentable cases (mouse motion,
    ///     stick direction) — the caller is expected to detect those via <see cref="IsSpecial"/>
    ///     and render a different UI affordance.
    /// </summary>
    public static StoredInputBinding ToBinding(MappingInputKind kind, int code)
    {
        switch (kind)
        {
            case MappingInputKind.KeyboardKey:
                return new StoredInputBinding(new KeyEventArgs((Keys)code));

            case MappingInputKind.MouseButton:
                if (code == MouseMotionSentinel) return StoredInputBinding.Empty; // special
                return new StoredInputBinding(new MouseEventArgs((MouseButtons)code, 0, 0, 0, 0));

            case MappingInputKind.GamepadButton:
                return new StoredInputBinding(((XboxButtonId)code).ToGamepadButton());

            case MappingInputKind.GamepadTrigger:
                return new StoredInputBinding(code == 0 ? GamepadSlider.LeftTrigger : GamepadSlider.RightTrigger);

            case MappingInputKind.GamepadStickDirection:
                // Not representable — stick direction can't be carried by a StoredInputBinding
                // because GamepadEventArgs only knows the axis, not the sign.
                return StoredInputBinding.Empty;

            default:
                return StoredInputBinding.Empty;
        }
    }

    // ============================================================================ FROM BINDING ====

    /// <summary>
    ///     Translate a <see cref="StoredInputBinding"/> back into a mapping endpoint. Returns
    ///     <c>null</c> if the binding can't be expressed in the <see cref="InputMapping"/> schema
    ///     (e.g. a stick axis without direction).
    /// </summary>
    public static (MappingInputKind Kind, int Code)? FromBinding(StoredInputBinding? binding)
    {
        if (binding is not { IsValid: true }) return null;

        if (binding.Is<KeyEventArgs>() && binding.KeyEventArgs != null)
            return (MappingInputKind.KeyboardKey, (int)binding.KeyEventArgs.KeyCode);

        if (binding.Is<MouseEventArgs>() && binding.MouseEventArgs != null)
            return (MappingInputKind.MouseButton, (int)binding.MouseEventArgs.Button);

        if (binding.Is<GamepadEventArgs>() && binding.GamepadEventArgs != null)
        {
            var gp = binding.GamepadEventArgs;
            if (gp.GamepadButton is { } gb)
                return (MappingInputKind.GamepadButton, (int)gb.ToXboxButtonId());
            if (gp.GamepadSlider is { } gs)
                return (MappingInputKind.GamepadTrigger, gs == GamepadSlider.LeftTrigger ? 0 : 1);
            // Stick axis (no direction information) — caller has to handle as "Special".
            return null;
        }
        return null;
    }

    // ============================================================================ SPECIAL CASES ====

    /// <summary>
    ///     True if the endpoint can't be expressed as a <see cref="StoredInputBinding"/> — the row
    ///     UI should render a "Special: …" label instead of an AKeyChanger.
    /// </summary>
    public static bool IsSpecial(MappingInputKind kind, int code) =>
        kind == MappingInputKind.GamepadStickDirection
        || (kind == MappingInputKind.MouseButton && code == MouseMotionSentinel);

    /// <summary>Human-readable label for a special endpoint.</summary>
    public static string SpecialLabel(MappingInputKind kind, int code) => kind switch
    {
        MappingInputKind.MouseButton when code == MouseMotionSentinel => Locale.SpecialLabelMouseMotion,
        MappingInputKind.GamepadStickDirection => string.Format(Locale.SpecialLabelGamepadStickFormat, (GamepadStickDirection)code),
        _ => Locale.UnknownParen,
    };
}
