namespace Core.Jev;

/// <summary>One answered <c>choice</c> question: picked option key, how concentrated the distribution was (0..1), full distribution.</summary>
public sealed record JevChoice(string Choice, double Confidence, IReadOnlyDictionary<string, double> Probabilities);

/// <summary>Parsed <c>/v1/systemone</c> response: choice answers and noul (yes-probability) answers, both by question id.</summary>
public sealed class JevDecision
{
    public Dictionary<string, JevChoice> Choices { get; } = new();
    public Dictionary<string, double> Nouls { get; } = new();
}

/// <summary>
///     What the strategic layer feeds back to the bot after a Jev decision. <see cref="Mode"/> is one of
///     <see cref="JevQuestionBuilder.Modes"/> (or "default"), <see cref="ModeProbabilities"/> is keyed by mode
///     NAME (not option key) so a UI can show it directly, <see cref="Danger"/> is the raw noul probability.
/// </summary>
public sealed record JevIntent(
    string Mode,
    string? Direction,
    string? Action,
    double Confidence,
    IReadOnlyDictionary<string, double> ModeProbabilities,
    double Danger);
