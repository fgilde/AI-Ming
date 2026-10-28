using PowerAim.Config;

namespace PowerAim.AI.AutoPlay;

/// <summary>
///     Picks the abilities (non-reflex profile actions) to use. Per-action models list every action they want
///     now in <see cref="AutoPlayIntent.Actions"/>; each is used once per intent, without cooldown. Otherwise
///     one action per cooldown: the model's hint, then the learned habit, then a random free-form ability.
/// </summary>
public sealed class AutoPlayTactics
{
    private readonly Random _rng;
    private DateTime _nextUse = DateTime.MinValue;
    private AutoPlayIntent? _servedIntent;
    private readonly HashSet<string> _served = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<AutoPlayAction, DateTime> _lastUsed = new();

    /// <summary>A model-listed action is not repeated sooner than this, however often the model asks.</summary>
    public static readonly TimeSpan MinRepeat = TimeSpan.FromMilliseconds(400);

    public AutoPlayTactics(Random? rng = null) => _rng = rng ?? new Random();

    /// <summary>Learned preference ("jump", "reload", …) for the current state, or null.</summary>
    public Func<string?>? LearnedPreference { get; set; }

    /// <summary>Probability of following <see cref="LearnedPreference"/> when it has an answer.</summary>
    public double LearnedBias { get; set; }

    /// <param name="ready">The reflex has room for an ability this frame.</param>
    /// <param name="isHeld">Whether an action is already down (never re-pressed).</param>
    public List<AutoPlayAction> Next(AutoPlayIntent intent, AutoPlayActionMap map, bool ready, bool inCombat,
        Func<AutoPlayAction, bool> isHeld, DateTime now)
    {
        var picked = new List<AutoPlayAction>();

        if (intent.Actions is { Count: > 0 } wanted)
        {
            if (!ReferenceEquals(_servedIntent, intent)) { _servedIntent = intent; _served.Clear(); }
            foreach (var name in wanted)
            {
                if (_served.Contains(name)) continue;
                var action = map.Strategic.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
                if (action == null || isHeld(action)) continue;
                if (_lastUsed.TryGetValue(action, out var last) && now - last < MinRepeat) continue;
                _served.Add(name);
                _lastUsed[action] = now;
                picked.Add(action);
            }
            return picked;
        }

        if (!ready || now < _nextUse || map.Strategic.Count == 0 && map.Tactical.Count == 0) return picked;

        AutoPlayAction? choice = null;
        if (!string.IsNullOrEmpty(intent.ActionHint))
        {
            var pool = map.Strategic.Count > 0 ? map.Strategic : map.Tactical;
            choice = pool.FirstOrDefault(a => !isHeld(a) && string.Equals(a.Name, intent.ActionHint, StringComparison.OrdinalIgnoreCase))
                  ?? pool.FirstOrDefault(a => !isHeld(a) && a.Name!.Contains(intent.ActionHint, StringComparison.OrdinalIgnoreCase));
        }

        if (choice == null && LearnedPreference?.Invoke() is { Length: > 0 } preferred && _rng.NextDouble() < LearnedBias)
            choice = map.Tactical.FirstOrDefault(a => !isHeld(a) && a.Name!.Contains(preferred, StringComparison.OrdinalIgnoreCase));

        if (choice == null)
        {
            var candidates = map.Tactical.Where(a => !isHeld(a)).ToList();
            if (candidates.Count == 0) { _nextUse = now.AddSeconds(2); return picked; }
            choice = candidates[_rng.Next(candidates.Count)];
        }

        picked.Add(choice);
        _nextUse = now.AddSeconds(inCombat ? _rng.NextDouble() * 4 + 3 : _rng.NextDouble() * 8 + 6);
        return picked;
    }
}
