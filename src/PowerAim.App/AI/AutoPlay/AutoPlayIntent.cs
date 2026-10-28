namespace PowerAim.AI.AutoPlay;

/// <summary>
///     High-level intent produced by a decision model, read by the reflex every frame.
///     <see cref="Mode"/> is "explore" | "engage" | "retreat" | "hold" | "tactical" | "default".
///     <see cref="ActionHint"/> names one profile action to use now; <see cref="Actions"/> lists every action a
///     per-action model wants used now (each fires once per intent). <see cref="Source"/> names the model that
///     decided; <see cref="Confidence"/>, <see cref="ModeProbabilities"/> and <see cref="Danger"/> are optional.
/// </summary>
public sealed record AutoPlayIntent(
    string Mode,
    string? Direction,
    string? ActionHint,
    DateTime At,
    string Source = "Heuristic",
    double? Confidence = null,
    IReadOnlyDictionary<string, double>? ModeProbabilities = null,
    double? Danger = null,
    IReadOnlyList<string>? Actions = null)
{
    public static AutoPlayIntent Default { get; } = new("default", null, null, DateTime.MinValue);

    /// <summary>"mode" or "mode/direction", the label kept in the recent-decision history.</summary>
    public string Label => string.IsNullOrEmpty(Direction) ? Mode : $"{Mode}/{Direction}";
}
