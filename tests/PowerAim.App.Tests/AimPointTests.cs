using System.Drawing;
using PowerAim.AI;
using PowerAim.AI.Aiming;
using PowerAim.Config;
using PowerAim.Types;

namespace PowerAim.App.Tests;

public class AimPointTests
{
    private static Prediction Det(int cls, float conf = 0.9f, float x = 300, float y = 300) => new()
    {
        ClassId = cls,
        Confidence = conf,
        Rectangle = new RectangleF(x, y, 40, 80),
    };

    // {0 enemy, 1 teammate, 2 head}
    private static readonly ClassTargetSpec[] Specs =
    [
        new() { ClassId = 2, Priority = 0, UseAnchor = true, AnchorX = 0.5f, AnchorY = 0.5f },
        new() { ClassId = 0, Priority = 1, UseAnchor = true, AnchorX = 0.5f, AnchorY = 0.25f },
        new() { ClassId = 1, Ignored = true },
    ];

    [Fact]
    public void Highest_priority_class_wins()
    {
        var kept = ClassTargeting.Filter([Det(0), Det(2), Det(1)], Specs);

        Assert.Equal(2, Assert.Single(kept).ClassId);
    }

    [Fact]
    public void Lower_priority_class_is_used_when_the_better_one_is_absent()
        => Assert.Equal(0, Assert.Single(ClassTargeting.Filter([Det(0), Det(1)], Specs)).ClassId);

    [Fact]
    public void Ignored_class_is_never_kept()
        => Assert.Empty(ClassTargeting.Filter([Det(1)], Specs));

    [Fact]
    public void Unprioritised_class_is_a_fallback_only()
    {
        ClassTargetSpec[] specs = [new() { ClassId = 0, Priority = 0 }];

        Assert.Equal(0, Assert.Single(ClassTargeting.Filter([Det(0), Det(5)], specs)).ClassId);
        Assert.Equal(5, Assert.Single(ClassTargeting.Filter([Det(5)], specs)).ClassId);
    }

    [Fact]
    public void Per_class_confidence_floor()
    {
        ClassTargetSpec[] specs = [new() { ClassId = 0, MinConfidence = 0.8f }];

        Assert.Empty(ClassTargeting.Filter([Det(0, conf: 0.7f)], specs));
        Assert.Single(ClassTargeting.Filter([Det(0, conf: 0.85f)], specs));
    }

    [Fact]
    public void No_specs_keep_everything()
        => Assert.Equal(3, ClassTargeting.Filter([Det(0), Det(1), Det(2)], null).Length);

    private static readonly RelativeRect Center = new(0f, 0f, 0.5f, 0.5f);

    [Fact]
    public void Without_options_the_region_point_is_used()
    {
        var (x, y) = AimPoint.Compute(new RectangleF(100, 100, 40, 80), 0, null, null, AimPointOptions.None,
            new RelativeRect(0.5f, 0.2f, 0.25f, 0.1f), 0.5, 0.5, 640);

        Assert.Equal(100 + 40 * 0.25 + 20 * 0.5, x, 3);
        Assert.Equal(100 + 80 * 0.1 + 16 * 0.5, y, 3);
    }

    [Fact]
    public void Class_anchor_overrides_the_region()
    {
        var (x, y) = AimPoint.Compute(new RectangleF(100, 100, 40, 80), 0, null, Specs[1], AimPointOptions.None,
            Center, 0.5, 0.5, 640);

        Assert.Equal(120, x, 3);
        Assert.Equal(120, y, 3);
    }

    [Fact]
    public void Flat_offset_of_the_class_is_added()
    {
        var spec = new ClassTargetSpec { ClassId = 0, UseAnchor = true, AnchorX = 0.5f, AnchorY = 0.5f, FlatOffsetX = 3, FlatOffsetY = -4 };
        var (x, y) = AimPoint.Compute(new RectangleF(100, 100, 40, 80), 0, null, spec, AimPointOptions.None,
            Center, 0.5, 0.5, 640);

        Assert.Equal(123, x, 3);
        Assert.Equal(136, y, 3);
    }

    [Fact]
    public void Additional_offset_is_always_added()
    {
        var o = AimPointOptions.None with { AdditionalOffsetX = 5, AdditionalOffsetY = 7 };
        var (x, y) = AimPoint.Compute(new RectangleF(100, 100, 40, 80), 0, null, Specs[1], o, Center, 0.5, 0.5, 640);

        Assert.Equal(125, x, 3);
        Assert.Equal(127, y, 3);
    }

    [Fact]
    public void Dynamic_offset_pulls_small_boxes_to_the_centre()
    {
        var o = AimPointOptions.None with { DynamicOffset = true, DynamicMinAreaPct = 0.05, DynamicMaxAreaPct = 1.5 };
        // 8x16 box in a 640 model = 0.03 % of the area → below min → aim at centre.
        var (_, ySmall) = AimPoint.Compute(new RectangleF(100, 100, 8, 16), 0, null, Specs[1], o, Center, 0.5, 0.5, 640);
        Assert.Equal(108, ySmall, 3);

        // 80x160 box = 3.1 % → above max → full anchor (0.25).
        var (_, yLarge) = AimPoint.Compute(new RectangleF(100, 100, 80, 160), 0, null, Specs[1], o, Center, 0.5, 0.5, 640);
        Assert.Equal(140, yLarge, 3);
    }

    [Fact]
    public void Dynamic_offset_interpolates_linearly()
    {
        var o = AimPointOptions.None with { DynamicOffset = true, DynamicMinAreaPct = 0, DynamicMaxAreaPct = 2 };
        // 64x64 in 640 → 1 % → t = 0.5 → halfway between centre (132) and anchor 0.25 (116).
        var (_, y) = AimPoint.Compute(new RectangleF(100, 100, 64, 64), 0, null, Specs[1], o, Center, 0.5, 0.5, 640);
        Assert.Equal(124, y, 3);
    }

    private static Keypoint[] Kps(params (int Index, float X, float Y, float C)[] set)
    {
        var k = new Keypoint[17];
        foreach (var (i, x, y, c) in set) k[i] = new Keypoint(x, y, c);
        return k;
    }

    private static readonly RectangleF Body = new(100, 100, 40, 100);

    [Fact]
    public void Head_uses_the_nose()
    {
        var o = AimPointOptions.None with { Pose = PoseAimTarget.Head, KeypointMinConfidence = 0.5f };
        var p = AimPoint.Compute(Body, 0, Kps((0, 121, 108, 0.9f), (1, 118, 106, 0.9f)), null, o, Center, 0.5, 0.5, 640);
        Assert.Equal((121.0, 108.0), (Math.Round(p.X, 3), Math.Round(p.Y, 3)));
    }

    [Fact]
    public void Head_falls_back_to_the_eyes()
    {
        var o = AimPointOptions.None with { Pose = PoseAimTarget.Head, KeypointMinConfidence = 0.5f };
        var both = AimPoint.Compute(Body, 0, Kps((0, 121, 108, 0.2f), (1, 116, 106, 0.9f), (2, 124, 104, 0.9f)), null, o, Center, 0.5, 0.5, 640);
        Assert.Equal((120.0, 105.0), (Math.Round(both.X, 3), Math.Round(both.Y, 3)));

        var one = AimPoint.Compute(Body, 0, Kps((2, 124, 104, 0.9f)), null, o, Center, 0.5, 0.5, 640);
        Assert.Equal((124.0, 104.0), (Math.Round(one.X, 3), Math.Round(one.Y, 3)));
    }

    [Fact]
    public void Head_without_keypoints_uses_the_box()
    {
        var o = AimPointOptions.None with { Pose = PoseAimTarget.Head, KeypointMinConfidence = 0.5f };
        var p = AimPoint.Compute(Body, 0, Kps(), null, o, Center, 0.5, 0.5, 640);
        Assert.Equal((120.0, 150 - 35.0), (Math.Round(p.X, 3), Math.Round(p.Y, 3)));
    }

    [Fact]
    public void Chest_uses_the_shoulders_or_the_box()
    {
        var o = AimPointOptions.None with { Pose = PoseAimTarget.Chest, KeypointMinConfidence = 0.5f };
        var both = AimPoint.Compute(Body, 0, Kps((5, 130, 120, 0.9f), (6, 110, 124, 0.9f)), null, o, Center, 0.5, 0.5, 640);
        Assert.Equal((120.0, 122.0), (Math.Round(both.X, 3), Math.Round(both.Y, 3)));

        var none = AimPoint.Compute(Body, 0, Kps(), null, o, Center, 0.5, 0.5, 640);
        Assert.Equal((120.0, 130.0), (Math.Round(none.X, 3), Math.Round(none.Y, 3)));
    }

    [Fact]
    public void Keypoint_outside_the_frame_is_invalid()
    {
        var o = AimPointOptions.None with { Pose = PoseAimTarget.Head, KeypointMinConfidence = 0.5f };
        var p = AimPoint.Compute(Body, 0, Kps((0, -3, 108, 0.9f)), null, o, Center, 0.5, 0.5, 640);
        Assert.Equal(115.0, Math.Round(p.Y, 3));
    }

    [Fact]
    public void Pose_is_ignored_for_models_without_keypoints()
    {
        var o = AimPointOptions.None with { Pose = PoseAimTarget.Head };
        var p = AimPoint.Compute(Body, 0, null, Specs[1], o, Center, 0.5, 0.5, 640);
        Assert.Equal(125.0, Math.Round(p.Y, 3));
    }
}
