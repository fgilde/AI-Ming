using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SharpDX.XInput;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.InputLogic.Contracts;
using PowerAim.InputLogic.Gamepad.Interaction;
using PowerAim.InputLogic.HidHide;

namespace PowerAim.InputLogic.Gamepad;

public enum ControllerKind { Physical, Virtual }

/// <summary>
///     A single row in the controller manager: a physical pad or our virtual ViGEm pad. Mutable
///     fields raise <see cref="INotifyPropertyChanged"/> so the list can update in place (no flicker)
///     when the catalog reconciles on its refresh tick.
/// </summary>
public sealed class ControllerInfo : INotifyPropertyChanged
{
    /// <summary>Device id (physical = SetupAPI instance path) or the constant "virtual:vigem".</summary>
    public string Id { get; init; } = "";
    public string VidPid { get; init; } = "";
    public ControllerKind Kind { get; init; }

    public string Name { get => _name; set => Set(ref _name, value); }
    /// <summary>XInput slot 0-3 if the pad is XInput-addressable; null for DirectInput-only or virtual.</summary>
    public int? Slot { get => _slot; set { if (Set(ref _slot, value)) { Raise(nameof(CanBeSyncSource)); Raise(nameof(SlotLabel)); } } }
    public bool IsConnected { get => _connected; set { if (Set(ref _connected, value)) Raise(nameof(CanBeSyncSource)); } }
    public bool IsSyncSource { get => _sync; set => Set(ref _sync, value); }
    public bool IsHidden { get => _hidden; set => Set(ref _hidden, value); }

    /// <summary>Any connected physical pad can feed the sync now — an XInput slot OR a DirectInput/HID
    /// pad (e.g. a raw PS5 DualSense), since the senders take a transport-neutral state source.</summary>
    public bool CanBeSyncSource => Kind == ControllerKind.Physical && _connected;
    public string SlotLabel => _slot != null ? $"Slot {_slot + 1}" : "";

    private string _name = "";
    private int? _slot;
    private bool _connected, _sync, _hidden;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        Raise(n!);
        return true;
    }
}

/// <summary>
///     Builds the merged controller list (physical pads with friendly names + the virtual pad +
///     persisted-hidden pads) and owns the actions on it (set sync source, hide/show). All enumeration
///     reads live state each <see cref="Build"/>; the UI control reconciles the result into its
///     observable collection.
/// </summary>
public static class ControllerCatalog
{
    /// <summary>True when the HidHide driver/CLI is installed (otherwise hide falls back to internal soft-hide).</summary>
    public static bool HidHideAvailable => !string.IsNullOrEmpty(HidHideHelper.GetHidHidePath());

    // Microsoft Xbox 360 wired VID/PID — the identity our ViGEm virtual pad uses (so games accept it).
    // It also makes the virtual indistinguishable from a real Xbox 360 pad by VID/PID alone.
    private const string ViGEmVidPid = "VID_045E&PID_028E";

    public static List<ControllerInfo> Build()
    {
        var list = new List<ControllerInfo>();
        var hidden = AppConfig.Current?.ControllerSettings?.HiddenControllerIds;
        var detected = HidGamepadEnumerator.Enumerate(); // friendly names (can be empty even with live XInput slots!)

        int? sourceSlot = GamepadManager.GamepadReader is IXInputGamepadReader xr ? (int)xr.CurrentSlot : null;
        bool wantsVirtual = GamepadManager.CanSend;
        bool virtualPlaced = false;
        string virtualName = VirtualName();

        // 1) XInput slots are the authoritative source of "connected gamepad" — build one entry per
        //    occupied slot. (The previous version built only from HID enumeration, which returns empty
        //    on some systems even when slots are live → the physical pad never showed.) The ViGEm
        //    virtual pad also occupies a slot and is VID/PID-identical to a real Xbox pad, so attribute
        //    a non-source Xbox-class slot to the virtual (best-effort) instead of double-listing it.
        for (var i = UserIndex.One; i <= UserIndex.Four; i++)
        {
            var c = new Controller(i);
            if (!c.IsConnected) continue;
            int slot = (int)i;
            string id = c.GetControllerId() ?? $"xinput:slot{slot}";
            string vp = ExtractVidPid(id) ?? "";

            bool isVirtual = wantsVirtual && !virtualPlaced && slot != sourceSlot
                             && string.Equals(vp, ViGEmVidPid, StringComparison.OrdinalIgnoreCase);
            if (isVirtual)
            {
                virtualPlaced = true;
                list.Add(new ControllerInfo
                {
                    Id = "virtual:vigem", Kind = ControllerKind.Virtual, Name = virtualName,
                    VidPid = vp, Slot = slot, IsConnected = true,
                });
                continue;
            }

            string name = detected.FirstOrDefault(d =>
                              string.Equals(ExtractVidPid(d.InstanceId), vp, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(ExtractVidPid(d.HardwareId), vp, StringComparison.OrdinalIgnoreCase))?.FriendlyName
                          ?? $"Controller (Slot {slot + 1})";
            list.Add(new ControllerInfo
            {
                Id = id, VidPid = vp, Kind = ControllerKind.Physical, Name = name,
                Slot = slot, IsConnected = true,
                IsSyncSource = slot == sourceSlot,
                IsHidden = hidden?.Contains(id) ?? false,
            });
        }

        // 2) HID-detected pads that aren't on an XInput slot (DirectInput-only / vendor pads).
        foreach (var d in detected)
        {
            var vp = ExtractVidPid(d.InstanceId) ?? ExtractVidPid(d.HardwareId) ?? "";
            if (list.Any(x => x.Slot != null && string.Equals(x.VidPid, vp, StringComparison.OrdinalIgnoreCase)))
                continue; // already represented by its XInput slot
            if (list.Any(x => string.Equals(x.Id, d.InstanceId, StringComparison.OrdinalIgnoreCase)))
                continue;
            list.Add(new ControllerInfo
            {
                Id = d.InstanceId, VidPid = vp, Kind = ControllerKind.Physical,
                Name = string.IsNullOrWhiteSpace(d.FriendlyName) ? "Unknown controller" : d.FriendlyName,
                Slot = null, IsConnected = true,
                // We're reading this class of pad when the active reader is the DirectInput one.
                IsSyncSource = GamepadManager.GamepadReader is DirectInputGamepadReader,
                IsHidden = !d.Enabled || (hidden?.Contains(d.InstanceId) ?? false),
            });
        }

        // 3) Persisted-hidden pads not currently present → re-inject so OUR list always shows every
        //    controller the user has ever hidden (the 'always visible to us' guarantee).
        if (hidden != null)
            foreach (var hid in hidden)
                if (!list.Any(x => string.Equals(x.Id, hid, StringComparison.OrdinalIgnoreCase)))
                    list.Add(new ControllerInfo
                    {
                        Id = hid, Kind = ControllerKind.Physical, Name = hid,
                        IsConnected = false, IsHidden = true,
                    });

        // 4) Virtual pad with no detectable XInput slot (e.g. vJoy/internal modes, or ViGEm not yet
        //    re-enumerated) — inject it from sender state so it still shows.
        if (wantsVirtual && !virtualPlaced)
            list.Add(new ControllerInfo
            {
                Id = "virtual:vigem", Kind = ControllerKind.Virtual, Name = virtualName, IsConnected = true,
            });

        return list;
    }

    /// <summary>Display name for the virtual pad — reflects the active send mode (it isn't always ViGEm).</summary>
    private static string VirtualName() => AppConfig.Current?.DropdownState?.GamepadSendMode switch
    {
        GamepadSendMode.ViGEm => "Virtual (ViGEm)",
        GamepadSendMode.VJoy => "Virtual (vJoy)",
        GamepadSendMode.XInputHook => "Virtual (XInput hook)",
        GamepadSendMode.Internal => "Virtual (internal)",
        _ => "Virtual",
    };

    /// <summary>Make a physical XInput pad the sync source (mirrored into the virtual pad). No-op otherwise.</summary>
    public static void SetSyncSource(ControllerInfo info)
    {
        if (!info.CanBeSyncSource) return;
        if (info.Slot is { } slot)
        {
            GamepadManager.SetSyncSource((UserIndex)slot);
        }
        else
        {
            // DirectInput-only pad (e.g. a raw DualSense): correlate to its DirectInput device by VID/PID
            // and switch the reader to it. Correlation is by VID/PID because the ControllerInfo.Id is a
            // SetupAPI instance path while DirectInput identifies devices by InstanceGuid.
            var guid = DirectInputGamepadReader.FindDeviceByVidPid(info.VidPid);
            if (guid is { } g) GamepadManager.UseDirectInputSource(g);
        }
    }

    /// <summary>
    ///     Hide / show a controller. Persists the choice; uses HidHide (hides from games, PowerAim stays
    ///     on the allow-list so OUR list still shows it) when installed, else an internal soft-hide. The
    ///     virtual pad isn't hideable.
    /// </summary>
    public static void SetHidden(ControllerInfo info, bool hide)
    {
        if (info.Kind == ControllerKind.Virtual) return;
        var cs = AppConfig.Current?.ControllerSettings;
        if (cs == null) return;

        if (hide)
        {
            if (!cs.HiddenControllerIds.Contains(info.Id)) cs.HiddenControllerIds.Add(info.Id);
            if (HidHideAvailable) HidHideHelper.HideDevice(info.Id);
        }
        else
        {
            cs.HiddenControllerIds.Remove(info.Id);
            if (HidHideAvailable) HidHideHelper.ShowDevice(info.Id);
        }
        info.IsHidden = hide;
    }

    /// <summary>
    ///     Make games read the VIRTUAL pad instead of the physical one. Games read the lowest XInput
    ///     slot, and an app can't reassign XInput slots — the only lever is to remove the physical from
    ///     XInput so the virtual drops to the low slot. Hides the current sync-source physical (HidHide
    ///     cloak — games stop seeing it while PowerAim stays allow-listed, so sync keeps working; or a
    ///     system Disable when elevated and HidHide is absent), then reconnects the virtual so it
    ///     re-grabs the freed slot. Returns false when neither mechanism is available (the caller then
    ///     tells the user to install HidHide or run elevated). The internal soft-hide can't help here —
    ///     it doesn't remove the pad from XInput.
    /// </summary>
    public static bool MakeVirtualPrimary()
    {
        var reader = GamepadManager.GamepadReader;
        if (reader == null) return false;

        if (HidHideAvailable && reader is IXInputGamepadReader xir)
            xir.Controller.Hide();
        else if (DeviceHide.IsElevated())
            DeviceHide.TryDisable(GamepadManager.ReadingControllerId ?? "");
        else
            return false;

        GamepadManager.ReconnectVirtual();
        return true;
    }

    /// <summary>Undo <see cref="MakeVirtualPrimary"/> — make the physical pad visible to games again.</summary>
    public static void RestorePhysical()
    {
        var reader = GamepadManager.GamepadReader;
        if (reader == null) return;
        if (HidHideAvailable && reader is IXInputGamepadReader xir) xir.Controller.Show();
        else if (DeviceHide.IsElevated()) DeviceHide.TryEnable(GamepadManager.ReadingControllerId ?? "");
        GamepadManager.ReconnectVirtual();
    }

    private static string? ExtractVidPid(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var m = Regex.Match(s, "VID_[0-9A-F]{4}&PID_[0-9A-F]{4}", RegexOptions.IgnoreCase);
        return m.Success ? m.Value.ToUpperInvariant() : null;
    }
}
