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

    /// <summary>Seconds of "get ready" countdown before the wizard starts moving the mouse.</summary>
    private const int StartCountdownSeconds = 5;

    private Step _step = Step.Welcome;
    private CancellationTokenSource? _cts;
    private CalibrationResult? _lastResult;
    private double _suggested;
    private bool _restoredGlobalActive;

    /// <summary>
    ///     When set, "Apply suggested" writes the recommended sensitivity to this profile (so the
    ///     profile editor's slider updates live) instead of the global live setting.
    /// </summary>
    public AimProfile? TargetProfile { get; set; }

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
        if (capture is null)
        {
            ShowError(Locale.NoCaptureSourceLoaded);
            return;
        }

        // Suspend the aim/trigger loops while we move the mouse around.
        var toggle = AppConfig.Current?.ToggleState;
        bool wasActive = toggle?.GlobalActive == true;
        if (wasActive && toggle is not null)
        {
            toggle.GlobalActive = false;
            _restoredGlobalActive = true;
        }

        _step = Step.Running;
        UpdateChrome();

        _cts = new();
        try
        {
            // Give the user a few seconds to switch to the game and line up their aim before we
            // start moving the mouse for them. The countdown is shown in the running panel.
            if (!await CountdownAsync(StartCountdownSeconds,
                    s => RunningStatus.Text = string.Format(Locale.CalibrationCountdownFormat, s), _cts.Token))
            {
                ShowError(Locale.Cancelled);
                return;
            }
            RunningStatus.Text = Locale.CalibrationRunning;

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
        catch (OperationCanceledException)
        {
            ShowError(Locale.Cancelled);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            // Restore the AI pipeline state we suspended.
            if (_restoredGlobalActive && toggle is not null)
            {
                toggle.GlobalActive = true;
                _restoredGlobalActive = false;
            }
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    ///     Counts down <paramref name="seconds"/> seconds, calling <paramref name="show"/> once per
    ///     second with the remaining count so the UI can display it. Returns <c>false</c> if the
    ///     countdown was cancelled, <c>true</c> if it completed.
    /// </summary>
    private static async Task<bool> CountdownAsync(int seconds, Action<int> show, CancellationToken ct)
    {
        for (int s = seconds; s > 0; s--)
        {
            show(s);
            try { await Task.Delay(1000, ct); }
            catch (OperationCanceledException) { return false; }
        }
        return !ct.IsCancellationRequested;
    }

    private void ShowResult(CalibrationResult result)
    {
        double currentSens = TargetProfile?.Sensitivity ?? AppConfig.Current?.SliderSettings?.MouseSensitivity ?? 0;
        _suggested = ComputeSuggestedSensitivity(result.Ratio);

        RatioText.Text = $"{result.Ratio:0.000}  ({result.MeasuredPixels:0.0} px / {result.MoveAmount} units)";
        SamplesText.Text = result.SamplesUsed.ToString();
        CurrentSensText.Text = currentSens.ToString("0.00");
        SuggestedSensText.Text = _suggested.ToString("0.00");

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

    /// <summary>
    ///     Turn the measured pixels-per-input ratio into a sensitivity that makes the aim cover a
    ///     stable fraction (~half) of a target's offset per frame — accounting for the active path's
    ///     own scaling, which the raw <see cref="CalibrationResult.SuggestedSensitivity"/> ignored
    ///     (it assumed a 1:1 move scale, so it suggested far too little damping → near-zero values).
    /// </summary>
    private double ComputeSuggestedSensitivity(double ratio)
    {
        if (ratio <= 0) return 0;
        const double f = 0.5; // cover ~half the offset per frame: fast but won't overshoot

        double model = Math.Max(1, PredictionLogic.IMAGE_SIZE);
        var capture = AIManager.Instance?.ImageCapture;
        double areaW = capture?.CaptureArea.Width ?? model;
        double areaH = capture?.CaptureArea.Height ?? model;
        bool smart = TargetProfile?.SmartAim ?? AppConfig.Current?.AISettings?.SmartAimEnabled ?? true;

        double s;
        if (smart)
        {
            // Smart move(counts) = gain·(FOV/model)·offset[px]; view shift(px) = move·ratio.
            // gain·(FOV/model)·ratio = f  →  gain = f / ((FOV/model)·ratio)
            double fov = AppConfig.Current?.SliderSettings?.ActualFovSize ?? model;
            double captureSize = Math.Clamp(Math.Round(fov), 16.0, Math.Max(16.0, Math.Min(areaW, areaH)));
            double scaleSmart = captureSize / model;
            s = f / (scaleSmart * ratio);
        }
        else
        {
            // Legacy move(counts) = (1-s)·(screenW/model)·offset[px]. (1-s)·scale·ratio = f.
            double scaleLegacy = areaW > 0 ? areaW / model : 1.0;
            s = 1.0 - f / (scaleLegacy * ratio);
        }
        return Math.Clamp(s, 0.01, 1.0);
    }

    private void ApplySuggested()
    {
        if (_lastResult is not { Ok: true }) return;
        if (TargetProfile != null)
        {
            // Profile edit flow: write to the profile so the editor's bound slider updates.
            TargetProfile.Sensitivity = _suggested;
            return;
        }
        var settings = AppConfig.Current?.SliderSettings;
        if (settings is null) return;
        settings.MouseSensitivity = _suggested;
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
        if (_restoredGlobalActive && AppConfig.Current?.ToggleState is not null)
        {
            AppConfig.Current.ToggleState.GlobalActive = true;
            _restoredGlobalActive = false;
        }
        base.OnClosed(e);
    }
}
