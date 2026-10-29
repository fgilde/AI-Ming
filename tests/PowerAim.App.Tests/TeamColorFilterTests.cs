using System.Drawing;
using PowerAim.AI;

namespace PowerAim.App.Tests;

public class TeamColorFilterTests
{
    private const int W = 100, H = 100, Stride = W * 4;

    private static byte[] Frame(Rectangle bar, byte r, byte g, byte b)
    {
        var px = new byte[Stride * H];
        for (int i = 0; i < px.Length; i += 4) { px[i] = 40; px[i + 1] = 40; px[i + 2] = 40; px[i + 3] = 255; } // grey
        for (int y = bar.Top; y < bar.Bottom; y++)
            for (int x = bar.Left; x < bar.Right; x++)
            {
                int o = y * Stride + x * 4;
                px[o] = b; px[o + 1] = g; px[o + 2] = r;
            }
        return px;
    }

    private static readonly RectangleF Box = new(40, 40, 20, 30);
    private static readonly Rectangle BarAbove = new(40, 32, 20, 6);
    private static readonly HsvRange Blue = new(200, 250, 50, 100, 40, 100);

    private static TeamColorOptions Options(TeamColorMode mode) =>
        new(mode, Blue, Above: true, Below: false, Left: false, Right: false, ExtentPct: 30, MatchPct: 20);

    [Fact]
    public void Box_with_a_teammate_coloured_bar_above_is_dropped()
    {
        var frame = Frame(BarAbove, 30, 90, 230);

        Assert.True(TeamColorFilter.ShouldDrop(frame, Stride, W, H, Box, Options(TeamColorMode.SuppressMatching)));
    }

    [Fact]
    public void Box_with_another_colour_above_is_kept()
    {
        var frame = Frame(BarAbove, 230, 30, 30);

        Assert.False(TeamColorFilter.ShouldDrop(frame, Stride, W, H, Box, Options(TeamColorMode.SuppressMatching)));
    }

    [Fact]
    public void Enemy_mode_keeps_only_matching_boxes()
    {
        Assert.False(TeamColorFilter.ShouldDrop(Frame(BarAbove, 30, 90, 230), Stride, W, H, Box, Options(TeamColorMode.KeepMatching)));
        Assert.True(TeamColorFilter.ShouldDrop(Frame(BarAbove, 230, 30, 30), Stride, W, H, Box, Options(TeamColorMode.KeepMatching)));
    }

    [Fact]
    public void Off_never_drops()
        => Assert.False(TeamColorFilter.ShouldDrop(Frame(BarAbove, 30, 90, 230), Stride, W, H, Box, Options(TeamColorMode.Off)));

    [Fact]
    public void Hue_range_can_wrap_around_red()
    {
        var red = new HsvRange(340, 20, 50, 100, 40, 100);
        Assert.True(red.Contains(355, 80, 80));
        Assert.True(red.Contains(5, 80, 80));
        Assert.False(red.Contains(180, 80, 80));
    }

    [Fact]
    public void Sample_region_outside_the_frame_matches_nothing()
    {
        var o = Options(TeamColorMode.SuppressMatching);
        var topBox = new RectangleF(40, 0, 20, 30); // nothing above it

        Assert.False(TeamColorFilter.ShouldDrop(Frame(BarAbove, 30, 90, 230), Stride, W, H, topBox, o));
    }

    [Fact]
    public void Enemy_mode_keeps_a_box_it_cannot_sample()
    {
        var topBox = new RectangleF(40, 0, 20, 30); // nothing above it to look at

        Assert.False(TeamColorFilter.ShouldDrop(Frame(BarAbove, 230, 30, 30), Stride, W, H, topBox, Options(TeamColorMode.KeepMatching)));
    }
}
