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
}
