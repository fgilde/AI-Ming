using PowerAim.AI;

namespace PowerAim.App.Tests;

public class OutputLayoutTests
{
    [Fact]
    public void Channels_first_head() => Assert.Equal((1, 8400, false), PredictionLogic.ReadOutputLayout([1, 5, 8400]));

    [Fact]
    public void Multi_class_channels_first_head() => Assert.Equal((80, 8400, false), PredictionLogic.ReadOutputLayout([1, 84, 8400]));

    [Fact]
    public void Channels_last_head() => Assert.Equal((2, 8400, true), PredictionLogic.ReadOutputLayout([1, 8400, 6]));

    [Fact]
    public void Nms_fused_export_is_not_mistaken_for_a_two_class_head()
        // [1, 300, 6] = x1 y1 x2 y2 score class per kept box; left to the shape warning, as on main.
        => Assert.Null(PredictionLogic.ReadOutputLayout([1, 300, 6]));

    [Fact]
    public void Dynamic_anchor_count() => Assert.Equal((1, 0, false), PredictionLogic.ReadOutputLayout([1, 5, -1]));

    [Fact]
    public void Small_input_channels_last_head_is_read_via_its_anchor_count()
        // A 192 px export has 756 anchors, below the generic row threshold.
        => Assert.Equal((2, 756, true), PredictionLogic.ReadOutputLayout([1, 756, 6], MathUtil.CalculateNumDetections(192)));

    [Theory]
    [InlineData(640, 8400)]
    [InlineData(960, 18900)]
    [InlineData(1280, 33600)]
    public void Large_input_heads_are_read_in_both_layouts(int imageSize, int anchors)
    {
        int expected = MathUtil.CalculateNumDetections(imageSize);
        Assert.Equal(anchors, expected);
        Assert.Equal((1, anchors, false), PredictionLogic.ReadOutputLayout([1, 5, anchors], expected));
        Assert.Equal((2, anchors, true), PredictionLogic.ReadOutputLayout([1, anchors, 6], expected));
    }

    [Fact]
    public void Nms_fused_export_stays_rejected_with_an_anchor_count()
        => Assert.Null(PredictionLogic.ReadOutputLayout([1, 300, 6], MathUtil.CalculateNumDetections(640)));
}
