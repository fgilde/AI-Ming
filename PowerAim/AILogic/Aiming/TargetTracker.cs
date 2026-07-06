using System.Drawing;

namespace PowerAim.AILogic.Aiming;

/// <summary>
///     A lightweight SORT-style multi-object tracker. Each frame it predicts every existing
///     <see cref="TargetTrack"/> forward, greedily associates the frame's detections to tracks by
///     bounding-box IoU, updates matched tracks, spawns tracks for unmatched detections and coasts
///     (keeps alive without a measurement) the rest. This turns YOLO's unordered, occasionally-
///     dropped per-frame boxes into stable identities with continuous position estimates.
///     <para>
///     Greedy nearest-IoU assignment (rather than the Hungarian algorithm) is more than enough for
///     the handful of on-screen targets in an aim scenario and keeps the code matrix-free.
///     </para>
/// </summary>
public sealed class TargetTracker
{
    private readonly List<TargetTrack> _tracks = new();
    private int _nextId = 1;

    // Tunables (mirrored from config each Update so live slider edits apply).
    public int MaxAgeFrames { get; set; } = 8;
    public int MinHits { get; set; } = 3;
    public double IoUThreshold { get; set; } = 0.2;
    public double Alpha { get; set; } = 0.5;
    public double Beta { get; set; } = 0.2;

    public IReadOnlyList<TargetTrack> Tracks => _tracks;

    public void Reset()
    {
        _tracks.Clear();
    }

    /// <summary>
    ///     Run one tracker step. <paramref name="detections"/> are this frame's boxes (model-space),
    ///     <paramref name="dt"/> the time since the previous step in seconds. Returns the confirmed,
    ///     still-alive tracks (including ones currently coasting through a detection gap).
    /// </summary>
    public IReadOnlyList<TargetTrack> Update(IReadOnlyList<Prediction> detections, double dt)
    {
        // 1) Predict every track forward.
        foreach (var t in _tracks) t.Predict(dt);

        // 2) Greedy IoU association. Collect all (track,detection) IoUs above threshold, then assign
        //    highest-first, skipping tracks/detections already taken.
        int detCount = detections?.Count ?? 0;
        var matchedDet = new bool[detCount];
        var matchedTrack = new bool[_tracks.Count];

        if (detCount > 0 && _tracks.Count > 0)
        {
            var pairs = new List<(double IoU, int TrackIdx, int DetIdx)>();
            for (int ti = 0; ti < _tracks.Count; ti++)
            {
                var tbox = _tracks[ti].Box;
                for (int di = 0; di < detCount; di++)
                {
                    double iou = IoU(tbox, detections![di].Rectangle);
                    if (iou >= IoUThreshold) pairs.Add((iou, ti, di));
                }
            }
            pairs.Sort((a, b) => b.IoU.CompareTo(a.IoU));
            foreach (var (_, ti, di) in pairs)
            {
                if (matchedTrack[ti] || matchedDet[di]) continue;
                matchedTrack[ti] = true;
                matchedDet[di] = true;
                _tracks[ti].Update(detections![di], dt, Alpha, Beta, MinHits);
            }
        }

        // 2b) Distance fallback for whatever IoU couldn't pair (issue #19): a fast view pan — very
        //     much including the aim assist's own correction — shifts every box together, so the
        //     predicted and detected boxes stop overlapping and pure-IoU association tears the
        //     identity apart (the enemy comes back as a NEW id and the selector's hysteresis is
        //     bypassed). Re-attach by centre distance, gated relative to the track's own size so a
        //     track can never grab a detection across the screen.
        if (detCount > 0 && _tracks.Count > 0)
        {
            var fallback = new List<(double DistSq, int TrackIdx, int DetIdx)>();
            for (int ti = 0; ti < _tracks.Count; ti++)
            {
                if (matchedTrack[ti]) continue;
                var t = _tracks[ti];
                double diag = System.Math.Sqrt(t.Width * t.Width + t.Height * t.Height);
                double gate = System.Math.Max(24.0, diag * 1.5);
                double gateSq = gate * gate;
                for (int di = 0; di < detCount; di++)
                {
                    if (matchedDet[di]) continue;
                    var r = detections![di].Rectangle;
                    double dx = (r.X + r.Width / 2.0) - t.X;
                    double dy = (r.Y + r.Height / 2.0) - t.Y;
                    double dSq = dx * dx + dy * dy;
                    if (dSq <= gateSq) fallback.Add((dSq, ti, di));
                }
            }
            fallback.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));
            foreach (var (_, ti, di) in fallback)
            {
                if (matchedTrack[ti] || matchedDet[di]) continue;
                matchedTrack[ti] = true;
                matchedDet[di] = true;
                _tracks[ti].Update(detections![di], dt, Alpha, Beta, MinHits);
            }
        }

        // 3) Unmatched tracks coast; 4) unmatched detections spawn new tracks.
        for (int ti = 0; ti < _tracks.Count; ti++)
            if (!matchedTrack[ti]) _tracks[ti].MarkMissed();

        for (int di = 0; di < detCount; di++)
            if (!matchedDet[di]) _tracks.Add(new TargetTrack(_nextId++, detections![di]));

        // 5) Age out dead tracks.
        _tracks.RemoveAll(t => t.MissedFrames > MaxAgeFrames);

        // Return confirmed, alive tracks (a confirmed track that's briefly coasting still counts).
        var result = new List<TargetTrack>(_tracks.Count);
        foreach (var t in _tracks)
            if (t.Confirmed) result.Add(t);
        return result;
    }

    /// <summary>Intersection-over-union of two boxes. 0 when disjoint, 1 when identical.</summary>
    private static double IoU(RectangleF a, RectangleF b)
    {
        float ix1 = System.Math.Max(a.Left, b.Left);
        float iy1 = System.Math.Max(a.Top, b.Top);
        float ix2 = System.Math.Min(a.Right, b.Right);
        float iy2 = System.Math.Min(a.Bottom, b.Bottom);
        float iw = ix2 - ix1;
        float ih = iy2 - iy1;
        if (iw <= 0 || ih <= 0) return 0;
        double inter = (double)iw * ih;
        double union = (double)a.Width * a.Height + (double)b.Width * b.Height - inter;
        return union <= 0 ? 0 : inter / union;
    }
}
