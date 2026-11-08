using System.Text.Json;
using System.Text.Json.Nodes;
using PowerAim.AI.Actions;
using PowerAim.Config;

namespace PowerAim.App.Tests;

/// <summary>
///     The global "Smart Neural Anti-Recoil" switch is gone: the Smart Neural profile engine works on its own,
///     and older configs that relied on the switch with no profile at all get a Smart Neural profile instead.
/// </summary>
[Collection(nameof(TestConfig))]
public class SmartNeuralSwitchTests
{
    public SmartNeuralSwitchTests() => TestConfig.Ensure();

    private static AppConfig OldConfig(bool switchOn, bool withProfile)
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(new AppConfig()))!;
        json["ToggleState"]!["SmartNeuralAntiRecoil"] = switchOn;
        var ar = json["AntiRecoilSettings"]!;
        ar["SchemaVersion"] = 1;
        ar["Profiles"] = withProfile
            ? new JsonArray(JsonNode.Parse(JsonSerializer.Serialize(new AntiRecoilProfile { Id = "legacy", Name = "Default", Mode = AntiRecoilMode.Legacy })))
            : new JsonArray();
        ar["ActiveProfileId"] = withProfile ? "legacy" : "";
        var cfg = JsonSerializer.Deserialize<AppConfig>(json.ToJsonString())!;
        cfg.AntiRecoilSettings.MigrateLegacyIfNeeded();
        cfg.MigrateSmartNeuralSwitch();
        return cfg;
    }

    [Fact]
    public void Switch_on_without_profiles_becomes_an_active_smart_neural_profile()
    {
        var cfg = OldConfig(switchOn: true, withProfile: false);

        var profile = Assert.Single(cfg.AntiRecoilSettings.Profiles);
        Assert.Equal(AntiRecoilMode.SmartNeural, profile.Mode);
        Assert.Same(profile, cfg.AntiRecoilSettings.ActiveProfile);
    }

    [Theory]
    [InlineData(true, true)]    // the switch did nothing next to an active legacy profile
    [InlineData(false, false)]
    public void Otherwise_the_profiles_stay_as_they_were(bool switchOn, bool withProfile)
    {
        var cfg = OldConfig(switchOn, withProfile);

        Assert.Equal(withProfile ? 1 : 0, cfg.AntiRecoilSettings.Profiles.Count);
        Assert.DoesNotContain(cfg.AntiRecoilSettings.Profiles, p => p.Mode == AntiRecoilMode.SmartNeural);
    }

    [Fact]
    public void The_switch_is_not_written_back()
    {
        var cfg = OldConfig(switchOn: true, withProfile: false);

        Assert.DoesNotContain("SmartNeuralAntiRecoil", JsonSerializer.Serialize(cfg));
    }

    [Fact]
    public void A_smart_neural_profile_works_on_its_own_and_nothing_runs_without_a_profile()
    {
        var toggles = AppConfig.Current.ToggleState;
        var ar = AppConfig.Current.AntiRecoilSettings;
        var (global, antiRecoil, active) = (toggles.GlobalActive, toggles.AntiRecoil, ar.ActiveProfileId);
        var profile = new AntiRecoilProfile { Id = "sn-test", Name = "Smart", Mode = AntiRecoilMode.SmartNeural };
        try
        {
            toggles.GlobalActive = true;
            toggles.AntiRecoil = true;
            ar.Profiles.Add(profile);
            ar.ActiveProfileId = profile.Id;
            var action = new SmartNeuralAntiRecoilAction();
            Assert.True(action.Active);

            ar.ActiveProfileId = "";
            Assert.False(action.Active);
        }
        finally
        {
            ar.Profiles.Remove(profile);
            (toggles.GlobalActive, toggles.AntiRecoil, ar.ActiveProfileId) = (global, antiRecoil, active);
        }
    }
}
