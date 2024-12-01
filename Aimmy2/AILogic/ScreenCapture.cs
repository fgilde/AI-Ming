using System.ComponentModel;
using Aimmy2.AILogic.Contracts;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Aimmy2.AILogic;

public class ScreenCapture : ICapture
{
    private Graphics? _graphics;
    public Screen Screen { get; }
    public Bitmap LastCapture { get; private set; }

    public ScreenCapture(): this(Screen.PrimaryScreen!)
    {}

    public ScreenCapture(Screen screen)
    {
        Screen = screen;
        OnPropertyChanged(nameof(Screen));
        OnPropertyChanged(nameof(CaptureArea));
    }

    public ScreenCapture(int screenIndex): this(Screen.AllScreens[screenIndex])
    {}

    public Bitmap Capture(Rectangle detectionBox)
    {
        if (detectionBox == Rectangle.Empty)
        {
            detectionBox = CaptureArea;
        }
        if (_graphics == null || LastCapture == null || LastCapture.Width != detectionBox.Width || LastCapture.Height != detectionBox.Height)
        {
            LastCapture?.Dispose();
            LastCapture = new Bitmap(detectionBox.Width, detectionBox.Height);

            _graphics?.Dispose();
            _graphics = Graphics.FromImage(LastCapture);
        }

        _graphics.CopyFromScreen(Screen.Bounds.Left + detectionBox.Left, Screen.Bounds.Top + detectionBox.Top, 0, 0, detectionBox.Size);
        //LastCapture = new Bitmap(_screenCaptureBitmap);
        return LastCapture;
    }

    public Task OnPause() => Task.CompletedTask;

    public Task OnResume() => Task.CompletedTask;

    public Rectangle CaptureArea => Screen.Bounds;

    public event PropertyChangedEventHandler? PropertyChanged;

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