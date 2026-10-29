using System.Collections.ObjectModel;
using PowerAim.Input.Gamepad;

namespace PowerAim.Config;

/// <summary>
///     User choices for the controller manager. Both are device-identity strings (VID/PID/instance from
///     <c>GetControllerId()</c>), not XInput slot indices, so they re-resolve correctly across reconnects
///     and slot changes.
/// </summary>
public class ControllerSettings : BaseSettings
{
    /// <summary>
    ///     Device id of the physical controller the user picked to feed the sync (mirror into the virtual
    ///     pad). Empty = auto (last-known slot, then scan all four). Re-resolved to a live XInput slot on
    ///     startup; if the device isn't currently present on any slot, the auto-scanned source is kept.
    /// </summary>
    public string PreferredSyncDeviceId
    {
        get;
        set => SetField(ref field, value ?? "");
    } = "";

    /// <summary>
    ///     Device ids the user has chosen to hide. Hidden via HidHide when its driver is installed (hides
    ///     from games only — PowerAim stays on the allow-list and still sees the pad), otherwise an
    ///     internal soft-hide (PowerAim ignores it for sync/source selection). Our own list always still
    ///     shows them.
    /// </summary>
    public ObservableCollection<string> HiddenControllerIds
    {
        get;
        set => SetField(ref field, value);
    } = new();

    // ---- Aim output on the right stick (gamepad movement method) ----

    /// <summary>The game's stick deadzone (%). Aim corrections are remapped above it so small ones still turn the view. 0 = off.</summary>
    public double GameDeadzonePct
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0, 50));
    }

    /// <summary>Ceiling of the aim's stick deflection (%).</summary>
    public double MaxOutputPct
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 5, 100));
    } = 100;

    /// <summary>The game's stick response curve; the aim compensates with its inverse.</summary>
    public StickResponseCurve ResponseCurve
    {
        get;
        set => SetField(ref field, value);
    } = StickResponseCurve.Linear;

    /// <summary>How much the physical right stick takes over while aiming: blend = this % · stick deflection. 0 = aim only.</summary>
    public double ManualOverridePct
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0, 100));
    }

    /// <summary>0–50: counter the crosshair drift from strafing with the left stick, toward the target only.</summary>
    public double AntiStrafePct
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0, 50));
    }

    /// <summary>
    ///     How fast the game turns the view at full stick deflection, in screen pixels per second. The V2 aim
    ///     engine plans stick deflection from it and learns the remaining difference while aiming.
    /// </summary>
    public double StickTurnSpeedPxPerSec
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 100, 30000));
    } = 3000;

    /// <summary>Southpaw: the aim drives the left stick (games with swapped sticks).</summary>
    public bool AimStickLeft
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>Invert only the aim's vertical stick output (games with inverted look).</summary>
    public bool InvertAiY
    {
        get;
        set => SetField(ref field, value);
    }
}
