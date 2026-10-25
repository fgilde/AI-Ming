using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using PowerAim.AI.Contracts;

namespace PowerAim.AI;

/// <summary>
/// Original GDI-based screen capture using <see cref="Graphics.CopyFromScreen(int,int,int,int,Size)"/>.
/// Used as the fallback path when DXGI Desktop Duplication is unavailable.
/// </summary>
public class GdiScreenCapture : ICapture
{
    private readonly object _sync = new();
    private Graphics? _graphics;
    private Bitmap? _lastCapture;

    public Screen Screen { get; }

    public Rectangle CaptureArea => Screen.Bounds;

    public event PropertyChangedEventHandler? PropertyChanged;

    public GdiScreenCapture() : this(Screen.PrimaryScreen!) { }

    public GdiScreenCapture(int screenIndex) : this(Screen.AllScreens[screenIndex]) { }

    public GdiScreenCapture(Screen screen)
    {
        Screen = screen;
        OnPropertyChanged(nameof(Screen));
        OnPropertyChanged(nameof(CaptureArea));
    }

    public Bitmap Capture(Rectangle detectionBox)
    {
        if (detectionBox == Rectangle.Empty)
        {
            detectionBox = CaptureArea;
        }

        lock (_sync)
        {
            if (_graphics == null || _lastCapture == null
                || _lastCapture.Width != detectionBox.Width
                || _lastCapture.Height != detectionBox.Height)
            {
                _lastCapture?.Dispose();
                _lastCapture = new Bitmap(detectionBox.Width, detectionBox.Height);

                _graphics?.Dispose();
                _graphics = Graphics.FromImage(_lastCapture);
            }

            _graphics.CopyFromScreen(
                Screen.Bounds.Left + detectionBox.Left,
                Screen.Bounds.Top + detectionBox.Top,
                0, 0,
                detectionBox.Size);
            return _lastCapture;
        }
    }

    public Bitmap? CopyLastCaptureCenter(int size)
    {
        lock (_sync)
            return CapturedFrame.CopyCenter(_lastCapture, size);
    }

    public Task OnPause() => Task.CompletedTask;

    public Task OnResume() => Task.CompletedTask;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _lastCapture?.Dispose();
            _graphics?.Dispose();
        }
    }
}
