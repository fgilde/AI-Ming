using System.Text;
using System.Text.Json;
using PowerAim.Config;

namespace PowerAim.AI.Rules;

/// <summary>Whether a rule decides "fire now?" (true/false) or ranks aim targets (higher score wins).</summary>
public enum RuleKind { TriggerCondition, AimScore }

/// <summary>A ready-made rule with its plain-language description.</summary>
public sealed record RulePreset(string Description, string Expression);

public static class RulePresets
{
    public static IReadOnlyList<RulePreset> Trigger =>
    [
        new(Locale.RulePresetHeadVeryLikely, "head_hit >= 0.8"),
        new(Locale.RulePresetHeadLikely, "head_hit >= 0.5"),
        new(Locale.RulePresetBodyVeryLikely, "body_hit >= 0.8"),
        new(Locale.RulePresetHeadCloseOnly, "head_hit >= 0.7 and size > 0.15"),
        new(Locale.RulePresetSlowTarget, "body_hit >= 0.7 and speed < 150"),
        new(Locale.RulePresetNearCrosshair, "distance < 80"),
    ];

    public static IReadOnlyList<RulePreset> Aim =>
    [
        new(Locale.RulePresetAimClosestToCrosshair, "-distance"),
        new(Locale.RulePresetAimClosestEnemy, "size"),
        new(Locale.RulePresetAimEasiestHeadshot, "head_hit * 2 - distance / 500"),
        new(Locale.RulePresetAimMostConfident, "confidence"),
        new(Locale.RulePresetAimSlowest, "-speed"),
    ];
}

/// <summary>
///     Turns a plain-language description into a rule with a local Ollama model, once, when the user asks —
///     never per frame. The answer is checked by parsing it against the known signals; anything else is rejected.
/// </summary>
public static class RuleTranslator
{
    public static string BuildPrompt(string description, RuleKind kind)
    {
        var sb = new StringBuilder();
        sb.Append(kind == RuleKind.TriggerCondition
            ? "Write a condition that is true when a game trigger should fire, for this request:\n"
            : "Write a score formula; the aim picks the target with the HIGHEST score, for this request:\n");
        sb.Append('"').Append(description.Trim()).Append("\"\n");
        sb.Append("It is evaluated for each detected target. Signals:\n");
        foreach (var (name, meaning) in TargetScope.Signals) sb.Append("  ").Append(name).Append(": ").Append(meaning).Append('\n');
        sb.Append("Syntax: numbers (0.8 or 80%), + - * /, < <= > >= == !=, and / or / not, parentheses, min(a,b) max(a,b) abs(x) clamp(x,lo,hi).\n");
        sb.Append(kind == RuleKind.TriggerCondition
            ? "Examples: \"very likely hits the head\" -> head_hit >= 0.8 ; \"close enemies only, body is enough\" -> body_hit >= 0.7 and size > 0.2\n"
            : "Examples: \"closest to my crosshair\" -> -distance ; \"easy headshots first\" -> head_hit * 2 - distance / 500\n");
        sb.Append("Answer with JSON only: {\"rule\":\"<the rule>\"}");
        return sb.ToString();
    }

    /// <summary>The rule from the model's answer when it parses against the known signals, else an error.</summary>
    public static (string? Rule, string? Error) ReadAnswer(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, "empty answer");
        string? rule = null;
        int start = raw.IndexOf('{'), end = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
                if (doc.RootElement.TryGetProperty("rule", out var r) && r.ValueKind == JsonValueKind.String) rule = r.GetString();
            }
            catch (JsonException) { /* not JSON: try the raw text */ }
        }
        rule ??= raw.Trim().Trim('`', '"');
        return RuleExpression.TryParse(rule ?? "", TargetScope.IsKnown, out var parsed, out var error)
            ? (parsed!.Source, null)
            : (null, error);
    }

    /// <summary>The first Ollama model in the decision-model list (text preferred), or null.</summary>
    public static string? PickModel(IEnumerable<DecisionModelDefinition>? models)
    {
        var list = models?.Where(m => !string.IsNullOrWhiteSpace(m.Model)).ToList() ?? [];
        return (list.FirstOrDefault(m => m.Kind == DecisionModelKind.OllamaText)
                ?? list.FirstOrDefault(m => m.Kind == DecisionModelKind.OllamaVision))?.Model;
    }

    public static async Task<(string? Rule, string? Error)> TranslateAsync(string description, RuleKind kind)
    {
        if (string.IsNullOrWhiteSpace(description)) return (null, Locale.RuleDescriptionEmpty);
        var model = PickModel(AppConfig.Current?.DecisionModels);
        if (model == null) return (null, Locale.RuleNoOllamaModel);
        var client = new OllamaClient();
        if (!await client.IsAvailableAsync()) return (null, Locale.OllamaPullServerOffline);
        var raw = await client.GenerateTextAsync(BuildPrompt(description, kind), model, maxTokens: 80);
        var (rule, error) = ReadAnswer(raw);
        return rule != null ? (rule, null) : (null, client.LastError ?? error);
    }
}
