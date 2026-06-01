using System.ComponentModel;

namespace PowerAim.Config;

/// <summary>
///     Pipeline used to deliver aim / recoil compensation motion. The legacy name is preserved
///     for JSON-config compatibility, but the enum now covers gamepad output too (see
///     <see cref="Gamepad"/>) — replacing the old separate <c>ToggleState.UseControllerForAim</c>
///     switch.
/// </summary>
public enum MouseMovementMethod
{
    [Description("Mouse Event")]
    MouseEvent,

    [Description("SendInput")]
    SendInput,

    [Description("LG HUB")]
    LGHUB,

    [Description("Razer Synapse (Require Razer Peripheral)")]
    RazerSynapse,

    [Description("ddxoft Virtual Input Driver")]
    ddxoft,

    /// <summary>
    ///     Drive the virtual Xbox right-stick instead of synthesising mouse motion. Only usable
    ///     when <see cref="PowerAim.InputLogic.InputSender.GamepadAimAvailable"/> is true (ViGEm
    ///     bus + sender configured). The UI greys this option out otherwise.
    /// </summary>
    [Description("Gamepad (Virtual right-stick)")]
    Gamepad,
}