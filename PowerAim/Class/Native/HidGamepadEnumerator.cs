using System.Runtime.InteropServices;

namespace PowerAim.Class.Native;

/// <summary>One detected gaming HID device — what shows up on the diagnostic panel.</summary>
public class DetectedGamepad
{
    public string FriendlyName { get; init; } = "";
    public string InstanceId { get; init; } = "";
    public string HardwareId { get; init; } = "";
    /// <summary>True if Windows currently reports the device as functional (not disabled).</summary>
    public bool Enabled { get; init; }
}

/// <summary>
///     Walks Windows' HID class via <c>SetupAPI</c> and returns one entry per detected
///     game-controller-style device. Friendly names are pulled from the device manager so the
///     user sees "Controller (XBOX 360 For Windows) Scuf Gaming PC Dongle" instead of just a
///     cryptic instance path.
///     <para>
///     We deliberately enumerate the entire HID class (GUID
///     <c>4D1E55B2-F16F-11CF-88CB-001111000030</c>) and filter on hardware-IDs that look like
///     XInput/gaming devices. Doing it via the broader HID class catches Scuf, Razer, and
///     other vendor variants that wouldn't show up if we hard-coded VID/PIDs.
///     </para>
/// </summary>
public static class HidGamepadEnumerator
{
    // ReSharper disable InconsistentNaming
    private static readonly Guid GUID_DEVINTERFACE_HID = new("4D1E55B2-F16F-11CF-88CB-001111000030");
    private const int DIGCF_PRESENT = 0x00000002;
    private const int DIGCF_ALLCLASSES = 0x00000004;
    private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    private const uint SPDRP_DEVICEDESC = 0x00000000;
    private const uint SPDRP_HARDWAREID = 0x00000001;
    private const uint DN_HAS_PROBLEM = 0x00000400;
    // ReSharper restore InconsistentNaming

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA did, IntPtr buffer, uint bufSize, out uint required);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA did, uint property, out uint regType, IntPtr buffer, uint bufSize, out uint required);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern int CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, int flags);

    /// <summary>
    ///     Enumerate game-controller-like HID devices that Windows currently considers <i>present</i>
    ///     (plugged in / actively reporting). Previously we also returned every past-connected
    ///     device Windows still remembered, which was tens of stale entries on a system that's
    ///     had a few controllers over time. <see cref="DetectedGamepad.Enabled"/> reports
    ///     whether the device is currently enabled (false ⇒ user has disabled it via Device
    ///     Manager or via this UI).
    /// </summary>
    public static List<DetectedGamepad> Enumerate()
    {
        var results = new List<DetectedGamepad>();
        var guid = GUID_DEVINTERFACE_HID;
        // DIGCF_PRESENT is the key flag — without it we get every HID node Windows has *ever*
        // seen. With it, only actually-attached devices appear. ALLCLASSES is dropped because we
        // already specified the HID class GUID.
        int flags = DIGCF_PRESENT;
        IntPtr info = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, flags);
        if (info == IntPtr.Zero || info == new IntPtr(-1)) return results;
        try
        {
            var data = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            for (uint i = 0; SetupDiEnumDeviceInfo(info, i, ref data); i++)
            {
                string instanceId = ReadString(info, ref data, instance: true) ?? "";
                string hardware = ReadProp(info, ref data, SPDRP_HARDWAREID) ?? "";
                if (!LooksLikeGamepad(hardware, instanceId)) continue;

                string friendly = ReadProp(info, ref data, SPDRP_FRIENDLYNAME)
                                  ?? ReadProp(info, ref data, SPDRP_DEVICEDESC)
                                  ?? "Unknown gaming device";

                // CM_PROB_DISABLED (0x16) is the actual "user disabled this in Device Manager"
                // problem code. DN_HAS_PROBLEM as a bitmask covers any problem — we used to
                // conflate "disabled" with "any error" which marked perfectly fine devices as
                // hidden.
                const uint CM_PROB_DISABLED = 0x16;
                bool enabled = true;
                try
                {
                    int rc = CM_Get_DevNode_Status(out uint status, out uint problem, data.DevInst, 0);
                    if (rc != 0)
                    {
                        enabled = true; // status call failed — assume enabled to avoid false hidden labels
                    }
                    else if ((status & DN_HAS_PROBLEM) != 0 && problem == CM_PROB_DISABLED)
                    {
                        enabled = false;
                    }
                }
                catch { enabled = true; }

                results.Add(new DetectedGamepad
                {
                    FriendlyName = friendly,
                    InstanceId = instanceId,
                    HardwareId = hardware,
                    Enabled = enabled,
                });
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(info);
        }
        // De-duplicate identical entries (some devices expose multiple HID collections that all
        // resolve to the same friendly name + instance prefix). Keep the first.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return results
            .Where(r => seen.Add(r.InstanceId))
            .ToList();
    }

    private static bool LooksLikeGamepad(string hardwareId, string instanceId)
    {
        // The hardware-ID string for XInput pads typically contains "IG_" plus an interface
        // index. Many vendor controllers (Scuf, etc.) also carry their VID in the path.
        if (string.IsNullOrEmpty(hardwareId) && string.IsNullOrEmpty(instanceId)) return false;
        string s = (hardwareId + "\n" + instanceId).ToUpperInvariant();
        // Hard filter — must be an HID node and have IG_ or known controller patterns.
        if (!s.Contains("HID")) return false;
        return s.Contains("IG_")
            || s.Contains("VID_045E")   // Microsoft (Xbox 360/One)
            || s.Contains("VID_054C")   // Sony (DualShock)
            || s.Contains("VID_2E95")   // Scuf
            || s.Contains("VID_1A86");  // common dongle vendor (your log had this too)
    }

    private static string? ReadProp(IntPtr info, ref SP_DEVINFO_DATA did, uint property)
    {
        SetupDiGetDeviceRegistryProperty(info, ref did, property, out _, IntPtr.Zero, 0, out uint required);
        if (required == 0) return null;
        IntPtr buf = Marshal.AllocHGlobal((int)required);
        try
        {
            if (!SetupDiGetDeviceRegistryProperty(info, ref did, property, out _, buf, required, out _))
                return null;
            return Marshal.PtrToStringAuto(buf);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private static string? ReadString(IntPtr info, ref SP_DEVINFO_DATA did, bool instance)
    {
        SetupDiGetDeviceInstanceId(info, ref did, IntPtr.Zero, 0, out uint required);
        if (required == 0) return null;
        IntPtr buf = Marshal.AllocHGlobal((int)required * 2);
        try
        {
            if (!SetupDiGetDeviceInstanceId(info, ref did, buf, required, out _))
                return null;
            return Marshal.PtrToStringAuto(buf);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }
}
