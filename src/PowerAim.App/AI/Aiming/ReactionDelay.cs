namespace PowerAim.AI.Aiming;

/// <summary>Holds the aim back for a reaction time after a target appears; losing the target restarts it.</summary>
public sealed class ReactionDelay
{
    private double _sinceMs = double.NaN;

    public void Reset() => _sinceMs = double.NaN;

    public bool Ready(bool hasTarget, double nowMs, double delayMs)
    {
        if (!hasTarget)
        {
            _sinceMs = double.NaN;
            return false;
        }
        if (double.IsNaN(_sinceMs)) _sinceMs = nowMs;
        return nowMs - _sinceMs >= delayMs;
    }
}
