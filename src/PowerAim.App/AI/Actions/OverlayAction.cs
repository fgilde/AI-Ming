using System.Windows;
using System.Windows.Forms;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.Other;
using PowerAim.UI.Visuality;
using Application = System.Windows.Application;


namespace PowerAim.AI.Actions;

public class OverlayAction : BaseAction
{
    private Form? _formOverlay = new();
    private DetectedPlayerWindow? _playerOverlay = new();
    private FOV? _fov = new();
    private bool _useForm = false;
    /// <summary>Last non-empty set used when a frame has no detections (hold ESP stable).</summary>
    private Prediction[] _lastDrawn = Array.Empty<Prediction>();
    private int _emptyStreak;
    private const int EmptyHoldFrames = 45; // ~0.5–1s depending on FPS before clear
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
        if (Active)
        {
            // Always ensure the WPF overlay is shown and opaque while ESP is on.
            var overlay = EnsurePlayerOverlay();
            SetOverlayEnabled(true);

            // Hold last non-empty detections so a brief miss does not blank the ESP.
            Prediction[] drawSet;
            if (predictions is { Length: > 0 })
            {
                _lastDrawn = predictions;
                _emptyStreak = 0;
                drawSet = predictions;
            }
            else
            {
                _emptyStreak++;
                drawSet = _emptyStreak <= EmptyHoldFrames ? _lastDrawn : Array.Empty<Prediction>();
                if (_emptyStreak > EmptyHoldFrames)
                    _lastDrawn = Array.Empty<Prediction>();
            }

            Prediction? best = drawSet is { Length: > 0 }
                ? drawSet.MaxBy(p => p.Confidence)
                : null;

            switch (AppConfig.Current.DropdownState.OverlayDrawingMethod)
            {
                case OverlayDrawingMethod.WpfWindowCanvas:
                    overlay.DrawPredictionCanvas(best);
                    break;
                case OverlayDrawingMethod.DrawingContextVisualHost:
                    overlay.DrawPredictions(drawSet, System.Windows.Rect.Empty);
                    break;
                case OverlayDrawingMethod.DesktopDC:
                    overlay.DrawPredictionCanvas(null);
                    PredictionDrawer.DrawPredictions(drawSet, targetArea: null);
                    break;
                case OverlayDrawingMethod.OverlayFormGDI:
                    _useForm = true;
                    overlay.DrawPredictionCanvas(null);
                    PredictionDrawer.DrawPredictions(EnsureFormOverlay(), drawSet, targetArea: null, useScreenSpace: false);
                    break;
            }
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
            NativeAPIMethods.MakeClickThrough(_formOverlay.Handle);
            NativeAPIMethods.HideForCapture(_formOverlay.Handle);
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

    public override Task OnResume()
    {
        _emptyStreak = 0;
        SetOverlayEnabled(true);
        try { EnsurePlayerOverlay(); } catch { /* UI not ready */ }
        return base.OnResume();
    }

    public override Task OnPause()
    {
        SetOverlayEnabled(false);
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


    private void SetOverlayEnabled(bool enabled)
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_playerOverlay != null)
                    _playerOverlay.Opacity = enabled ? 1 : 0;
            });
        }
        catch (Exception)
        { }
    }
}