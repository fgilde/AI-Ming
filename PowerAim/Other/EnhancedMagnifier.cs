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
    private readonly DispatcherTimer _timer;
    private readonly IntPtr _handle;
    private float _magnification = 2f;
    private Bitmap? _src, _dst;
    private WriteableBitmap? _wb;

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
        // ~30 fps on the UI thread — capture + bicubic of a small window is cheap, and a faster tick
        // would just compete with the game for cycles for no visible gain.
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Render();
        _timer.Start();
    }

    private void Render()
    {
        try
        {
            var cr = new RECT();
            NativeAPIMethods.GetClientRect(_handle, ref cr);
            int dw = cr.Right - cr.Left, dh = cr.Bottom - cr.Top;     // destination = client size (device px)
            if (dw < 2 || dh < 2) return;

            int sw = Math.Max(1, (int)(dw / _magnification));          // source = what we sample, in device px
            int sh = Math.Max(1, (int)(dh / _magnification));

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

            // Blit the GDI result into a reused WriteableBitmap (avoids per-frame HBITMAP churn).
            if (_wb == null || _wb.PixelWidth != dw || _wb.PixelHeight != dh)
            {
                _wb = new WriteableBitmap(dw, dh, 96, 96, WpfPixelFormats.Bgra32, null);
                _image.Source = _wb;
            }
            var data = _dst.LockBits(new Rectangle(0, 0, dw, dh), ImageLockMode.ReadOnly, GdiPixelFormat.Format32bppArgb);
            try { _wb.WritePixels(new Int32Rect(0, 0, dw, dh), data.Scan0, data.Stride * dh, data.Stride); }
            finally { _dst.UnlockBits(data); }
        }
        catch { /* a dropped frame is fine; never tear down the loop on a transient capture error */ }
    }

    public void Dispose()
    {
        _timer.Stop();
        _src?.Dispose();
        _dst?.Dispose();
        _src = _dst = null;
        _wb = null;
        if (_image != null) _image.Source = null;
    }
}
