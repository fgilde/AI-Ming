using System.Windows.Forms;
using Nextended.UI.Input;
using PowerAim.Config;

namespace PowerAim.App.Tests;

public class AimGateTests
{
    private static AimProfile Profile(string name, StoredInputBinding aimKey, params StoredInputBinding[] offGate)
    {
        var p = new AimProfile { Name = name };
        p.AimKeyBindings.Add(aimKey);
        foreach (var k in offGate) p.OffGateBindings.Add(k);
        return p;
    }

    private static Func<StoredInputBinding, bool> Held(params StoredInputBinding[] held)
        => k => held.Any(h => h.Equals(k));

    [Fact]
    public void Held_aim_key_engages_its_profile()
    {
        var p = Profile("a", MouseButtons.Right);

        Assert.Same(p, AimSettings.Resolve([p], Held(MouseButtons.Right), _ => true));
    }

    [Fact]
    public void Held_off_gate_suppresses_the_profile_even_with_its_aim_key_held()
    {
        var p = Profile("a", MouseButtons.Right, Keys.E);

        Assert.Null(AimSettings.Resolve([p], Held(MouseButtons.Right, Keys.E), _ => true));
        Assert.Same(p, AimSettings.Resolve([p], Held(MouseButtons.Right), _ => true));
    }

    [Fact]
    public void Off_gate_of_one_profile_does_not_block_another()
    {
        var a = Profile("a", MouseButtons.Right, Keys.E);
        var b = Profile("b", MouseButtons.Right);

        Assert.Same(b, AimSettings.Resolve([a, b], Held(MouseButtons.Right, Keys.E), _ => true));
    }

    [Fact]
    public void Profile_for_another_game_does_not_engage()
    {
        var p = Profile("a", MouseButtons.Right);

        Assert.Null(AimSettings.Resolve([p], Held(MouseButtons.Right), _ => false));
    }
}
