using System.IO;
using System.Text.Json;

namespace PowerAim.AI.AutoPlay;

/// <summary>One recorded moment of your own play: what the bot would have seen, and which profile actions you held.</summary>
public sealed record Demonstration(string ProfileId, float[] Features, string[] Actions);

/// <summary>
///     The situation as a fixed-length vector in roughly 0..1, the input of the learned model. Index meaning
///     must never change once samples are saved (add new features at the end, bump <see cref="Version"/>).
/// </summary>
public static class DecisionFeatures
{
    public const int Version = 1;
    public const int Length = 8;

    public static float[] From(AutoPlayPerception seen, double secondsSinceEnemy, double? ammo, double? health)
    {
        var e = seen.Nearest;
        float halfW = Math.Max(1, seen.Area.Width / 2f), halfH = Math.Max(1, seen.Area.Height / 2f);
        return
        [
            Math.Min(seen.EnemyCount, 5) / 5f,
            e == null ? 0 : Math.Clamp(e.Dx / halfW, -1, 1),
            e == null ? 0 : Math.Clamp(e.Dy / halfH, -1, 1),
            Math.Clamp(seen.NearestSize, 0, 1),
            e == null ? 0 : 1,
            (float)Math.Clamp(secondsSinceEnemy, 0, 10) / 10f,
            (float)Math.Clamp((ammo ?? 30) / 30, 0, 1),
            (float)Math.Clamp((health ?? 100) / 100, 0, 1),
        ];
    }
}

/// <summary>
///     Recorded demonstrations, capped (oldest dropped first) and saved as JSON next to the learning table.
///     Thread-safe: the recorder appends on the AI thread while the learned model reads from the strategist.
/// </summary>
public sealed class DemonstrationStore
{
    public const int Capacity = 20_000;

    private static readonly Lazy<DemonstrationStore> _instance = new(() => new DemonstrationStore());
    public static DemonstrationStore Instance => _instance.Value;

    private readonly List<Demonstration> _samples = new();
    private readonly object _lock = new();

    public event EventHandler? Changed;

    public int Count { get { lock (_lock) return _samples.Count; } }

    /// <summary>Changed since the last <see cref="Save"/> / <see cref="Load"/>.</summary>
    public bool IsDirty { get; private set; }

    public void Add(Demonstration sample)
    {
        if (sample.Features.Length != DecisionFeatures.Length) return;
        lock (_lock)
        {
            if (_samples.Count >= Capacity) _samples.RemoveAt(0);
            _samples.Add(sample);
            IsDirty = true;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Copy of the samples recorded for one AutoPlay profile.</summary>
    public List<Demonstration> For(string profileId)
    {
        lock (_lock) return _samples.Where(s => s.ProfileId == profileId).ToList();
    }

    public void Clear()
    {
        lock (_lock) { _samples.Clear(); IsDirty = true; }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static string PathNextTo(string learningModelPath)
        => Path.Combine(Path.GetDirectoryName(learningModelPath) ?? ".", "autoplay_demonstrations.json");

    /// <summary>Next to the learning table file (its configured path, else its default location).</summary>
    public static string DefaultPath
    {
        get
        {
            var modelPath = Config.AppConfig.Current?.AutoPlayLearningSettings?.ModelPath;
            return PathNextTo(string.IsNullOrWhiteSpace(modelPath) ? AutoPlayLearningModel.Instance.DefaultModelPath : modelPath);
        }
    }

    private sealed record File(int Version, List<Demonstration> Samples);

    public void Save(string path)
    {
        List<Demonstration> copy;
        lock (_lock) copy = _samples.ToList();
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(new File(DecisionFeatures.Version, copy)));
        IsDirty = false;
    }

    /// <summary>
    ///     Replaces the samples with the file's. False (store unchanged) when missing, unreadable or from
    ///     another feature version.
    /// </summary>
    public bool Load(string path)
    {
        File? file;
        try
        {
            if (!System.IO.File.Exists(path)) return false;
            file = JsonSerializer.Deserialize<File>(System.IO.File.ReadAllText(path));
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return false; }
        if (file?.Samples == null || file.Version != DecisionFeatures.Version) return false;
        lock (_lock)
        {
            _samples.Clear();
            _samples.AddRange(file.Samples.Where(s => s.Features?.Length == DecisionFeatures.Length).TakeLast(Capacity));
            IsDirty = false;
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
