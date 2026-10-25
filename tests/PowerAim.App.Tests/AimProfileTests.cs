using PowerAim.Config;

namespace PowerAim.App.Tests;

public class AimProfileTests
{
    [Theory]
    [InlineData(nameof(AimProfile.PidKp))]
    [InlineData(nameof(AimProfile.PidKi))]
    [InlineData(nameof(AimProfile.PidKd))]
    public void Pid_gain_changes_are_reported(string property)
    {
        var profile = new AimProfile();
        var changed = new List<string?>();
        profile.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        typeof(AimProfile).GetProperty(property)!.SetValue(profile, 0.33);

        Assert.Contains(property, changed);
    }
}
