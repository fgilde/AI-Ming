using PowerAim.Class;

namespace PowerAim.App.Tests;

public class FrameProfilerTests
{
    [Fact]
    public void Percentiles_per_stage()
    {
        var profiler = new FrameProfiler(capacity: 100);
        for (int i = 1; i <= 100; i++) profiler.Add(i, 2 * i, 0.5);

        Assert.Equal(50, profiler.Percentile(FrameStage.Capture, 50), 0);
        Assert.Equal(95, profiler.Percentile(FrameStage.Capture, 95), 0);
        Assert.Equal(190, profiler.Percentile(FrameStage.Inference, 95), 0);
        Assert.Equal(0.5, profiler.Percentile(FrameStage.Actions, 99), 3);
    }

    [Fact]
    public void Only_the_newest_frames_count()
    {
        var profiler = new FrameProfiler(capacity: 10);
        for (int i = 0; i < 10; i++) profiler.Add(100, 0, 0);
        for (int i = 0; i < 10; i++) profiler.Add(1, 0, 0);

        Assert.Equal(1, profiler.Percentile(FrameStage.Capture, 99), 3);
    }

    [Fact]
    public void A_frame_over_twice_the_median_is_slow_once_there_is_a_baseline()
    {
        var profiler = new FrameProfiler(capacity: 100);
        Assert.False(profiler.Add(1, 50, 1)); // no baseline yet

        for (int i = 0; i < 40; i++) Assert.False(profiler.Add(1, 8, 1));

        Assert.True(profiler.Add(1, 30, 1));
        Assert.False(profiler.Add(1, 12, 1));
    }

    [Fact]
    public void Summary_lists_the_stages()
    {
        var profiler = new FrameProfiler(capacity: 10);
        profiler.Add(1, 8, 2);

        Assert.Equal("cap 1.0 · inf 8.0 · act 2.0 · p95 11.0", profiler.Summary());
    }
}
