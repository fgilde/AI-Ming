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

    // Random aim-point state. _aimFracX/Y are the chosen position inside the aim region as a
    // 0..1 fraction; 0.5/0.5 = region centre. Re-rolled once per aim session (see HandleAim),
    // never per frame — per-frame randomisation would fight EMA smoothing and shake the crosshair.
    private readonly Random _rng = new();
    private double _aimFracX = 0.5;
    private double _aimFracY = 0.5;
    private bool _wasAiming = false;

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
        _wasAiming = false;
        return base.OnPause();
    }

    private void CalculateCoordinates(Prediction closestPrediction, float scaleX, float scaleY)
    {
        // Take the aim point from the visual aim region (the same "head area" sub-rectangle model
        // the triggers use). The chosen point inside the region is _aimFracX/_aimFracY: 0.5/0.5 =
        // centre, or a random per-session point when RandomAimPoint is on (see HandleAim).
        var region = AppConfig.Current.SliderSettings.AimRegion;
        var r = closestPrediction.Rectangle;
        float subX = r.X + r.Width * region.LeftMarginPercentage;
        float subY = r.Y + r.Height * region.TopMarginPercentage;
        float subW = r.Width * region.WidthPercentage;
        float subH = r.Height * region.HeightPercentage;
        detectedX = (int)((subX + subW * (float)_aimFracX) * scaleX);
        detectedY = (int)((subY + subH * (float)_aimFracY) * scaleY);
    }

    private void HandleAim(Prediction closestPrediction)
    {
        bool isAiming = AppConfig.Current.ToggleState.AimAssist
            && (!HasValidKey(AppConfig.Current.BindingSettings.AimKeyBindings)
                || AnyKeyIsHold(AppConfig.Current.BindingSettings.AimKeyBindings))
            && !AimDisengage.ShouldPause();

        if (!isAiming)
        {
            _wasAiming = false;
            return;
        }

        // New aim session (key freshly held / re-engaged): re-roll the random aim point so each
        // flick lands on a slightly different spot in the region. Held continuously → stable point.
        if (!_wasAiming)
        {
            if (AppConfig.Current.ToggleState.RandomAimPoint)
            {
                _aimFracX = _rng.NextDouble();
                _aimFracY = _rng.NextDouble();
            }
            else
            {
                _aimFracX = 0.5;
                _aimFracY = 0.5;
            }
        }
        _wasAiming = true;

        {
            var area = ImageCapture.CaptureArea;
            // Map model-space detection coords (0..imageSize) to screen pixels. Use the ACTUAL model
            // input size — hardcoding 640 mis-scaled the mouse move whenever a dynamic-shape model
            // ran at a non-640 ImageSize (detection/overlay were already correct, only the aim move
            // was off). Identical to the old behaviour for the default fixed-640 model.
            float modelSize = Math.Max(1, global::PowerAim.AILogic.PredictionLogic.IMAGE_SIZE);
            float scaleX = area.Width / modelSize;
            float scaleY = area.Height / modelSize;

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
