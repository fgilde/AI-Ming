using System.Drawing;

namespace PowerAim.AI;

/// <summary>
///     Third-person games: drops the player's own character, which a person model detects like anyone else.
///     <para>
///     The capture is centred on the crosshair, and in third person the own character stands right next to it,
///     reaches out of the bottom of the captured area and stays put relative to it however the view turns.
///     A large box that reaches out of the bottom and stays put for <see cref="LearnMs"/> is taken for "me" and
///     followed frame to frame (so it stays "me" when it moves or changes shape); boxes inside it (my head)
///     go with it. Where "I" was is remembered for <see cref="MemoryMs"/>: after a pose change (cover, aiming)
///     the new box there is "me" at once. Boxes filling most of the capture (the camera inside my shoulder)
///     are dropped but neither remembered nor used to hide boxes inside them.
///     </para>
/// </summary>
public sealed class SelfFilter
{
    public const double LearnMs = 400;
    public const double MemoryMs = 10_000;
    private const float JitterShare = 0.05f;       // of the capture size: how far "staying put" may wander
    private const float BottomShare = 0.97f;        // reaches out of the bottom
    private const float MinHeightShare = 0.30f;
    private const float HugeAreaShare = 0.50f;

    private sealed class Track
    {
        public RectangleF Box;
        public PointF StillFrom;
        public double StillMs;
        public bool IsSelf;
    }

    private List<Track> _tracks = [];
    private readonly List<(RectangleF Box, double Ms)> _memory = [];
    private double? _lastMs;

    public void Reset()
    {
        if (_lastMs == null) return;   // called every frame while off: nothing to clear
        _tracks = [];
        _memory.Clear();
        _lastMs = null;
    }

    /// <summary>Returns the predictions that are not the player's own character (model-space boxes, 0..size).</summary>
    public List<Prediction> Apply(IReadOnlyList<Prediction> predictions, int size, double nowMs)
    {
        double dt = _lastMs is { } last ? Math.Clamp(nowMs - last, 0, 250) : 0;
        _lastMs = nowMs;
        _memory.RemoveAll(m => nowMs - m.Ms > MemoryMs);

        float jitter = JitterShare * size;
        var used = new HashSet<Track>();
        var tracks = new List<Track>(predictions.Count);
        foreach (var p in predictions)
        {
            var box = p.Rectangle;
            var prev = _tracks.Where(t => !used.Contains(t)).MaxBy(t => IoU(box, t.Box));
            var track = new Track { Box = box, StillFrom = Centre(box) };
            if (prev != null && IoU(box, prev.Box) > 0.3f)
            {
                used.Add(prev);
                track.IsSelf = prev.IsSelf;
                if (Distance(Centre(box), prev.StillFrom) < jitter)
                {
                    track.StillFrom = prev.StillFrom;
                    track.StillMs = prev.StillMs + dt;
                }
            }

            bool reachesOut = box.Bottom >= BottomShare * size;
            if (!track.IsSelf && reachesOut && box.Height > MinHeightShare * size && track.StillMs >= LearnMs)
                track.IsSelf = true;
            // Where "I" was a moment ago: a new box reaching out of the bottom there is me in another pose.
            if (!track.IsSelf && reachesOut && _memory.Any(m => IoU(box, m.Box) > 0.3f))
                track.IsSelf = true;
            tracks.Add(track);
        }
        _tracks = tracks;

        float huge = HugeAreaShare * size * size;
        var me = tracks.Where(t => t.IsSelf && Area(t.Box) < huge).Select(t => t.Box).ToList();
        foreach (var box in me) _memory.Add((box, nowMs));
        if (_memory.Count > 64) _memory.RemoveRange(0, _memory.Count - 64);

        var kept = new List<Prediction>(predictions.Count);
        for (int i = 0; i < predictions.Count; i++)
        {
            var box = tracks[i].Box;
            if (tracks[i].IsSelf || me.Any(m => InsideShare(box, m) > 0.7f)) continue;
            kept.Add(predictions[i]);
        }
        return kept;
    }

    private static PointF Centre(RectangleF r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
    private static float Distance(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static float Area(RectangleF r) => r.Width * r.Height;

    private static float IoU(RectangleF a, RectangleF b)
    {
        var i = RectangleF.Intersect(a, b);
        if (i.IsEmpty) return 0;
        float inter = Area(i);
        return inter / (Area(a) + Area(b) - inter);
    }

    /// <summary>Share of <paramref name="a"/> that lies inside <paramref name="b"/>.</summary>
    private static float InsideShare(RectangleF a, RectangleF b)
    {
        var i = RectangleF.Intersect(a, b);
        return i.IsEmpty || Area(a) <= 0 ? 0 : Area(i) / Area(a);
    }
}
