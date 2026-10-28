using PowerAim.Config;

namespace PowerAim.AI.Decisions;

/// <summary>Builds the runtime model for a list entry. Null for <see cref="DecisionModelKind.Heuristic"/> (no model).</summary>
public static class DecisionModelFactory
{
    public static IDecisionModel? Create(DecisionModelDefinition definition) => definition.Kind switch
    {
        DecisionModelKind.Jev => new JevDecisionModel(definition),
        DecisionModelKind.OllamaVision => new OllamaVisionDecisionModel(definition),
        DecisionModelKind.OllamaText => new OllamaTextDecisionModel(definition),
        DecisionModelKind.Learned => new LearnedDecisionModel(definition),
        _ => null,
    };
}
