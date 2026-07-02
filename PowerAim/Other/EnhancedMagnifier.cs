using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PowerAim.Class.Native;
using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;
using WpfPixelFormats = System.Windows.Media.PixelFormats;
using WpfImage = System.Windows.Controls.Image;

/// <summary>
///     A higher-quality alternative to the native Windows Magnification API for the magnifier's
///     "Enhanced" scaling mode. Each tick it captures the cursor-centred source region and upscales it
///     into the dialog with GDI+ <see cref="InterpolationMode.HighQualityBicubic"/> (sharper than the
///     native bilinear), then blits the result into a reused <see cref="WriteableBitmap"/> shown by a
///     WPF <see cref="Image"/>. The dialog must be excluded from capture (WDA_EXCLUDEFROMCAPTURE) so
///     the screen grab doesn't pick up the magnifier itself.
/// </summary>
public sealed class EnhancedMagnifier : IDisposable
{
    private readonly System.Windows.Window _form;
    private readonly WpfImage _image;
    private readonly DispatcherTimer _blitTimer;
    private readonly Thread _worker;
    private readonly IntPtr _handle;
    private volatile bool _running = true;
    private float _magnification = 2f;
    private Bitmap? _src, _dst;                 // background-thread only

    // Frame hand-off: the worker fills _shared under _lock, the UI blit-timer copies it into _wb under
    // the same lock. This keeps the expensive CopyFromScreen + bicubic OFF the UI thread (it used to
    // run there every 33 ms and stall the dispatcher); the UI only does the cheap WritePixels.
    private readonly object _lock = new();
    private byte[]? _shared;
    private int _shW, _shH, _shStride;
    private bool _shDirty;
    private WriteableBitmap? _wb;               // UI-thread only

    public float Magnification
    {
        get => _magnification;
        set => _magnification = value < 1f ? 1f : value;
    }

    public EnhancedMagnifier(System.Windows.Window form, WpfImage image)
    {
        _form = form;
        _image = image;
        _handle = new WindowInteropHelper(form).Handle;
        // Cheap UI-side blit (~30 fps): just pushes the latest worker frame into the WriteableBitmap.
        _blitTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _blitTimer.Tick += (_, _) => Blit();
        _blitTimer.Start();
        // Heavy capture + upscale runs on its own background thread.
        _worker = new Thread(CaptureLoop) { IsBackground = true, Name = "EnhancedMagnifier" };
        _worker.Start();
    }

    private void CaptureLoop()
    {
        var sw = new System.Diagnostics.Stopwatch();
        while (_running)
        {
            sw.Restart();
            try { CaptureFrame(); } catch { /* dropped frame is fine; keep the loop alive */ }
            var rest = 33 - (int)sw.ElapsedMilliseconds;
            if (rest > 0) Thread.Sleep(rest);
        }
        // The GDI bitmaps belong to this thread — cleaning them up here (not in Dispose) means the
        // disposer can never pull them out from under a capture still in flight.
        _src?.Dispose();
        _dst?.Dispose();
        _src = _dst = null;
    }

    private void CaptureFrame()
    {
        var cr = new RECT();
        NativeAPIMethods.GetClientRect(_handle, ref cr);
        int dw = cr.Right - cr.Left, dh = cr.Bottom - cr.Top;     // destination = client size (device px)
        if (dw < 2 || dh < 2) return;

        float m = _magnification < 1f ? 1f : _magnification;   // snapshot once for this frame
        int sw = Math.Max(1, (int)(dw / m));                      // source = what we sample, in device px
        int sh = Math.Max(1, (int)(dh / m));

        NativeAPIMethods.GetCursorPos(out POINT p);
        int scrW = NativeAPIMethods.GetSystemMetrics(NativeStruct.SM_CXSCREEN);
        int scrH = NativeAPIMethods.GetSystemMetrics(NativeStruct.SM_CYSCREEN);
        int sx = Math.Clamp(p.X - sw / 2, 0, Math.Max(0, scrW - sw));
        int sy = Math.Clamp(p.Y - sh / 2, 0, Math.Max(0, scrH - sh));

        if (_src == null || _src.Width != sw || _src.Height != sh)
        {
            _src?.Dispose();
            _src = new Bitmap(sw, sh, GdiPixelFormat.Format32bppArgb);
        }
        using (var g = Graphics.FromImage(_src))
            g.CopyFromScreen(sx, sy, 0, 0, new System.Drawing.Size(sw, sh), CopyPixelOperation.SourceCopy);

        if (_dst == null || _dst.Width != dw || _dst.Height != dh)
        {
            _dst?.Dispose();
            _dst = new Bitmap(dw, dh, GdiPixelFormat.Format32bppArgb);
        }
        using (var g = Graphics.FromImage(_dst))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(_src, new Rectangle(0, 0, dw, dh), new Rectangle(0, 0, sw, sh), GraphicsUnit.Pixel);
        }

        // Copy the GDI result into the shared managed buffer for the UI thread to pick up.
        var data = _dst.LockBits(new Rectangle(0, 0, dw, dh), ImageLockMode.ReadOnly, GdiPixelFormat.Format32bppArgb);
        try
        {
            int bytes = data.Stride * dh;
            lock (_lock)
            {
                if (_shared == null || _shared.Length < bytes) _shared = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, _shared, 0, bytes);
                _shW = dw; _shH = dh; _shStride = data.Stride; _shDirty = true;
            }
        }
        finally { _dst.UnlockBits(data); }
    }

    private void Blit()
    {
        byte[] buf; int w, h, stride;
        lock (_lock)
        {
            if (!_shDirty || _shared == null) return;
            w = _shW; h = _shH; stride = _shStride;
            // WritePixels copies synchronously below, so referencing the shared buffer under-lock only
            // for the dimensions is enough — but the copy itself must also be inside the lock so the
            // worker can't overwrite it mid-write.
            if (_wb == null || _wb.PixelWidth != w || _wb.PixelHeight != h)
            {
                _wb = new WriteableBitmap(w, h, 96, 96, WpfPixelFormats.Bgra32, null);
                _image.Source = _wb;
            }
            _wb.WritePixels(new Int32Rect(0, 0, w, h), _shared, stride, 0);
            _shDirty = false;
        }
    }

    public void Dispose()
    {
        _running = false;
        _blitTimer.Stop();
        // Worker takes no UI lock and we hold none here → a short join can't deadlock. The worker
        // disposes its own GDI bitmaps when its loop exits (even if this join times out).
        if (!_worker.Join(TimeSpan.FromMilliseconds(200))) { /* background thread; it exits on its own */ }
        _wb = null;
        if (_image != null) _image.Source = null;
    }
}
