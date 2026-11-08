using PowerAim.AI.Actions;

namespace PowerAim.App.Tests;

public class CapturedAreaOverlayTests
{
    [Fact]
    public void The_captured_area_border_shows_while_the_loop_is_auto_paused()
    {
        // Global Active switched on inside PowerAim: auto-pause holds the loop (PowerAim has focus),
        // but the border is a setup aid and must show anyway.
        Assert.True(OverlayAction.OverlayShouldShow(globalActive: true, running: false, esp: false, capturedArea: true));
    }

    [Fact]
    public void Esp_boxes_need_a_running_loop()
    {
        Assert.False(OverlayAction.OverlayShouldShow(globalActive: true, running: false, esp: true, capturedArea: false));
        Assert.True(OverlayAction.OverlayShouldShow(globalActive: true, running: true, esp: true, capturedArea: false));
    }

    [Fact]
    public void Nothing_shows_without_global_active()
    {
        Assert.False(OverlayAction.OverlayShouldShow(globalActive: false, running: false, esp: true, capturedArea: true));
        Assert.False(OverlayAction.OverlayShouldShow(globalActive: true, running: true, esp: false, capturedArea: false));
    }
}
