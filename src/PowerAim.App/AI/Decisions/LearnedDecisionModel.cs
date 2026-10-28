using PowerAim.AI.AutoPlay;
using PowerAim.Config;

namespace PowerAim.AI.Decisions;

/// <summary>
///     Plays like you: finds the recorded moments most similar to now (k nearest neighbours over
///     <see cref="DecisionFeatures"/>) and does what you did then. The share of neighbours holding an action is
///     its probability. Movement becomes the intent's mode/direction, abilities above the threshold fire.
///     Local and ~1 ms even with the full sample cap, so it can decide every 100 ms.
/// </summary>
public sealed class LearnedDecisionModel(DecisionModelDefinition definition, DemonstrationStore? store = null) : IDecisionModel
{
    public const int K = 9;

    /// <summary>Below this many samples for the profile the model abstains.</summary>
    public const int MinSamples = 30;

    private readonly DemonstrationStore _store = store ?? DemonstrationStore.Instance;

    public string Source => "Learned";
    public string? LastError { get; private set; }
    public double? LastLatencyMs { get; private set; }

    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);

    public Task<AutoPlayIntent?> DecideAsync(DecisionRequest request, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var samples = _store.For(request.Profile.Id);
        if (samples.Count < MinSamples)
        {
            LastError = $"only {samples.Count} recorded samples for this profile (need {MinSamples}) — record some play first";
            return Task.FromResult<AutoPlayIntent?>(null);
        }

        var s = request.Situation;
        var probs = Probabilities(samples, DecisionFeatures.From(s.Seen, s.SecondsSinceEnemy, s.Ammo, s.Health));
        var intent = ToIntent(probs, request, definition.ActionThreshold);
        LastLatencyMs = sw.Elapsed.TotalMilliseconds;
        LastError = null;
        return Task.FromResult<AutoPlayIntent?>(intent);
    }

    /// <summary>Per action name: distance-weighted share of the <see cref="K"/> nearest samples that held it.</summary>
    public static Dictionary<string, double> Probabilities(IReadOnlyList<Demonstration> samples, float[] features)
    {
        var nearest = samples
            .Select(d => (d, dist: Distance(d.Features, features)))
            .OrderBy(x => x.dist)
            .Take(K)
            .ToList();
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double total = 0;
        foreach (var (d, dist) in nearest)
        {
            double w = 1 / (0.05 + Math.Sqrt(dist));
            total += w;
            foreach (var a in d.Actions) result[a] = result.GetValueOrDefault(a) + w;
        }
        foreach (var key in result.Keys.ToList()) result[key] /= total;
        return result;
    }

    /// <summary>Turns action probabilities into an intent (movement → mode/direction, the rest → actions).</summary>
    public static AutoPlayIntent ToIntent(IReadOnlyDictionary<string, double> probs, DecisionRequest request, double threshold)
    {
        var map = AutoPlayActionMap.Build(request.Profile);
        double P(ControlRole role) => map.Get(role)?.Name is { } n ? probs.GetValueOrDefault(n) : 0;

        double fwd = P(ControlRole.MoveForward), back = P(ControlRole.MoveBackward);
        double left = P(ControlRole.MoveLeft), right = P(ControlRole.MoveRight);
        var moves = new[] { ("forward", fwd), ("backward", back), ("left", left), ("right", right) };
        var (dir, dirP) = moves.MaxBy(m => m.Item2);

        bool enemy = request.Situation.Seen.Nearest != null;
        string mode = dirP < 0.3 ? "hold"
            : enemy ? (back > fwd ? "retreat" : "engage")
            : (dir == "backward" ? "retreat" : "explore");

        var actions = map.Strategic
            .Where(a => probs.GetValueOrDefault(a.Name!) >= threshold)
            .OrderByDescending(a => probs[a.Name!])
            .Select(a => a.Name!)
            .ToList();

        return new AutoPlayIntent(mode, dirP < 0.3 ? null : dir, null, DateTime.Now, "Learned",
            Confidence: dirP, ModeProbabilities: moves.ToDictionary(m => m.Item1, m => m.Item2), Actions: actions);
    }

    private static double Distance(float[] a, float[] b)
    {
        double sum = 0;
        for (int i = 0; i < a.Length; i++) { double d = a[i] - b[i]; sum += d * d; }
        return sum;
    }
}
