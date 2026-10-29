namespace PowerAim.AI.Aiming;

/// <summary>
/// Track-based target lock with switch hysteresis.
/// Stronger than the raw-prediction sticky path: uses stable track IDs from StrongSORT.
/// </summary>
public sealed class TargetSelector
{
    private int _currentId = -1;
    private int _challengerId = -1;
    private int _challengerFrames;
    private double _lastX, _lastY;
    private bool _hasLast;
    private int _framesWithoutCurrent;

    private const int MaxFramesWithoutCurrent = 18;

    public void Reset()
    {
        _currentId = -1;
        _challengerId = -1;
        _challengerFrames = 0;
        _hasLast = false;
        _framesWithoutCurrent = 0;
    }

    /// <param name="switchMarginPct">Challenger must beat current by this fraction (e.g. 0.12).</param>
    /// <param name="switchFrames">Consecutive frames challenger must lead before switch.</param>
    /// <param name="stickyRadiusPx">Re-acquire radius when current ID is briefly missing.</param>
    public TargetTrack? Select(
        IReadOnlyList<TargetTrack> tracks,
        double centerX,
        double centerY,
        double switchMarginPct,
        int switchFrames,
        double stickyRadiusPx,
        Func<TargetTrack, double>? score = null)
    {
        if (tracks == null || tracks.Count == 0)
        {
            _framesWithoutCurrent++;
            if (_framesWithoutCurrent >= MaxFramesWithoutCurrent)
                Reset();
            return null;
        }

        _framesWithoutCurrent = 0;

        TargetTrack? best = null;
        double bestScore = double.NegativeInfinity;
        TargetTrack? current = null;
        double currentScore = double.NegativeInfinity;

        foreach (var t in tracks)
        {
            double s = score?.Invoke(t) ?? Score(t, centerX, centerY);
            if (double.IsNaN(s)) continue;
            if (s > bestScore) { bestScore = s; best = t; }
            if (t.Id == _currentId) { current = t; currentScore = s; }
        }

        if (best is null) return null;

        // Lost current ID — try adopt nearest to last pose within sticky radius.
        if (current is null)
        {
            TargetTrack? adopt = null;
            if (_hasLast && stickyRadiusPx > 0)
            {
                double adoptSq = stickyRadiusPx * stickyRadiusPx;
                double bestSq = double.MaxValue;
                foreach (var t in tracks)
                {
                    if (score != null && double.IsNaN(score(t))) continue; // the rule rejects it
                    double dx = t.X - _lastX, dy = t.Y - _lastY;
                    double dSq = dx * dx + dy * dy;
                    if (dSq <= adoptSq && dSq < bestSq) { bestSq = dSq; adopt = t; }
                }
            }

            var chosen = adopt ?? best;
            _currentId = chosen.Id;
            _challengerId = -1;
            _challengerFrames = 0;
            return Remember(chosen);
        }

        if (best.Id == _currentId)
        {
            _challengerId = -1;
            _challengerFrames = 0;
            return Remember(current);
        }

        // Require sustained lead before switching (kills flicker between two nearby targets).
        double margin = Math.Max(0.05, switchMarginPct);
        int needFrames = Math.Max(1, switchFrames);
        // Lead by a share of the current score (additive, so it also works for negative rule scores;
        // for the built-in positive score this is the same as best > current * (1 + margin)).
        if (bestScore - currentScore > Math.Abs(currentScore) * margin)
        {
            if (_challengerId == best.Id) _challengerFrames++;
            else { _challengerId = best.Id; _challengerFrames = 1; }

            if (_challengerFrames >= needFrames)
            {
                _currentId = best.Id;
                _challengerId = -1;
                _challengerFrames = 0;
                return Remember(best);
            }
        }
        else
        {
            _challengerId = -1;
            _challengerFrames = 0;
        }

        return Remember(current);
    }

    private TargetTrack Remember(TargetTrack t)
    {
        _lastX = t.X;
        _lastY = t.Y;
        _hasLast = true;
        return t;
    }

    private static double Score(TargetTrack t, double centerX, double centerY)
    {
        double dx = t.X - centerX;
        double dy = t.Y - centerY;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double proximity = 1.0 / (1.0 + dist / 100.0);
        double conf = 0.5 + 0.5 * Math.Clamp(t.Confidence, 0f, 1f);
        double freshness = t.MissedFrames == 0 ? 1.0 : (t.MissedFrames <= 3 ? 0.92 : 0.78);
        double stability = t.Hits >= 5 ? 1.08 : 1.0;
        return proximity * conf * freshness * stability;
    }
}