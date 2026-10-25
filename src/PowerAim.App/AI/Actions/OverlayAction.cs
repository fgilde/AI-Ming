using System.Windows;
using System.Windows.Forms;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.Other;
using PowerAim.UI.Visuality;
using Nextended.UI.Native;
using Nextended.UI.WPF.Extensions;
using Application = System.Windows.Application;


namespace PowerAim.AI.Actions;

public class OverlayAction : BaseAction
{
    private Form? _formOverlay = new();
    private DetectedPlayerWindow? _playerOverlay = new();
    private FOV? _fov = new();
    private bool _useForm = false;
    /// <summary>Last non-empty set, redrawn for <see cref="EmptyHold"/> when a frame has no detections.</summary>
    private Prediction[] _lastDrawn = Array.Empty<Prediction>();
    private DateTime _lastDetectionUtc;
    /// <summary>Bridges single-frame detection drops (no flicker) without leaving boxes behind a gone target.</summary>
    private static readonly TimeSpan EmptyHold = TimeSpan.FromMilliseconds(150);
    /// <summary>
    ///     The WPF overlay window is shown and opaque. It carries the ESP boxes and the captured-area border,
    ///     so it is shown while either is on — toggled only on change, never per frame.
    /// </summary>
    private bool _overlayShown;
    /// <summary>ESP boxes may still be on the overlay and must be cleared when ESP turns off.</summary>
    private bool _espDrawn;
    public OverlayAction()
    {
        EnsureFormOverlay();
    }

    // Drawing (GDI / DesktopDC / form invoke) is real synchronous work per frame — keep it off the
    // AI-loop thread so it runs alongside the other actions instead of adding to the frame time.
    public override Task Execute(Prediction[] predictions) => Task.Run(() => ExecuteAsync(predictions.ToArray()));

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        var formActive = _useForm;
        _useForm = false;
        // Show/hide only on change: each call is a blocking UI-thread round trip.
        if (OverlayWanted != _overlayShown)
        {
            if (OverlayWanted) ShowPlayerOverlay();
            else HidePlayerOverlay();
        }

        if (Active && _playerOverlay is { } overlay)
        {
            _espDrawn = true;

            var now = DateTime.UtcNow;
            if (predictions is { Length: > 0 })
            {
                _lastDrawn = predictions;
                _lastDetectionUtc = now;
            }
            var drawSet = now - _lastDetectionUtc <= EmptyHold ? _lastDrawn : Array.Empty<Prediction>();

            Prediction? best = drawSet is { Length: > 0 }
                ? drawSet.MaxBy(p => p.Confidence)
                : null;

            // Predictions are relative to the capture area. The overlay window and the form sit on the
            // capture area (draw local, origin 0,0); the desktop DC is the whole screen (offset by it).
            var captureArea = ImageCapture.CaptureArea;
            var local = new System.Drawing.Rectangle(0, 0, captureArea.Width, captureArea.Height);
            switch (AppConfig.Current.DropdownState.OverlayDrawingMethod)
            {
                case OverlayDrawingMethod.WpfWindowCanvas:
                    overlay.DrawPredictionCanvas(best);
                    break;
                case OverlayDrawingMethod.DrawingContextVisualHost:
                    overlay.DrawPredictions(drawSet, local.ToRect());
                    break;
                case OverlayDrawingMethod.DesktopDC:
                    overlay.DrawPredictionCanvas(null);
                    PredictionDrawer.DrawPredictions(drawSet, captureArea);
                    break;
                case OverlayDrawingMethod.OverlayFormGDI:
                    _useForm = true;
                    overlay.DrawPredictionCanvas(null);
                    PredictionDrawer.DrawPredictions(EnsureFormOverlay(), drawSet, local);
                    break;
            }
        }
        else if (_espDrawn)
        {
            // ESP switched off while running: clear the boxes (the window may stay for the captured-area border).
            _espDrawn = false;
            _lastDrawn = Array.Empty<Prediction>();
            _playerOverlay?.DrawPredictions(Array.Empty<Prediction>(), System.Windows.Rect.Empty);
            _playerOverlay?.DrawPredictionCanvas(null);
        }
        if (formActive != _useForm && !_useForm)
        {
            EnsureFormOverlay(true);
        }

        return Task.CompletedTask;
    }

    private Form EnsureFormOverlay(bool close = false)
    {
        _formOverlay ??= new Form();

        if (_formOverlay.InvokeRequired)
        {
            return (Form)_formOverlay.Invoke(() => EnsureFormOverlay(close));
        }

        if (close && _formOverlay.Visible)
        {
            _formOverlay.Hide();
            return _formOverlay;
        }

        if (ImageCapture?.CaptureArea != null)
        {
            _formOverlay.Left = ImageCapture.CaptureArea.Left;
            _formOverlay.Top = ImageCapture.CaptureArea.Top;
            _formOverlay.Width = ImageCapture.CaptureArea.Width;
            _formOverlay.Height = ImageCapture.CaptureArea.Height;
        }

        _formOverlay.FormBorderStyle = FormBorderStyle.None;
        _formOverlay.TopMost = true;
        _formOverlay.BackColor = System.Drawing.Color.DeepPink; 
        _formOverlay.TransparencyKey = System.Drawing.Color.DeepPink; 
        _formOverlay.ShowInTaskbar = false; 
        _formOverlay.AllowTransparency = true; 
        //_formOverlay.DoubleBuffered = false; // Disable double buffering

        _formOverlay.Show();

        if (_formOverlay.IsHandleCreated)
        {
            NativeMethods.MakeClickThrough(_formOverlay.Handle);
            NativeMethods.HideForCapture(_formOverlay.Handle);
        }

        return _formOverlay;
    }


    private DetectedPlayerWindow EnsurePlayerOverlay()
    {
        void ShowIt(DetectedPlayerWindow w)
        {
            if (w.Visibility != Visibility.Visible)
                w.Show();
            if (w.Opacity < 1)
                w.Opacity = 1;
            if (!w.IsVisible)
                w.Show();
            // Keep above game without stealing focus
            try { w.Topmost = true; } catch { /* ignore */ }
        }

        if (_playerOverlay == null)
        {
            // Must create on UI thread
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    _playerOverlay ??= new DetectedPlayerWindow();
                    ShowIt(_playerOverlay);
                    return _playerOverlay;
                });
            }
            _playerOverlay = new DetectedPlayerWindow();
        }

        if (_playerOverlay.Dispatcher.CheckAccess())
            ShowIt(_playerOverlay);
        else
            _playerOverlay.Dispatcher.Invoke(() => ShowIt(_playerOverlay!));

        return _playerOverlay;
    }

    private void ShowPlayerOverlay()
    {
        EnsurePlayerOverlay();
        SetOverlayEnabled(true);
        _overlayShown = true;
    }

    private void HidePlayerOverlay()
    {
        SetOverlayEnabled(false);
        _overlayShown = false;
    }

    public override Task OnResume()
    {
        // The overlay itself is shown by the next frame (ExecuteAsync) when ESP or the captured-area border is on.
        _lastDrawn = Array.Empty<Prediction>();
        return base.OnResume();
    }

    public override Task OnPause()
    {
        HidePlayerOverlay();
        return base.OnPause();
    }

    public override void Dispose()
    {
        if (_playerOverlay != null)
        {
            _playerOverlay.Dispatcher.Invoke(() =>
            {
                _playerOverlay.Close();
                _playerOverlay = null;
            });
        }

        if (_fov != null)
        {
            _fov.Dispatcher.Invoke(() =>
            {
                _fov.Close();
                _fov = null;
            });
        }

        base.Dispose();
    }

    public override bool Active => base.Active && AppConfig.Current.ToggleState.ShowDetectedPlayer;

    private bool OverlayWanted => base.Active && AppConfig.Current.ToggleState is { } t && (t.ShowDetectedPlayer || t.ShowCapturedArea);


    private void SetOverlayEnabled(bool enabled)
    {
        var overlay = _playerOverlay;
        if (overlay == null) return;
        try
        {
            overlay.Dispatcher.Invoke(() => overlay.Opacity = enabled ? 1 : 0);
        }
        catch (Exception)
        { }
    }
}