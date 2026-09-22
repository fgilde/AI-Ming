namespace Core.Jev;

/// <summary>
///     One thing the bot can do, as declared in the AutoPlay profile. This is the profile's own
///     vocabulary — <see cref="Name"/> is what the user named the action, <see cref="Description"/> is the
///     field the profile editor explicitly provides "to give the LLM context", and <see cref="Type"/> is the
///     action type (Continuous / Instant / Modifier / Toggle) so the model knows a tap from a hold.
///     <see cref="Role"/> is the semantic slot the heuristic matched it to ("shoot", "reload", …) or null
///     for a free-form ability; it is informational — the caller decides which ones are actually offerable.
/// </summary>
public sealed record JevAbility(string Name, string? Description = null, string? Type = null, string? Role = null)
{
    /// <summary>One-line label for a criteria entry: what it is called, what it does, how it fires.</summary>
    public string Label
    {
        get
        {
            var text = string.IsNullOrWhiteSpace(Description) ? Name : $"{Name}: {Description.Trim()}";
            return string.IsNullOrWhiteSpace(Type) ? text : $"{text} ({Type.ToLowerInvariant()})";
        }
    }
}
