using System.Drawing;

namespace PowerAim.AILogic.Aiming;

/// <summary>
///     One persistent tracked target. Holds a constant-velocity state estimate of the box centre
///     (model-space pixels) using a steady-state alpha-beta filter — the matrix-free equivalent of
///     a constant-velocity Kalman filter, which is all we need and trivial to reason about.
///     <para>
///     A track survives short detection drop-outs by "coasting": when no detection matches it for a
///     frame, the centre is advanced by its estimated velocity instead of being discarded. That is
///     the core fix for "the AI sees an enemy, aims, then the box flickers off for a frame and the
///     aim snaps away" — the track keeps the identity and a plausible position across the gap.
///     </para>
/// </summary>
public sealed class TargetTrack
{
    public int Id { get; }

    // Estimated box centre + velocity (model-space px, px/sec) and smoothed box size.
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Vx { get; private set; }
    public double Vy { get; private set; }
    public double Width { get; private set; }
    public double Height { get; private set; }

    public float Confidence { get; private set; }
    public int ClassId { get; private set; }
    public string ClassName { get; private set; } = "Enemy";

    /// <summary>Total matched detections — a track is only "confirmed" after enough of them.</summary>
    public int Hits { get; private set; }

    /// <summary>Consecutive frames with no matching detection (0 right after a match).</summary>
    public int MissedFrames { get; private set; }

    /// <summary>Sticky once set — a confirmed track stays confirmed even while briefly coasting.</summary>
    public bool Confirmed { get; private set; }

    public RectangleF Box => new(
        (float)(X - Width / 2.0), (float)(Y - Height / 2.0), (float)Width, (float)Height);

    public TargetTrack(int id, Prediction det)
    {
        Id = id;
        X = det.Rectangle.X + det.Rectangle.Width / 2.0;
        Y = det.Rectangle.Y + det.Rectangle.Height / 2.0;
        Width = det.Rectangle.Width;
        Height = det.Rectangle.Height;
        Confidence = det.Confidence;
        ClassId = det.ClassId;
        ClassName = det.ClassName;
        Hits = 1;
        MissedFrames = 0;
    }

    /// <summary>Advance the centre by the estimated velocity. Called once per frame for every track.</summary>
    public void Predict(double dt)
    {
        X += Vx * dt;
        Y += Vy * dt;
    }

    /// <summary>
    ///     Correct the (already-predicted) state toward a matched detection with an alpha-beta
    ///     update. <paramref name="alpha"/> drives the position correction, <paramref name="beta"/>
    ///     the velocity correction.
    /// </summary>
    public void Update(Prediction det, double dt, double alpha, double beta, int minHits)
    {
        if (dt <= 0) dt = 1.0 / 60.0;
        double measX = det.Rectangle.X + det.Rectangle.Width / 2.0;
        double measY = det.Rectangle.Y + det.Rectangle.Height / 2.0;

        double rx = measX - X;
        double ry = measY - Y;
        X += alpha * rx;
        Y += alpha * ry;
        Vx += beta * rx / dt;
        Vy += beta * ry / dt;

        // Box size + class follow the latest detection with light EMA smoothing.
        Width = Width * 0.6 + det.Rectangle.Width * 0.4;
        Height = Height * 0.6 + det.Rectangle.Height * 0.4;
        Confidence = det.Confidence;
        ClassId = det.ClassId;
        ClassName = det.ClassName;

        Hits++;
        MissedFrames = 0;
        if (Hits >= minHits) Confirmed = true;
    }

    /// <summary>No detection matched this frame — the track coasts on its velocity.</summary>
    public void MarkMissed() => MissedFrames++;
}
