using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using PowerAim.Models;
using PowerAim.Extensions;
using Nextended.Core.Extensions;

namespace PowerAim.AI;

public class CaptureSource
{
    public CaptureTargetType TargetType { get; set; }
    public int? ProcessOrScreenId { get; set; }
    public string Title { get; set; }

    /// <summary>Capture device only: the card delivers the picture upside down.</summary>
    public bool FlipVertical { get; set; }

    public CaptureSource()
    {}

    private CaptureSource(CaptureTargetType targetType, string title, int? processOrScreenId = null)
    {
        TargetType = targetType;
        ProcessOrScreenId = processOrScreenId;
        Title = title;
    }

    public static CaptureSource MainScreen() => new(CaptureTargetType.Screen, System.Windows.Forms.Screen.PrimaryScreen.DeviceFriendlyName());

    public static CaptureSource Screen(Screen screen) => new(CaptureTargetType.Screen, screen.DeviceFriendlyName(), System.Windows.Forms.Screen.AllScreens.IndexOf(screen));
    public static CaptureSource Screen(int index) => new(CaptureTargetType.Screen, System.Windows.Forms.Screen.AllScreens[index].DeviceFriendlyName(), index);

    public static CaptureSource Process(int processId) => new(CaptureTargetType.Process, System.Diagnostics.Process.GetProcessById(processId).MainWindowTitle, processId);

    public static CaptureSource Process(ProcessModel process) => Process(process.Process);

    public static CaptureSource Process(Process process) => new(CaptureTargetType.Process, process.MainWindowTitle, process.Id);

    /// <summary>A video input device (capture card) by its DirectShow name; the index is the fallback when the name isn't found.</summary>
    public static CaptureSource Device(int index, string name, bool flipVertical = false)
        => new(CaptureTargetType.CaptureDevice, name, index) { FlipVertical = flipVertical };

    /// <summary>One still frame (preview). Not for capture devices: opening the card here would race the running capture.</summary>
    public Bitmap Capture()
    {
        if (TargetType == CaptureTargetType.CaptureDevice) throw new NotSupportedException("No still frame from a capture device.");
        using var capture = AIManager.CreateScreenCapture(this);
        var frame = capture.Capture(Rectangle.Empty);
        return capture.CallerOwnsFrame ? frame : new Bitmap(frame);
    }

    public override string ToString() => $"{TargetType} {Title} ({ProcessOrScreenId})";
}

public enum CaptureTargetType
{
    Screen,
    Process,
    CaptureDevice
}