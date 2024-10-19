using System.Windows;
using System.Windows.Forms;
using Aimmy2.Class.Native;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.Other;
using Visuality;
using Application = System.Windows.Application;


namespace Aimmy2.AILogic.Actions;

public class OverlayAction : BaseAction
{
    private Form? _formOverlay = new Form();
    private DetectedPlayerWindow? _playerOverlay = new();
    private FOV? _fov = new();
    private bool _useForm = false;
    public OverlayAction()
    {
        EnsureFormOverlay();
    }

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        var formActive = _useForm;
        _useForm = false;
        if (Active)
        {
            switch (AppConfig.Current.DropdownState.OverlayDrawingMethod)
            {
                case OverlayDrawingMethod.WpfWindowCanvas:
                    _playerOverlay?.DrawPredictionCanvas(predictions.MinBy(p => p.Confidence));
                    break;
                case OverlayDrawingMethod.DrawingContextVisualHost:
                    _playerOverlay?.DrawPredictions(predictions, ImageCapture.CaptureArea.ToRect());
                    break;
                case OverlayDrawingMethod.DesktopDC:
                    _playerOverlay?.DrawPredictionCanvas(null);
                    PredictionDrawer.DrawPredictions(predictions, ImageCapture.CaptureArea);
                    break;
                case OverlayDrawingMethod.OverlayFormGDI:
                    _useForm = true;
                    _playerOverlay?.DrawPredictionCanvas(null);
                    PredictionDrawer.DrawPredictions(EnsureFormOverlay(), predictions, ImageCapture.CaptureArea);
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
        catch (Exception e)
        { }
    }
}