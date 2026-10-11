using System.Runtime.InteropServices;

namespace PowerAim.Class.Native;

/// <summary>
///     Stock-Windows alternative to HidHide for "make this gamepad invisible to games": uses
///     <c>SetupAPI</c> / <c>CfgMgr32</c> to disable a HID device node by its instance path.
///     <para>
///     Caveat compared to HidHide: this disables the device <i>system-wide</i> — Steam, other
///     apps, and PowerAim itself lose access. HidHide can hide per-application via a whitelist;
///     this implementation can't. Use it only when you specifically want one process (the game)
///     to see exclusively the virtual pad and you don't need the physical one elsewhere.
///     </para>
///     <para>
///     Requires elevated privileges. <see cref="TryDisable"/> / <see cref="TryEnable"/> return a
///     boolean status — failures (most commonly CR_ACCESS_DENIED when not elevated) are surfaced
///     via <see cref="LastError"/>.
///     </para>
/// </summary>
public static class DeviceHide
{
    private const int CR_SUCCESS = 0x00000000;
    private const int CM_DISABLE_PERSIST = 0x00000008;
    private const int CM_DISABLE_UI_NOT_OK = 0x00000004;

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int CM_Locate_DevNode(out uint pdnDevInst, string pDeviceID, int ulFlags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern int CM_Disable_DevNode(uint dnDevInst, int ulFlags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern int CM_Enable_DevNode(uint dnDevInst, int ulFlags);

    /// <summary>Last error message from <see cref="TryDisable"/>/<see cref="TryEnable"/> — empty on success.</summary>
    public static string LastError { get; private set; } = "";

    /// <summary>
    ///     Disable the device identified by its instance path (e.g.
    ///     <c>HID\VID_045E&amp;PID_028E&amp;IG_01\\9&amp;2862efeb&amp;1&amp;0000</c>). The device
    ///     stops being visible to Windows on the next pump — XInput drops the controller from its
    ///     slot, games see one fewer pad.
    /// </summary>
    public static bool TryDisable(string deviceInstanceId)
    {
        LastError = "";
        if (string.IsNullOrWhiteSpace(deviceInstanceId))
        {
            LastError = "Device instance ID is empty.";
            return false;
        }
        int rc = CM_Locate_DevNode(out uint devNode, deviceInstanceId, 0);
        if (rc != CR_SUCCESS)
        {
            LastError = $"CM_Locate_DevNode failed (0x{rc:X8}). Path may be wrong or device gone.";
            return false;
        }
        rc = CM_Disable_DevNode(devNode, 0);
        if (rc != CR_SUCCESS)
        {
            LastError = $"CM_Disable_DevNode failed (0x{rc:X8}). Most commonly this means PowerAim isn't running as administrator.";
            return false;
        }
        return true;
    }

    public static bool TryEnable(string deviceInstanceId)
    {
        LastError = "";
        if (string.IsNullOrWhiteSpace(deviceInstanceId))
        {
            LastError = "Device instance ID is empty.";
            return false;
        }
        int rc = CM_Locate_DevNode(out uint devNode, deviceInstanceId, 0);
        if (rc != CR_SUCCESS)
        {
            LastError = $"CM_Locate_DevNode failed (0x{rc:X8}).";
            return false;
        }
        rc = CM_Enable_DevNode(devNode, 0);
        if (rc != CR_SUCCESS)
        {
            LastError = $"CM_Enable_DevNode failed (0x{rc:X8}).";
            return false;
        }
        return true;
    }

    /// <summary>Best-effort check whether the current process is elevated. Used to gate the disable UI.</summary>
    public static bool IsElevated()
    {
        try
        {
            var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            var p = new System.Security.Principal.WindowsPrincipal(id);
            return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }
}
