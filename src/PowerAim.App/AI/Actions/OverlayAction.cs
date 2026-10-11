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
            // Always ensure the WPF overlay is shown when ESP is active (constructor alone does not Show).
            var overlay = EnsurePlayerOverlay();
            // Best detection for the single-box canvas path (old MinBy picked the weakest).
            Prediction? best = predictions is { Length: > 0 }
                ? predictions.MaxBy(p => p.Confidence)
                : null;

            switch (AppConfig.Current.DropdownState.OverlayDrawingMethod)
            {
                case OverlayDrawingMethod.WpfWindowCanvas:
                    overlay.DrawPredictionCanvas(best);
                    break;
                case OverlayDrawingMethod.DrawingContextVisualHost:
                    // VisualHost is capture-aligned; drawer uses model-space boxes (no screen offset).
                    overlay.DrawPredictions(predictions, System.Windows.Rect.Empty);
                    break;
                case OverlayDrawingMethod.DesktopDC:
                    overlay.DrawPredictionCanvas(null);
                    // Desktop DC is screen space — use TranslatedRectangle (screen absolute); no extra offset.
                    PredictionDrawer.DrawPredictions(predictions, targetArea: null);
                    break;
                case OverlayDrawingMethod.OverlayFormGDI:
                    _useForm = true;
                    overlay.DrawPredictionCanvas(null);
                    // Form is already positioned on CaptureArea; draw in form-local coords (model Rectangle).
                    PredictionDrawer.DrawPredictions(EnsureFormOverlay(), predictions, targetArea: null, useScreenSpace: false);
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
        _playerOverlay ??= new DetectedPlayerWindow();
        if (_playerOverlay.Visibility != Visibility.Visible)
        {
            _playerOverlay.Show();
        }
        return _playerOverlay;
    }

    public override Task OnResume()
    {
        SetOverlayEnabled(true);
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