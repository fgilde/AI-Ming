namespace PowerAim.Class;

/// <summary>
///     Wildcard-aware matcher for process names. Used by Per-Game Auto-Profile and Auto-Pause
///     to decide whether the current foreground process matches a user-configured pattern.
///     <para>
///     Matching is case-insensitive and ignores a trailing <c>.exe</c>. Supported pattern forms:
///     <list type="bullet">
///       <item><c>cs2</c>          — exact match</item>
///       <item><c>cs2*</c>         — prefix match (matches <c>cs2</c>, <c>cs2_demo</c>, …)</item>
///       <item><c>*cs2</c>         — suffix match</item>
///       <item><c>*cs2*</c>        — substring match</item>
///       <item><c>cs?</c>          — single-char wildcard</item>
///       <item>multi-pattern via <c>|</c>: <c>cs2|valorant|apex*</c></item>
///     </list>
///     </para>
/// </summary>
public static class ProcessMatcher
{
    /// <summary>
    ///     Returns <c>true</c> when <paramref name="currentProcessName"/> matches
    ///     <paramref name="pattern"/>. An empty pattern matches everything (the caller treats
    ///     "no filter" as "always active"). An empty current process never matches a non-empty
    ///     pattern.
    /// </summary>
    public static bool Matches(string? pattern, string? currentProcessName)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return true;
        if (string.IsNullOrWhiteSpace(currentProcessName)) return false;

        var name = Normalize(currentProcessName);
        var parts = pattern.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var raw in parts)
        {
            if (MatchesSingle(Normalize(raw), name)) return true;
        }
        return false;
    }

    private static string Normalize(string s)
    {
        s = s.Trim().ToLowerInvariant();
        if (s.EndsWith(".exe")) s = s[..^4];
        return s;
    }

    private static bool MatchesSingle(string pattern, string name)
    {
        if (string.IsNullOrEmpty(pattern)) return false;
        if (!pattern.Contains('*') && !pattern.Contains('?'))
            return pattern == name;

        // Glob → regex
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex);
    }
}
