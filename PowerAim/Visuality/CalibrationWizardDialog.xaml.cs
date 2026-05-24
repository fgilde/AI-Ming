using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PowerAim.AILogic;
using PowerAim.Class.Native;
using PowerAim.Config;

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
                LeftButton.Content = "Cancel";
                RightButton.Content = "Start calibration";
                RightButton.IsEnabled = true;
                LeftButton.IsEnabled = true;
                break;
            case Step.Running:
                LeftButton.Content = "Cancel";
                RightButton.Content = "Running…";
                RightButton.IsEnabled = false;
                LeftButton.IsEnabled = true;
                break;
            case Step.Result:
                LeftButton.Content = "Close";
                RightButton.Content = "Apply suggested";
                RightButton.IsEnabled = _lastResult is { Ok: true };
                LeftButton.IsEnabled = true;
                break;
            case Step.Error:
                LeftButton.Content = "Close";
                RightButton.Content = "Try again";
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
            ShowError("No capture source available. Make sure a model is loaded and PowerAim is initialised.");
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
                ShowError("Cancelled.");
            }
            else if (!result.Ok)
            {
                ShowError(result.ErrorMessage ?? "Unknown error.");
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
            <= 0.5 => "Your in-game sensitivity is quite low (the screen barely moves per mouse unit). Consider raising it so PowerAim has more headroom to correct.",
            <= 1.05 => "1:1 sensitivity. No damping needed — keep MouseSensitivity at 0.",
            <= 1.6  => "Slightly high in-game sensitivity. A small amount of damping helps the AI settle on targets.",
            <= 3.0  => "High in-game sensitivity. The damping factor will keep the AI from overshooting on close-range corrections.",
            _       => "Very high in-game sensitivity. Heavy damping is required; you may also want to lower your in-game sens for finer control.",
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
