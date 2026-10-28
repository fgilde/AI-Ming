using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using PowerAim.AI.AutoPlay;
using PowerAim.AI.Decisions;
using PowerAim.Config;
using PowerAim.Core.Jev;

namespace PowerAim.App.Tests;

public class StuckDetectorTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);
    private static byte[] Frame(byte value) => Enumerable.Repeat(value, StuckDetector.Side * StuckDetector.Side).ToArray();

    [Fact]
    public void Moving_with_an_unchanged_view_is_stuck_after_the_grace_time()
    {
        var d = new StuckDetector();
        d.Update(Frame(100), true, T0);
        Assert.False(d.Update(Frame(101), true, T0.AddMilliseconds(250)));
        Assert.False(d.Update(Frame(100), true, T0.AddMilliseconds(1000)));
        Assert.True(d.Update(Frame(101), true, T0.AddMilliseconds(1500)));
    }

    [Fact]
    public void Standing_still_or_a_changing_view_is_not_stuck()
    {
        var still = new StuckDetector();
        for (int i = 0; i < 10; i++) Assert.False(still.Update(Frame(100), false, T0.AddMilliseconds(250 * i)));

        var walking = new StuckDetector();
        for (int i = 0; i < 10; i++) Assert.False(walking.Update(Frame((byte)(i * 20)), true, T0.AddMilliseconds(250 * i)));
    }

    [Fact]
    public void Thumbnail_is_grey_and_side_squared()
    {
        using var bmp = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.White);
        var thumb = StuckDetector.Thumbnail(bmp);
        Assert.Equal(StuckDetector.Side * StuckDetector.Side, thumb.Length);
        Assert.All(thumb, v => Assert.True(v > 240));
    }
}

public class LlmIntentParserTests
{
    private static readonly string[] Abilities = ["grenade", "ping"];

    [Fact]
    public void Reads_json_with_an_action_list_and_drops_unknown_names()
    {
        var intent = LlmIntentParser.Parse("""Sure! {"mode":"engage","direction":"left","actions":["ping","fly"]}""", Abilities, "Ollama text");
        Assert.NotNull(intent);
        Assert.Equal("engage", intent!.Mode);
        Assert.Equal("left", intent.Direction);
        Assert.Equal(["ping"], intent.Actions!);
        Assert.Equal("Ollama text", intent.Source);
    }

    [Fact]
    public void Falls_back_to_the_loose_match_for_chatty_answers()
    {
        var intent = LlmIntentParser.Parse("""I think "mode": "tactical", "action": "grenade" now""", Abilities, "Ollama");
        Assert.Equal("tactical", intent!.Mode);
        Assert.Equal("grenade", intent.ActionHint);
    }

    [Theory]
    [InlineData("idle")]
    [InlineData("""{"mode":"dance"}""")]
    [InlineData("")]
    public void Rejects_answers_without_a_known_mode(string raw) => Assert.Null(LlmIntentParser.Parse(raw, Abilities, "x"));
}

public class LearnedDecisionModelTests
{
    private static readonly Size Area = new(1920, 1080);

    private static DecisionRequest Request(AutoPlayProfile profile, AutoPlayPerception seen) =>
        new(profile, new DecisionSituation("fps", seen, seen.Nearest == null ? 5 : 0, null, null, false, []), [], [], () => null);

    private static AutoPlayPerception EnemyLeft => new(1, new EnemyView(-500, 0, 60, 120), Area);
    private static AutoPlayPerception Nobody => new(0, null, Area);

    private static DemonstrationStore Recorded(AutoPlayProfile profile)
    {
        var store = new DemonstrationStore();
        for (int i = 0; i < 40; i++)
        {
            // Enemy on the left: strafe left and ping. Nobody around: run forward.
            store.Add(new Demonstration(profile.Id, DecisionFeatures.From(EnemyLeft, 0, null, null), ["move_left", "ping"]));
            store.Add(new Demonstration(profile.Id, DecisionFeatures.From(Nobody, 5, null, null), ["move_forward"]));
        }
        return store;
    }

    [Fact]
    public async Task Does_what_you_did_in_similar_moments()
    {
        var profile = AutoPlayFixtures.Fps();
        var model = new LearnedDecisionModel(new DecisionModelDefinition { Kind = DecisionModelKind.Learned, ActionThreshold = 0.5 }, Recorded(profile));

        var fight = await model.DecideAsync(Request(profile, EnemyLeft), CancellationToken.None);
        Assert.Equal("engage", fight!.Mode);
        Assert.Equal("left", fight.Direction);
        Assert.Equal(["ping"], fight.Actions!);

        var roam = await model.DecideAsync(Request(profile, Nobody), CancellationToken.None);
        Assert.Equal("explore", roam!.Mode);
        Assert.Equal("forward", roam.Direction);
        Assert.Empty(roam.Actions!);
    }

    [Fact]
    public async Task Abstains_until_enough_play_is_recorded()
    {
        var profile = AutoPlayFixtures.Fps();
        var model = new LearnedDecisionModel(new DecisionModelDefinition { Kind = DecisionModelKind.Learned }, new DemonstrationStore());
        Assert.Null(await model.DecideAsync(Request(profile, Nobody), CancellationToken.None));
        Assert.Contains("record", model.LastError);
    }

    [Fact]
    public void Store_round_trips_through_a_file_and_caps_its_size()
    {
        var store = new DemonstrationStore();
        store.Add(new Demonstration("p", DecisionFeatures.From(Nobody, 1, 20, 50), ["jump"]));
        store.Add(new Demonstration("p", new float[3], ["bad length"]));
        var path = Path.Combine(Path.GetTempPath(), $"demos_{Guid.NewGuid():N}.json");
        try
        {
            store.Save(path);
            var loaded = new DemonstrationStore();
            Assert.True(loaded.Load(path));
            Assert.Equal(["jump"], loaded.For("p").Single().Actions);
        }
        finally { File.Delete(path); }
    }
}

public class DecisionModelRegistryTests
{
    private static ObservableCollection<DecisionModelDefinition> Models() => new(DecisionModelRegistry.CreateBuiltIns());

    [Fact]
    public void Legacy_profiles_resolve_through_their_old_backend()
    {
        var models = Models();
        Assert.Equal(DecisionModelRegistry.HeuristicId, DecisionModelRegistry.Resolve(new AutoPlayProfile { Backend = AutoPlayDecisionBackend.Heuristic }, models)!.Id);
        Assert.Equal(DecisionModelRegistry.JevId, DecisionModelRegistry.Resolve(new AutoPlayProfile { Backend = AutoPlayDecisionBackend.Jev }, models)!.Id);

        var llava = new AutoPlayProfile { Backend = AutoPlayDecisionBackend.Ollama, OllamaModel = "llava:7b" };
        DecisionModelRegistry.AdoptLegacyOllamaModels(models, [llava]);
        DecisionModelRegistry.AdoptLegacyOllamaModels(models, [llava]); // idempotent
        var resolved = DecisionModelRegistry.Resolve(llava, models)!;
        Assert.Equal("llava:7b", resolved.Model);
        Assert.Single(models, m => m.Model == "llava:7b");
    }

    [Fact]
    public void Assign_points_the_profile_at_the_model_and_keeps_legacy_fields_sensible()
    {
        var models = Models();
        var profile = new AutoPlayProfile();
        var perAction = models.Single(m => m.Id == DecisionModelRegistry.JevPerActionId);

        DecisionModelRegistry.Assign(profile, perAction);

        Assert.Same(perAction, DecisionModelRegistry.Resolve(profile, models));
        Assert.Equal(AutoPlayDecisionBackend.Jev, profile.Backend);
    }

    [Fact]
    public void Missing_built_ins_are_restored_and_flagged()
    {
        var custom = new DecisionModelDefinition { Name = "mine", Kind = DecisionModelKind.OllamaText, Model = "phi3:mini" };
        var models = new ObservableCollection<DecisionModelDefinition> { custom };

        DecisionModelRegistry.EnsureBuiltIns(models);
        DecisionModelRegistry.EnsureBuiltIns(models);

        Assert.Equal(DecisionModelRegistry.CreateBuiltIns().Count() + 1, models.Count);
        Assert.All(models.Where(m => m != custom), m => Assert.True(m.IsBuiltIn));
        Assert.True(custom.CanDelete);
    }
}

public class AutoPlayStrategistTests
{
    private sealed class FakeModel(Func<AutoPlayIntent?> answer, bool available = true) : IDecisionModel
    {
        public int Calls;
        public string Source => "Fake";
        public string? LastError => available ? null : "down";
        public double? LastLatencyMs => 3;
        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(available);
        public Task<AutoPlayIntent?> DecideAsync(DecisionRequest request, CancellationToken ct) { Calls++; return Task.FromResult(answer()); }
    }

    private static AutoPlayStrategist Strategist(AutoPlayProfile profile, IDecisionModel? model) =>
        new(() => profile, p => new DecisionRequest(p, new DecisionSituation("fps", default, 0, null, null, false, []), [], [], () => null),
            _ => model);

    public AutoPlayStrategistTests()
        => typeof(AppConfig).GetProperty(nameof(AppConfig.Current))!.SetValue(null, new AppConfig());

    [Fact]
    public async Task Publishes_the_models_intent_and_remembers_recent_decisions()
    {
        var profile = new AutoPlayProfile { DecisionModelId = DecisionModelRegistry.JevId };
        var s = Strategist(profile, new FakeModel(() => new AutoPlayIntent("engage", "left", null, DateTime.Now, "Fake", Actions: ["ping"])));

        await s.StepAsync(CancellationToken.None);

        Assert.Equal("engage", s.Intent.Mode);
        Assert.Equal(["engage/left"], s.RecentDecisions);
        Assert.Equal(["ping"], s.Current!.Actions!);
        Assert.Null(s.Current.Error);
    }

    [Fact]
    public async Task An_unsure_answer_keeps_the_previous_intent_and_says_why()
    {
        var profile = new AutoPlayProfile { DecisionModelId = DecisionModelRegistry.JevId };
        var answers = new Queue<AutoPlayIntent?>([new AutoPlayIntent("explore", "right", null, DateTime.Now), null]);
        var s = Strategist(profile, new FakeModel(() => answers.Dequeue()));

        await s.StepAsync(CancellationToken.None);
        await s.StepAsync(CancellationToken.None);

        Assert.Equal("explore", s.Intent.Mode);
        Assert.NotNull(s.Current!.Error);
    }

    [Fact]
    public async Task An_unreachable_model_drops_to_the_reflex()
    {
        var profile = new AutoPlayProfile { DecisionModelId = DecisionModelRegistry.JevId };
        var model = new FakeModel(() => new AutoPlayIntent("engage", null, null, DateTime.Now), available: false);
        var s = Strategist(profile, model);

        await s.StepAsync(CancellationToken.None);

        Assert.Equal("default", s.Intent.Mode);
        Assert.Equal(0, model.Calls);
        Assert.Equal("down", s.Current!.Error);
    }
}

public class JevDecisionModelMappingTests
{
    [Fact]
    public void Per_action_answers_become_an_action_list()
    {
        var abilities = new[] { new JevAbility("ping", "marks enemies"), new JevAbility("grenade", "throws a grenade") };
        var decision = new JevDecision();
        decision.Choices["mode"] = new JevChoice("2", 0.8, new Dictionary<string, double> { ["2"] = 0.8 });
        decision.Nouls["danger"] = 0.1;
        decision.Nouls[JevQuestionBuilder.UseQuestionId(0)] = 0.9;
        decision.Nouls[JevQuestionBuilder.UseQuestionId(1)] = 0.2;

        var intent = JevIntentMapper.MapPerAction(decision, abilities, 0.35, 0.6);

        Assert.Equal("engage", intent!.Mode);
        Assert.Equal(["ping"], intent.Actions!);
    }
}
