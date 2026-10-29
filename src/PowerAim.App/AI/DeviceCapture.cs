using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using OpenCvSharp;
using PowerAim.AI.Contracts;

namespace PowerAim.AI;

/// <summary>
///     Capture from a video input device — typically a capture card showing a console — through DirectShow.
///     A reader thread keeps the newest frame; <see cref="Capture"/> cuts the detection box out of it. The
///     frame's own size is the capture area. A video file path works the same (used as a stand-in in tests).
/// </summary>
public sealed class DeviceCapture : ICapture
{
    /// <summary>Tried in order until the card delivers one: high refresh first, then resolution.</summary>
    private static readonly (int W, int H, double Fps)[] Modes =
    [
        (2560, 1440, 120), (2560, 1440, 60), (1920, 1080, 120), (1920, 1080, 60), (1280, 720, 120), (1280, 720, 60),
    ];

    private readonly bool _flipVertical;
    private readonly object _gate = new();
    private readonly Thread _reader;
    private volatile bool _running = true;
    private Mat? _latest;
    private Rectangle _area;

    public DeviceCapture(int deviceIndex, bool flipVertical) : this(() => OpenDevice(deviceIndex), flipVertical, throttle: false) { }

    public DeviceCapture(string videoPath, bool flipVertical) : this(() => new VideoCapture(videoPath), flipVertical, throttle: true) { }

    private DeviceCapture(Func<VideoCapture> open, bool flipVertical, bool throttle)
    {
        _flipVertical = flipVertical;
        _reader = new Thread(() => ReadLoop(open, throttle)) { IsBackground = true, Name = "PowerAim device capture" };
        _reader.Start();
    }

    public Screen Screen => Screen.PrimaryScreen!;

    public Rectangle CaptureArea
    {
        get { lock (_gate) return _area; }
    }

    /// <summary>Every frame handed out is a fresh bitmap.</summary>
    public bool CallerOwnsFrame => true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Bitmap Capture(Rectangle detectionBox)
    {
        lock (_gate)
        {
            if (detectionBox == Rectangle.Empty) detectionBox = _area;
            if (_latest == null || _latest.Empty())
                return new Bitmap(Math.Max(1, detectionBox.Width), Math.Max(1, detectionBox.Height), PixelFormat.Format32bppArgb);
            return Crop(_latest, detectionBox, _flipVertical);
        }
    }

    public Bitmap? CopyLastCaptureCenter(int size)
    {
        lock (_gate)
        {
            if (_latest == null || _latest.Empty() || _area.Width < size || _area.Height < size) return null;
            return Crop(_latest, new Rectangle((_area.Width - size) / 2, (_area.Height - size) / 2, size, size), _flipVertical);
        }
    }

    public Task OnPause() => Task.CompletedTask;
    public Task OnResume() => Task.CompletedTask;

    public void Dispose()
    {
        _running = false;
        _reader.Join(2000);
        lock (_gate)
        {
            _latest?.Dispose();
            _latest = null;
        }
    }

    /// <summary>The first of <see cref="Modes"/> the card reports back as set (same size, at least 90 % of the rate).</summary>
    internal static (int W, int H, double Fps)? ProbeMode(Func<(int W, int H, double Fps), (int W, int H, double Fps)> trySet)
    {
        foreach (var mode in Modes)
        {
            var got = trySet(mode);
            if (got.W == mode.W && got.H == mode.H && got.Fps >= mode.Fps * 0.9) return got;
        }
        return null;
    }

    /// <summary>The <paramref name="box"/> part of a BGR frame (clipped to it) as a 32-bit bitmap.</summary>
    internal static unsafe Bitmap Crop(Mat frame, Rectangle box, bool flipVertical)
    {
        var area = Rectangle.Intersect(box, new Rectangle(0, 0, frame.Width, frame.Height));
        if (area.Width <= 0 || area.Height <= 0) return new Bitmap(Math.Max(1, box.Width), Math.Max(1, box.Height), PixelFormat.Format32bppArgb);

        using var source = flipVertical ? frame.Flip(FlipMode.X) : null;
        using var roi = new Mat(source ?? frame, new OpenCvSharp.Rect(area.X, area.Y, area.Width, area.Height));
        using var bgra = new Mat();
        if (frame.Channels() == 4) roi.CopyTo(bgra);
        else Cv2.CvtColor(roi, bgra, ColorConversionCodes.BGR2BGRA);

        var bmp = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, area.Width, area.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = area.Width * 4;
            for (int y = 0; y < area.Height; y++)
                Buffer.MemoryCopy((byte*)bgra.Ptr(y), (byte*)data.Scan0 + y * data.Stride, rowBytes, rowBytes);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return bmp;
    }

    private static VideoCapture OpenDevice(int index)
    {
        var device = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
        var mode = ProbeMode(m =>
        {
            // Uncompressed YUY2: MJPG modes add decode latency.
            device.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('Y', 'U', 'Y', '2'));
            device.Set(VideoCaptureProperties.FrameWidth, m.W);
            device.Set(VideoCaptureProperties.FrameHeight, m.H);
            device.Set(VideoCaptureProperties.Fps, m.Fps);
            return ((int)device.Get(VideoCaptureProperties.FrameWidth), (int)device.Get(VideoCaptureProperties.FrameHeight),
                device.Get(VideoCaptureProperties.Fps));
        });
        if (mode != null) return device;
        // Nothing matched: reopen so the card runs in its own default mode, not the last one tried.
        device.Dispose();
        return new VideoCapture(index, VideoCaptureAPIs.DSHOW);
    }

    private void ReadLoop(Func<VideoCapture> open, bool throttle)
    {
        VideoCapture? device = null;
        try
        {
            device = open();
            double frameMs = throttle && device.Fps > 0 ? 1000.0 / device.Fps : 0;
            var mat = new Mat();
            while (_running)
            {
                if (!device.IsOpened() || !device.Read(mat) || mat.Empty())
                {
                    if (throttle) device.Set(VideoCaptureProperties.PosFrames, 0); // file source: loop it
                    Thread.Sleep(20);
                    continue;
                }
                bool sizeChanged;
                lock (_gate)
                {
                    (_latest, mat) = (mat, _latest ?? new Mat()); // swap buffers: the next read refills the old one
                    sizeChanged = _area.Width != _latest.Width || _area.Height != _latest.Height;
                    if (sizeChanged) _area = new Rectangle(0, 0, _latest.Width, _latest.Height);
                }
                if (sizeChanged)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CaptureArea)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Screen)));
                }
                if (frameMs > 0) Thread.Sleep((int)frameMs);
            }
            mat.Dispose();
        }
        catch (Exception ex)
        {
            PowerAim.Other.AppDebugLog.Debug("Capture", "Video input stopped: " + ex.Message);
        }
        finally
        {
            device?.Dispose();
        }
    }
}
