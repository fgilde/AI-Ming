using System.Text.Json.Serialization;
using Nextended.Core;

namespace PowerAim.Config;

/// <summary>What kind of decision model a <see cref="DecisionModelDefinition"/> describes.</summary>
public enum DecisionModelKind
{
    /// <summary>No model: the built-in reflex alone (plus HUD cues). No network, no screenshots.</summary>
    Heuristic,

    /// <summary>Ollama vision model: looks at the frame. Slow (seconds), sees the map.</summary>
    OllamaVision,

    /// <summary>Ollama text model: reads the detected state as JSON. Fast with small models (~0.2–1 s).</summary>
    OllamaText,

    /// <summary>Jev: answers fixed questions with probabilities (~0.1–0.5 s). Optionally one yes/no per action.</summary>
    Jev,

    /// <summary>Learned from your own play (recorded demonstrations, nearest neighbours). Local, ~1 ms.</summary>
    Learned,
}

/// <summary>
///     One entry of the decision-model list. AutoPlay profiles pick a model by <see cref="Id"/>, so the same
///     model (and its settings) can serve several profiles. Built-in entries are seeded on load and can be
///     tuned but not deleted.
/// </summary>
public class DecisionModelDefinition : NotificationObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set => SetProperty(ref field, value); } = "";

    public DecisionModelKind Kind
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            RaisePropertyChanged(nameof(UsesModelName));
            RaisePropertyChanged(nameof(SupportsPerAction));
            RaisePropertyChanged(nameof(UsesThreshold));
        }
    }

    /// <summary>Ollama model name, or the Jev model (blank = the Jev settings' model). Unused otherwise.</summary>
    public string Model { get; set => SetProperty(ref field, value); } = "";

    /// <summary>Jev: ask one yes/no question per action instead of one "which ability" choice.</summary>
    public bool AskPerAction
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) RaisePropertyChanged(nameof(UsesThreshold));
        }
    }

    /// <summary>Per-action and learned models: yes-probability at or above which an action is used.</summary>
    public double ActionThreshold { get; set => SetProperty(ref field, value); } = 0.6;

    /// <summary>Jev: mode confidence below this keeps the previous intent. Null = the Jev settings' value.</summary>
    public double? MinConfidence { get; set => SetProperty(ref field, value); }

    public bool IsBuiltIn
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) RaisePropertyChanged(nameof(CanDelete));
        }
    }

    [JsonIgnore] public bool CanDelete => !IsBuiltIn;

    /// <summary>Whether <see cref="Model"/> means anything for this kind.</summary>
    [JsonIgnore]
    public bool UsesModelName => Kind is DecisionModelKind.OllamaVision or DecisionModelKind.OllamaText or DecisionModelKind.Jev;

    [JsonIgnore] public bool SupportsPerAction => Kind == DecisionModelKind.Jev;

    /// <summary>Whether <see cref="ActionThreshold"/> is used (per-action Jev, learned).</summary>
    [JsonIgnore]
    public bool UsesThreshold => Kind == DecisionModelKind.Learned || (Kind == DecisionModelKind.Jev && AskPerAction);

    /// <summary>Changes whenever a setting that shapes the runtime model changes (cache key).</summary>
    [JsonIgnore]
    public string Signature => $"{Id}|{Kind}|{Model}|{AskPerAction}|{ActionThreshold}|{MinConfidence}";

    public override string ToString() => Name;
}
