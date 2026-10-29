
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace PowerAim.AI.Contracts;

public interface ICapture : INotifyPropertyChanged, IDisposable
{
    Screen Screen { get; }
    Rectangle CaptureArea { get; }
    Bitmap Capture(Rectangle detectionBox);

    /// <summary>
    ///     True when <see cref="Capture"/> hands out a bitmap the caller owns and must dispose; false when it
    ///     is the backend's own buffer (reused or kept for <see cref="CopyLastCaptureCenter"/>).
    /// </summary>
    bool CallerOwnsFrame => false;

    /// <summary>
    ///     A copy of the centred <paramref name="size"/>² patch of the most recent frame, safe from any
    ///     thread (the frame itself is never handed out). Null before the first frame or when it is smaller.
    /// </summary>
    Bitmap? CopyLastCaptureCenter(int size);
    Task OnPause();
    Task OnResume();
}
