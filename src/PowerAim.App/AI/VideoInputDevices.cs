using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace PowerAim.AI;

/// <summary>
///     Names of the DirectShow video input devices (capture cards, webcams) in DirectShow order — the order
///     <see cref="DeviceCapture"/> opens them by index. Only lists them; no device is opened.
/// </summary>
public static class VideoInputDevices
{
    private static readonly Guid SystemDeviceEnum = new("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
    private static readonly Guid VideoInputCategory = new("860BB310-5D01-11d0-BD3B-00A0C911CE86");

    public static List<string> Names()
    {
        var names = new List<string>();
        object? devEnum = null;
        IEnumMoniker? monikers = null;
        try
        {
            var type = Type.GetTypeFromCLSID(SystemDeviceEnum);
            devEnum = type == null ? null : Activator.CreateInstance(type);
            if (devEnum is not ICreateDevEnum create) return names;
            var category = VideoInputCategory;
            if (create.CreateClassEnumerator(ref category, out monikers, 0) != 0 || monikers == null) return names;
            var one = new IMoniker[1];
            while (monikers.Next(1, one, IntPtr.Zero) == 0)
            {
                object? bagObject = null;
                try
                {
                    var bagId = typeof(IPropertyBag).GUID;
                    one[0].BindToStorage(null!, null!, ref bagId, out bagObject);
                    string name = "Video device";
                    if (bagObject is IPropertyBag bag && bag.Read("FriendlyName", out var value, IntPtr.Zero) == 0 && value is string s)
                        name = s;
                    names.Add(name);
                }
                finally
                {
                    Release(bagObject);
                    Release(one[0]);
                }
            }
        }
        catch
        {
            // No DirectShow — no devices to offer.
        }
        finally
        {
            Release(monikers);
            Release(devEnum);
        }
        return names;
    }

    private static void Release(object? com)
    {
        if (com != null && Marshal.IsComObject(com)) Marshal.ReleaseComObject(com);
    }

    /// <summary>Index of the device called <paramref name="name"/> right now, or null (unplugged / renamed).</summary>
    public static int? IndexOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        int i = Names().FindIndex(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 ? i : null;
    }

    [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICreateDevEnum
    {
        [PreserveSig] int CreateClassEnumerator([In] ref Guid type, out IEnumMoniker? enumMoniker, int flags);
    }

    [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyBag
    {
        [PreserveSig] int Read([MarshalAs(UnmanagedType.LPWStr)] string propName, [MarshalAs(UnmanagedType.Struct)] out object? value, IntPtr errorLog);
        [PreserveSig] int Write([MarshalAs(UnmanagedType.LPWStr)] string propName, [MarshalAs(UnmanagedType.Struct)] ref object value);
    }
}
