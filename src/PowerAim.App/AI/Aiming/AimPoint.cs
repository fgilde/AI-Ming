using System.Drawing;
using PowerAim.Config;
using PowerAim.Types;

namespace PowerAim.AI.Aiming;

/// <summary>Drops detections the aim must not consider, using the profile's per-class rules.</summary>
public static class ClassTargeting
{
    /// <summary>
    ///     Removes ignored classes and detections below their class confidence, then keeps only the
    ///     best visible priority. Classes without a priority (-1 or no rule) are kept only when no
    ///     prioritised class is visible.
    /// </summary>
    public static Prediction[] Filter(Prediction[] predictions, IReadOnlyList<ClassTargetSpec>? specs)
    {
        if (specs == null || specs.Count == 0 || predictions.Length == 0) return predictions;

        ClassTargetSpec? SpecOf(int cls)
        {
            foreach (var s in specs) if (s.ClassId == cls) return s;
            return null;
        }

        var kept = new List<(Prediction P, int Priority)>(predictions.Length);
        int best = int.MaxValue;
        foreach (var p in predictions)
        {
            var spec = SpecOf(p.ClassId);
            if (spec is { Ignored: true }) continue;
            if (spec != null && p.Confidence < spec.MinConfidence) continue;
            int priority = spec?.Priority ?? -1;
            if (priority >= 0) best = Math.Min(best, priority);
            kept.Add((p, priority));
        }

        return best == int.MaxValue
            ? kept.Select(k => k.P).ToArray()
            : kept.Where(k => k.Priority == best).Select(k => k.P).ToArray();
    }
}

/// <summary>Aim-point options of an <see cref="AimProfile"/> beyond its aim region.</summary>
/// <param name="DynamicOffset">Pull the aim point toward the box centre for small (far) boxes.</param>
/// <param name="DynamicMinAreaPct">Box area (% of the model frame) at or below which the box centre is used.</param>
/// <param name="DynamicMaxAreaPct">Box area (% of the model frame) from which the full offset applies.</param>
/// <param name="AdditionalOffsetX">Fixed shift added last, in model pixels.</param>
/// <param name="AdditionalOffsetY">Fixed shift added last, in model pixels.</param>
/// <param name="Pose">Aim at keypoints of pose models.</param>
/// <param name="KeypointMinConfidence">Keypoints below this confidence are ignored.</param>
public readonly record struct AimPointOptions(
    bool DynamicOffset,
    double DynamicMinAreaPct,
    double DynamicMaxAreaPct,
    double AdditionalOffsetX,
    double AdditionalOffsetY,
    PoseAimTarget Pose,
    float KeypointMinConfidence)
{
    public static AimPointOptions None => new(false, 0.05, 1.5, 0, 0, PoseAimTarget.Off, 0.5f);
}

/// <summary>Where inside a detection to aim (model space).</summary>
public static class AimPoint
{
    private const int Nose = 0, LeftEye = 1, RightEye = 2, LeftShoulder = 5, RightShoulder = 6;

    /// <summary>
    ///     Priority: pose keypoints (when enabled and present) → the class anchor → the aim region at
    ///     <paramref name="fracX"/>/<paramref name="fracY"/>. The dynamic offset scales the vertical
    ///     distance from the box centre for anchor and region; the additional offset is added last.
    /// </summary>
    public static (double X, double Y) Compute(RectangleF box, int classId, Keypoint[]? keypoints,
        ClassTargetSpec? spec, AimPointOptions o, RelativeRect region, double fracX, double fracY, float modelSize)
    {
        double cx = box.X + box.Width / 2.0;
        double cy = box.Y + box.Height / 2.0;
        double x, y;

        if (o.Pose != PoseAimTarget.Off && keypoints is { Length: > RightShoulder })
        {
            (x, y) = PosePoint(box, keypoints, o.Pose, o.KeypointMinConfidence, modelSize);
        }
        else
        {
            if (spec is { UseAnchor: true })
            {
                x = box.X + spec.AnchorX * box.Width + spec.FlatOffsetX;
                y = box.Y + spec.AnchorY * box.Height + spec.FlatOffsetY;
            }
            else
            {
                x = box.X + box.Width * region.LeftMarginPercentage + box.Width * region.WidthPercentage * fracX;
                y = box.Y + box.Height * region.TopMarginPercentage + box.Height * region.HeightPercentage * fracY;
            }

            if (o.DynamicOffset && o.DynamicMaxAreaPct > o.DynamicMinAreaPct && modelSize > 0)
            {
                double areaPct = box.Width * box.Height / (modelSize * (double)modelSize) * 100.0;
                double t = Math.Clamp((areaPct - o.DynamicMinAreaPct) / (o.DynamicMaxAreaPct - o.DynamicMinAreaPct), 0, 1);
                y = cy + (y - cy) * t;
            }
        }

        return (x + o.AdditionalOffsetX, y + o.AdditionalOffsetY);
    }

    private static (double X, double Y) PosePoint(RectangleF box, Keypoint[] k, PoseAimTarget target, float minConf, float modelSize)
    {
        bool Ok(int i) => k[i].Confidence >= minConf && k[i].X >= 0 && k[i].Y >= 0 && k[i].X <= modelSize && k[i].Y <= modelSize;
        double cx = box.X + box.Width / 2.0;
        double cy = box.Y + box.Height / 2.0;

        if (target == PoseAimTarget.Head)
        {
            if (Ok(Nose)) return (k[Nose].X, k[Nose].Y);
            if (Ok(LeftEye) && Ok(RightEye)) return ((k[LeftEye].X + k[RightEye].X) / 2.0, (k[LeftEye].Y + k[RightEye].Y) / 2.0);
            if (Ok(LeftEye)) return (k[LeftEye].X, k[LeftEye].Y);
            if (Ok(RightEye)) return (k[RightEye].X, k[RightEye].Y);
            return (cx, cy - 0.35 * box.Height);
        }

        if (Ok(LeftShoulder) && Ok(RightShoulder))
            return ((k[LeftShoulder].X + k[RightShoulder].X) / 2.0, (k[LeftShoulder].Y + k[RightShoulder].Y) / 2.0);
        if (Ok(LeftShoulder)) return (k[LeftShoulder].X, k[LeftShoulder].Y);
        if (Ok(RightShoulder)) return (k[RightShoulder].X, k[RightShoulder].Y);
        return (cx, cy - 0.20 * box.Height);
    }
}
