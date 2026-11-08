using System.Windows.Forms;
using Nextended.UI.Input;
using PowerAim.Config;

namespace PowerAim.App.Tests;

/// <summary>Duplicating aim, anti-recoil and trigger profiles to build test variants.</summary>
[Collection(nameof(TestConfig))]
public class ProfileDuplicateTests
{
    [Fact]
    public void An_aim_profile_copy_is_independent_disabled_and_keeps_the_settings()
    {
        TestConfig.Ensure();
        var p = new AimProfile { Name = "Rifle", Sensitivity = 0.7, AimEngine = AI.Aiming.AimEngine.V2, Enabled = true };
        p.ClassSpecs.Add(new ClassTargetSpec());

        var copy = p.Duplicate();

        Assert.NotEqual(p.Id, copy.Id);
        Assert.Equal("Rifle" + Locale.CopySuffix, copy.Name);
        Assert.False(copy.Enabled); // same aim key: the copy must not aim next to the original
        Assert.Equal(0.7, copy.Sensitivity);
        Assert.Equal(AI.Aiming.AimEngine.V2, copy.AimEngine);
        Assert.Single(copy.ClassSpecs);
        Assert.NotSame(p.ClassSpecs[0], copy.ClassSpecs[0]);

        copy.Sensitivity = 0.2;
        copy.ClassSpecs.Clear();
        Assert.Equal(0.7, p.Sensitivity);
        Assert.Single(p.ClassSpecs);
    }

    [Fact]
    public void An_anti_recoil_profile_copy_gets_its_own_id_and_is_not_the_active_one()
    {
        TestConfig.Ensure();
        var p = new AntiRecoilProfile { Name = "AR", Mode = AntiRecoilMode.SmartNeural, YRecoil = 12, NeuralStrength = 0.8 };

        var copy = p.Duplicate();

        Assert.NotEqual(p.Id, copy.Id);
        Assert.Equal("AR" + Locale.CopySuffix, copy.Name);
        Assert.Equal(AntiRecoilMode.SmartNeural, copy.Mode);
        Assert.Equal(12, copy.YRecoil);
        Assert.Equal(0.8, copy.NeuralStrength);
    }

    [Fact]
    public void A_trigger_copy_is_independent_and_disabled()
    {
        TestConfig.Ensure();
        var t = new ActionTrigger { Name = "Shoot", Enabled = true, Delay = 40 };
        t.Actions.Add(new StoredInputBinding(Keys.F1));
        t.OcrConditionTree.Children.Add(new OcrConditionLeaf());

        var copy = t.Duplicate();

        Assert.NotEqual(t.Id, copy.Id);
        Assert.Equal("Shoot" + Locale.CopySuffix, copy.Name);
        Assert.False(copy.Enabled);
        Assert.Equal(40, copy.Delay);
        Assert.Equal(t.Actions.Count, copy.Actions.Count); // the two defaults plus F1, not appended twice
        Assert.Single(copy.OcrConditionTree.Children);

        copy.OcrConditionTree.Children.Clear();
        Assert.Single(t.OcrConditionTree.Children);
    }
}
