using System.Drawing;
using PowerAim.AILogic.Aiming;
using PowerAim.AILogic.Contracts;
using PowerAim.Config;
using PowerAim.InputLogic; // InputSender (Move/MoveCrosshair) lives here after the MoveInputManager fold-in

namespace PowerAim.AILogic.Actions;

public class AimingAction : BaseAction
{
    private int PrevX = 0;
    private int PrevY = 0;
    private int detectedX { get; set; }
    private int detectedY { get; set; }

    private readonly KalmanPrediction kalmanPrediction = new();
    private readonly WiseTheFoxPrediction wtfpredictionManager = new();

    // Random aim-point state. _aimFracX/Y are the chosen position inside the aim region as a
    // 0..1 fraction; 0.5/0.5 = region centre. Re-rolled once per aim session, never per frame —
    // per-frame randomisation would fight the smoother and shake the crosshair.
    private readonly Random _rng = new();
    private double _aimFracX = 0.5;
    private double _aimFracY = 0.5;
    private bool _wasAiming = false;

    // Legacy single-target sticky selector — used only when SmartAimEnabled is off.
    private readonly StickyAimSelector _stickyAim = new();

    // ---- Smart-aim pipeline (SmartAimEnabled): detect → track → select → smooth → control ----
    private readonly TargetTracker _tracker = new();
    private readonly TargetSelector _selector = new();
    private readonly OneEuroFilter2D _oneEuro = new();
    private readonly AimController _controller = new();
    private DateTime _lastTickUtc = DateTime.MinValue;

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        // Frame delta for all time-aware stages (tracker velocity, 1€ filter, damped control).
        var now = DateTime.UtcNow;
        double dt = _lastTickUtc == DateTime.MinValue ? 1.0 / 60.0 : (now - _lastTickUtc).TotalSeconds;
        dt = Math.Clamp(dt, 0.001, 0.1);
        _lastTickUtc = now;

        if (AppConfig.Current.AISettings.SmartAimEnabled)
        {
            ExecuteSmart(predictions, dt);
            return Task.CompletedTask;
        }

        // ---- Legacy path ----
        var fallback = predictions.MinBy(p => p.Confidence);
        var ai = AppConfig.Current.AISettings;
        var selected = _stickyAim.SelectTarget(
            predictions, fallback, ai.StickyAimEnabled, ai.StickyAimThreshold, ai.StickyAimMaxLockScore);
        if (selected != null) HandleAim(selected);
        return Task.CompletedTask;
    }

    public override Task OnPause()
    {
        // Don't carry a stale lock across pause/resume — the player might re-aim somewhere else
        // before turning the assist back on.
        _stickyAim.Reset();
        _tracker.Reset();
        _selector.Reset();
        _oneEuro.Reset();
        _controller.Reset();
        _wasAiming = false;
        _lastTickUtc = DateTime.MinValue;
        return base.OnPause();
    }

    /// <summary>True when the user actually wants the aim to move right now (toggle + key + not disengaged).</summary>
    private bool IsAiming() =>
        AppConfig.Current.ToggleState.AimAssist
        && (!HasValidKey(AppConfig.Current.BindingSettings.AimKeyBindings)
            || AnyKeyIsHold(AppConfig.Current.BindingSettings.AimKeyBindings))
        && !AimDisengage.ShouldPause();

    // ============================================================================ SMART PATH ====

    private void ExecuteSmart(Prediction[] predictions, double dt)
    {
        var ai = AppConfig.Current.AISettings;

        // Keep the tracker tunables in sync with config (cheap; lets slider edits apply live).
        _tracker.MaxAgeFrames = ai.TrackMaxAgeFrames;
        _tracker.MinHits = ai.TrackMinHits;
        _tracker.IoUThreshold = ai.TrackIoUThreshold;
        _tracker.Alpha = ai.TrackAlpha;
        _tracker.Beta = ai.TrackBeta;

        // Always step the tracker — even when not pressing the aim key — so identities stay warm
        // and the moment the user engages we're already locked, not acquiring from scratch.
        var tracks = _tracker.Update(predictions, dt);

        if (!IsAiming())
        {
            _wasAiming = false;
            _controller.Reset();
            _oneEuro.Reset();
            return;
        }

        float modelSize = Math.Max(1, global::PowerAim.AILogic.PredictionLogic.IMAGE_SIZE);
        double center = modelSize / 2.0; // crosshair in model space

        var target = _selector.Select(tracks, center, center, ai.SwitchMarginPct, ai.SwitchFrames);
        if (target == null)
        {
            _wasAiming = false;
            _controller.Reset();
            _oneEuro.Reset();
            return;
        }

        // New aim session → re-roll the random region point (if enabled) once.
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
            // Seed the smoother at the target so the first frame doesn't lurch from a stale value.
            _oneEuro.Reset();
        }
        _wasAiming = true;

        // Aim point inside the box (model space) using the configured aim region. Prefer the raw
        // latest detection — identical to the proven legacy aim point — and fall back to the
        // velocity-predicted estimate ONLY while the track is coasting through a detection gap.
        // Aiming at the predicted/smoothed centre while a detection is present biased the closed aim
        // loop: the tracker reads the assist's own view-pan as target velocity, so the estimate (and
        // thus the crosshair) drifted consistently in the pan direction — "aims away from the target".
        var aimBox = target.MissedFrames == 0 ? target.LastDetectionBox : target.Box;
        var (px, py) = RegionPoint(aimBox);

        // Optional velocity lead to compensate input+render latency.
        double leadSec = ai.LeadTimeMs / 1000.0;
        if (leadSec > 0)
        {
            px += target.Vx * leadSec;
            py += target.Vy * leadSec;
        }

        // Adaptive jitter removal on the aim point.
        if (ai.UseOneEuro)
        {
            _oneEuro.Configure(ai.OneEuroMinCutoff, ai.OneEuroBeta);
            var f = _oneEuro.Filter(px, py, dt);
            px = f.X;
            py = f.Y;
        }

        // Model space (px,py ∈ [0,modelSize]) → capture-box pixels. The model only ever sees the FOV
        // patch (a SQUARE box, captureSize×captureSize, centred on the crosshair) — NOT the whole
        // screen. So the scale is FOV/model. Using screenWidth/model here over-scaled every move by
        // screenWidth/FOV (≈3× on 1080p with a 640 FOV); combined with the controller's gain==
        // sensitivity that pushed the effective per-frame gain above 1 and the aim diverged/ran away
        // at higher sensitivities. captureSize mirrors AIManager's capture box so the units match.
        var area = ImageCapture.CaptureArea;
        double maxCap = Math.Min(area.Width, area.Height);
        double captureSize = Math.Clamp(Math.Round(AppConfig.Current.SliderSettings.ActualFovSize), 16.0, maxCap);
        double scale = captureSize / modelSize;
        double targetX = px * scale;
        double targetY = py * scale;
        // Square reference box → crosshair sits at its centre and there's no X/Y aspect skew.
        var aimArea = new Rectangle(0, 0, (int)captureSize, (int)captureSize);

        // Feed the debug input visualiser the coarse aim direction (mouse path only).
        if (!InputSender.GamepadAimActive)
            InputEventBus.MouseMove(targetX - captureSize / 2.0, targetY - captureSize / 2.0);

        // Damped, frame-rate-independent move. Sensitivity is the per-60Hz-frame approach fraction
        // (higher = snappier). No per-frame jitter here — that's the shake we're removing; the
        // optional random aim point already provides per-engagement variation.
        _controller.MoveTo(targetX, targetY, aimArea, dt,
            AppConfig.Current.SliderSettings.MouseSensitivity, ai.AimDeadzonePx, 150, 0);
    }

    /// <summary>Compute the aim point (model space) inside <paramref name="box"/> from the configured aim region + frac.</summary>
    private (double X, double Y) RegionPoint(RectangleF box)
    {
        var region = AppConfig.Current.SliderSettings.AimRegion;
        double subX = box.X + box.Width * region.LeftMarginPercentage;
        double subY = box.Y + box.Height * region.TopMarginPercentage;
        double subW = box.Width * region.WidthPercentage;
        double subH = box.Height * region.HeightPercentage;
        return (subX + subW * _aimFracX, subY + subH * _aimFracY);
    }

    // ============================================================================ LEGACY PATH ====

    private void CalculateCoordinates(Prediction closestPrediction, float scaleX, float scaleY)
    {
        var (px, py) = RegionPoint(closestPrediction.Rectangle);
        detectedX = (int)(px * scaleX);
        detectedY = (int)(py * scaleY);
    }

    private void HandleAim(Prediction closestPrediction)
    {
        if (!IsAiming())
        {
            _wasAiming = false;
            return;
        }

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
