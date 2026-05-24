using PowerAim.Config;

namespace PowerAim.AILogic;

/// <summary>
/// Velocity-aware 2D Kalman-style filter with configurable lead time.
///
/// State vector: [x, y, vx, vy]. Covariance is approximated as a diagonal
/// to keep the math cheap while still tracking process noise per-component.
/// Lead time can be derived from <see cref="AppConfig.Current.SliderSettings.PredictionLeadTime"/>
/// and an optional mouse speed hint.
///
/// Ported from upstream Babyhamsta/Aimmy commit 817b2c1 — replaces the
/// previous Accord.Statistics.Running.KalmanFilter2D-based implementation
/// which lacked any lead-time prediction.
/// </summary>
internal class KalmanPrediction
{
    public struct Detection
    {
        public int X;
        public int Y;
        public DateTime Timestamp;
    }

    // State: [x, y, vx, vy]
    private double _x, _y, _vx, _vy;

    // Covariance matrix (4x4 simplified to its diagonal entries).
    private double _p00 = 1.0, _p11 = 1.0, _p22 = 1.0, _p33 = 1.0;

    // Tuning parameters.
    private const double ProcessNoise = 0.1;
    private const double MeasurementNoise = 0.5;
    private const double MaxVelocity = 5000.0;

    private DateTime _lastUpdateTime = DateTime.UtcNow;
    private bool _initialized;

    /// <summary>
    /// Fuses a new measurement into the filter, advancing the
    /// position/velocity estimate and shrinking the covariance.
    /// </summary>
    public void UpdateKalmanFilter(Detection detection)
    {
        var now = DateTime.UtcNow;

        if (!_initialized)
        {
            _x = detection.X;
            _y = detection.Y;
            _vx = 0;
            _vy = 0;
            _lastUpdateTime = now;
            _initialized = true;
            return;
        }

        // Time step in seconds, clamped to avoid spikes after long pauses.
        double dt = (now - _lastUpdateTime).TotalSeconds;
        dt = Math.Clamp(dt, 0.001, 0.1);

        // Prediction step using the constant-velocity model.
        double predictedX = _x + _vx * dt;
        double predictedY = _y + _vy * dt;

        // Inject process noise into the covariance (velocity gets more).
        _p00 += ProcessNoise;
        _p11 += ProcessNoise;
        _p22 += ProcessNoise * 10;
        _p33 += ProcessNoise * 10;

        // Innovation (measurement residual).
        double innovationX = detection.X - predictedX;
        double innovationY = detection.Y - predictedY;

        // Scalar Kalman gain for the position channel.
        double k = _p00 / (_p00 + MeasurementNoise);

        // Update position.
        _x = predictedX + k * innovationX;
        _y = predictedY + k * innovationY;

        // Update velocity from the innovation, normalised by dt.
        _vx += k * innovationX / dt;
        _vy += k * innovationY / dt;

        // Clamp velocity so a single bad measurement cannot explode the state.
        _vx = Math.Clamp(_vx, -MaxVelocity, MaxVelocity);
        _vy = Math.Clamp(_vy, -MaxVelocity, MaxVelocity);

        // Shrink covariance proportional to the gain.
        _p00 *= 1 - k;
        _p11 *= 1 - k;

        _lastUpdateTime = now;
    }

    /// <summary>
    /// Returns the extrapolated target position, optionally using a mouse
    /// speed hint to scale the lead time dynamically.
    /// </summary>
    /// <param name="mouseSpeed">
    /// Optional mouse animation speed. When greater than zero, lead time
    /// is biased toward 40% of the estimated animation completion time.
    /// </param>
    public Detection GetKalmanPosition(double mouseSpeed = 0)
    {
        var now = DateTime.UtcNow;
        double dt = (now - _lastUpdateTime).TotalSeconds;

        // Catch the present using the last known velocity.
        double currentX = _x + _vx * dt;
        double currentY = _y + _vy * dt;

        double leadTime = PredictionSettings.KalmanLeadTime;

        // ---------- Adaptive lead-time ----------
        // Recoil is irrelevant here — we're predicting the *target* not our own gun. The lead
        // time we need depends on how fast the target is currently moving across the screen:
        //   • slow / stationary target → very little lead, otherwise we shoot ahead of empty air
        //   • fast strafing target     → more lead, otherwise we shoot behind them
        // The Kalman state already tracks (vx, vy) in pixels/sec, so we can scale lead between
        // a configured min and max based on the magnitude. The slider value
        // (PredictionSettings.KalmanLeadTime) is taken as the *median* / centre point of the
        // adaptive range, keeping backwards-compatibility with user calibration.
        if (PredictionSettings.AdaptiveKalmanLead)
        {
            double speed = Math.Sqrt(_vx * _vx + _vy * _vy); // pixels per second
            // 0 px/s → multiplier 0.4 (less lead than baseline)
            // 200 px/s → 1.0 (baseline)
            // 800+ px/s → up to 1.6 (more lead)
            double mult = Math.Clamp(0.4 + speed / 250.0, 0.4, 1.6);
            leadTime *= mult;
        }

        if (mouseSpeed > 0.0)
        {
            // Estimate animation completion time from mouse speed.
            double estimatedCompletionTime = 100.0 / mouseSpeed;
            double dynamicLead = estimatedCompletionTime * 0.4;
            // Treat the user setting as a multiplier around the dynamic lead
            // (0.10 acts as the unity multiplier, matching upstream).
            leadTime = dynamicLead * (leadTime / 0.10);
            leadTime = Math.Clamp(leadTime, 0.02, 0.3);
        }

        double predictedX = currentX + _vx * leadTime;
        double predictedY = currentY + _vy * leadTime;

        return new Detection
        {
            X = (int)predictedX,
            Y = (int)predictedY,
            Timestamp = now
        };
    }

    /// <summary>Reinitialises the filter on the next update.</summary>
    public void Reset()
    {
        _x = _y = _vx = _vy = 0;
        _p00 = _p11 = _p22 = _p33 = 1.0;
        _initialized = false;
    }
}

/// <summary>
/// EMA-smoothed predictor with explicit velocity tracking.
///
/// The original wisethef0x prediction was a pure EMA (no prediction at all).
/// Upstream 817b2c1 added a velocity term so the output actually leads the
/// target by <see cref="PredictionSettings.WiseTheFoxLeadTime"/> seconds.
/// </summary>
internal class WiseTheFoxPrediction
{
    public struct WTFDetection
    {
        public int X;
        public int Y;
        public DateTime Timestamp;
    }

    private DateTime _lastUpdateTime;
    private const double Alpha = 0.5; // EMA smoothing factor.

    private double _emaX, _emaY;
    private double _velocityX, _velocityY;
    private double _prevX, _prevY;
    private bool _initialized;

    public WiseTheFoxPrediction()
    {
        _lastUpdateTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Push a measurement into the EMA + velocity tracker.
    /// </summary>
    public void UpdateDetection(WTFDetection detection)
    {
        var now = DateTime.UtcNow;

        if (!_initialized)
        {
            _emaX = detection.X;
            _emaY = detection.Y;
            _prevX = detection.X;
            _prevY = detection.Y;
            _velocityX = 0;
            _velocityY = 0;
            _lastUpdateTime = now;
            _initialized = true;
            return;
        }

        double dt = (now - _lastUpdateTime).TotalSeconds;
        dt = Math.Clamp(dt, 0.001, 0.1);

        // EMA on position.
        _emaX = Alpha * detection.X + (1.0 - Alpha) * _emaX;
        _emaY = Alpha * detection.Y + (1.0 - Alpha) * _emaY;

        // Instantaneous velocity in pixels/second.
        double newVelocityX = (_emaX - _prevX) / dt;
        double newVelocityY = (_emaY - _prevY) / dt;

        // EMA on velocity to suppress jitter.
        _velocityX = Alpha * newVelocityX + (1.0 - Alpha) * _velocityX;
        _velocityY = Alpha * newVelocityY + (1.0 - Alpha) * _velocityY;

        _prevX = _emaX;
        _prevY = _emaY;
        _lastUpdateTime = now;
    }

    /// <summary>
    /// Returns the smoothed position extrapolated by the configured lead time.
    /// </summary>
    public WTFDetection GetEstimatedPosition()
    {
        double leadTime = PredictionSettings.WiseTheFoxLeadTime;

        double predictedX = _emaX + _velocityX * leadTime;
        double predictedY = _emaY + _velocityY * leadTime;

        return new WTFDetection
        {
            X = (int)predictedX,
            Y = (int)predictedY,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>Reinitialises the predictor on the next update.</summary>
    public void Reset()
    {
        _emaX = _emaY = 0;
        _velocityX = _velocityY = 0;
        _prevX = _prevY = 0;
        _initialized = false;
    }
}

/// <summary>
/// Historical-velocity-averaging predictor (Shall0e V2).
///
/// The legacy implementation summed past <em>positions</em> (plus the cursor
/// position) which is mathematically meaningless. Upstream 817b2c1 rewrote
/// it to average actual frame-to-frame velocity and project the target
/// forward by a configurable multiplier.
///
/// For backward compatibility with existing callers in this fork the public
/// <see cref="xValues"/>/<see cref="yValues"/> lists are kept and reused
/// as the velocity history buffer.
/// </summary>
internal class ShalloePredictionV2
{
    /// <summary>Recent X-velocity samples (pixels per frame).</summary>
    public static List<int> xValues = [];

    /// <summary>Recent Y-velocity samples (pixels per frame).</summary>
    public static List<int> yValues = [];

    /// <summary>
    /// Legacy field kept for source compatibility; no longer used directly
    /// since the upstream rewrite reads <see cref="PredictionSettings.ShalloeLeadMultiplier"/>
    /// instead of a fixed integer multiplier.
    /// </summary>
    public static int AmountCount = 2;

    private const int MaxHistorySize = 5;

    private static int _prevX;
    private static int _prevY;
    private static bool _initialized;

    /// <summary>
    /// Push the latest target position so the velocity buffer is updated.
    /// The legacy callsites that directly append to <see cref="xValues"/>
    /// also work, but using this helper keeps the previous-position state
    /// in sync.
    /// </summary>
    public static void UpdatePosition(int targetX, int targetY)
    {
        if (!_initialized)
        {
            _prevX = targetX;
            _prevY = targetY;
            _initialized = true;
            return;
        }

        int velocityX = targetX - _prevX;
        int velocityY = targetY - _prevY;

        if (xValues.Count >= MaxHistorySize) xValues.RemoveAt(0);
        if (yValues.Count >= MaxHistorySize) yValues.RemoveAt(0);

        xValues.Add(velocityX);
        yValues.Add(velocityY);

        _prevX = targetX;
        _prevY = targetY;
    }

    /// <summary>Returns the predicted X position based on average X velocity.</summary>
    public static int GetSPX()
    {
        if (xValues.Count == 0) return _prevX;

        double leadMultiplier = PredictionSettings.ShalloeLeadMultiplier;
        double avgVelocity = xValues.Average();
        return (int)(_prevX + avgVelocity * leadMultiplier);
    }

    /// <summary>Returns the predicted Y position based on average Y velocity.</summary>
    public static int GetSPY()
    {
        if (yValues.Count == 0) return _prevY;

        double leadMultiplier = PredictionSettings.ShalloeLeadMultiplier;
        double avgVelocity = yValues.Average();
        return (int)(_prevY + avgVelocity * leadMultiplier);
    }

    /// <summary>Clears the velocity history and resets the previous-position state.</summary>
    public static void Reset()
    {
        xValues.Clear();
        yValues.Clear();
        _prevX = _prevY = 0;
        _initialized = false;
    }
}

/// <summary>
/// Central place for prediction tuning constants. These are exposed as
/// static fields so future UI surfaces can bind to them without further
/// API breakage; defaults match upstream Babyhamsta/Aimmy 817b2c1.
/// </summary>
internal static class PredictionSettings
{
    /// <summary>Lead time used by the Kalman predictor, in seconds.</summary>
    public static double KalmanLeadTime { get; set; } = 0.10;

    /// <summary>
    ///     When true, the Kalman predictor scales lead-time based on the observed target speed:
    ///     stationary targets get ~40 % of the baseline (less over-shoot), fast strafing targets
    ///     get up to 160 % (more anticipation). Default <c>true</c>.
    /// </summary>
    public static bool AdaptiveKalmanLead { get; set; } = true;

    /// <summary>Lead time used by the WiseTheFox predictor, in seconds.</summary>
    public static double WiseTheFoxLeadTime { get; set; } = 0.15;

    /// <summary>Multiplier applied to the averaged velocity by Shall0e V2.</summary>
    public static double ShalloeLeadMultiplier { get; set; } = 5.0;
}
