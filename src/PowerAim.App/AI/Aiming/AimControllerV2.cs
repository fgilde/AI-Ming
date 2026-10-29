using PowerAim.Input;

namespace PowerAim.AI.Aiming;

/// <summary>Which controller turns the smart path's aim point into mouse moves.</summary>
public enum AimEngine
{
    /// <summary>Proportional approach per frame (<see cref="AimController"/>).</summary>
    Classic,
    /// <summary>Per-axis model-based control with settle zone and online calibration (<see cref="AimControllerV2"/>).</summary>
    V2,
}

/// <summary>
///     Aim engine V2 for the mouse path: per-axis control on a model of the game's view.
///     <list type="bullet">
///       <item><b>Own-view model:</b> every sent move is kept with its time; its pixel effect is the
///             counts · px-per-count · learned gain. Moves made after the analysed frame was captured are
///             subtracted from the measured error (no double correction under latency).</item>
///       <item><b>Target in world coordinates:</b> error + own view position at capture time, filtered per
///             axis; a target moving steadily gets a velocity lead.</item>
///       <item><b>True zero:</b> two error estimates that disagree close to the target give exactly 0, and a
///             settle zone holds the axis still until the error clearly leaves it.</item>
///       <item><b>Anti-hunting:</b> repeated large sign flips soften the correction; a stalled approach is
///             boosted.</item>
///       <item><b>Online calibration:</b> the observed shift of the target between frames is compared with the
///             shift the sent moves predict — that learns the real gain and corrects the latency.</item>
///     </list>
///     Units are screen pixels and milliseconds.
/// </summary>
public sealed class AimControllerV2
{
    private readonly Action<int, int> _move;
    private readonly Func<double> _clockMs;

    // Sent moves in raw pixels (counts · px-per-count, before the learned gain).
    private readonly List<(double T, double X, double Y)> _sent = [];
    private double _trimmedX, _trimmedY; // moves dropped from _sent, so the view position stays continuous
    private double _lastStepMs = double.NaN;
    private double _frameMs = 1000.0 / 60;
    private double _accX, _accY;
    private readonly Axis _x = new(), _y = new();

    // Online calibration state.
    private double _prevMs = double.NaN, _prevErrX, _prevErrY;
    private double _gainSxx, _gainSxy;
    private static readonly double[] LatencyCandidates = [-36, -18, 0, 18, 36];
    private readonly double[] _latSxx = new double[5], _latSxy = new double[5];
    private double _latencyPauseUntil;

    /// <summary>Learned real / assumed view gain (0.6–2.2).</summary>
    public double GainCorrection { get; private set; } = 1.0;

    /// <summary>Learned correction of the configured latency (−40…+40 ms).</summary>
    public double LatencyOffsetMs { get; private set; }

    public AimControllerV2(Action<int, int>? move = null, Func<double>? clockMs = null)
    {
        _move = move ?? InputSender.Move;
        if (clockMs == null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            clockMs = () => sw.Elapsed.TotalMilliseconds;
        }
        _clockMs = clockMs;
    }

    /// <summary>Forget the current approach (target lost / disengaged). The learned gain and latency stay.</summary>
    public void Reset()
    {
        _sent.Clear();
        _trimmedX = _trimmedY = 0;
        _lastStepMs = double.NaN;
        _prevMs = double.NaN;
        _accX = _accY = 0;
        _x.Reset();
        _y.Reset();
    }

    /// <param name="errX">Target minus crosshair in the analysed frame, screen px.</param>
    /// <param name="errY">Target minus crosshair in the analysed frame, screen px.</param>
    /// <param name="boxWidthPx">Width of the target box (screen px); sets the settle zone.</param>
    /// <param name="pixelsPerCount">Calibrated px per mouse count; 0 = 1:1.</param>
    /// <param name="latencyMs">Configured capture-to-output latency.</param>
    public void Step(double errX, double errY, double boxWidthPx, double pixelsPerCount, double latencyMs)
    {
        double now = _clockMs();
        double dt = double.IsNaN(_lastStepMs) ? _frameMs : Math.Clamp(now - _lastStepMs, 0.5, 200);
        if (dt > Math.Max(32, 4 * _frameMs)) dt = _frameMs; // a gap, not a frame
        _frameMs += (dt - _frameMs) * 0.2;
        _lastStepMs = now;
        double ppc = pixelsPerCount > 0 ? pixelsPerCount : 1.0;

        double baseLatency = Math.Max(0, latencyMs);
        double latency = Math.Max(0, baseLatency + LatencyOffsetMs);
        while (_sent.Count > 0 && _sent[0].T < now - 2000)
        {
            _trimmedX += _sent[0].X;
            _trimmedY += _sent[0].Y;
            _sent.RemoveAt(0);
        }

        if (!double.IsNaN(_prevMs)) Calibrate(now, errX, errY, latency);
        _prevMs = now;
        _prevErrX = errX;
        _prevErrY = errY;

        double capture = now - latency;
        var (camNowX, camNowY) = Cam(now);
        var (camCapX, camCapY) = Cam(capture);
        double outX = _x.Control(errX, (camNowX - camCapX) * GainCorrection, camCapX * GainCorrection, camNowX * GainCorrection,
            now, dt, latency, _frameMs, boxWidthPx);
        double outY = _y.Control(errY, (camNowY - camCapY) * GainCorrection, camCapY * GainCorrection, camNowY * GainCorrection,
            now, dt, latency, _frameMs, boxWidthPx);

        double cx = outX / (ppc * GainCorrection) + _accX;
        double cy = outY / (ppc * GainCorrection) + _accY;
        int ix = (int)Math.Round(cx), iy = (int)Math.Round(cy);
        _accX = cx - ix;
        _accY = cy - iy;
        ix = Math.Clamp(ix, -150, 150);
        iy = Math.Clamp(iy, -150, 150);
        if (ix == 0 && iy == 0) return;
        _sent.Add((now, ix * ppc, iy * ppc));
        _move(ix, iy);
    }

    private (double X, double Y) Cam(double t)
    {
        double x = _trimmedX, y = _trimmedY;
        foreach (var m in _sent)
            if (m.T <= t) { x += m.X; y += m.Y; }
        return (x, y);
    }

    /// <summary>
    ///     The target's shift between two analysed frames is the view's own shift (static target). Compare it
    ///     with what the sent moves predict for the same capture window: the ratio is the real gain, and the
    ///     latency candidate that predicts it best tells which way the latency is off.
    /// </summary>
    private void Calibrate(double now, double errX, double errY, double latency)
    {
        foreach (var (observed, axisX) in new[] { (-(errX - _prevErrX), true), (-(errY - _prevErrY), false) })
        {
            // Raw (gain-free) view shift each latency candidate predicts for this capture window.
            var xs = new double[LatencyCandidates.Length];
            double largest = 0;
            for (int c = 0; c < xs.Length; c++)
            {
                double lat = Math.Max(0, latency + LatencyCandidates[c]);
                var a = Cam(now - lat);
                var b = Cam(_prevMs - lat);
                xs[c] = axisX ? a.X - b.X : a.Y - b.Y;
                largest = Math.Max(largest, Math.Abs(xs[c]));
            }
            if (largest < 1) continue;                               // no own motion to learn from
            if (Math.Abs(observed) > 3 * largest * GainCorrection + 5) continue; // the target moved on its own

            for (int c = 0; c < xs.Length; c++)
            {
                _latSxx[c] = 0.98 * _latSxx[c] + xs[c] * xs[c];
                _latSxy[c] = 0.98 * _latSxy[c] + xs[c] * observed;
            }

            // Learn the gain from the timing that currently explains the observations best.
            double x = xs[BestLatencyCandidate()];
            double ratio = (_gainSxy + 0.1) / (_gainSxx + 0.1);
            if (Math.Abs(x) < 1 || Math.Abs(observed - x * ratio) > 1.2 * Math.Abs(x) + 0.6) continue;
            _gainSxx = x * x + 0.996 * _gainSxx;
            _gainSxy = x * observed + 0.996 * _gainSxy;
            double r = Math.Clamp((_gainSxy + 0.1) / (_gainSxx + 0.1), 0.3, 3);
            GainCorrection = Math.Clamp(GainCorrection + (r - GainCorrection) * 0.1, 0.6, 2.2);
        }

        if (now < _latencyPauseUntil) return;
        int best = BestLatencyCandidate();
        if (best != 2 && LatencyScore(best) > 1.2 * LatencyScore(2))
        {
            LatencyOffsetMs = Math.Clamp(LatencyOffsetMs + Math.Sign(LatencyCandidates[best]), -40, 40);
            _latencyPauseUntil = now + 150;
        }
    }

    /// <summary>Explained share of the observed shifts; only agreeing (positively correlated) timing counts.</summary>
    private double LatencyScore(int c) => _latSxy[c] > 0 ? _latSxy[c] * _latSxy[c] / (_latSxx[c] + 1) : 0;

    private int BestLatencyCandidate()
    {
        int best = 2;
        for (int c = 0; c < LatencyCandidates.Length; c++)
            if (LatencyScore(c) > LatencyScore(best)) best = c;
        return best;
    }

    /// <summary>Per-axis target filter, settle zone, anti-hunting and anti-stall.</summary>
    private sealed class Axis
    {
        private bool _init;
        private double _p, _v, _lockUntil;
        private int _lastSign;
        private double _signSince, _emaAbsErr;
        private bool _settled;
        private int _framesOutside;
        private int _overshoots;
        private double _lastFlipMs = double.NegativeInfinity;
        private int _lastErrSign;
        private double _boost = 1, _bestAbs = double.MaxValue, _lastProgressMs;

        public void Reset()
        {
            _init = false;
            _v = 0;
            _lastSign = 0;
            _settled = false;
            _framesOutside = 0;
            _overshoots = 0;
            _lastErrSign = 0;
            _boost = 1;
            _bestAbs = double.MaxValue;
        }

        /// <returns>Desired view movement over the next frame, px.</returns>
        public double Control(double measured, double inFlight, double camAtCapture, double camNow,
            double now, double dt, double latency, double frameMs, double boxWidth)
        {
            // Target in world coordinates, alpha-beta filtered.
            double world = measured + camAtCapture;
            if (!_init)
            {
                _p = world;
                _v = 0;
                _init = true;
                _signSince = now;
                _lastProgressMs = now;
            }
            else
            {
                _p += _v * dt;
                double innovation = world - _p;
                if (Math.Abs(innovation) > 35)
                {
                    // A jump: a new target or a re-detection elsewhere. Its sign change is no overshoot.
                    _p = world;
                    _v *= 0.25;
                    _lockUntil = now + 250;
                    _overshoots = 0;
                    _lastErrSign = 0;
                    _settled = false;
                }
                else if (now < _lockUntil)
                {
                    _p = world;
                }
                else
                {
                    _p += innovation * (1 - Math.Exp(-dt / 20));
                    _v += Math.Clamp(innovation, -12, 12) / dt * (1 - Math.Exp(-dt / 80));
                    _v = Math.Clamp(_v, -1.2, 1.2);
                }
            }

            // Lead only for a steady, slow-enough target that is already roughly tracked.
            int sign = Math.Abs(_v) >= 0.055 ? Math.Sign(_v) : 0;
            if (sign != _lastSign) { _lastSign = sign; _signSince = now; }
            double smith = measured - inFlight;
            _emaAbsErr += (Math.Abs(smith) - _emaAbsErr) * (1 - Math.Exp(-dt / 300));
            bool lead = sign != 0 && now - _signSince >= 160 && _emaAbsErr < 30 && now >= _lockUntil;
            double vLead = lead ? Math.Clamp(_v, -0.7, 0.7) : 0;
            if (lead && Math.Abs(smith) >= 30) vLead *= 0.5;

            // Two estimates of the error right now: Smith-corrected measurement and the filtered world target.
            double a = _p + (lead ? _v * latency : 0) - camNow;
            double b = smith + vLead * latency;
            double err = Consensus(a, b);

            // Settle zone with hysteresis: hold the axis still once on target.
            double zone = Math.Max(0.6, boxWidth * 0.45 * 0.5);
            if (_settled)
            {
                _framesOutside = Math.Abs(err) > zone ? _framesOutside + 1 : 0;
                if (_framesOutside >= Math.Ceiling(14 / frameMs) || lead) _settled = false;
                else return 0;
            }
            else if (Math.Abs(err) < zone && !lead && Math.Abs(inFlight) < 6)
            {
                _settled = true;
                _framesOutside = 0;
                return 0;
            }

            // Anti-hunting: big error flipping sign again soon → longer horizon (softer correction).
            int errSign = Math.Abs(err) >= 16 ? Math.Sign(err) : 0;
            if (errSign != 0)
            {
                if (_lastErrSign != 0 && errSign != _lastErrSign)
                {
                    _overshoots = now - _lastFlipMs <= 420 ? Math.Min(3, _overshoots + 1) : 0;
                    _lastFlipMs = now;
                }
                _lastErrSign = errSign;
            }
            if (now - _lastFlipMs > 420) _overshoots = 0;
            double extraHorizon = _overshoots > 0 ? (_overshoots + 1.6) * latency + 2 * frameMs : 0;

            // Anti-stall: no progress for 140 ms → push harder; progress → relax.
            double abs = Math.Abs(err);
            if (abs > _bestAbs + 10 || abs <= 4) { _bestAbs = abs; _lastProgressMs = now; _boost = 1 + (_boost - 1) * 0.85; }
            else if (abs < _bestAbs - 0.5) { _bestAbs = abs; _lastProgressMs = now; _boost = 1 + (_boost - 1) * 0.85; }
            else if (now - _lastProgressMs > 140)
            {
                // A swinging approach is no stall — boosting it would fight the anti-hunting.
                _boost = _overshoots > 0 ? 1 : Math.Min(6, _boost * 1.6);
                _lastProgressMs = now;
            }

            // Half the latency in the horizon keeps the loop stable while the latency estimate is still
            // off by a frame or two (a near-deadbeat step would swing into a limit cycle there).
            double velocity = err / (frameMs + 2.5 + 0.5 * latency + extraHorizon) + vLead;
            return velocity * frameMs * _boost;
        }

        /// <summary>Same-sign estimates → their mean, shrunk by their disagreement near the target; opposite signs → 0.</summary>
        private static double Consensus(double a, double b)
        {
            double k = Math.Sign(a) == Math.Sign(b) ? Math.Min(Math.Abs(a), Math.Abs(b)) : 0;
            double w = Math.Clamp((14 - k) / 9, 0, 1);
            double mean = (a + b) / 2;
            double r = Math.Abs(mean) - Math.Abs(a - b) / 2 * w;
            return r > 0 ? Math.Sign(mean) * r : 0;
        }
    }
}
