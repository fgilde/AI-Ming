namespace PowerAim.AI.Actions;

/// <summary>True once a condition has held without interruption for the dwell time; any false frame restarts it.</summary>
public sealed class DwellGate
{
    private double _sinceMs = double.NaN;

    public bool Update(bool condition, double nowMs, double dwellMs)
    {
        if (!condition)
        {
            _sinceMs = double.NaN;
            return false;
        }
        if (double.IsNaN(_sinceMs)) _sinceMs = nowMs;
        return nowMs - _sinceMs >= dwellMs;
    }
}
