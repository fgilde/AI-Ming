using PowerAim.AI.AutoPlay;
using PowerAim.Config;
using PowerAim.Core.Jev;

namespace PowerAim.AI.Decisions;

/// <summary>
///     Jev: no eyes, answers fixed questions about the state with probabilities. In per-action mode every
///     ability gets its own "use it now?" question, so several can fire at once and each is judged by its
///     description.
/// </summary>
public sealed class JevDecisionModel(DecisionModelDefinition definition) : IDecisionModel
{
    private readonly JevDecisionClient _client = new();

    public string Source => definition.AskPerAction ? "Jev (per action)" : "Jev";
    public string? LastError => _client.LastError;
    public double? LastLatencyMs => _client.LastLatencyMs;

    public Task<bool> IsAvailableAsync(CancellationToken ct) => _client.IsAvailableAsync(ct);

    public async Task<AutoPlayIntent?> DecideAsync(DecisionRequest request, CancellationToken ct)
    {
        double minConf = definition.MinConfidence ?? AppConfig.Current?.JevSettings?.MinConfidence ?? 0.35;
        var abilities = request.Abilities;
        var offered = definition.AskPerAction ? JevQuestionBuilder.PerActionOfferable(abilities) : JevQuestionBuilder.Offerable(abilities);
        var questions = definition.AskPerAction ? JevQuestionBuilder.BuildPerAction(abilities) : JevQuestionBuilder.Build(abilities);

        var decision = await _client.DecideAsync(DecisionState.Build(request, offered), questions, ct, definition.Model);
        if (decision == null) return null;

        var mapped = definition.AskPerAction
            ? JevIntentMapper.MapPerAction(decision, abilities, minConf, definition.ActionThreshold)
            : JevIntentMapper.Map(decision, abilities, minConf);
        return mapped == null
            ? null
            : new AutoPlayIntent(mapped.Mode, mapped.Direction, mapped.Action, DateTime.Now, Source,
                mapped.Confidence, mapped.ModeProbabilities, mapped.Danger, mapped.Actions);
    }
}
