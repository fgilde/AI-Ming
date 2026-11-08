using System.Windows.Media;
using PowerAim.AI.Actions;
using PowerAim.Class;
using PowerAim.Config;

namespace PowerAim.App.Tests;

// Writes the global auto-pause state and reads AppConfig — not side by side with other config tests.
[Collection(nameof(TestConfig))]
public class CapturedAreaOverlayTests
{
    [Fact]
    public void Auto_pause_is_reported_once_per_change()
    {
        AutoPauseManager.Report(false);
        int changes = 0;
        EventHandler count = (_, _) => changes++;
        AutoPauseManager.StateChanged += count;
        try
        {
            AutoPauseManager.Report(true);
            AutoPauseManager.Report(true);
            Assert.True(AutoPauseManager.IsAutoPaused);
            AutoPauseManager.Report(false);
            Assert.False(AutoPauseManager.IsAutoPaused);
            Assert.Equal(2, changes);
        }
        finally { AutoPauseManager.StateChanged -= count; }
    }

    [Fact]
    public void The_border_fades_while_auto_paused()
    {
        TestConfig.Ensure();
        AppConfig.Current.ToggleState.ShowCapturedArea = true;
        try
        {
            AutoPauseManager.Report(false);
            var active = ((SolidColorBrush)AppConfig.Current.ColorState.ActiveCapturedAreaBorderBrush).Color;
            AutoPauseManager.Report(true);
            var paused = ((SolidColorBrush)AppConfig.Current.ColorState.ActiveCapturedAreaBorderBrush).Color;

            Assert.Equal(255, active.A);
            Assert.InRange(paused.A, 1, 128);
            Assert.Equal((active.R, active.G, active.B), (paused.R, paused.G, paused.B));
        }
        finally { AutoPauseManager.Report(false); }
    }

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
