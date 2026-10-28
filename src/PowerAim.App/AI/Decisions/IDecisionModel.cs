using System.Drawing;
using PowerAim.AI.AutoPlay;
using PowerAim.Config;
using PowerAim.Core.Jev;

namespace PowerAim.AI.Decisions;

/// <summary>What the bot knows when a decision model is asked, as numbers rather than pixels.</summary>
public sealed record DecisionSituation(
    string Game,
    AutoPlayPerception Seen,
    double SecondsSinceEnemy,
    double? Ammo,
    double? Health,
    bool Stuck,
    IReadOnlyList<string> RecentDecisions);

/// <param name="Abilities">The profile actions the model may use: name, description, type, role.</param>
/// <param name="ReflexControls">Role keys the reflex drives itself every frame (movement, aim, shoot).</param>
/// <param name="CaptureFrame">Grabs the current frame; only vision models call it.</param>
public sealed record DecisionRequest(
    AutoPlayProfile Profile,
    DecisionSituation Situation,
    IReadOnlyList<JevAbility> Abilities,
    IReadOnlyList<string> ReflexControls,
    Func<Bitmap?> CaptureFrame);

/// <summary>
///     A strategic decision maker for AutoPlay. Asked every <see cref="AutoPlayProfile.DecisionInterval"/>;
///     a null answer keeps the previous intent, so a slow or failing model never stops the reflex.
/// </summary>
public interface IDecisionModel
{
    /// <summary>Shown in the decision panel ("Jev", "Ollama", "Learned", …).</summary>
    string Source { get; }

    string? LastError { get; }

    double? LastLatencyMs { get; }

    Task<bool> IsAvailableAsync(CancellationToken ct);

    Task<AutoPlayIntent?> DecideAsync(DecisionRequest request, CancellationToken ct);
}

/// <summary>The structured state handed to text models (Jev, Ollama text) — the situation as plain JSON fields.</summary>
public static class DecisionState
{
    public static object Build(DecisionRequest r, IEnumerable<JevAbility> offered)
    {
        var s = r.Situation;
        var e = s.Seen.Nearest;
        return new
        {
            game = s.Game,
            enemies_visible = s.Seen.EnemyCount,
            nearest_enemy = e == null ? null : new
            {
                horizontal_offset_px = (int)e.Dx,   // negative = left of crosshair
                vertical_offset_px = (int)e.Dy,     // negative = above crosshair
                distance_px = (int)e.Distance,
                size_of_screen = Math.Round(s.Seen.NearestSize, 2), // box height / screen height: bigger = closer
            },
            s.Ammo,
            s.Health,
            stuck = s.Stuck,
            recent_decisions = s.RecentDecisions,
            seconds_since_last_enemy = Math.Round(s.SecondsSinceEnemy, 1),
            abilities = offered.Select(a => new { name = a.Name, does = a.Description, type = a.Type, role = a.Role }).ToArray(),
            reflex_controls = r.ReflexControls,
        };
    }
}
