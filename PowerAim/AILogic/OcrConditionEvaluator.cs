using System.Globalization;
using PowerAim.Config;

namespace PowerAim.AILogic;

/// <summary>
///     Shared comparison logic for OCR-driven gates: trigger conditions
///     (<see cref="ActionTrigger.OcrConditions"/>) and aim-disengage rules
///     (<see cref="AimDisengageRule"/>). Numeric comparisons require both sides to parse as numbers;
///     equality falls back to a trimmed case-insensitive text compare.
/// </summary>
public static class OcrConditionEvaluator
{
    public static bool Evaluate(OcrComparison comparison, string? value, OcrResult reading)
    {
        var target = value ?? "";
        switch (comparison)
        {
            case OcrComparison.Contains:
                return (reading.Text ?? "").Contains(target, StringComparison.OrdinalIgnoreCase);
            case OcrComparison.NotContains:
                return !(reading.Text ?? "").Contains(target, StringComparison.OrdinalIgnoreCase);
        }

        bool haveNumbers = reading.Number.HasValue &&
                           double.TryParse(target, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
        double left = reading.Number ?? 0;
        double right = haveNumbers ? double.Parse(target, NumberStyles.Any, CultureInfo.InvariantCulture) : 0;

        return comparison switch
        {
            OcrComparison.GreaterThan => haveNumbers && left > right,
            OcrComparison.GreaterOrEqual => haveNumbers && left >= right,
            OcrComparison.LessThan => haveNumbers && left < right,
            OcrComparison.LessOrEqual => haveNumbers && left <= right,
            OcrComparison.Equals => haveNumbers
                ? Math.Abs(left - right) < 0.0001
                : string.Equals((reading.Text ?? "").Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase),
            OcrComparison.NotEquals => haveNumbers
                ? Math.Abs(left - right) >= 0.0001
                : !string.Equals((reading.Text ?? "").Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }
}

/// <summary>
///     Evaluates the user's <see cref="AimDisengageRule"/> list against the live OCR readings to
///     decide whether aim assist should pause right now (e.g. while scoped or holding a knife).
/// </summary>
public static class AimDisengage
{
    public static bool ShouldPause()
    {
        var rules = AppConfig.Current?.AimDisengageRules;
        if (rules == null || rules.Count == 0) return false;
        if (AppConfig.Current!.OcrSettings is not { Enabled: true }) return false;

        var latest = OcrService.Instance.Latest;
        foreach (var rule in rules)
        {
            if (!rule.Enabled || string.IsNullOrWhiteSpace(rule.RegionName)) continue;
            if (!ProcessApplies(rule.MatchProcess)) continue;
            if (!latest.TryGetValue(rule.RegionName, out var reading)) continue;
            if (OcrConditionEvaluator.Evaluate(rule.Comparison, rule.Value, reading)) return true;
        }

        return false;
    }

    private static bool ProcessApplies(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return true;
        return PowerAim.Class.ProcessMatcher.Matches(pattern, PowerAim.Class.WindowFocusWatcher.Instance.CurrentProcessName);
    }
}
