using PowerAim.AI;
using PowerAim.Class;

namespace PowerAim.AI;

/// <summary>
/// Sticky target lock for raw-prediction aim path.
/// Improvements over baseline:
/// - velocity EMA (less snap on single-frame noise)
/// - explicit reacquire radius after brief loss
/// - size-ratio + proximity gates before accepting a switch
/// - lock score decays smoothly instead of hard reset
/// </summary>
public sealed class StickyAimSelector
{
    private Prediction? _currentTarget;
    private float _lastTargetVelocityX;
    private float _lastTargetVelocityY;
    private float _targetLockScore;
    private int _consecutiveFramesWithoutTarget;
    private float _lastCx, _lastCy, _lastW, _lastH;
    private bool _hasLastPose;

    private const int MaxFramesWithoutTarget = 12;
    private const float VelocityEma = 0.35f;
    private const float LockDecayPerMiss = 0.85f;
    private const float SizeRatioMin = 0.45f;
    private const float SizeRatioMax = 2.20f;

    public void Reset()
    {
        _currentTarget = null;
        _lastTargetVelocityX = 0;
        _lastTargetVelocityY = 0;
        _targetLockScore = 0;
        _consecutiveFramesWithoutTarget = 0;
        _hasLastPose = false;
    }

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
            return HandleNoDetections();

        _consecutiveFramesWithoutTarget = 0;

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
            return HandleNoDetections();

        if (_currentTarget == null)
            return AcquireNewTarget(best);

        float targetArea = MathF.Max(_currentTarget.Rectangle.Width * _currentTarget.Rectangle.Height, 1f);
        float targetSize = MathF.Sqrt(targetArea);
        float matchRadius = Math.Max(threshold * 0.55f, targetSize * 1.15f);

        float dx = best.CenterXTranslated - predictedX;
        float dy = best.CenterYTranslated - predictedY;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        float bestArea = MathF.Max(best.Rectangle.Width * best.Rectangle.Height, 1f);
        float sizeRatio = bestArea / targetArea;

        bool sameTarget =
            dist <= matchRadius
            && sizeRatio >= SizeRatioMin
            && sizeRatio <= SizeRatioMax;

        if (sameTarget)
            return UpdateHeldTarget(best, maxLockScore);

        // Not the same body — only switch if the challenger clearly wins on score.
        if (bestScore > _targetLockScore * 1.15f + 8f)
            return AcquireNewTarget(best);

        // Keep holding; decay lock slightly so a persistent rival can eventually take over.
        _targetLockScore = Math.Max(0, _targetLockScore * 0.98f);
        return _currentTarget;
    }

    private Prediction? HandleNoDetections()
    {
        _consecutiveFramesWithoutTarget++;
        _targetLockScore *= LockDecayPerMiss;

        if (_consecutiveFramesWithoutTarget >= MaxFramesWithoutTarget || _targetLockScore < 1f)
        {
            Reset();
            return null;
        }

        // Coast: synthesise a prediction at last pose + velocity for lock continuity.
        if (_currentTarget != null && _hasLastPose)
        {
            _lastCx += _lastTargetVelocityX;
            _lastCy += _lastTargetVelocityY;
            _currentTarget.CenterXTranslated = _lastCx;
            _currentTarget.CenterYTranslated = _lastCy;
        }

        return _currentTarget;
    }

    private Prediction AcquireNewTarget(Prediction best)
    {
        float prevX = _currentTarget?.CenterXTranslated ?? best.CenterXTranslated;
        float prevY = _currentTarget?.CenterYTranslated ?? best.CenterYTranslated;

        _currentTarget = best;
        float vx = best.CenterXTranslated - prevX;
        float vy = best.CenterYTranslated - prevY;
        _lastTargetVelocityX = _lastTargetVelocityX * (1 - VelocityEma) + vx * VelocityEma;
        _lastTargetVelocityY = _lastTargetVelocityY * (1 - VelocityEma) + vy * VelocityEma;
        _targetLockScore = Math.Max(_targetLockScore, 25f);
        _hasLastPose = true;
        _lastCx = best.CenterXTranslated;
        _lastCy = best.CenterYTranslated;
        _lastW = best.Rectangle.Width;
        _lastH = best.Rectangle.Height;
        return best;
    }

    private Prediction UpdateHeldTarget(Prediction best, float maxLockScore)
    {
        float vx = best.CenterXTranslated - (_currentTarget?.CenterXTranslated ?? best.CenterXTranslated);
        float vy = best.CenterYTranslated - (_currentTarget?.CenterYTranslated ?? best.CenterYTranslated);
        _lastTargetVelocityX = _lastTargetVelocityX * (1 - VelocityEma) + vx * VelocityEma;
        _lastTargetVelocityY = _lastTargetVelocityY * (1 - VelocityEma) + vy * VelocityEma;

        _currentTarget = best;
        _targetLockScore = Math.Min(maxLockScore, _targetLockScore + 4f);
        _hasLastPose = true;
        _lastCx = best.CenterXTranslated;
        _lastCy = best.CenterYTranslated;
        _lastW = best.Rectangle.Width;
        _lastH = best.Rectangle.Height;
        return best;
    }
}