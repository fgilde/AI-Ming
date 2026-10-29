using System.Drawing;

namespace PowerAim.AI;

/// <summary>The bones of a COCO-17 pose, mapped into the same space as the detection's drawn box.</summary>
public static class PoseSkeleton
{
    private static readonly (int A, int B)[] Bones =
    [
        (15, 13), (13, 11), (16, 14), (14, 12), (11, 12), (5, 11), (6, 12), (5, 6), (5, 7), (6, 8),
        (7, 9), (8, 10), (1, 2), (0, 1), (0, 2), (1, 3), (2, 4), (3, 5), (4, 6),
    ];

    /// <summary>
    ///     Line segments between keypoints both at or above <paramref name="minConfidence"/>, in the coordinates
    ///     of <see cref="Prediction.TranslatedRectangle"/> (model pixels scaled and offset like the box).
    /// </summary>
    public static IEnumerable<(PointF A, PointF B)> Segments(Prediction p, float minConfidence)
    {
        var k = p.Keypoints;
        if (k is not { Length: >= 17 } || p.Rectangle.Width <= 0 || p.Rectangle.Height <= 0) yield break;
        float sx = p.TranslatedRectangle.Width / p.Rectangle.Width;
        float sy = p.TranslatedRectangle.Height / p.Rectangle.Height;
        PointF Map(Keypoint kp) => new(p.TranslatedRectangle.X + (kp.X - p.Rectangle.X) * sx,
                                       p.TranslatedRectangle.Y + (kp.Y - p.Rectangle.Y) * sy);
        foreach (var (a, b) in Bones)
            if (k[a].Confidence >= minConfidence && k[b].Confidence >= minConfidence)
                yield return (Map(k[a]), Map(k[b]));
    }
}
