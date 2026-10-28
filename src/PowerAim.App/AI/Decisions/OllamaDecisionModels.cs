using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PowerAim.AI.AutoPlay;
using PowerAim.Config;

namespace PowerAim.AI.Decisions;

/// <summary>Reads an LLM's intent answer: JSON first, the old regex for chatty vision models second.</summary>
public static class LlmIntentParser
{
    public static readonly string[] Modes = ["explore", "engage", "retreat", "hold", "tactical"];
    public static readonly string[] Directions = ["forward", "backward", "left", "right"];

    private static readonly Regex Loose = new(
        @"""mode""\s*:\s*""(?<mode>[a-zA-Z_]+)""(?:[^}]*?""direction""\s*:\s*""(?<dir>[a-zA-Z_]+)"")?(?:[^}]*?""action""\s*:\s*""(?<act>[^""]+)"")?",
        RegexOptions.Compiled);

    /// <summary>
    ///     Accepts <c>{"mode":…, "direction":…, "action":…, "actions":[…]}</c> (extra text around it is ignored).
    ///     Unknown modes or directions are dropped; actions must name one of <paramref name="abilityNames"/>.
    /// </summary>
    public static AutoPlayIntent? Parse(string? raw, IReadOnlyCollection<string> abilityNames, string source)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string? mode = null, dir = null, act = null;
        var actions = new List<string>();

        int start = raw.IndexOf('{'), end = raw.LastIndexOf('}');
        bool parsed = false;
        if (start >= 0 && end > start)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
                var root = doc.RootElement;
                mode = Str(root, "mode");
                dir = Str(root, "direction");
                act = Str(root, "action");
                if (root.TryGetProperty("actions", out var list) && list.ValueKind == JsonValueKind.Array)
                    actions.AddRange(list.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.String).Select(a => a.GetString()!));
                parsed = true;
            }
            catch (JsonException) { /* fall through to the loose match */ }
        }
        if (!parsed)
        {
            var m = Loose.Match(raw);
            if (!m.Success) return null;
            mode = m.Groups["mode"].Value;
            dir = m.Groups["dir"].Success ? m.Groups["dir"].Value : null;
            act = m.Groups["act"].Success ? m.Groups["act"].Value : null;
        }

        mode = Modes.FirstOrDefault(x => string.Equals(x, mode, StringComparison.OrdinalIgnoreCase));
        if (mode == null) return null;
        dir = Directions.FirstOrDefault(x => string.Equals(x, dir, StringComparison.OrdinalIgnoreCase));
        string? Known(string? name) => abilityNames.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        var wanted = actions.Select(Known).OfType<string>().Distinct().ToList();
        // Vision models may name an action loosely ("throw grenade"); the tactics layer substring-matches hints.
        return new AutoPlayIntent(mode, dir, Known(act) ?? act, DateTime.Now, source, Actions: wanted.Count > 0 ? wanted : null);
    }

    private static string? Str(JsonElement root, string name)
        => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>Base for both Ollama kinds: availability, recent-decision variety hint, answer parsing.</summary>
public abstract class OllamaDecisionModelBase(DecisionModelDefinition definition) : IDecisionModel
{
    protected readonly OllamaClient Client = new();
    protected DecisionModelDefinition Definition => definition;

    public abstract string Source { get; }
    public string? LastError { get; private set; }
    public double? LastLatencyMs { get; private set; }

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(definition.Model)) { LastError = "no Ollama model set"; return false; }
        bool ok;
        try { ok = await Client.IsAvailableAsync(); }
        catch (Exception e) { LastError = e.Message; return false; }
        LastError = ok ? null : Client.LastError ?? "Ollama not reachable";
        return ok;
    }

    public async Task<AutoPlayIntent?> DecideAsync(DecisionRequest request, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? raw = await AskAsync(request, ct);
        if (raw == null) return null;
        var intent = LlmIntentParser.Parse(raw, request.Abilities.Select(a => a.Name).ToList(), Source);
        LastLatencyMs = sw.Elapsed.TotalMilliseconds;
        LastError = intent == null ? (Client.LastError ?? $"unreadable answer: {Truncate(raw)}") : null;
        return intent;
    }

    protected abstract Task<string?> AskAsync(DecisionRequest request, CancellationToken ct);

    protected static void AppendModes(StringBuilder sb, DecisionRequest r, bool allowActionList)
    {
        sb.Append("Modes: explore = move around looking for enemies; engage = push toward visible enemies and fight; ")
          .Append("retreat = back off and break line of sight; hold = stay put and cover; tactical = use one ability now.\n");
        if (r.Abilities.Count > 0)
        {
            sb.Append("Abilities (use the exact name):\n");
            foreach (var a in r.Abilities) sb.Append("  - ").Append(a.Label).Append('\n');
        }
        sb.Append("Answer with ONE JSON object: {\"mode\":\"explore|engage|retreat|hold|tactical\",")
          .Append("\"direction\":\"forward|backward|left|right\"")
          .Append(allowActionList ? ",\"actions\":[\"<ability names to use right now, may be empty>\"]}" : ",\"action\":\"<ability name, only for tactical>\"}")
          .Append('\n');
        if (r.Situation.RecentDecisions.Count > 0)
            sb.Append("Your recent decisions (oldest first): ").Append(string.Join(", ", r.Situation.RecentDecisions))
              .Append(". Vary them when nothing changed.\n");
    }

    private static string Truncate(string s) => s.Length <= 80 ? s : s[..80] + "…";
}

/// <summary>Ollama vision model: looks at the current frame. Sees walls, doors and the map; slow.</summary>
public sealed class OllamaVisionDecisionModel(DecisionModelDefinition definition) : OllamaDecisionModelBase(definition)
{
    public override string Source => "Ollama";

    protected override async Task<string?> AskAsync(DecisionRequest r, CancellationToken ct)
    {
        using var frame = r.CaptureFrame();
        if (frame == null) return null;
        var sb = new StringBuilder();
        sb.Append("You are guiding an AI bot playing ").Append(r.Situation.Game).Append(". Look at the current frame.\n")
          .Append("Detector: ").Append(r.Situation.Seen.EnemyCount).Append(" enemies on screen");
        if (r.Situation.Ammo is { } ammo) sb.Append(", ammo ").Append(ammo.ToString("0"));
        if (r.Situation.Health is { } hp) sb.Append(", health ").Append(hp.ToString("0"));
        if (r.Situation.Stuck) sb.Append(", the bot is stuck against something");
        sb.Append(".\n");
        AppendModes(sb, r, allowActionList: false);
        sb.Append("Reply with the JSON only.");
        return await Client.AnalyzeImageAsync(frame, sb.ToString(), Definition.Model);
    }
}

/// <summary>
///     Ollama text model: gets the detected state as JSON instead of a picture. With a small model
///     (qwen2.5:1.5b, llama3.2:1b, phi3) that's a fraction of a second, so it can steer far more often.
/// </summary>
public sealed class OllamaTextDecisionModel(DecisionModelDefinition definition) : OllamaDecisionModelBase(definition)
{
    public override string Source => "Ollama text";

    protected override async Task<string?> AskAsync(DecisionRequest r, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.Append("You control a bot playing ").Append(r.Situation.Game).Append(". Pick what it does in the next second.\n")
          .Append("The bot already aims, shoots and walks by itself; you choose the plan and which abilities to use.\n")
          .Append("State: ").Append(JsonSerializer.Serialize(DecisionState.Build(r, r.Abilities))).Append('\n');
        AppendModes(sb, r, allowActionList: true);
        return await Client.GenerateTextAsync(sb.ToString(), Definition.Model);
    }
}
