using System.Collections.ObjectModel;

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
}
