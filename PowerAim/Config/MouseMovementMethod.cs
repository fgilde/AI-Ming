using System.ComponentModel;

namespace PowerAim.Config;

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
}