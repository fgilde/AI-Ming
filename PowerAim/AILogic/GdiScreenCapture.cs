using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using PowerAim.AILogic.Contracts;

namespace PowerAim.AILogic;

/// <summary>
/// Original GDI-based screen capture using <see cref="Graphics.CopyFromScreen(int,int,int,int,Size)"/>.
/// Used as the fallback path when DXGI Desktop Duplication is unavailable.
/// </summary>
public class GdiScreenCapture : ICapture
{
    private Graphics? _graphics;

    public Screen Screen { get; }

    public Bitmap LastCapture { get; private set; } = null!;

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

        if (_graphics == null || LastCapture == null
            || LastCapture.Width != detectionBox.Width
            || LastCapture.Height != detectionBox.Height)
        {
            LastCapture?.Dispose();
            LastCapture = new Bitmap(detectionBox.Width, detectionBox.Height);

            _graphics?.Dispose();
            _graphics = Graphics.FromImage(LastCapture);
        }

        _graphics.CopyFromScreen(
            Screen.Bounds.Left + detectionBox.Left,
            Screen.Bounds.Top + detectionBox.Top,
            0, 0,
            detectionBox.Size);
        return LastCapture;
    }

    public Task OnPause() => Task.CompletedTask;

    public Task OnResume() => Task.CompletedTask;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        LastCapture?.Dispose();
        _graphics?.Dispose();
    }
}
