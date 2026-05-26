using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PowerAim.AILogic;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim;

namespace PowerAim.Visuality;

/// <summary>
///     Step-based wizard that drives <see cref="SensitivityCalibrator"/> from the UI, presents the
///     result, and offers to apply the suggested <see cref="SliderSettings.MouseSensitivity"/>.
///     <para>
///     The wizard temporarily forces <c>GlobalActive = false</c> while it runs so the aim/trigger
///     pipelines don't fight the calibration impulses. The previous state is restored on close.
///     </para>
/// </summary>
public partial class CalibrationWizardDialog
{
    private enum Step { Welcome, Running, Result, Error }

    private Step _step = Step.Welcome;
    private CancellationTokenSource? _cts;
    private CalibrationResult? _lastResult;
    private bool _restoredGlobalActive;

    public CalibrationWizardDialog()
    {
        InitializeComponent();
        DataContext = this;
        UpdateChrome();
    }

    private void UpdateChrome()
    {
        WelcomePanel.Visibility = _step == Step.Welcome ? Visibility.Visible : Visibility.Collapsed;
        RunningPanel.Visibility = _step == Step.Running ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility  = _step == Step.Result  ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility   = _step == Step.Error   ? Visibility.Visible : Visibility.Collapsed;

        switch (_step)
        {
            case Step.Welcome:
                LeftButton.Content = Locale.Cancel;
                RightButton.Content = Locale.StartCalibration;
                RightButton.IsEnabled = true;
                LeftButton.IsEnabled = true;
                break;
            case Step.Running:
                LeftButton.Content = Locale.Cancel;
                RightButton.Content = Locale.Running;
                RightButton.IsEnabled = false;
                LeftButton.IsEnabled = true;
                break;
            case Step.Result:
                LeftButton.Content = Locale.Close;
                RightButton.Content = Locale.ApplySuggested;
                RightButton.IsEnabled = _lastResult is { Ok: true };
                LeftButton.IsEnabled = true;
                break;
            case Step.Error:
                LeftButton.Content = Locale.Close;
                RightButton.Content = Locale.TryAgain;
                RightButton.IsEnabled = true;
                LeftButton.IsEnabled = true;
                break;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.HideForCaptureIfEnabled();
    }

    private void LeftButton_Click(object sender, RoutedEventArgs e)
    {
        if (_step == Step.Running)
        {
            _cts?.Cancel();
        }
        else
        {
            Close();
        }
    }

    private async void RightButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case Step.Welcome:
            case Step.Error:
                await RunCalibration();
                break;
            case Step.Result:
                ApplySuggested();
                Close();
                break;
        }
    }

    private async Task RunCalibration()
    {
        var capture = AIManager.Instance?.ImageCapture;
        if (capture == null)
        {
            ShowError(Locale.NoCaptureSourceLoaded);
            return;
        }

        // Suspend the aim/trigger loops while we move the mouse around.
        var toggle = AppConfig.Current?.ToggleState;
        bool wasActive = toggle?.GlobalActive == true;
        if (wasActive && toggle != null)
        {
            toggle.GlobalActive = false;
            _restoredGlobalActive = true;
        }

        _step = Step.Running;
        UpdateChrome();

        _cts = new CancellationTokenSource();
        try
        {
            var result = await SensitivityCalibrator.CalibrateAsync(capture, moveAmount: 200, rounds: 6, _cts.Token);
            _lastResult = result;

            if (result.WasCancelled)
            {
                ShowError(Locale.Cancelled);
            }
            else if (!result.Ok)
            {
                ShowError(result.ErrorMessage ?? Locale.UnknownError);
            }
            else
            {
                ShowResult(result);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            // Restore the AI pipeline state we suspended.
            if (_restoredGlobalActive && toggle != null)
            {
                toggle.GlobalActive = true;
                _restoredGlobalActive = false;
            }
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void ShowResult(CalibrationResult result)
    {
        var settings = AppConfig.Current?.SliderSettings;
        double currentSens = settings?.MouseSensitivity ?? 0;

        RatioText.Text = $"{result.Ratio:0.000}  ({result.MeasuredPixels:0.0} px / {result.MoveAmount} units)";
        SamplesText.Text = result.SamplesUsed.ToString();
        CurrentSensText.Text = currentSens.ToString("0.00");
        SuggestedSensText.Text = result.SuggestedSensitivity.ToString("0.00");

        InterpretationText.Text = result.Ratio switch
        {
            <= 0.5 => Locale.CalibrationInterpretLow,
            <= 1.05 => Locale.CalibrationInterpret1to1,
            <= 1.6  => Locale.CalibrationInterpretSlightHigh,
            <= 3.0  => Locale.CalibrationInterpretHigh,
            _       => Locale.CalibrationInterpretVeryHigh,
        };

        _step = Step.Result;
        UpdateChrome();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        _step = Step.Error;
        UpdateChrome();
    }

    private void ApplySuggested()
    {
        var settings = AppConfig.Current?.SliderSettings;
        if (settings == null || _lastResult == null || !_lastResult.Ok) return;
        settings.MouseSensitivity = _lastResult.SuggestedSensitivity;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        if (_restoredGlobalActive && AppConfig.Current?.ToggleState != null)
        {
            AppConfig.Current.ToggleState.GlobalActive = true;
            _restoredGlobalActive = false;
        }
        base.OnClosed(e);
    }
}
