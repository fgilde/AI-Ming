using System.Drawing;
using Microsoft.ML.OnnxRuntime.Tensors;
using PowerAim.AI;
using PowerAim.Config;

namespace PowerAim.App.Tests;

public class IgnoreZoneTests
{
    private static List<Prediction> Parse(float[] box, IReadOnlyList<DetectionMaskRegion>? zones = null, float minOverlap = 0,
        bool circular = false)
    {
        var t = new DenseTensor<float>([1, 5, 8400]);
        for (int c = 0; c < 5; c++) t[0, c, 0] = box[c];
        return PredictionFilter.CreatePredictions(t, new Rectangle(0, 0, 640, 640), 640, 0, 1,
            new Dictionary<int, string> { [0] = "Enemy" }, 0.5f, null, 0, 640, 0, 640, zones,
            ignoreMinOverlap: minOverlap, circularFov: circular);
    }

    [Fact]
    public void A_box_reaching_past_the_frame_edge_is_clipped_not_dropped()
    {
        // centre 620, width 80 → 580..660, 20 px outside the 640 frame.
        var p = Assert.Single(Parse([620, 300, 80, 100, 0.9f]));

        Assert.Equal(new RectangleF(580, 250, 60, 100), p.Rectangle);
    }

    [Fact]
    public void A_box_entirely_outside_is_dropped()
        => Assert.Empty(Parse([700, 300, 40, 40, 0.9f]));

    // Zone covering x 0..320 (left half).
    private static readonly DetectionMaskRegion[] LeftHalf = [new() { X = 0, Y = 0, Width = 0.5, Height = 1 }];

    [Fact]
    public void Centre_inside_a_zone_always_suppresses()
        => Assert.Empty(Parse([300, 300, 100, 100, 0.9f], LeftHalf));

    [Fact]
    public void Overlap_alone_does_not_suppress_by_default()
        // 290..390 → 30 % inside the zone, centre outside.
        => Assert.Single(Parse([340, 300, 100, 100, 0.9f], LeftHalf));

    [Fact]
    public void Overlap_above_the_minimum_suppresses()
    {
        Assert.Empty(Parse([340, 300, 100, 100, 0.9f], LeftHalf, minOverlap: 0.25f));
        Assert.Single(Parse([340, 300, 100, 100, 0.9f], LeftHalf, minOverlap: 0.4f));
    }

    [Fact]
    public void Circular_fov_drops_corner_detections()
    {
        // Centre (40, 40) is inside the square frame but outside the inscribed circle (r = 320).
        Assert.Single(Parse([40, 40, 30, 30, 0.9f]));
        Assert.Empty(Parse([40, 40, 30, 30, 0.9f], circular: true));
        Assert.Single(Parse([320, 60, 30, 30, 0.9f], circular: true));
    }
}
