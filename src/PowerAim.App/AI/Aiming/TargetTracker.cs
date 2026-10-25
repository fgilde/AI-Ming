using System.Drawing;

namespace PowerAim.AI.Aiming;

/// <summary>
/// StrongSORT-style multi-object tracker for PowerAim.
/// NSA Kalman predict + Hungarian IoU/distance cost + Mahalanobis gate.
/// Drop-in replacement for the previous greedy SORT tracker: same
/// <see cref="Update"/> signature and <see cref="TargetTrack"/> public API.
/// </summary>
public sealed class TargetTracker
{
    private readonly List<TargetTrack> _tracks = new();
    private readonly NsaKalmanFilter _kf = new();
    private int _nextId = 1;
    private float[,] _costMatrix = new float[0, 0];

    // χ² 99% for 4 DoF
    private const float MahaGate = 13.28f;

    // Tunables (AimingAction can still overwrite each frame from config).
    public int MaxAgeFrames { get; set; } = 30;
    public int MinHits { get; set; } = 2;
    public double IoUThreshold { get; set; } = 0.25;
    /// <summary>Legacy alpha — unused by Kalman path; kept so callers compile.</summary>
    public double Alpha { get; set; } = 0.5;
    /// <summary>Legacy beta — unused by Kalman path; kept so callers compile.</summary>
    public double Beta { get; set; } = 0.2;
    public float MinConfidence { get; set; } = 0.10f;
    public float MaxCost { get; set; } = 0.90f;

    public IReadOnlyList<TargetTrack> Tracks => _tracks;

    public void Reset()
    {
        _tracks.Clear();
        _nextId = 1;
    }

    public IReadOnlyList<TargetTrack> Update(IReadOnlyList<Prediction> detections, double dt)
    {
        if (dt <= 0) dt = 1.0 / 60.0;

        // 1) Predict
        foreach (var t in _tracks)
            t.Predict(_kf, dt);

        // 2) Filter detections
        var dets = new List<Prediction>();
        if (detections != null)
        {
            for (int i = 0; i < detections.Count; i++)
            {
                var d = detections[i];
                if (d.Confidence < MinConfidence) continue;
                if (d.Rectangle.Width <= 1 || d.Rectangle.Height <= 1) continue;
                dets.Add(d);
            }
        }

        var matches = new List<(int ti, int di)>();
        var unmatchedTracks = new List<int>();
        var unmatchedDets = new List<int>();

        // 3) Associate
        if (_tracks.Count > 0 && dets.Count > 0)
        {
            float[,] cost = BuildCostMatrix(_tracks, dets);
            int[] assignment = Hungarian(cost, _tracks.Count, dets.Count);

            var usedDets = new HashSet<int>();
            for (int ti = 0; ti < _tracks.Count; ti++)
            {
                int di = assignment[ti];
                if (di < 0 || di >= dets.Count)
                {
                    unmatchedTracks.Add(ti);
                    continue;
                }
                if (cost[ti, di] > MaxCost)
                {
                    unmatchedTracks.Add(ti);
                    continue;
                }

                float[] meas = ToXyah(dets[di]);
                float maha = _kf.GatingDistance(_tracks[ti].Mean, _tracks[ti].Covariance, meas);
                if (maha > MahaGate)
                {
                    unmatchedTracks.Add(ti);
                    continue;
                }

                matches.Add((ti, di));
                usedDets.Add(di);
            }
            for (int di = 0; di < dets.Count; di++)
                if (!usedDets.Contains(di)) unmatchedDets.Add(di);
        }
        else
        {
            for (int ti = 0; ti < _tracks.Count; ti++) unmatchedTracks.Add(ti);
            for (int di = 0; di < dets.Count; di++) unmatchedDets.Add(di);
        }

        // 3b) Distance fallback for whatever IoU/Mahalanobis couldn't pair (issue #19): a fast view pan
        //     — very much including the aim assist's own correction — shifts every box together, so
        //     predicted and detected boxes stop overlapping and the identity would tear apart (the enemy
        //     comes back as a NEW track and the selector's hysteresis is bypassed). Re-attach by centre
        //     distance, gated relative to the track's own size so a track never grabs a far detection.
        if (unmatchedTracks.Count > 0 && unmatchedDets.Count > 0)
        {
            var fallback = new List<(double DistSq, int TrackIdx, int DetIdx)>();
            foreach (int ti in unmatchedTracks)
            {
                var tr = _tracks[ti];
                double gate = Math.Max(24.0, Math.Sqrt(tr.Width * tr.Width + tr.Height * tr.Height) * 1.5);
                foreach (int di in unmatchedDets)
                {
                    var r = dets[di].Rectangle;
                    double dx = r.X + r.Width / 2.0 - tr.X;
                    double dy = r.Y + r.Height / 2.0 - tr.Y;
                    double distSq = dx * dx + dy * dy;
                    if (distSq <= gate * gate) fallback.Add((distSq, ti, di));
                }
            }
            fallback.Sort((x, y) => x.DistSq.CompareTo(y.DistSq));
            foreach (var (_, ti, di) in fallback)
            {
                if (!unmatchedTracks.Contains(ti) || !unmatchedDets.Contains(di)) continue;
                matches.Add((ti, di));
                unmatchedTracks.Remove(ti);
                unmatchedDets.Remove(di);
            }
        }

        // 4) Apply
        foreach (var (ti, di) in matches)
            _tracks[ti].Update(_kf, dets[di], MinHits);

        foreach (int ti in unmatchedTracks)
            _tracks[ti].MarkMissed();

        foreach (int di in unmatchedDets)
            _tracks.Add(new TargetTrack(_nextId++, dets[di], _kf));

        _tracks.RemoveAll(t =>
            t.MissedFrames > MaxAgeFrames
            || (!t.Confirmed && t.MissedFrames > 5 && t.Hits <= 1));

        var result = new List<TargetTrack>(_tracks.Count);
        foreach (var t in _tracks)
        {
            if (!t.Confirmed && t.Hits < MinHits) continue;
            result.Add(t);
        }
        return result;
    }

    private void EnsureCostMatrix(int rows, int cols)
    {
        if (_costMatrix.GetLength(0) == rows && _costMatrix.GetLength(1) == cols)
            return;
        _costMatrix = rows > 0 && cols > 0 ? new float[rows, cols] : new float[0, 0];
    }

    private float[,] BuildCostMatrix(List<TargetTrack> tracks, List<Prediction> dets)
    {
        EnsureCostMatrix(tracks.Count, dets.Count);
        var cost = _costMatrix;
        for (int i = 0; i < tracks.Count; i++)
        {
            var tb = tracks[i].Box;
            float tcx = tb.X + tb.Width * 0.5f;
            float tcy = tb.Y + tb.Height * 0.5f;
            for (int j = 0; j < dets.Count; j++)
            {
                var dr = dets[j].Rectangle;
                float iou = IoU(tb.X, tb.Y, tb.Width, tb.Height,
                    dr.X, dr.Y, dr.Width, dr.Height);
                float dcx = dr.X + dr.Width * 0.5f;
                float dcy = dr.Y + dr.Height * 0.5f;
                float dist = MathF.Sqrt((tcx - dcx) * (tcx - dcx) + (tcy - dcy) * (tcy - dcy));
                float scale = Math.Max(20f, Math.Max(tb.Width, tb.Height));
                float distCost = Math.Clamp(dist / (scale * 3f), 0f, 1f);
                cost[i, j] = iou > 0.01f ? (1f - iou) : (0.5f + 0.5f * distCost);
            }
        }
        return cost;
    }

    private static float IoU(float ax, float ay, float aw, float ah,
        float bx, float by, float bw, float bh)
    {
        float x1 = Math.Max(ax, bx);
        float y1 = Math.Max(ay, by);
        float x2 = Math.Min(ax + aw, bx + bw);
        float y2 = Math.Min(ay + ah, by + bh);
        float inter = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        float uni = aw * ah + bw * bh - inter;
        return uni <= 0 ? 0 : inter / uni;
    }

    private static float[] ToXyah(Prediction d)
    {
        float w = Math.Max(d.Rectangle.Width, 1f);
        float h = Math.Max(d.Rectangle.Height, 1f);
        return new[] { d.Rectangle.X + w * 0.5f, d.Rectangle.Y + h * 0.5f, w / h, h };
    }

    private static int[] Hungarian(float[,] cost, int rows, int cols)
    {
        rows = Math.Min(rows, cost.GetLength(0));
        cols = Math.Min(cols, cost.GetLength(1));
        int n = Math.Max(rows, cols);
        float[,] a = new float[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                a[i, j] = (i < rows && j < cols) ? cost[i, j] : 1e3f;

        float[] u = new float[n + 1];
        float[] v = new float[n + 1];
        int[] p = new int[n + 1];
        int[] way = new int[n + 1];

        for (int i = 1; i <= n; i++)
        {
            p[0] = i;
            int j0 = 0;
            float[] minv = new float[n + 1];
            bool[] used = new bool[n + 1];
            for (int j = 0; j <= n; j++) minv[j] = float.PositiveInfinity;

            do
            {
                used[j0] = true;
                int i0 = p[j0];
                float delta = float.PositiveInfinity;
                int j1 = 0;
                for (int j = 1; j <= n; j++)
                {
                    if (used[j]) continue;
                    float cur = a[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cur < minv[j]) { minv[j] = cur; way[j] = j0; }
                    if (minv[j] < delta) { delta = minv[j]; j1 = j; }
                }
                for (int j = 0; j <= n; j++)
                {
                    if (used[j]) { u[p[j]] += delta; v[j] -= delta; }
                    else minv[j] -= delta;
                }
                j0 = j1;
            } while (p[j0] != 0);

            do
            {
                int j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            } while (j0 != 0);
        }

        int[] assignment = new int[rows];
        Array.Fill(assignment, -1);
        for (int j = 1; j <= n; j++)
        {
            if (p[j] == 0) continue;
            int ti = p[j] - 1;
            int di = j - 1;
            if (ti < rows && di < cols) assignment[ti] = di;
        }
        return assignment;
    }
}