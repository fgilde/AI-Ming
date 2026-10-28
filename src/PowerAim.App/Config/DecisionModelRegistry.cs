using System.Collections.ObjectModel;

namespace PowerAim.Config;

/// <summary>
///     The built-in decision models and how a profile finds its model. Profiles saved before the model list
///     existed carry only the legacy <see cref="AutoPlayProfile.Backend"/> (+ <see cref="AutoPlayProfile.OllamaModel"/>);
///     <see cref="Resolve"/> maps those onto list entries, so old configs keep their behaviour without a rewrite.
/// </summary>
public static class DecisionModelRegistry
{
    public const string HeuristicId = "builtin.heuristic";
    public const string OllamaVisionId = "builtin.ollama.vision";
    public const string OllamaTextId = "builtin.ollama.text";
    public const string JevId = "builtin.jev";
    public const string JevPerActionId = "builtin.jev.peraction";
    public const string LearnedId = "builtin.learned";

    public static IEnumerable<DecisionModelDefinition> CreateBuiltIns() =>
    [
        new() { Id = HeuristicId, Name = "Heuristic (no model)", Kind = DecisionModelKind.Heuristic, IsBuiltIn = true },
        new() { Id = JevId, Name = "Jev", Kind = DecisionModelKind.Jev, IsBuiltIn = true },
        new() { Id = JevPerActionId, Name = "Jev – per action", Kind = DecisionModelKind.Jev, AskPerAction = true, IsBuiltIn = true },
        new() { Id = OllamaTextId, Name = "Ollama text (qwen2.5:1.5b)", Kind = DecisionModelKind.OllamaText, Model = "qwen2.5:1.5b", IsBuiltIn = true },
        new() { Id = OllamaVisionId, Name = "Ollama vision (moondream)", Kind = DecisionModelKind.OllamaVision, Model = "moondream", IsBuiltIn = true },
        new() { Id = LearnedId, Name = "Learned from my play", Kind = DecisionModelKind.Learned, ActionThreshold = 0.5, IsBuiltIn = true },
    ];

    /// <summary>Adds missing built-ins (in their default order, ahead of user entries). Idempotent.</summary>
    public static void EnsureBuiltIns(ObservableCollection<DecisionModelDefinition> models)
    {
        int insertAt = 0;
        foreach (var builtIn in CreateBuiltIns())
        {
            var existing = models.FirstOrDefault(m => m.Id == builtIn.Id);
            if (existing == null) models.Insert(Math.Min(insertAt, models.Count), builtIn);
            else existing.IsBuiltIn = true;
            insertAt = models.IndexOf(models.First(m => m.Id == builtIn.Id)) + 1;
        }
    }

    /// <summary>
    ///     Legacy Ollama profiles name their vision model directly; give each such model a list entry so
    ///     <see cref="Resolve"/> finds it instead of falling back to the default vision model. Idempotent.
    /// </summary>
    public static void AdoptLegacyOllamaModels(ObservableCollection<DecisionModelDefinition> models, IEnumerable<AutoPlayProfile> profiles)
    {
        foreach (var p in profiles)
        {
            if (!string.IsNullOrEmpty(p.DecisionModelId) || p.Backend != AutoPlayDecisionBackend.Ollama ||
                string.IsNullOrWhiteSpace(p.OllamaModel)) continue;
            if (models.Any(m => m.Kind == DecisionModelKind.OllamaVision &&
                                string.Equals(m.Model, p.OllamaModel, StringComparison.OrdinalIgnoreCase))) continue;
            models.Add(new DecisionModelDefinition
            {
                Name = $"Ollama vision ({p.OllamaModel})", Kind = DecisionModelKind.OllamaVision, Model = p.OllamaModel,
            });
        }
    }

    /// <summary>
    ///     The profile's model: its <see cref="AutoPlayProfile.DecisionModelId"/> when that exists in the list,
    ///     otherwise the entry matching its legacy backend. Never null while the built-ins are present.
    /// </summary>
    public static DecisionModelDefinition? Resolve(AutoPlayProfile profile, IReadOnlyList<DecisionModelDefinition> models)
    {
        if (!string.IsNullOrEmpty(profile.DecisionModelId) &&
            models.FirstOrDefault(m => m.Id == profile.DecisionModelId) is { } picked)
            return picked;

        return profile.Backend switch
        {
            AutoPlayDecisionBackend.Jev => models.FirstOrDefault(m => m.Id == JevId),
            AutoPlayDecisionBackend.Ollama =>
                models.FirstOrDefault(m => m.Kind == DecisionModelKind.OllamaVision &&
                                           string.Equals(m.Model, profile.OllamaModel, StringComparison.OrdinalIgnoreCase))
                ?? models.FirstOrDefault(m => m.Id == OllamaVisionId),
            _ => models.FirstOrDefault(m => m.Id == HeuristicId),
        };
    }

    /// <summary>
    ///     The entry to use when a profile changes <paramref name="current"/>'s model name to <paramref name="name"/>.
    ///     Own entries are edited in place (and returned). Built-ins are shared defaults, so an own entry for
    ///     that model is reused or added (named "<paramref name="kindLabel"/>: name") and returned instead.
    /// </summary>
    public static DecisionModelDefinition WithModelName(ObservableCollection<DecisionModelDefinition> models,
        DecisionModelDefinition current, string name, string kindLabel)
    {
        if (!current.IsBuiltIn)
        {
            current.Model = name;
            return current;
        }
        var own = models.FirstOrDefault(m => !m.IsBuiltIn && m.Kind == current.Kind &&
                                             string.Equals(m.Model, name, StringComparison.OrdinalIgnoreCase));
        if (own != null) return own;
        own = new DecisionModelDefinition { Name = $"{kindLabel}: {name}", Kind = current.Kind, Model = name };
        models.Add(own);
        return own;
    }

    /// <summary>
    ///     Point the profile at <paramref name="model"/>. The legacy backend fields follow along, so a config
    ///     saved here still opens with a sensible strategic layer in an older build.
    /// </summary>
    public static void Assign(AutoPlayProfile profile, DecisionModelDefinition model)
    {
        profile.Backend = model.Kind switch
        {
            DecisionModelKind.Jev => AutoPlayDecisionBackend.Jev,
            DecisionModelKind.OllamaVision => AutoPlayDecisionBackend.Ollama,
            _ => AutoPlayDecisionBackend.Heuristic,
        };
        if (model.Kind == DecisionModelKind.OllamaVision && !string.IsNullOrWhiteSpace(model.Model))
            profile.OllamaModel = model.Model;
        profile.DecisionModelId = model.Id;
    }
}
