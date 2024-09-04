using System.Windows;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.Other;
using Class;
using Visuality;


namespace Aimmy2.AILogic.Actions;

public class OverlayAction : BaseAction
{
    private DetectedPlayerWindow? _playerOverlay = new();
    private FOV? _fov = new();

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        if (Active)
        {
            switch (AppConfig.Current.DropdownState.OverlayDrawingMethod)
            {
                case OverlayDrawingMethod.DesktopDC:
                    _playerOverlay?.DrawPredictionOverlay(null);
                    PredictionDrawer.DrawPredictions(predictions, ImageCapture.CaptureArea);
                    break;
                case OverlayDrawingMethod.WpfWindow:
                    DrawWithWpf(predictions);
                    break;
                //case OverlayDrawingMethod.OverlayWindowDC:
                //    PredictionDrawer.DrawPredictions(EnsurePlayerOverlay(), predictions);
                //    break;
            }
        }

        return Task.CompletedTask;
    }


    private DetectedPlayerWindow EnsurePlayerOverlay()
    {
        _playerOverlay ??= new DetectedPlayerWindow();
        if(_playerOverlay.Visibility != Visibility.Visible)
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
        if(_playerOverlay != null)
        {
            _playerOverlay.Dispatcher.Invoke(() =>
            {
                _playerOverlay.Close();
                _playerOverlay = null;
            });
        }

        if(_fov != null)
        {
            _fov.Dispatcher.Invoke(() =>
            {
                _fov.Close();
                _fov = null;
            });
        }

        base.Dispose();
    }

    protected override bool Active => base.Active && AppConfig.Current.ToggleState.ShowDetectedPlayer;

    private void DrawWithWpf(Prediction[] predictions)
    {
        var prediction = predictions.MinBy(p => p.Confidence);
        _playerOverlay?.DrawPredictionOverlay(prediction);
    }
    
    private void SetOverlayEnabled(bool enabled)
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if(_playerOverlay != null)
                    _playerOverlay.Opacity = enabled ? 1 : 0;
            });
        }
        catch (Exception e)
        {}
    }
}