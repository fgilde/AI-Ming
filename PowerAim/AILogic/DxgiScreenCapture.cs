using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using PowerAim.AILogic.Contracts;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace PowerAim.AILogic;

/// <summary>
/// DXGI Desktop Duplication based screen capture
/// </summary>
public sealed class DxgiScreenCapture : ICapture
{
    /// <summary>Frame cache TTL — see upstream CaptureManager.</summary>
    private static readonly TimeSpan FrameCacheTtl = TimeSpan.FromMilliseconds(8);

    /// <summary>Feature level fallback chain. 12_2 first (50-series GPUs), then walk down.</summary>
    private static readonly FeatureLevel[] FeatureLevels =
    [
        FeatureLevel.Level_12_2,
        FeatureLevel.Level_12_1,
        FeatureLevel.Level_12_0,
        FeatureLevel.Level_11_1,
        FeatureLevel.Level_11_0,
    ];

    private readonly object _captureLock = new();

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGIOutputDuplication? _duplication;
    private ID3D11Texture2D? _stagingTexture;
    private int _stagingWidth;
    private int _stagingHeight;

    private Bitmap? _lastCapture;
    private Rectangle _lastBox;
    private DateTime _lastCaptureUtc = DateTime.MinValue;

    private bool _disposed;

    public Screen Screen { get; }

    public Rectangle CaptureArea => Screen.Bounds;

    public Bitmap LastCapture => _lastCapture!;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DxgiScreenCapture() : this(Screen.PrimaryScreen!) { }

    public DxgiScreenCapture(int screenIndex) : this(Screen.AllScreens[screenIndex]) { }

    public DxgiScreenCapture(Screen screen)
    {
        Screen = screen ?? throw new ArgumentNullException(nameof(screen));
        InitializeDuplication();
        OnPropertyChanged(nameof(Screen));
        OnPropertyChanged(nameof(CaptureArea));
    }

    /// <summary>
    /// Probe whether DXGI Desktop Duplication is usable on the current hardware.
    /// Used by <see cref="ScreenCapture"/> to decide between the DXGI and GDI backends.
    /// </summary>
    public static bool IsSupported()
    {
        try
        {
            using var probe = new DxgiScreenCapture();
            return probe._duplication != null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DxgiScreenCapture] IsSupported probe failed: {ex.Message}");
            return false;
        }
    }

    public Bitmap Capture(Rectangle detectionBox)
    {
        if (detectionBox == Rectangle.Empty)
            detectionBox = CaptureArea;

        lock (_captureLock)
        {
            // Frame cache: serve the previous bitmap if we just captured for the same box.
            if (_lastCapture != null
                && _lastBox == detectionBox
                && DateTime.UtcNow - _lastCaptureUtc < FrameCacheTtl)
            {
                return _lastCapture;
            }

            try
            {
                var bmp = CaptureInternal(detectionBox);
                if (bmp != null)
                {
                    _lastCapture = bmp;
                    _lastBox = detectionBox;
                    _lastCaptureUtc = DateTime.UtcNow;
                    return bmp;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DxgiScreenCapture] Capture failed: {ex.Message} — re-initialising.");
                ResetDuplication();
            }

            // If we get here, the capture failed and we couldn't recover. Return the previous
            // frame so the AI loop doesn't crash on a null bitmap.
            return _lastCapture ?? new Bitmap(detectionBox.Width, detectionBox.Height, PixelFormat.Format32bppArgb);
        }
    }

    public Task OnPause() => Task.CompletedTask;
    public Task OnResume() => Task.CompletedTask;

    private void InitializeDuplication()
    {
        ReleaseDuplication();

        IDXGIFactory1? factory = null;
        IDXGIAdapter1? adapter = null;
        IDXGIOutput? output = null;
        IDXGIOutput1? output1 = null;
        try
        {
            factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            // Find the adapter+output that owns the target screen.
            (adapter, output) = FindOutputForScreen(factory, Screen);
            if (adapter == null || output == null)
            {
                Debug.WriteLine("[DxgiScreenCapture] Could not match Screen to a DXGI output.");
                return;
            }

            // Create device with fallback chain.
            foreach (var fl in FeatureLevels)
            {
                try
                {
                    var result = D3D11.D3D11CreateDevice(
                        adapter,
                        DriverType.Unknown,
                        DeviceCreationFlags.BgraSupport,
                        [fl],
                        out _device,
                        out _context);
                    if (result.Success && _device != null)
                    {
                        Debug.WriteLine($"[DxgiScreenCapture] D3D11 device created at feature level {fl}.");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DxgiScreenCapture] Feature level {fl} failed: {ex.Message}");
                    _device?.Dispose(); _device = null;
                    _context?.Dispose(); _context = null;
                }
            }

            if (_device == null)
            {
                Debug.WriteLine("[DxgiScreenCapture] D3D11 device creation exhausted all feature levels.");
                return;
            }

            output1 = output.QueryInterface<IDXGIOutput1>();
            _duplication = output1.DuplicateOutput(_device);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DxgiScreenCapture] InitializeDuplication failed: {ex.Message}");
            ReleaseDuplication();
        }
        finally
        {
            output1?.Dispose();
            output?.Dispose();
            adapter?.Dispose();
            factory?.Dispose();
        }
    }

    private static (IDXGIAdapter1? adapter, IDXGIOutput? output) FindOutputForScreen(IDXGIFactory1 factory, Screen screen)
    {
        IDXGIAdapter1? matchedAdapter = null;
        IDXGIOutput? matchedOutput = null;

        for (uint ai = 0; ai < 64; ai++)
        {
            var adapterResult = factory.EnumAdapters1(ai, out IDXGIAdapter1? adapter);
            if (adapterResult.Failure || adapter == null)
                break;

            bool keepAdapter = false;
            for (uint oi = 0; oi < 64; oi++)
            {
                var outputResult = adapter.EnumOutputs(oi, out IDXGIOutput? output);
                if (outputResult.Failure || output == null)
                    break;

                var desc = output.Description;
                var b = desc.DesktopCoordinates;
                var rect = new Rectangle(b.Left, b.Top, b.Right - b.Left, b.Bottom - b.Top);
                if (matchedOutput == null && rect == screen.Bounds)
                {
                    matchedAdapter = adapter;
                    matchedOutput = output;
                    keepAdapter = true;
                }
                else
                {
                    output.Dispose();
                }
            }

            if (!keepAdapter)
                adapter.Dispose();

            if (matchedOutput != null)
                return (matchedAdapter, matchedOutput);
        }
        return (null, null);
    }

    private unsafe Bitmap? CaptureInternal(Rectangle detectionBox)
    {
        if (_duplication == null || _device == null || _context == null)
        {
            InitializeDuplication();
            if (_duplication == null) return null;
        }

        IDXGIResource? desktopResource = null;
        ID3D11Texture2D? acquiredTexture = null;
        try
        {
            var acquireResult = _duplication!.AcquireNextFrame(100, out _, out desktopResource);
            if (acquireResult == Vortice.DXGI.ResultCode.WaitTimeout)
            {
                // No new frame within the timeout window — fall back to the previous bitmap.
                return _lastCapture;
            }
            if (acquireResult == Vortice.DXGI.ResultCode.AccessLost
                || acquireResult.Code == unchecked((int)0x887A0026) /* DXGI_ERROR_ACCESS_LOST */)
            {
                Debug.WriteLine("[DxgiScreenCapture] DXGI access lost — re-initialising.");
                ResetDuplication();
                return null;
            }
            if (acquireResult.Failure || desktopResource == null)
            {
                Debug.WriteLine($"[DxgiScreenCapture] AcquireNextFrame failed: 0x{acquireResult.Code:X8}");
                ResetDuplication();
                return null;
            }

            acquiredTexture = desktopResource.QueryInterface<ID3D11Texture2D>();
            var srcDesc = acquiredTexture.Description;
            int srcWidth = (int)srcDesc.Width;
            int srcHeight = (int)srcDesc.Height;

            EnsureStagingTexture(srcWidth, srcHeight);

            // Compute the clipped capture region in screen-local coordinates.
            int srcLeft = Math.Max(0, Math.Min(detectionBox.Left, srcWidth));
            int srcTop = Math.Max(0, Math.Min(detectionBox.Top, srcHeight));
            int srcRight = Math.Max(srcLeft, Math.Min(detectionBox.Right, srcWidth));
            int srcBottom = Math.Max(srcTop, Math.Min(detectionBox.Bottom, srcHeight));
            int copyWidth = srcRight - srcLeft;
            int copyHeight = srcBottom - srcTop;

            if (copyWidth <= 0 || copyHeight <= 0)
                return _lastCapture;

            // Copy the full desktop into our staging texture (GPU-side copy is cheap),
            // then crop on the CPU side when blitting into the managed Bitmap. This avoids
            // having to wire the D3D11_BOX type from Vortice (whose name varies by version).
            _context!.CopyResource(_stagingTexture!, acquiredTexture);

            var mapped = _context.Map(_stagingTexture!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                var bmp = new Bitmap(copyWidth, copyHeight, PixelFormat.Format32bppArgb);
                var bmpData = bmp.LockBits(new Rectangle(0, 0, copyWidth, copyHeight),
                                           ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int bytesPerRow = copyWidth * 4;
                    byte* src = (byte*)mapped.DataPointer.ToPointer();
                    byte* dst = (byte*)bmpData.Scan0.ToPointer();
                    int srcStride = (int)mapped.RowPitch;
                    int srcByteOffset = srcLeft * 4;
                    for (int y = 0; y < copyHeight; y++)
                    {
                        Buffer.MemoryCopy(src + (srcTop + y) * srcStride + srcByteOffset,
                                          dst + y * bmpData.Stride,
                                          bytesPerRow,
                                          bytesPerRow);
                    }
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }
                return bmp;
            }
            finally
            {
                _context.Unmap(_stagingTexture!, 0);
            }
        }
        finally
        {
            acquiredTexture?.Dispose();
            desktopResource?.Dispose();
            try { _duplication?.ReleaseFrame(); } catch { /* ignore */ }
        }
    }

    private void EnsureStagingTexture(int width, int height)
    {
        if (_stagingTexture != null && _stagingWidth == width && _stagingHeight == height)
            return;

        _stagingTexture?.Dispose();
        var desc = new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None
        };
        _stagingTexture = _device!.CreateTexture2D(desc);
        _stagingWidth = width;
        _stagingHeight = height;
    }

    private void ResetDuplication()
    {
        ReleaseDuplication();
        try
        {
            InitializeDuplication();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DxgiScreenCapture] Re-init failed: {ex.Message}");
        }
    }

    private void ReleaseDuplication()
    {
        try { _duplication?.Dispose(); } catch { }
        _duplication = null;

        try { _stagingTexture?.Dispose(); } catch { }
        _stagingTexture = null;
        _stagingWidth = 0;
        _stagingHeight = 0;

        try { _context?.Dispose(); } catch { }
        _context = null;

        try { _device?.Dispose(); } catch { }
        _device = null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_captureLock)
        {
            ReleaseDuplication();
            _lastCapture?.Dispose();
            _lastCapture = null;
        }
    }
}
