namespace PowerAim.AILogic.Aiming;

/// <summary>
///     Picks which confirmed <see cref="TargetTrack"/> to aim at, with switch hysteresis so the
///     reticle doesn't flip-flop between two similar targets frame-to-frame. The current target is
///     kept until a challenger is meaningfully better (by <c>switchMarginPct</c>) for several
///     consecutive frames (<c>switchFrames</c>) — the "stickiness" real aim-assist uses.
/// </summary>
public sealed class TargetSelector
{
    private int _currentId = -1;
    private int _challengerId = -1;
    private int _challengerFrames;

    public int CurrentId => _currentId;

    public void Reset()
    {
        _currentId = -1;
        _challengerId = -1;
        _challengerFrames = 0;
    }

    /// <summary>
    ///     Choose a track to aim at. <paramref name="centerX"/>/<paramref name="centerY"/> are the
    ///     crosshair position in the same (model) space as the tracks.
    /// </summary>
    public TargetTrack? Select(IReadOnlyList<TargetTrack> tracks, double centerX, double centerY,
        double switchMarginPct, int switchFrames)
    {
        if (tracks == null || tracks.Count == 0)
        {
            Reset();
            return null;
        }

        TargetTrack? best = null;
        double bestScore = double.MinValue;
        TargetTrack? current = null;
        double currentScore = double.MinValue;

        foreach (var t in tracks)
        {
            double score = Score(t, centerX, centerY);
            if (score > bestScore) { bestScore = score; best = t; }
            if (t.Id == _currentId) { current = t; currentScore = score; }
        }

        // Lost the held target (aged out / not in confirmed set) → take the best immediately.
        if (current == null)
        {
            _currentId = best!.Id;
            _challengerId = -1;
            _challengerFrames = 0;
            return best;
        }

        // Best IS the current target → stay, clear any challenger streak.
        if (best!.Id == _currentId)
        {
            _challengerId = -1;
            _challengerFrames = 0;
            return current;
        }

        // A different track is better. Only switch if it stays meaningfully better for N frames.
        if (bestScore > currentScore * (1.0 + switchMarginPct))
        {
            if (_challengerId == best.Id) _challengerFrames++;
            else { _challengerId = best.Id; _challengerFrames = 1; }

            if (_challengerFrames >= switchFrames)
            {
                _currentId = best.Id;
                _challengerId = -1;
                _challengerFrames = 0;
                return best;
            }
        }
        else
        {
            _challengerId = -1;
            _challengerFrames = 0;
        }

        return current;
    }

    /// <summary>
    ///     Higher = better target. Proximity to the crosshair dominates (closest is usually what the
    ///     player means), confidence is a secondary boost, and a coasting track is mildly penalised
    ///     so a solidly-tracked rival can take over from one that's only being extrapolated.
    /// </summary>
    private static double Score(TargetTrack t, double centerX, double centerY)
    {
        double dx = t.X - centerX;
        double dy = t.Y - centerY;
        double dist = System.Math.Sqrt(dx * dx + dy * dy);
        double proximity = 1.0 / (1.0 + dist / 100.0);          // (0,1], 1 = on the crosshair
        double conf = 0.5 + 0.5 * System.Math.Clamp(t.Confidence, 0f, 1f);
        double freshness = t.MissedFrames == 0 ? 1.0 : 0.85;     // mild coast penalty
        return proximity * conf * freshness;
    }
}
