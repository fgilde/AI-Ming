using System.Text.Json;
using Core.Jev;
using Xunit;

namespace Core.Tests;

public class JevResponseParserTests
{
    private const string Sample = """
        {"model":"Qwen/Qwen3.5-0.8B",
         "answers":{
           "mode":{"type":"choice","choice":"2","confidence":0.81,"probabilities":{"1":0.1,"2":0.81,"3":0.05,"4":0.04}},
           "direction":{"type":"choice","choice":"1","confidence":0.6,"probabilities":{"1":0.6,"2":0.1,"3":0.2,"4":0.1}},
           "danger":{"type":"noul","noul":0.12},
           "weird":{"type":"score","score":2.4}
         },
         "usage":{"input_tokens":600,"output_tokens":0}}
        """;

    [Fact]
    public void Parses_choice_and_noul_and_skips_unknown_types()
    {
        var d = JevResponseParser.Parse(Sample)!;
        Assert.Equal("2", d.Choices["mode"].Choice);
        Assert.Equal(0.81, d.Choices["mode"].Confidence, 3);
        Assert.Equal(0.05, d.Choices["mode"].Probabilities["3"], 3);
        Assert.Equal(0.12, d.Nouls["danger"], 3);
        Assert.False(d.Choices.ContainsKey("weird"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"answers\":[]}")]
    public void Returns_null_for_garbage(string json) => Assert.Null(JevResponseParser.Parse(json));
}

public class PythonVersionTests
{
    [Theory]
    [InlineData("Python 3.11.9", 3, 11, 9)]
    [InlineData("Python 3.13", 3, 13, 0)]
    [InlineData("  Python 3.12.0rc1\r\n", 3, 12, 0)]   // pre-release banners still parse
    public void Parses_banner(string text, int major, int minor, int patch)
        => Assert.Equal(new Version(major, minor, patch), PythonVersion.Parse(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("'python' is not recognized as an internal or external command")]
    public void Returns_null_when_not_a_banner(string? text) => Assert.Null(PythonVersion.Parse(text));

    [Fact]
    public void Rejects_too_old_and_missing_interpreters()
    {
        Assert.False(PythonVersion.IsSupported(null));
        Assert.False(PythonVersion.IsSupported(PythonVersion.Parse("Python 3.9.13")));
        Assert.True(PythonVersion.IsSupported(PythonVersion.Parse("Python 3.10.0")));
        Assert.True(PythonVersion.IsSupported(PythonVersion.Parse("Python 3.11.9")));
    }
}

public class JevAbilityTests
{
    [Fact]
    public void Label_carries_profile_description_and_action_type()
    {
        Assert.Equal("smoke: blocks line of sight (instant)",
            new JevAbility("smoke", "blocks line of sight", "Instant").Label);
        // No description configured → name still usable, type still shown.
        Assert.Equal("flash (toggle)", new JevAbility("flash", "  ", "Toggle").Label);
        Assert.Equal("nade", new JevAbility("nade").Label);
    }
}

public class JevQuestionBuilderTests
{
    private static readonly JevAbility[] Two =
    [
        new("smoke", "blocks line of sight", "Instant"),
        new("heal", "restores health over 5s", "Instant"),
    ];

    [Fact]
    public void Option_keys_are_single_digits_and_tactical_only_with_abilities()
    {
        var none = Serialize(JevQuestionBuilder.Build([]));
        var some = Serialize(JevQuestionBuilder.Build(Two));

        // simple-jev needs one-token labels — every criteria key must be a single digit.
        foreach (var doc in new[] { none, some })
            foreach (var q in doc.RootElement.EnumerateObject())
                if (q.Value.TryGetProperty("criteria", out var crit))
                    foreach (var k in crit.EnumerateObject())
                        Assert.Matches("^[1-9]$", k.Name);

        Assert.Equal(4, none.RootElement.GetProperty("mode").GetProperty("criteria").EnumerateObject().Count());
        Assert.Equal(5, some.RootElement.GetProperty("mode").GetProperty("criteria").EnumerateObject().Count());
        Assert.False(none.RootElement.TryGetProperty("ability", out _));
        Assert.Equal("noul", some.RootElement.GetProperty("danger").GetProperty("type").GetString());
    }

    [Fact]
    public void Ability_criteria_expose_what_each_action_does()
    {
        var doc = Serialize(JevQuestionBuilder.Build(Two));
        var ability = doc.RootElement.GetProperty("ability");
        Assert.Equal("heal: restores health over 5s (instant)", ability.GetProperty("criteria").GetProperty("2").GetString());
        // The instructions repeat the legend so models that ignore criteria still see the meanings.
        Assert.Contains("blocks line of sight", ability.GetProperty("instructions").GetString());
    }

    [Fact]
    public void Caps_abilities_at_nine_and_skips_blank_names()
    {
        var many = Enumerable.Range(1, 15).Select(i => new JevAbility($"ability{i}")).ToList();
        var doc = Serialize(JevQuestionBuilder.Build(many));
        Assert.Equal(9, doc.RootElement.GetProperty("ability").GetProperty("criteria").EnumerateObject().Count());

        Assert.Single(JevQuestionBuilder.Offerable([new JevAbility(" "), new JevAbility("real")]));
    }

    private static JsonDocument Serialize(object o) => JsonDocument.Parse(JsonSerializer.Serialize(o));
}

public class JevIntentMapperTests
{
    private static JevDecision Decision(string mode = "2", double conf = 0.8, string? dir = "3", double danger = 0.1, string? ability = null)
    {
        var d = new JevDecision();
        d.Choices["mode"] = new JevChoice(mode, conf, new Dictionary<string, double> { ["1"] = 0.1, ["2"] = 0.7, ["3"] = 0.1, ["4"] = 0.1 });
        if (dir != null) d.Choices["direction"] = new JevChoice(dir, 0.5, new Dictionary<string, double>());
        if (ability != null) d.Choices["ability"] = new JevChoice(ability, 0.9, new Dictionary<string, double>());
        d.Nouls["danger"] = danger;
        return d;
    }

    private static readonly JevAbility[] Abilities =
    [
        new("smoke", "blocks line of sight", "Instant"),
        new("flash", "blinds enemies", "Instant"),
        new("reload", "refills the magazine", "Instant", Role: "reload"),
    ];

    [Fact]
    public void Maps_digits_to_names_and_rekeys_probabilities()
    {
        var i = JevIntentMapper.Map(Decision(), [], 0.35)!;
        Assert.Equal("engage", i.Mode);
        Assert.Equal("left", i.Direction);
        Assert.Equal(0.7, i.ModeProbabilities["engage"], 3);
        Assert.Equal(0.8, i.Confidence, 3);
        Assert.Null(i.Action);
    }

    [Fact]
    public void Low_confidence_returns_null_so_caller_keeps_intent()
        => Assert.Null(JevIntentMapper.Map(Decision(conf: 0.2), [], 0.35));

    [Fact]
    public void Danger_overrides_mode_to_retreat()
        => Assert.Equal("retreat", JevIntentMapper.Map(Decision(mode: "2", danger: 0.9), [], 0.35)!.Mode);

    [Fact]
    public void Tactical_mode_resolves_ability_to_the_profiles_own_action_name()
    {
        var i = JevIntentMapper.Map(Decision(mode: "5", ability: "2"), Abilities, 0.35)!;
        Assert.Equal("tactical", i.Mode);
        Assert.Equal("flash", i.Action); // the bare Name, never the decorated label — the action lookup matches on it
    }

    [Fact]
    public void Ability_indices_line_up_with_what_the_builder_offered()
    {
        // Same list into both sides: whatever key the builder assigned must map back to that action.
        var offered = JevQuestionBuilder.Offerable(Abilities);
        for (int i = 0; i < offered.Count; i++)
        {
            var intent = JevIntentMapper.Map(Decision(mode: "5", ability: (i + 1).ToString()), Abilities, 0.35)!;
            Assert.Equal(offered[i].Name, intent.Action);
        }
    }

    [Fact]
    public void Ability_is_ignored_outside_tactical_mode()
        => Assert.Null(JevIntentMapper.Map(Decision(mode: "2", ability: "1"), Abilities, 0.35)!.Action);

    [Fact]
    public void Out_of_range_choice_becomes_default_and_no_mode_answer_is_null()
    {
        Assert.Equal("default", JevIntentMapper.Map(Decision(mode: "9"), [], 0.35)!.Mode);
        Assert.Null(JevIntentMapper.Map(new JevDecision(), [], 0.35));
    }
}
