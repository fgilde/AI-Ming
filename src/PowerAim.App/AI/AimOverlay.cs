using System.Drawing;

namespace PowerAim.AI;

/// <summary>
///     The aim point the aim is steering at and where it is expected to be once the moves still in flight
///     have turned the view (capture-area coordinates, like <see cref="Prediction.TranslatedRectangle"/>).
///     Published by the aim each frame, drawn by the overlay when "show aim point" is on.
/// </summary>
public static class AimOverlay
{
    private const long MaxAgeMs = 150;
    private static readonly object Gate = new();
    private static (PointF Aim, PointF Predicted, long AtMs)? _last;

    public static void Publish(PointF aim, PointF predicted, long nowMs)
    {
        lock (Gate) _last = (aim, predicted, nowMs);
    }

    public static void Clear()
    {
        lock (Gate) _last = null;
    }

    /// <summary>The latest point unless it is older than 150 ms (aim stopped).</summary>
    public static bool TryGet(long nowMs, out PointF aim, out PointF predicted)
    {
        lock (Gate)
        {
            if (_last is { } l && nowMs - l.AtMs <= MaxAgeMs)
            {
                (aim, predicted) = (l.Aim, l.Predicted);
                return true;
            }
        }
        aim = predicted = default;
        return false;
    }

    /// <summary>A model-space point in capture-area coordinates, mapped like <paramref name="reference"/>'s box.</summary>
    public static PointF ToScreen(double modelX, double modelY, Prediction reference)
    {
        var r = reference.Rectangle;
        var t = reference.TranslatedRectangle;
        float sx = r.Width > 0 ? t.Width / r.Width : 1, sy = r.Height > 0 ? t.Height / r.Height : 1;
        return new PointF(t.X + (float)(modelX - r.X) * sx, t.Y + (float)(modelY - r.Y) * sy);
    }
}
