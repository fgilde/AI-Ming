
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Aimmy2.AILogic.Contracts;

public interface ICapture : INotifyPropertyChanged, IDisposable
{
    Screen Screen { get; }
    Rectangle CaptureArea { get; }
    Bitmap Capture(Rectangle detectionBox);
    Task OnPause();
    Task OnResume();
}
