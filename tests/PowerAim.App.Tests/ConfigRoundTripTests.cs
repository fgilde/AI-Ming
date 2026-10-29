using System.Text.Json;
using System.Text.Json.Nodes;
using PowerAim.Config;

namespace PowerAim.App.Tests;

// Deserialize + migrate directly: AppConfig.Load would also record the file as the user's "last config".
public class ConfigRoundTripTests
{
    // Serialising reads computed properties that consult AppConfig.Current; without this the tests
    // only passed when another test happened to load a config first. A missing path loads defaults
    // and records nothing.
    public ConfigRoundTripTests()
    {
        TestConfig.Ensure();
    }

    private static AppConfig Reload(string json)
    {
        var cfg = JsonSerializer.Deserialize<AppConfig>(json)!;
        cfg.MigrateDecisionModels();
        return cfg;
    }

    [Fact]
    public void New_settings_survive_save_and_load()
    {
        var cfg = new AppConfig();
        cfg.DecisionModels.Add(new DecisionModelDefinition { Name = "mine", Kind = DecisionModelKind.OllamaText, Model = "phi3:mini" });
        cfg.AutoPlayProfiles[0].DecisionModelId = DecisionModelRegistry.JevPerActionId;
        cfg.Triggers[0].ExecutionIntersectionCheck = TriggerCheck.Rule;
        cfg.Triggers[0].HitRule = "head_hit >= 0.9";
        cfg.Triggers[0].ShotLatencyMs = 80;

        var loaded = Reload(JsonSerializer.Serialize(cfg));

        Assert.Contains(loaded.DecisionModels, m => m is { Name: "mine", Model: "phi3:mini" });
        Assert.Equal(DecisionModelRegistry.CreateBuiltIns().Count() + 1, loaded.DecisionModels.Count);
        Assert.Equal(DecisionModelRegistry.JevPerActionId, loaded.AutoPlayProfiles[0].DecisionModelId);
        Assert.Equal(TriggerCheck.Rule, loaded.Triggers[0].ExecutionIntersectionCheck);
        Assert.Equal("head_hit >= 0.9", loaded.Triggers[0].HitRule);
        Assert.Equal(80, loaded.Triggers[0].ShotLatencyMs);
    }

    [Fact]
    public void Aim_point_settings_survive_save_and_load()
    {
        var cfg = new AppConfig();
        var profile = new AimProfile { Name = "p", DynamicOffset = true, AdditionalOffsetY = -6, PoseTarget = PoseAimTarget.Chest };
        profile.ClassSpecs.Add(new ClassTargetSpec { ClassId = 2, Priority = 0, UseAnchor = true, AnchorY = 0.25f, Ignored = false });
        cfg.AimSettings.Profiles.Add(profile);

        var loaded = Reload(JsonSerializer.Serialize(cfg)).AimSettings.Profiles.Single(p => p.Name == "p");

        Assert.True(loaded.DynamicOffset);
        Assert.Equal(-6, loaded.AdditionalOffsetY);
        Assert.Equal(PoseAimTarget.Chest, loaded.PoseTarget);
        var spec = Assert.Single(loaded.ClassSpecs);
        Assert.Equal((2, 0, true, 0.25f), (spec.ClassId, spec.Priority, spec.UseAnchor, spec.AnchorY));
    }

    [Fact]
    public void A_config_from_before_the_model_list_gets_the_built_ins_and_keeps_its_ollama_model()
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(new AppConfig()))!.AsObject();
        node.Remove(nameof(AppConfig.DecisionModels));
        var profile = node[nameof(AppConfig.AutoPlayProfiles)]![0]!.AsObject();
        profile.Remove(nameof(AutoPlayProfile.DecisionModelId));
        profile[nameof(AutoPlayProfile.DecisionBackend)] = (int)AutoPlayDecisionBackend.Ollama;
        profile[nameof(AutoPlayProfile.OllamaModel)] = "llava:13b";

        var loaded = Reload(node.ToJsonString());

        Assert.All(DecisionModelRegistry.CreateBuiltIns(), b => Assert.Contains(loaded.DecisionModels, m => m.Id == b.Id));
        var model = DecisionModelRegistry.Resolve(loaded.AutoPlayProfiles[0], loaded.DecisionModels)!;
        Assert.Equal(DecisionModelKind.OllamaVision, model.Kind);
        Assert.Equal("llava:13b", model.Model);
    }
}
