using System.Drawing;
using PowerAim.AILogic.Contracts;
using PowerAim.Config;
using Class;
using InputLogic;
using PowerAim.InputLogic; // InputSender (Move/MoveCrosshair) lives here after the MoveInputManager fold-in

namespace PowerAim.AILogic.Actions;

public class AimingAction : BaseAction
{
    private int PrevX = 0;
    private int PrevY = 0;
    private int detectedX { get; set; }
    private int detectedY { get; set; }

    private KalmanPrediction kalmanPrediction = new();
    private WiseTheFoxPrediction wtfpredictionManager = new();

    // Sticky-aim state. Lives on the action instance (i.e. across frames) so the selector can
    // accumulate lock-score and velocity. The trigger / overlay actions intentionally do not share
    // this — they consume the unfiltered prediction set so the user still sees every detection.
    private readonly StickyAimSelector _stickyAim = new();

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        // Existing behaviour: pick the entry returned by MinBy(Confidence) (preserved for the
        // sticky-disabled fallback so we don't silently change targeting semantics).
        var fallback = predictions.MinBy(p => p.Confidence);

        var ai = AppConfig.Current.AISettings;
        var selected = _stickyAim.SelectTarget(
            predictions,
            fallback,
            ai.StickyAimEnabled,
            ai.StickyAimThreshold,
            ai.StickyAimMaxLockScore);

        if (selected != null)
        {
            HandleAim(selected);
        }
        return Task.CompletedTask;
    }

    public override Task OnPause()
    {
        // Don't carry a stale lock across pause/resume — the player might re-aim somewhere else
        // before turning the assist back on.
        _stickyAim.Reset();
        return base.OnPause();
    }

    private void CalculateCoordinates(Prediction closestPrediction, float scaleX, float scaleY)
    {
        double YOffset = AppConfig.Current.SliderSettings.YOffset;
        double XOffset = AppConfig.Current.SliderSettings.XOffset;

        double YOffsetPercentage = AppConfig.Current.SliderSettings.YOffsetPercentage;
        double XOffsetPercentage = AppConfig.Current.SliderSettings.XOffsetPercentage;

        var rect = closestPrediction.Rectangle;

        if (AppConfig.Current.ToggleState.XAxisPercentageAdjustment)
        {
            detectedX = (int)((rect.X + (rect.Width * (XOffsetPercentage / 100))) * scaleX);
        }
        else
        {
            detectedX = (int)((rect.X + rect.Width / 2) * scaleX + XOffset);
        }

        if (AppConfig.Current.ToggleState.YAxisPercentageAdjustment)
        {
            detectedY = (int)((rect.Y + rect.Height - (rect.Height * (YOffsetPercentage / 100))) * scaleY + YOffset);
        }
        else
        {
            detectedY = CalculateDetectedY(scaleY, YOffset, closestPrediction);
        }
    }

    private static int CalculateDetectedY(float scaleY, double YOffset, Prediction closestPrediction)
    {
        var rect = closestPrediction.Rectangle;
        float yBase = rect.Y;
        float yAdjustment = 0;

        switch (AppConfig.Current.DropdownState.AimingBoundariesAlignment)
        {
            case AimingBoundariesAlignment.Center:
                yAdjustment = rect.Height / 2;
                break;

            case AimingBoundariesAlignment.Top:
                // yBase is already at the top
                break;

            case AimingBoundariesAlignment.Bottom:
                yAdjustment = rect.Height;
                break;
        }

        return (int)((yBase + yAdjustment) * scaleY + YOffset);
    }

    private void HandleAim(Prediction closestPrediction)
    {
        if (AppConfig.Current.ToggleState.AimAssist
            && (!HasValidKey(AppConfig.Current.BindingSettings.AimKeyBindings)
                || AnyKeyIsHold(AppConfig.Current.BindingSettings.AimKeyBindings))
            && !AimDisengage.ShouldPause())
        {
            var area = ImageCapture.CaptureArea;
            float scaleX = area.Width / 640f;
            float scaleY = area.Height / 640f;

            CalculateCoordinates(closestPrediction, scaleX, scaleY);
            if (AppConfig.Current.ToggleState.Predictions)
            {
                HandlePredictions(kalmanPrediction, closestPrediction, area);
            }
            else
            {
                InputSender.MoveCrosshair(detectedX, detectedY, area);
            }
        }
    }

    private void HandlePredictions(KalmanPrediction kalmanPrediction, Prediction closestPrediction, Rectangle area )
    {
        var predictionMethod = AppConfig.Current.DropdownState.PredictionMethod;
        switch (predictionMethod)
        {
            case PredictionMethod.KalmanFilter:
                KalmanPrediction.Detection detection = new()
                {
                    X = detectedX,
                    Y = detectedY,
                    Timestamp = DateTime.UtcNow
                };

                kalmanPrediction.UpdateKalmanFilter(detection);
                var predictedPosition = kalmanPrediction.GetKalmanPosition();

                InputSender.MoveCrosshair(predictedPosition.X, predictedPosition.Y, area);
                break;

            case PredictionMethod.Shall0:
                ShalloePredictionV2.UpdatePosition(detectedX, detectedY);
                InputSender.MoveCrosshair(ShalloePredictionV2.GetSPX(), ShalloePredictionV2.GetSPY(), area);

                PrevX = detectedX;
                PrevY = detectedY;
                break;

            case PredictionMethod.WiseThef0x:
                WiseTheFoxPrediction.WTFDetection wtfdetection = new()
                {
                    X = detectedX,
                    Y = detectedY,
                    Timestamp = DateTime.UtcNow
                };

                wtfpredictionManager.UpdateDetection(wtfdetection);
                var wtfpredictedPosition = wtfpredictionManager.GetEstimatedPosition();

                InputSender.MoveCrosshair(wtfpredictedPosition.X, wtfpredictedPosition.Y, area);
                break;
        }
    }
}
