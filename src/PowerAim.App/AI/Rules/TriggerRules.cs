using System.Collections.Concurrent;

namespace PowerAim.AI.Rules;

/// <summary>Parses rules once per source text and checks them against a frame's targets.</summary>
public static class TriggerRules
{
    private static readonly ConcurrentDictionary<string, (RuleExpression? Rule, string? Error)> Cache = new();

    /// <summary>The parsed rule, or null with the reason (cached by text, so editing re-parses once).</summary>
    public static (RuleExpression? Rule, string? Error) Compile(string? source)
    {
        source ??= "";
        if (Cache.Count > 256) Cache.Clear();
        return Cache.GetOrAdd(source, s => RuleExpression.TryParse(s, TargetScope.IsKnown, out var r, out var e) ? (r, null) : (null, e));
    }

    /// <summary>True when the rule holds for at least one target.</summary>
    public static bool AnyMatch(RuleExpression rule, IReadOnlyList<RuleTarget> targets, RuleContext context)
    {
        foreach (var t in targets)
            if (rule.IsTrue(new TargetScope(t, context))) return true;
        return false;
    }

    /// <summary>The target with the highest score (NaN scores never win), or null.</summary>
    public static RuleTarget? Best(RuleExpression score, IReadOnlyList<RuleTarget> targets, RuleContext context)
    {
        RuleTarget? best = null;
        double bestScore = double.NegativeInfinity;
        foreach (var t in targets)
        {
            double s = score.Score(new TargetScope(t, context));
            if (double.IsNaN(s) || s <= bestScore) continue;
            bestScore = s;
            best = t;
        }
        return best;
    }
}
