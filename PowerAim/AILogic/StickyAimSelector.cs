using System.Drawing;

namespace PowerAim.AILogic;

/// <summary>
///     Stateful target-selection helper that keeps the crosshair locked on the same enemy across
///     frames instead of flipping between two near-identical detections. Each frame the selector
///     scores every candidate against a velocity-projected position of the held target and picks
///     the highest-scoring one; when nothing close-enough is found it briefly keeps predicting the
///     last known target before releasing the lock.
///
///     Adapted from upstream Babyhamsta/Aimmy <c>StickyAimSelector</c> (1e91b4e, 3294904, 817b2c1).
///     Differences vs upstream:
///       * uses this fork's <see cref="Prediction"/> shape (<c>CenterXTranslated/Y</c> in place of
///         the upstream <c>ScreenCenterX/Y</c>);
///       * delegates the scoring formula to <see cref="MathUtil.CalculateTargetScore"/> which is
///         already shared with other targeting code;
///       * <see cref="MaxLockScore"/> and the candidate <see cref="Threshold"/> are injected from
///         <c>AppConfig.Current.AISettings</c> instead of being hard-coded constants.
/// </summary>
public sealed class StickyAimSelector
{
    private const int MaxFramesWithoutTarget = 3;
    private const float LockScoreDecay = 0.85f;
    private const float LockScoreGain = 15f;

    private Prediction? _currentTarget;
    private int _consecutiveFramesWithoutTarget;
    private float _lastTargetVelocityX;
    private float _lastTargetVelocityY;
    private float _targetLockScore;

    /// <summary>
    ///     Selects the prediction the aim logic should consume this frame.
    /// </summary>
    /// <param name="candidates">All detections produced for the current frame.</param>
    /// <param name="fallback">
    ///     The candidate the caller would have used without sticky aim (typically its
    ///     best-confidence pick). Returned verbatim when <paramref name="enabled"/> is <c>false</c>
    ///     or the candidate list is empty.
    /// </param>
    /// <param name="enabled">Master switch — <c>false</c> bypasses the selector entirely.</param>
    /// <param name="threshold">Score radius in translated-screen pixels (typ. 80).</param>
    /// <param name="maxLockScore">Cap for the running lock-score (typ. 100).</param>
    public Prediction? SelectTarget(
        IReadOnlyList<Prediction> candidates,
        Prediction? fallback,
        bool enabled,
        float threshold,
        float maxLockScore)
    {
        if (!enabled)
        {
            Reset();
            return fallback;
        }

        if (candidates == null || candidates.Count == 0)
        {
            return HandleNoDetections();
        }

        _consecutiveFramesWithoutTarget = 0;

        // Project the held target forward by its last estimated velocity. When no target is held
        // we score candidates against their own position (delta = 0), which collapses to
        // confidence/size ranking.
        bool hasTarget = _currentTarget != null;
        float predictedX = hasTarget ? _currentTarget!.CenterXTranslated + _lastTargetVelocityX : 0f;
        float predictedY = hasTarget ? _currentTarget!.CenterYTranslated + _lastTargetVelocityY : 0f;

        Prediction? best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            float score = MathUtil.CalculateTargetScore(
                candidate,
                _currentTarget,
                hasTarget ? predictedX : candidate.CenterXTranslated,
                hasTarget ? predictedY : candidate.CenterYTranslated,
                _targetLockScore,
                maxLockScore,
                threshold);

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best == null)
        {
            return HandleNoDetections();
        }

        if (_currentTarget == null)
        {
            return AcquireNewTarget(best);
        }

        // Decide whether the winning candidate is "the same target" we already track. We use
        // proximity (radius scaled to target size) and a loose size-ratio gate to reject sudden
        // big jumps (which usually means a different enemy entered the frame).
        float targetArea = _currentTarget.Rectangle.Width * _currentTarget.Rectangle.Height;
        float targetSize = MathF.Sqrt(MathF.Max(targetArea, 1f));
        float trackingRadius = targetSize * 3f;
        float trackingRadiusSq = trackingRadius * trackingRadius;
        float distSq = MathUtil.Distance(best, _currentTarget);
        float bestArea = best.Rectangle.Width * best.Rectangle.Height;
        float minArea = MathF.Min(targetArea, bestArea);
        float maxArea = MathF.Max(targetArea, bestArea);
        float sizeRatio = maxArea > 0f ? minArea / maxArea : 0f;
        bool isSameTarget = distSq < trackingRadiusSq && sizeRatio > 0.5f;

        if (isSameTarget)
        {
            UpdateVelocity(best);
            _targetLockScore = Math.Min(maxLockScore, _targetLockScore + LockScoreGain);
            _currentTarget = best;
            return best;
        }

        // Different target won the score. Switch immediately — the score formula already factors
        // in the existing lock bonus, so if the challenger still beat us it deserves the lock.
        return AcquireNewTarget(best);
    }

    private Prediction? HandleNoDetections()
    {
        if (_currentTarget != null && ++_consecutiveFramesWithoutTarget <= MaxFramesWithoutTarget)
        {
            _targetLockScore *= LockScoreDecay;

            // Synthesize a "ghost" prediction at the velocity-projected position so the aim logic
            // can keep tracking through brief detection drops. Confidence is decayed linearly so
            // downstream consumers (overlays) can visualise the fading lock.
            return new Prediction
            {
                CenterXTranslated = _currentTarget.CenterXTranslated + _lastTargetVelocityX * _consecutiveFramesWithoutTarget,
                CenterYTranslated = _currentTarget.CenterYTranslated + _lastTargetVelocityY * _consecutiveFramesWithoutTarget,
                Rectangle = _currentTarget.Rectangle,
                TranslatedRectangle = _currentTarget.TranslatedRectangle,
                Confidence = _currentTarget.Confidence * (1f - _consecutiveFramesWithoutTarget * 0.2f),
                ClassId = _currentTarget.ClassId,
                ClassName = _currentTarget.ClassName
            };
        }

        Reset();
        return null;
    }

    private Prediction AcquireNewTarget(Prediction target)
    {
        _lastTargetVelocityX = 0f;
        _lastTargetVelocityY = 0f;
        _targetLockScore = LockScoreGain;
        _currentTarget = target;
        return target;
    }

    private void UpdateVelocity(Prediction newTarget)
    {
        if (_currentTarget == null)
        {
            return;
        }

        // EMA of frame-to-frame delta. Bigger targets get more smoothing on the assumption that
        // they're closer to the camera and therefore noisier in pixel-space.
        float area = newTarget.Rectangle.Width * newTarget.Rectangle.Height;
        float sizeFactor = Math.Clamp(10000f / MathF.Max(area, 100f), 1.0f, 3.0f);
        float smoothing = Math.Clamp(0.6f + sizeFactor * 0.1f, 0.7f, 0.9f);
        float newWeight = 1f - smoothing;

        float newVelX = newTarget.CenterXTranslated - _currentTarget.CenterXTranslated;
        float newVelY = newTarget.CenterYTranslated - _currentTarget.CenterYTranslated;
        _lastTargetVelocityX = _lastTargetVelocityX * smoothing + newVelX * newWeight;
        _lastTargetVelocityY = _lastTargetVelocityY * smoothing + newVelY * newWeight;
    }

    /// <summary>
    ///     Clears all tracking state. Call when the model is reloaded, the user changes the target
    ///     class filter, or the global active toggle goes off so a stale lock doesn't bleed into
    ///     the next session.
    /// </summary>
    public void Reset()
    {
        _currentTarget = null;
        _consecutiveFramesWithoutTarget = 0;
        _lastTargetVelocityX = 0f;
        _lastTargetVelocityY = 0f;
        _targetLockScore = 0f;
    }
}
