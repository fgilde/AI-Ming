namespace Core.Jev;

/// <summary>
///     Builds the <c>questions</c> map for a Jev decision. Option keys are single digits on purpose:
///     simple-jev requires every answer label to be exactly one token, and digits are one token in every
///     tokenizer. The meaning lives in <c>instructions</c> and in the <c>criteria</c> descriptions — which
///     carry the profile's own action names, descriptions and types, so the model decides against what the
///     bot can ACTUALLY do. <see cref="JevIntentMapper"/> maps the digits back.
/// </summary>
public static class JevQuestionBuilder
{
    public static readonly string[] Modes = ["explore", "engage", "retreat", "hold", "tactical"];
    public static readonly string[] Directions = ["forward", "backward", "left", "right"];

    /// <summary>Abilities beyond this are not offered — keeps ability keys single-digit.</summary>
    public const int MaxAbilities = 9;

    private static readonly string[] ModeDescriptions =
    [
        "explore: move around the map looking for enemies",
        "engage: push toward the visible enemies and fight",
        "retreat: back off and break line of sight",
        "hold: stay put and cover the current position",
        "tactical: use one of the abilities listed below right now",
    ];

    /// <summary>The abilities actually offered, after filtering blanks and capping — mirror this when mapping answers.</summary>
    public static List<JevAbility> Offerable(IReadOnlyList<JevAbility> abilities)
        => abilities.Where(a => !string.IsNullOrWhiteSpace(a.Name)).Take(MaxAbilities).ToList();

    public static Dictionary<string, object> Build(IReadOnlyList<JevAbility> abilities)
    {
        var usable = Offerable(abilities);

        // "tactical" only makes sense when there is something to use.
        int modeCount = usable.Count > 0 ? Modes.Length : Modes.Length - 1;
        var modes = new Dictionary<string, string>();
        for (int i = 0; i < modeCount; i++) modes[(i + 1).ToString()] = ModeDescriptions[i];

        var directions = new Dictionary<string, string>();
        for (int i = 0; i < Directions.Length; i++) directions[(i + 1).ToString()] = Directions[i];

        var q = new Dictionary<string, object>
        {
            ["mode"] = new
            {
                type = "choice",
                instructions = "You guide a bot in the game described by the state. Pick its tactical mode for the next second. "
                               + string.Join(" ", modes.Select(kv => $"{kv.Key} = {kv.Value}.")),
                criteria = modes,
            },
            ["direction"] = new
            {
                type = "choice",
                instructions = "If the bot moves, which way? " + string.Join(" ", directions.Select(kv => $"{kv.Key} = {kv.Value}.")),
                criteria = directions,
            },
            ["danger"] = new
            {
                type = "noul",
                instructions = "Is the bot in immediate danger (low health, outnumbered, enemy very close) and should retreat right now?",
            },
        };

        if (usable.Count > 0)
        {
            // Labels carry the profile's description + action type, so the model picks on meaning, not name guessing.
            var criteria = new Dictionary<string, string>();
            for (int i = 0; i < usable.Count; i++) criteria[(i + 1).ToString()] = usable[i].Label;
            q["ability"] = new
            {
                type = "choice",
                instructions = "If an ability should be used now, which one? These are the bot's own configured actions. "
                               + string.Join(" ", criteria.Select(kv => $"{kv.Key} = {kv.Value}.")),
                criteria,
            };
        }
        return q;
    }
}
