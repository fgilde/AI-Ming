using PowerAim.AI.Aiming;

namespace PowerAim.App.Tests;

public class ReactionDelayTests
{
    [Fact]
    public void Zero_delay_reacts_at_once()
        => Assert.True(new ReactionDelay().Ready(hasTarget: true, nowMs: 0, delayMs: 0));

    [Fact]
    public void A_new_target_is_engaged_only_after_the_delay()
    {
        var delay = new ReactionDelay();
        Assert.False(delay.Ready(true, 0, 120));
        Assert.False(delay.Ready(true, 100, 120));
        Assert.True(delay.Ready(true, 120, 120));
        Assert.True(delay.Ready(true, 500, 120));
    }

    [Fact]
    public void Losing_the_target_restarts_the_delay()
    {
        var delay = new ReactionDelay();
        delay.Ready(true, 0, 120);
        delay.Ready(true, 200, 120);
        Assert.False(delay.Ready(false, 210, 120));

        Assert.False(delay.Ready(true, 220, 120));
        Assert.True(delay.Ready(true, 340, 120));
    }
}
