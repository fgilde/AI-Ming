using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Aimmy2.AILogic.Contracts;

namespace Aimmy2.AILogic;

/// <summary>
/// Public screen-capture entry point. Internally delegates to either the DXGI Desktop
/// Duplication backend (<see cref="DxgiScreenCapture"/>) when supported by the running
/// hardware, or falls back to the original GDI implementation (<see cref="GdiScreenCapture"/>).
/// <para>
/// Existing call-sites (<c>new ScreenCapture()</c>, <c>new ScreenCapture(screen)</c>,
/// <c>new ScreenCapture(index)</c>) keep working unchanged — the <see cref="ICapture"/>
/// contract is preserved verbatim.
/// </para>
/// </summary>
public class ScreenCapture : ICapture
{
    /// <summary>
    /// One-shot result of probing DXGI on this machine. We avoid probing per instance because
    /// instantiating a DXGI device is expensive.
    /// </summary>
    private static readonly Lazy<bool> _dxgiSupported = new(DxgiScreenCapture.IsSupported);

    private readonly ICapture _inner;

    public Screen Screen => _inner.Screen;

    public Rectangle CaptureArea => _inner.CaptureArea;

    public Bitmap LastCapture => _inner.LastCapture;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ScreenCapture() : this(Screen.PrimaryScreen!) { }

    public ScreenCapture(int screenIndex) : this(Screen.AllScreens[screenIndex]) { }

    public ScreenCapture(Screen screen)
    {
        _inner = CreateBackend(screen);
        _inner.PropertyChanged += (_, e) => PropertyChanged?.Invoke(this, e);
        OnPropertyChanged(nameof(Screen));
        OnPropertyChanged(nameof(CaptureArea));
    }

    private static ICapture CreateBackend(Screen screen)
    {
        if (_dxgiSupported.Value)
        {
            try
            {
                return new DxgiScreenCapture(screen);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenCapture] DXGI backend creation failed, falling back to GDI: {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine("[ScreenCapture] DXGI not supported on this machine — using GDI capture.");
        }

        return new GdiScreenCapture(screen);
    }

    public Bitmap Capture(Rectangle detectionBox) => _inner.Capture(detectionBox);

    public Task OnPause() => _inner.OnPause();

    public Task OnResume() => _inner.OnResume();

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose() => _inner.Dispose();
}
