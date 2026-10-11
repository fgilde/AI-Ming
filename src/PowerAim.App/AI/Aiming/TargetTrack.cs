using System.Drawing;

namespace PowerAim.AI.Aiming;

/// <summary>
/// One persistent tracked target using NSA Kalman filter (StrongSORT-style).
/// Public surface matches the previous alpha-beta track so TargetSelector /
/// AimingAction keep working unchanged.
/// </summary>
public sealed class TargetTrack
{
    public int Id { get; }

    public double X { get; private set; }
    public double Y { get; private set; }
    public double Vx { get; private set; }
    public double Vy { get; private set; }
    public double Width { get; private set; }
    public double Height { get; private set; }

    public float Confidence { get; private set; }
    public int ClassId { get; private set; }
    public string ClassName { get; private set; } = "Enemy";

    public int Hits { get; private set; }
    public int MissedFrames { get; private set; }
    public bool Confirmed { get; private set; }

    public RectangleF Box => new(
        (float)(X - Width / 2.0), (float)(Y - Height / 2.0), (float)Width, (float)Height);

    /// <summary>Raw last matched detection box (aim prefers this over filtered centre).</summary>
    public RectangleF LastDetectionBox { get; private set; }

    // Kalman state (cx, cy, a, h, vx, vy, va, vh)
    internal float[] Mean = new float[8];
    internal float[,] Covariance = new float[8, 8];

    public TargetTrack(int id, Prediction det, NsaKalmanFilter kf)
    {
        Id = id;
        float w = Math.Max(det.Rectangle.Width, 1f);
        float h = Math.Max(det.Rectangle.Height, 1f);
        float cx = det.Rectangle.X + w * 0.5f;
        float cy = det.Rectangle.Y + h * 0.5f;
        float[] meas = { cx, cy, w / h, h };
        kf.Initiate(meas, out Mean, out Covariance);
        SyncFromMean();
        LastDetectionBox = det.Rectangle;
        Confidence = det.Confidence;
        ClassId = det.ClassId;
        ClassName = det.ClassName ?? "Enemy";
        Hits = 1;
        MissedFrames = 0;
    }

    /// <summary>Legacy ctor kept for any external callers — uses a throwaway KF initiate.</summary>
    public TargetTrack(int id, Prediction det)
        : this(id, det, new NsaKalmanFilter())
    {
    }

    public void Predict(NsaKalmanFilter kf, double dt)
    {
        // Filter is unit-dt (1 frame). Scale velocity by dt*60 so variable FPS stays sane.
        float scale = dt > 0 ? (float)(dt * 60.0) : 1f;
        if (Math.Abs(scale - 1f) > 1e-3f)
        {
            Mean[4] *= scale;
            Mean[5] *= scale;
            Mean[6] *= scale;
            Mean[7] *= scale;
        }
        kf.Predict(ref Mean, ref Covariance);
        if (Math.Abs(scale - 1f) > 1e-3f && scale != 0)
        {
            Mean[4] /= scale;
            Mean[5] /= scale;
            Mean[6] /= scale;
            Mean[7] /= scale;
        }
        SyncFromMean();
        MissedFrames++; // will be reset to 0 on Update
    }

    public void Update(NsaKalmanFilter kf, Prediction det, int minHits)
    {
        float w = Math.Max(det.Rectangle.Width, 1f);
        float h = Math.Max(det.Rectangle.Height, 1f);
        float cx = det.Rectangle.X + w * 0.5f;
        float cy = det.Rectangle.Y + h * 0.5f;
        float[] meas = { cx, cy, w / h, h };
        kf.Update(ref Mean, ref Covariance, meas, det.Confidence);
        SyncFromMean();
        LastDetectionBox = det.Rectangle;
        Confidence = det.Confidence;
        ClassId = det.ClassId;
        ClassName = det.ClassName ?? ClassName;
        Hits++;
        MissedFrames = 0;
        if (Hits >= minHits) Confirmed = true;
    }

    public void MarkMissed()
    {
        // MissedFrames already bumped in Predict; keep API.
    }

    internal float[] MeasurementXyah(Prediction det)
    {
        float w = Math.Max(det.Rectangle.Width, 1f);
        float h = Math.Max(det.Rectangle.Height, 1f);
        return new[] { det.Rectangle.X + w * 0.5f, det.Rectangle.Y + h * 0.5f, w / h, h };
    }

    private void SyncFromMean()
    {
        float cx = Mean[0];
        float cy = Mean[1];
        float a = Math.Max(Mean[2], 1e-3f);
        float h = Math.Max(Mean[3], 1f);
        float w = a * h;
        X = cx;
        Y = cy;
        Width = w;
        Height = h;
        // Velocity is in model-px per frame at 60Hz baseline; expose as px/sec.
        Vx = Mean[4] * 60.0;
        Vy = Mean[5] * 60.0;
    }
}