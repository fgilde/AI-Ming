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
    // Last aimed position (model-space) — lets the lock survive tracker re-identification: when the
    // held ID disappears (fast pans break IoU association and the same enemy comes back under a new
    // id), we re-adopt whatever track is nearest to where we were aiming instead of hard-switching.
    private double _lastX, _lastY;
    private bool _hasLast;

    public int CurrentId => _currentId;

    public void Reset()
    {
        _currentId = -1;
        _challengerId = -1;
        _challengerFrames = 0;
        _hasLast = false;
    }

    /// <summary>
    ///     Choose a track to aim at. <paramref name="centerX"/>/<paramref name="centerY"/> are the
    ///     crosshair position in the same (model) space as the tracks. <paramref name="adoptRadius"/>
    ///     (model-space px) is how far the held enemy may reappear from its last position and still be
    ///     re-adopted after an ID change (issue #19 — without this, every tracker re-identification
    ///     bypassed the switch hysteresis entirely and the aim flip-flopped between enemies).
    /// </summary>
    public TargetTrack? Select(IReadOnlyList<TargetTrack> tracks, double centerX, double centerY,
        double switchMarginPct, int switchFrames, double adoptRadius = 0)
    {
        if (tracks == null || tracks.Count == 0)
        {
            // Keep _lastX/_lastY: a 1-2 frame full dropout shouldn't forget where the enemy was.
            _currentId = -1;
            _challengerId = -1;
            _challengerFrames = 0;
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

        // Lost the held ID (aged out / re-identified). Prefer adopting the track nearest the last
        // aimed position — that IS the same enemy in almost every case — and only fall back to the
        // best-scored track when nothing plausible is close enough.
        if (current == null)
        {
            TargetTrack? adopt = null;
            if (_hasLast && adoptRadius > 0)
            {
                double adoptSq = adoptRadius * adoptRadius;
                double bestSq = double.MaxValue;
                foreach (var t in tracks)
                {
                    double dx = t.X - _lastX, dy = t.Y - _lastY;
                    double dSq = dx * dx + dy * dy;
                    if (dSq <= adoptSq && dSq < bestSq) { bestSq = dSq; adopt = t; }
                }
            }

            var chosen = adopt ?? best!;
            _currentId = chosen.Id;
            _challengerId = -1;
            _challengerFrames = 0;
            return Remember(chosen);
        }

        // Best IS the current target → stay, clear any challenger streak.
        if (best!.Id == _currentId)
        {
            _challengerId = -1;
            _challengerFrames = 0;
            return Remember(current);
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
