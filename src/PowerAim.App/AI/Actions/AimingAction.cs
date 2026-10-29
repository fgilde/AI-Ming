using System.Drawing;
using PowerAim.Class;
using PowerAim.AI.Aiming;
using PowerAim.AI.Rules;
using PowerAim.AI.Contracts;
using PowerAim.Config;
using Nextended.UI.WPF.Extensions;
using PowerAim.Input; // InputSender (Move/MoveCrosshair) lives here after the MoveInputManager fold-in

namespace PowerAim.AI.Actions;

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

    // ---- Smart aim (SmartAimEnabled): aim at a detection, move proportionally toward it ----
    private readonly AimController _controller = new();
    private readonly AimControllerV2 _controllerV2 = new();
    private readonly ReactionDelay _reaction = new();
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>False while the profile's reaction time after a new target is still running.</summary>
    private bool ReactionReady(bool hasTarget)
        => _reaction.Ready(hasTarget, _clock.Elapsed.TotalMilliseconds, _effectiveProfile?.ReactionDelayMs ?? 0);
    private DateTime _lastTickUtc = DateTime.MinValue;
    // Sticky-nearest target (model space) — keeps the aim on the same enemy across frames instead of
    // snapping to whoever happens to be nearest the crosshair. Used when target tracking is OFF.
    private double _lastTargetX, _lastTargetY;
    private bool _hasLastTarget;
    // Opt-in target tracking (UseTargetTracking): stable identity + switch hysteresis. The aim point
    // is always taken from the track's RAW last detection, never its velocity-extrapolated box, so the
    // assist's own view-pan can't bias the crosshair (no ego-motion coupling).
    private readonly TargetTracker _tracker = new();
    private readonly TargetSelector _selector = new();
    // Aim-point smoothing. EMA state (SmoothingMode.Ema) + the adaptive 1€ filter (SmoothingMode.OneEuro).
    private double _emaX, _emaY;
    private bool _hasEma;
    private readonly OneEuroFilter2D _oneEuro = new();
    private readonly PidAimSmoother _pid = new();
    private float _lastTargetConf = 1f;
    private RectangleF _lastAimBox;
    // Last-seen mode flags — detect a mid-session mode flip so we never resume a smoother/tracker on
    // stale state (a frozen track or a stale EMA/1€ anchor). Seeded to the config defaults.
    private bool _lastUseTracking;
    private AimSmoothingMode _lastSmoothingMode = AimSmoothingMode.OneEuro;

    // The profile driving the aim THIS frame — the enabled profile whose own aim-key is held (+ OCR
    // condition met); null when nothing is engaged. Set each frame at the top of ExecuteAsync.
    private AimProfile? _effectiveProfile;

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        // Frame delta for all time-aware stages (tracker velocity, 1€ filter, damped control).
        var now = DateTime.UtcNow;
        double dt = _lastTickUtc == DateTime.MinValue ? 1.0 / 60.0 : (now - _lastTickUtc).TotalSeconds;
        dt = Math.Clamp(dt, 0.001, 0.1);
        _lastTickUtc = now;

        // Resolve which profile drives the aim this frame (a held per-profile aim-key overrides the
        // selected profile — head on one key, chest on another). On a change the manager applies the
        // effective profile's feel into the live globals (marshalled to the UI thread); the pipeline
        // below keeps reading those globals unchanged. With no per-profile keys this is the selected
        // ActiveProfile every frame = today's behaviour.
        var aimSettings = AppConfig.Current.AimSettings;
        if (aimSettings != null)
        {
            _effectiveProfile = aimSettings.ResolveEffectiveProfile();
            AimProfileManager.Instance.UpdateEffective(_effectiveProfile);
        }

        // Per-class rules of the engaged profile: ignored classes, class confidence floors, priority.
        predictions = ClassTargeting.Filter(predictions, _effectiveProfile?.ClassSpecs);

        if (AppConfig.Current.AISettings.SmartAimEnabled)
        {
            ExecuteSmart(predictions, dt);
            return Task.CompletedTask;
        }

        // ---- Legacy path ----
        var rule = TargetRule();
        var fallback = rule != null ? BestByRule(rule, predictions) : predictions.MaxBy(p => p.Confidence);
        var ai = AppConfig.Current.AISettings;
        var selected = _stickyAim.SelectTarget(
            predictions, fallback, ai.StickyAimEnabled, ai.StickyAimThreshold, ai.StickyAimMaxLockScore,
            acquireFallback: rule != null);
        if (selected != null) HandleAim(selected);
        else ReactionReady(false);
        if (selected == null) SetStatus(!IsAiming() ? AimStatus.Idle : predictions.Length > 0 ? AimStatus.NoTarget : AimStatus.NoDetections);
        return Task.CompletedTask;
    }

    private static void SetStatus(AimStatus status) => PowerAim.Class.SessionStats.Instance.AimStatus = status;

    /// <summary>For the overlay's aim-point marker: the aim point and where it lands after the moves in flight.</summary>
    private void PublishAimPoint(double modelX, double modelY, Prediction[] predictions, AimEngine engine)
    {
        if (AppConfig.Current.ToggleState.ShowPredictedAim != true || predictions.Length == 0) return;
        var aim = AimOverlay.ToScreen(modelX, modelY, predictions[0]);
        var (ix, iy) = engine == AimEngine.V2 ? _controllerV2.LastInFlightPx : _controller.LastInFlightPx;
        AimOverlay.Publish(aim, new PointF((float)(aim.X - ix), (float)(aim.Y - iy)), Environment.TickCount64);
    }

    public override Task OnPause()
    {
        SetStatus(AimStatus.Idle);
        // Don't carry a stale lock across pause/resume — the player might re-aim somewhere else
        // before turning the assist back on.
        _stickyAim.Reset();
        _tracker.Reset();
        _selector.Reset();
        _oneEuro.Reset();
        _pid.Reset();
        _controller.Reset();
        _controllerV2.Reset();
        _wasAiming = false;
        _hasLastTarget = false;
        _hasEma = false;
        _lastTickUtc = DateTime.MinValue;
        // Paused → nothing is engaged. Clear the effective profile so the badge clears and the next
        // resolve after resume re-applies cleanly (the resolver stops running while paused).
        _effectiveProfile = null;
        AimProfileManager.Instance.UpdateEffective(null);
        return base.OnPause();
    }

    /// <summary>True when the user actually wants the aim to move right now (toggle + key + not disengaged).</summary>
    private bool IsAiming()
    {
        if (!AppConfig.Current.ToggleState.AimAssist) return false;
        // A profile aims only while its OWN aim-key is held — the resolver put that profile in
        // _effectiveProfile (null = no key held → not aiming). No global aim-key gate any more.
        if (_effectiveProfile == null) return false;
        // Disengage rules belong to the effective profile (the one actually aiming).
        return !AimDisengage.ShouldPause(_effectiveProfile);
    }

    // ============================================================================ SMART PATH ====

    private void ExecuteSmart(Prediction[] predictions, double dt)
    {
        var ai = AppConfig.Current.AISettings;

        float modelSize = Math.Max(1, global::PowerAim.AI.PredictionLogic.IMAGE_SIZE);
        double center = modelSize / 2.0; // crosshair in model space

        // Resets everything that must not carry across a disengage. The tracker/selector are kept warm
        // on purpose (so identities survive a brief aim-key release within a session); OnPause does the
        // full reset when the assist is actually paused.
        void Disengage()
        {
            _wasAiming = false;
            _hasLastTarget = false;
            _hasEma = false;
            _oneEuro.Reset();
        _pid.Reset();
            _controller.Reset();
            _controllerV2.Reset();
            _reaction.Reset();
        }

        // Mid-session mode flips must not resume a smoother/tracker on stale state: re-seed the
        // smoother when the smoothing mode changes, and drop tracker/selector state when tracking is
        // toggled (so re-enabling re-acquires cleanly instead of resuming frozen, aged-out tracks).
        if (ai.SmoothingMode != _lastSmoothingMode)
        {
            _oneEuro.Reset();
        _pid.Reset();
            _hasEma = false;
            _lastSmoothingMode = ai.SmoothingMode;
        }
        if (ai.UseTargetTracking != _lastUseTracking)
        {
            _tracker.Reset();
            _selector.Reset();
            _lastUseTracking = ai.UseTargetTracking;
        }

        // Opt-in target tracking. Step it every frame (even when not pressing aim) so identities stay
        // warm and we're locked the instant the user engages. Association is position/IoU-based and
        // ego-motion-immune; we only ever aim at a track's RAW last detection (never the velocity-
        // extrapolated box), so the assist's own view-pan can't bias the crosshair.
        IReadOnlyList<TargetTrack>? tracks = null;
        if (ai.UseTargetTracking)
        {
            _tracker.MaxAgeFrames = ai.TrackMaxAgeFrames;
            _tracker.MinHits = ai.MinTrackedFrames;
            _tracker.ConfidenceDecayMs = ai.TrackConfidenceDecayMs;
            tracks = _tracker.Update(predictions, dt);
        }

        if (!IsAiming())
        {
            SetStatus(AimStatus.Idle);
            Disengage();
            return;
        }

        // ---- Choose the box to aim at ----
        // Sticky radius: configured in FOV/screen pixels, converted to model space for this frame's
        // selection geometry (detections/tracks live in model space). At FOV == model input it's 1:1.
        var capArea = ImageCapture.CaptureArea;
        double capMax = Math.Max(16.0, Math.Min(capArea.Width, capArea.Height));
        double capSize = Math.Clamp(Math.Round(AppConfig.Current.SliderSettings.ActualFovSize), 16.0, capMax);
        double stickyModelPx = Math.Max(8.0, ai.StickyRadiusPx * (modelSize / capSize));

        RectangleF box;
        int aimClass;
        Keypoint[]? aimKeypoints;
        if (ai.UseTargetTracking)
        {
            var rule = TargetRule();
            var ctx = rule == null ? null : RuleContextFor(predictions.Length);
            var track = _selector.Select(tracks!, center, center, ai.SwitchMarginPct, ai.SwitchFrames, stickyModelPx,
                rule == null ? null : t => rule.Score(new TargetScope(RuleTarget.From(t), ctx!)));
            if (track == null) { SetStatus(predictions.Length > 0 ? AimStatus.NoTarget : AimStatus.NoDetections); Disengage(); return; }
            // RAW detection — never a velocity-extrapolated box: in this closed loop the track's velocity
            // includes our own view movement (no ego-motion compensation), so a lead would overshoot.
            box = track.LastDetectionBox;
            aimClass = track.ClassId;
            aimKeypoints = track.LastKeypoints;
            _lastTargetConf = track.Confidence;
            _lastAimBox = box;
        }
        else
        {
            var target = SelectNearestSticky(predictions, center, center, stickyModelPx);
            if (target == null) { SetStatus(predictions.Length > 0 ? AimStatus.NoTarget : AimStatus.NoDetections); Disengage(); return; }
            box = target.Rectangle;
            aimClass = target.ClassId;
            aimKeypoints = target.Keypoints;
            _lastTargetConf = target.Confidence;
            _lastAimBox = box;
        }
        _lastTargetX = box.X + box.Width / 2.0;
        _lastTargetY = box.Y + box.Height / 2.0;
        _hasLastTarget = true;

        // New aim session → re-roll the random region point (if enabled) once + reset the smoothers.
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
            _hasEma = false;
            _oneEuro.Reset();
        _pid.Reset();
        }
        _wasAiming = true;

        // Aim point = the configured region (head area) inside the chosen detection — where the target
        // actually IS this frame — or the class anchor / pose keypoint the profile asks for.
        var (px, py) = AimPointFor(box, aimClass, aimKeypoints);

        // Optional aim-point smoothing. All modes act on POSITION only (never extrapolate), so none
        // have closed-loop ego-motion coupling. The proportional move below is itself a low-pass too.
        switch (ai.SmoothingMode)
        {
            case AimSmoothingMode.Ema:
                {
                    const double a = 0.5;
                    if (!_hasEma) { _emaX = px; _emaY = py; _hasEma = true; }
                    else { _emaX = a * px + (1 - a) * _emaX; _emaY = a * py + (1 - a) * _emaY; }
                    px = _emaX;
                    py = _emaY;
                    break;
                }
            case AimSmoothingMode.OneEuro:
                {
                    _oneEuro.Configure(ai.OneEuroMinCutoff, ai.OneEuroBeta);
                    (px, py) = _oneEuro.Filter(px, py, dt);
                    break;
                }
            case AimSmoothingMode.Pid:
                {
                    _pid.Configure(ai.PidKp, ai.PidKi, ai.PidKd);
                    _pid.Adaptive = true;
                    double fovUnits = Math.Max(16.0, modelSize);
                    var ab = _lastAimBox.Width > 1 ? _lastAimBox : box;
                    (px, py) = _pid.Filter(px, py, dt, _lastTargetConf, ab.Width, ab.Height, fovUnits);
                    break;
                }
                // AimSmoothingMode.None → aim at the raw point.
        }

        // Model space (0..modelSize) → capture-box (FOV) pixels. The model only sees the square FOV
        // patch centred on the crosshair, so the scale is FOV/model (resolution-independent).
        var area = ImageCapture.CaptureArea;
        double maxCap = Math.Min(area.Width, area.Height);
        double captureSize = Math.Clamp(Math.Round(AppConfig.Current.SliderSettings.ActualFovSize), 16.0, maxCap);
        double scale = captureSize / modelSize;
        double targetX = px * scale;
        double targetY = py * scale;
        var aimArea = new Rectangle(0, 0, (int)captureSize, (int)captureSize);
        PublishAimPoint(px, py, predictions, ai.AimEngine);
        if (!ReactionReady(true)) { SetStatus(AimStatus.Pulling); return; }
        double offX = targetX - captureSize / 2.0, offY = targetY - captureSize / 2.0;
        SetStatus(Math.Sqrt(offX * offX + offY * offY) <= Math.Max(1.0, ai.AimDeadzonePx) ? AimStatus.OnTarget : AimStatus.Pulling);

        // Feed the debug input visualiser the coarse aim direction (mouse path only).
        if (!InputSender.GamepadAimActive)
            InputEventBus.MouseMove(targetX - captureSize / 2.0, targetY - captureSize / 2.0);

        // Timing jitter: add ±Gaussian noise to the inter-frame aim interval so the
        // periodicity signature is broken for behavioral analysis.
        int jitterMs = HumanTiming.JitterMs();
        if (jitterMs > 0)
            System.Threading.Thread.Sleep(jitterMs);

        if (ai.AimEngine == AimEngine.V2)
        {
            // Model-based engine: needs the capture-to-output latency (50 ms until configured — it learns
            // the rest) and the aim region's width for its settle zone. Strength/deadzone don't apply.
            var region = AppConfig.Current.SliderSettings.AimRegion;
            double regionWidthPx = box.Width * Math.Max(0.05, region.WidthPercentage) * scale;
            // With the gamepad method the stick sets the turn SPEED: V2 plans deflection from the game's turn speed.
            var stick = InputSender.GamepadAimActive && AppConfig.Current.ControllerSettings is { } pad
                ? new StickModel(pad.StickTurnSpeedPxPerSec / 1000.0, InputSender.CurrentStickOptions, InputSender.SetAimStick)
                : null;
            _controllerV2.Step(targetX - captureSize / 2.0, targetY - captureSize / 2.0, regionWidthPx,
                ai.CalibratedPixelsPerCount, ai.LatencyCompensationMs > 0 ? ai.LatencyCompensationMs : 50, stick);
            return;
        }

        // Proportional move toward the target. Strength = approach fraction per frame; the calibration
        // ratio (if set) converts the pixel error to exact mouse counts → game-independent feel.
        _controller.MoveTo(targetX, targetY, aimArea, dt,
            AppConfig.Current.SliderSettings.MouseSensitivity, ai.AimDeadzonePx, 150, ai.CalibratedPixelsPerCount,
            ai.AimSpeedMultiplier, ai.LatencyCompensationMs);
    }

    /// <summary>
    ///     Pick the detection to aim at: stick to the one nearest last frame's target (so the aim
    ///     stays on the same enemy), otherwise the one nearest the crosshair. If the held target drifts
    ///     beyond <paramref name="stickRadius"/> (model-space px, from the configurable sticky radius —
    ///     issue #19; used to be hard-coded to model-size·0.25), it's gone — fall back to
    ///     nearest-the-crosshair. No tracker, no extrapolation.
    /// </summary>
    private Prediction? SelectNearestSticky(Prediction[] predictions, double centerX, double centerY, double stickRadius)
    {
        if (predictions.Length == 0) return null;

        // Nothing held yet: an aim profile's target rule picks; otherwise the one nearest the crosshair.
        var rule = TargetRule();
        if (!_hasLastTarget)
            return rule != null ? BestByRule(rule, predictions) : Nearest(predictions, centerX, centerY).Best;

        var (best, bestSq) = Nearest(predictions, _lastTargetX, _lastTargetY);

        // If we were sticking but the closest detection is no longer near the old target, the enemy is
        // gone/occluded — re-acquire instead of chasing a ghost.
        if (Math.Sqrt(bestSq) > stickRadius)
            best = rule != null ? BestByRule(rule, predictions) : Nearest(predictions, centerX, centerY).Best;
        return best;
    }

    /// <summary>The effective aim profile's target rule ("which enemy first"), when set and valid.</summary>
    private RuleExpression? TargetRule()
        => _effectiveProfile is { UseTargetRule: true } p ? TriggerRules.Compile(p.TargetRule).Rule : null;

    /// <summary>Rule signals for aim: crosshair at the model centre, the profile's aim region as "head".</summary>
    private RuleContext RuleContextFor(int enemyCount)
    {
        int size = Math.Max(1, global::PowerAim.AI.PredictionLogic.IMAGE_SIZE);
        return new RuleContext(size, new PointF(size / 2f, size / 2f), AppConfig.Current.SliderSettings.AimRegion,
            LatencySec: 0.05, SpreadPx: 4, enemyCount, AutoTriggerAction.OcrNumber);
    }

    private Prediction? BestByRule(RuleExpression rule, Prediction[] predictions)
    {
        var ctx = RuleContextFor(predictions.Length);
        Prediction? best = null;
        double bestScore = double.NegativeInfinity;
        foreach (var p in predictions)
        {
            double s = rule.Score(new TargetScope(RuleTarget.From(p), ctx));
            if (!double.IsNaN(s) && s > bestScore) { bestScore = s; best = p; }
        }
        return best;
    }

    private static (Prediction? Best, double DistSq) Nearest(Prediction[] predictions, double x, double y)
    {
        Prediction? best = null;
        double bestSq = double.MaxValue;
        foreach (var p in predictions)
        {
            double cx = p.Rectangle.X + p.Rectangle.Width / 2.0;
            double cy = p.Rectangle.Y + p.Rectangle.Height / 2.0;
            double dSq = (cx - x) * (cx - x) + (cy - y) * (cy - y);
            if (dSq < bestSq) { bestSq = dSq; best = p; }
        }
        return (best, bestSq);
    }

    /// <summary>
    ///     Aim point (model space) inside <paramref name="box"/>: the configured aim region + frac, or what
    ///     the effective profile adds on top (class anchor, pose keypoints, dynamic and additional offset).
    /// </summary>
    private (double X, double Y) AimPointFor(RectangleF box, int classId, Keypoint[]? keypoints)
    {
        var region = AppConfig.Current.SliderSettings.AimRegion;
        float modelSize = Math.Max(1, global::PowerAim.AI.PredictionLogic.IMAGE_SIZE);
        var p = _effectiveProfile;
        if (p == null)
            return AimPoint.Compute(box, classId, null, null, AimPointOptions.None, region, _aimFracX, _aimFracY, modelSize);

        // The additional offset is set in FOV pixels; detections live in model space.
        var area = ImageCapture.CaptureArea;
        double capSize = Math.Clamp(Math.Round(AppConfig.Current.SliderSettings.ActualFovSize), 16.0,
            Math.Max(16.0, Math.Min(area.Width, area.Height)));
        double toModel = modelSize / capSize;
        var options = new AimPointOptions(p.DynamicOffset, p.DynamicMinAreaPct, p.DynamicMaxAreaPct,
            p.AdditionalOffsetX * toModel, p.AdditionalOffsetY * toModel, p.PoseTarget, (float)p.KeypointMinConfidence);
        var spec = p.ClassSpecs.FirstOrDefault(s => s.ClassId == classId);
        return AimPoint.Compute(box, classId, keypoints, spec, options, region, _aimFracX, _aimFracY, modelSize);
    }

    // ============================================================================ LEGACY PATH ====

    private void CalculateCoordinates(Prediction closestPrediction, float scaleX, float scaleY)
    {
        var (px, py) = AimPointFor(closestPrediction.Rectangle, closestPrediction.ClassId, closestPrediction.Keypoints);
        detectedX = (int)(px * scaleX);
        detectedY = (int)(py * scaleY);
    }

    private void HandleAim(Prediction closestPrediction)
    {
        if (!IsAiming())
        {
            SetStatus(AimStatus.Idle);
            ReactionReady(false);
            _wasAiming = false;
            return;
        }
        SetStatus(AimStatus.Pulling);
        if (!ReactionReady(true)) return;

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
            float modelSize = Math.Max(1, global::PowerAim.AI.PredictionLogic.IMAGE_SIZE);
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

    private void HandlePredictions(KalmanPrediction kalmanPrediction, Prediction closestPrediction, Rectangle area)
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