using System.Drawing;
using PowerAim.AI;
using PowerAim.AI.Aiming;
using PowerAim.AI.Rules;
using PowerAim.Config;
using PowerAim.Types;

namespace PowerAim.App.Tests;

public class RuleExpressionTests
{
    private sealed class Scope(Dictionary<string, RuleValue> values) : IRuleScope
    {
        public RuleValue Get(string name) => values.TryGetValue(name, out var v) ? v : RuleValue.Missing;
    }

    private static readonly Scope S = new(new()
    {
        ["head_hit"] = new(0.85),
        ["distance"] = new(100),
        ["class"] = new(double.NaN, "Head"),
        ["ocr.health"] = new(40),
    });

    [Theory]
    [InlineData("head_hit >= 0.8", true)]
    [InlineData("head_hit >= 90%", false)]
    [InlineData("head_hit >= 0.8 and distance < 50", false)]
    [InlineData("head_hit >= 0.8 && distance < 150", true)]
    [InlineData("distance < 50 or head_hit > 0.5", true)]
    [InlineData("not (distance > 50)", false)]
    [InlineData("!(distance > 150)", true)]
    [InlineData("class == \"head\"", true)]
    [InlineData("class != 'Body'", true)]
    [InlineData("ocr.health < 50", true)]
    [InlineData("ocr.ammo < 50", false)]          // missing signal: comparisons are false
    [InlineData("1 + 2 * 3 == 7", true)]          // precedence
    [InlineData("-distance < -99", true)]
    [InlineData("max(distance, 300) = 300", true)]
    [InlineData("clamp(distance, 0, 10) == 10 and abs(-2) == 2 and min(1, 2) == 1", true)]
    [InlineData("distance / 0 > 1", false)]       // NaN, not a crash
    public void Evaluates(string rule, bool expected)
        => Assert.Equal(expected, RuleExpression.Parse(rule).IsTrue(S));

    [Fact]
    public void Scores_are_numbers()
        => Assert.Equal(-100 + 0.85 * 200, RuleExpression.Parse("-distance + head_hit * 200").Score(S), 6);

    [Theory]
    [InlineData("head_hitt > 0.5", "unknown signal 'head_hitt'")]
    [InlineData("head_hit >", "rule ends too early")]
    [InlineData("(head_hit > 0.5", "missing ')'")]
    [InlineData("foo(1)", "unknown function 'foo'")]
    [InlineData("max(1)", "max takes 2 values")]
    [InlineData("head_hit # 1", "unexpected '#'")]
    [InlineData("", "empty rule")]
    public void Explains_mistakes(string rule, string error)
    {
        Assert.False(RuleExpression.TryParse(rule, TargetScope.IsKnown, out _, out var message));
        Assert.Contains(error, message);
    }

    [Fact]
    public void Lists_the_signals_it_reads()
        => Assert.Equal(["head_hit", "ocr.health"], RuleExpression.Parse("head_hit > 0.5 and ocr.health < 30 and head_hit < 1").Signals);
}

public class HitProbabilityTests
{
    private static readonly PointF Crosshair = new(320, 320);

    [Fact]
    public void Crosshair_in_the_middle_of_a_still_head_is_a_near_certain_hit()
        => Assert.True(HitProbability.Estimate(new RectangleF(300, 300, 40, 40), PointF.Empty, Crosshair, 0.06, 4) > 0.99);

    [Fact]
    public void Crosshair_far_from_the_head_is_a_miss()
        => Assert.True(HitProbability.Estimate(new RectangleF(400, 300, 40, 40), PointF.Empty, Crosshair, 0.06, 4) < 0.001);

    [Fact]
    public void A_head_moving_off_the_crosshair_is_less_likely_hit_than_a_still_one()
    {
        var head = new RectangleF(310, 310, 20, 20);
        double still = HitProbability.Estimate(head, PointF.Empty, Crosshair, 0.06, 4);
        double running = HitProbability.Estimate(head, new PointF(400, 0), Crosshair, 0.06, 4); // 24 px gone by the shot
        Assert.True(running < still * 0.2, $"still {still:0.000}, running {running:0.000}");
    }

    [Fact]
    public void A_head_moving_onto_the_crosshair_counts_as_a_hit()
    {
        var approaching = new RectangleF(286, 310, 20, 20); // left of the crosshair, moving right
        Assert.True(HitProbability.Estimate(approaching, new PointF(400, 0), Crosshair, 0.06, 4) > 0.5);
        Assert.True(HitProbability.Estimate(approaching, PointF.Empty, Crosshair, 0.06, 4) < 0.1);
    }

    [Fact]
    public void Phi_matches_known_values()
    {
        Assert.Equal(0.5, HitProbability.Phi(0), 6);
        Assert.Equal(0.841345, HitProbability.Phi(1), 5);
        Assert.Equal(0.022750, HitProbability.Phi(-2), 5);
    }
}

public class TargetScopeTests
{
    private static RuleContext Ctx(int enemies = 2) =>
        new(640, new PointF(320, 320), RelativeRect.Default, 0.06, 4, enemies, name => name == "health" ? 55 : null);

    [Fact]
    public void Exposes_target_and_frame_signals()
    {
        var target = new RuleTarget(new RectangleF(300, 240, 40, 160), new PointF(30, 40), 0.9f, "Enemy", 0);
        var s = new TargetScope(target, Ctx());

        Assert.Equal(50, s.Get("speed").Number, 6);
        Assert.Equal(0.25, s.Get("size").Number, 6);
        Assert.Equal(0.9, s.Get("confidence").Number, 6);
        Assert.Equal("Enemy", s.Get("class").Text);
        Assert.Equal(2, s.Get("enemies").Number);
        Assert.Equal(55, s.Get("ocr.health").Number);
        Assert.True(double.IsNaN(s.Get("ocr.ammo").Number));
        Assert.Equal(0, s.Get("distance").Number, 3); // box centre is (320, 320)
        Assert.InRange(s.Get("body_hit").Number, 0.9, 1);
    }

    [Fact]
    public void Every_documented_signal_parses()
    {
        foreach (var name in TargetScope.Signals.Keys.Where(k => !k.Contains('<')))
            Assert.True(RuleExpression.TryParse($"{name} == {name}", TargetScope.IsKnown, out _, out var e), e);
        Assert.True(TargetScope.IsKnown("ocr.anything"));
        Assert.False(TargetScope.IsKnown("ocr."));
    }

    [Fact]
    public void Any_and_best_over_a_frames_targets()
    {
        var near = new RuleTarget(new RectangleF(310, 300, 20, 40), PointF.Empty, 0.6f, "Enemy", 0);
        var far = new RuleTarget(new RectangleF(500, 100, 60, 120), PointF.Empty, 0.95f, "Enemy", 0);

        Assert.True(TriggerRules.AnyMatch(RuleExpression.Parse("body_hit > 0.5"), [far, near], Ctx()));
        Assert.False(TriggerRules.AnyMatch(RuleExpression.Parse("body_hit > 0.5"), [far], Ctx()));
        Assert.Same(near, TriggerRules.Best(RuleExpression.Parse("-distance"), [far, near], Ctx()));
        Assert.Same(far, TriggerRules.Best(RuleExpression.Parse("confidence"), [far, near], Ctx()));
    }

    [Fact]
    public void Compile_caches_and_reports_errors()
    {
        Assert.Same(TriggerRules.Compile("head_hit > 0.7").Rule, TriggerRules.Compile("head_hit > 0.7").Rule);
        Assert.Null(TriggerRules.Compile("nope > 1").Rule);
        Assert.NotNull(TriggerRules.Compile("nope > 1").Error);
    }
}

public class RuleTranslatorTests
{
    [Theory]
    [InlineData("""{"rule":"head_hit >= 0.8"}""", "head_hit >= 0.8")]
    [InlineData("""Here you go: {"rule": "body_hit > 0.7 and size > 0.2"} hope it helps""", "body_hit > 0.7 and size > 0.2")]
    [InlineData("`head_hit >= 0.9`", "head_hit >= 0.9")]
    public void Reads_the_rule_from_the_answer(string raw, string rule)
        => Assert.Equal(rule, RuleTranslator.ReadAnswer(raw).Rule);

    [Theory]
    [InlineData("""{"rule":"enemy_is_looking_at_me > 0.5"}""")]
    [InlineData("idle")]
    [InlineData("")]
    public void Rejects_rules_that_do_not_parse(string raw)
    {
        var (rule, error) = RuleTranslator.ReadAnswer(raw);
        Assert.Null(rule);
        Assert.NotNull(error);
    }

    [Fact]
    public void Prompt_names_every_signal_and_the_request()
    {
        var prompt = RuleTranslator.BuildPrompt("only when the head is very likely hit", RuleKind.TriggerCondition);
        Assert.Contains("only when the head is very likely hit", prompt);
        Assert.All(TargetScope.Signals.Keys, k => Assert.Contains(k, prompt));
        Assert.Contains("HIGHEST", RuleTranslator.BuildPrompt("closest", RuleKind.AimScore));
    }

    [Fact]
    public void Picks_a_text_model_before_a_vision_model()
    {
        var models = new[]
        {
            new DecisionModelDefinition { Kind = DecisionModelKind.OllamaVision, Model = "moondream" },
            new DecisionModelDefinition { Kind = DecisionModelKind.OllamaText, Model = "qwen2.5:1.5b" },
        };
        Assert.Equal("qwen2.5:1.5b", RuleTranslator.PickModel(models));
        Assert.Equal("moondream", RuleTranslator.PickModel(models.Take(1)));
        Assert.Null(RuleTranslator.PickModel([]));
    }

    [Fact]
    public void All_presets_are_valid_rules()
    {
        foreach (var preset in RulePresets.Trigger.Concat(RulePresets.Aim))
            Assert.True(RuleExpression.TryParse(preset.Expression, TargetScope.IsKnown, out _, out var e), $"{preset.Expression}: {e}");
    }
}

public class TargetRuleSelectionTests
{
    private static Prediction Det(float x, float y, float size = 40, float confidence = 0.9f) => new()
    {
        Confidence = confidence,
        Rectangle = new RectangleF(x - size / 2, y - size, size, size * 2),
        TranslatedRectangle = new RectangleF(x - size / 2, y - size, size, size * 2),
    };

    [Fact]
    public void Tracked_selection_follows_a_custom_score()
    {
        var tracker = new TargetTracker();
        IReadOnlyList<TargetTrack> tracks = [];
        for (int i = 0; i < 4; i++)
            tracks = tracker.Update([Det(320, 330), Det(500, 330, size: 80)], 1.0 / 60);

        var nearest = new TargetSelector().Select(tracks, 320, 320, 0.25, 6, 160);
        var biggest = new TargetSelector().Select(tracks, 320, 320, 0.25, 6, 160, score: t => t.Height);

        Assert.True(nearest!.X < 400);
        Assert.True(biggest!.X > 400);
    }

    [Fact]
    public void Negative_scores_still_need_a_real_lead_to_switch()
    {
        var tracker = new TargetTracker();
        IReadOnlyList<TargetTrack> tracks = [];
        for (int i = 0; i < 4; i++) tracks = tracker.Update([Det(300, 330), Det(340, 330)], 1.0 / 60);
        var selector = new TargetSelector();
        Func<TargetTrack, double> negDistance = t => -Math.Abs(t.X - 300);

        int held = selector.Select(tracks, 320, 320, 0.25, 2, 160, negDistance)!.Id;
        // Score -40 vs -0: the held one keeps winning; flip the preference and it switches after the frames.
        Func<TargetTrack, double> other = t => -Math.Abs(t.X - 340);
        selector.Select(tracks, 320, 320, 0.25, 2, 160, other);
        var switched = selector.Select(tracks, 320, 320, 0.25, 2, 160, other);

        Assert.NotEqual(held, switched!.Id);
    }

    [Fact]
    public void Sticky_aim_acquires_the_rules_pick_when_asked()
    {
        var near = Det(330, 330, confidence: 0.6f);
        var big = Det(500, 330, size: 90, confidence: 0.99f);
        var selector = new StickyAimSelector();

        Assert.Same(near, selector.SelectTarget([near, big], near, true, 80, 100, acquireFallback: true));
        selector.Reset();
        Assert.Same(big, selector.SelectTarget([near, big], near, true, 80, 100));
    }

    [Fact]
    public void Trigger_knows_when_it_uses_a_rule()
    {
        var t = new ActionTrigger();
        Assert.False(t.UsesRule);
        t.ExecutionIntersectionCheck = TriggerCheck.Rule;
        Assert.True(t.UsesRule);
    }
}
