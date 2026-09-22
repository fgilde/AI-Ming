namespace Core.Jev;

/// <summary>Maps a raw <see cref="JevDecision"/> (digit option keys) back onto mode / direction / ability names.</summary>
public static class JevIntentMapper
{
    /// <summary>A "danger" yes-probability at or above this forces <c>retreat</c> regardless of the picked mode.</summary>
    public const double DangerThreshold = 0.7;

    /// <param name="abilities">The SAME list handed to <see cref="JevQuestionBuilder.Build"/> — indices must line up.</param>
    /// <returns>
    ///     The intent, or <c>null</c> when there is no usable mode answer or its confidence is below
    ///     <paramref name="minConfidence"/> (a flat distribution shouldn't yank the bot around — caller keeps its current intent).
    /// </returns>
    public static JevIntent? Map(JevDecision decision, IReadOnlyList<JevAbility> abilities, double minConfidence)
    {
        if (!decision.Choices.TryGetValue("mode", out var mode)) return null;
        if (mode.Confidence < minConfidence) return null;

        string modeName = IndexOf(mode.Choice, JevQuestionBuilder.Modes.Length) is { } mi
            ? JevQuestionBuilder.Modes[mi]
            : "default";

        // Re-key the distribution by mode name so a UI can show "engage 62 % / explore 21 % …".
        var modeProbs = new Dictionary<string, double>();
        foreach (var kv in mode.Probabilities)
            if (IndexOf(kv.Key, JevQuestionBuilder.Modes.Length) is { } pi)
                modeProbs[JevQuestionBuilder.Modes[pi]] = kv.Value;

        double danger = decision.Nouls.TryGetValue("danger", out var dv) ? dv : 0;
        if (danger >= DangerThreshold) modeName = "retreat";

        string? direction = null;
        if (decision.Choices.TryGetValue("direction", out var dir) &&
            IndexOf(dir.Choice, JevQuestionBuilder.Directions.Length) is { } di)
            direction = JevQuestionBuilder.Directions[di];

        string? action = null;
        if (modeName == "tactical" && decision.Choices.TryGetValue("ability", out var ab))
        {
            var usable = JevQuestionBuilder.Offerable(abilities);
            if (IndexOf(ab.Choice, usable.Count) is { } ai) action = usable[ai].Name;
        }

        return new JevIntent(modeName, direction, action, mode.Confidence, modeProbs, danger);
    }

    /// <summary>"1".."N" → 0..N-1, else null.</summary>
    private static int? IndexOf(string key, int count)
        => int.TryParse(key, out var n) && n >= 1 && n <= count ? n - 1 : null;
}
