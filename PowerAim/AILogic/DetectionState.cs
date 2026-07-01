namespace PowerAim.AILogic;

/// <summary>
///     Caches the most recent detector output so code outside the detection loop — chiefly the custom
///     tool / workflow runner — can read the latest and "best" target as variables (e.g. <c>{target.x}</c>).
///     <see cref="AIManager"/> pushes each frame's predictions here right where it reports them, so it's
///     always current without any subscription-timing gap.
/// </summary>
public static class DetectionState
{
    private static volatile Prediction[] _latest = System.Array.Empty<Prediction>();

    /// <summary>Called once per inference tick by <see cref="AIManager"/>.</summary>
    public static void Set(Prediction[]? predictions) => _latest = predictions ?? System.Array.Empty<Prediction>();

    /// <summary>The detections from the most recent frame (empty if none / detector idle).</summary>
    public static Prediction[] Latest => _latest;

    /// <summary>The highest-confidence detection from the last frame, or null if there were none.</summary>
    public static Prediction? Best
    {
        get
        {
            var arr = _latest;
            if (arr.Length == 0) return null;
            var best = arr[0];
            for (var i = 1; i < arr.Length; i++)
                if (arr[i].Confidence > best.Confidence) best = arr[i];
            return best;
        }
    }
}
