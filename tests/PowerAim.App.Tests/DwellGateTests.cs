using PowerAim.AI.Actions;

namespace PowerAim.App.Tests;

public class DwellGateTests
{
    [Fact]
    public void Zero_dwell_follows_the_condition()
    {
        var gate = new DwellGate();

        Assert.True(gate.Update(true, 0, 0));
        Assert.False(gate.Update(false, 1, 0));
    }

    [Fact]
    public void Condition_must_hold_for_the_whole_dwell()
    {
        var gate = new DwellGate();

        Assert.False(gate.Update(true, 0, 40));
        Assert.False(gate.Update(true, 39, 40));
        Assert.True(gate.Update(true, 40, 40));
    }

    [Fact]
    public void A_false_frame_restarts_the_dwell()
    {
        var gate = new DwellGate();
        gate.Update(true, 0, 40);
        gate.Update(true, 30, 40);
        gate.Update(false, 31, 40);   // sweeping past the target

        Assert.False(gate.Update(true, 45, 40));
        Assert.True(gate.Update(true, 85, 40));
    }
}
