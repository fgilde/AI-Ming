using System.ComponentModel;
using System.IO;
using Newtonsoft.Json;

namespace PowerAim.AILogic;

/// <summary>
///     A tiny state→action frequency table that captures the user's habitual responses to a
///     handful of discretized game-states (e.g. "enemy in centre" → mostly "shoot", "no enemy" →
///     mostly "move_forward"). Persisted as JSON so users can inspect, share, and edit it.
/// </summary>
public sealed class AutoPlayLearningModel : INotifyPropertyChanged
{
    private static readonly Lazy<AutoPlayLearningModel> _lazy = new(() => new AutoPlayLearningModel());
    public static AutoPlayLearningModel Instance => _lazy.Value;

    // state → (actionName → count)
    private readonly Dictionary<string, Dictionary<string, int>> _table = new();
    private readonly object _lock = new();
    private int _totalSamples;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int TotalSamples => _totalSamples;
    public int StateCount { get { lock (_lock) return _table.Count; } }

    private void Notify(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    /// <summary>Increment the (state, action) cell. Thread-safe.</summary>
    public void Record(string state, string actionName)
    {
        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(actionName)) return;
        lock (_lock)
        {
            if (!_table.TryGetValue(state, out var row))
            {
                row = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _table[state] = row;
            }
            row.TryGetValue(actionName, out int count);
            row[actionName] = count + 1;
            _totalSamples++;
        }
        Notify(nameof(TotalSamples));
    }

    /// <summary>
    ///     Return the action name with the highest count for the given state, or <c>null</c> if no
    ///     samples exist. Used by the AutoPlay selector when ApplyModel is on.
    /// </summary>
    public string? Preferred(string state)
    {
        lock (_lock)
        {
            if (!_table.TryGetValue(state, out var row) || row.Count == 0) return null;
            string? best = null;
            int bestCount = -1;
            foreach (var kv in row)
                if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
            return best;
        }
    }

    /// <summary>
    ///     Normalized probability distribution for the given state.
    ///     Returns empty dict if state hasn't been observed.
    /// </summary>
    public IReadOnlyDictionary<string, double> Distribution(string state)
    {
        lock (_lock)
        {
            if (!_table.TryGetValue(state, out var row) || row.Count == 0)
                return new Dictionary<string, double>();
            int total = row.Values.Sum();
            if (total == 0) return new Dictionary<string, double>();
            return row.ToDictionary(k => k.Key, v => (double)v.Value / total);
        }
    }

    public void Clear()
    {
        lock (_lock) _table.Clear();
        _totalSamples = 0;
        Notify(nameof(TotalSamples));
        Notify(nameof(StateCount));
    }

    public string DefaultModelPath
    {
        get
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerAim");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "autoplay_model.json");
        }
    }

    public void Save(string? overridePath = null)
    {
        string path = string.IsNullOrEmpty(overridePath) ? DefaultModelPath : overridePath!;
        Dictionary<string, Dictionary<string, int>> snapshot;
        lock (_lock) snapshot = _table.ToDictionary(kv => kv.Key, kv => new Dictionary<string, int>(kv.Value));
        var payload = new { totalSamples = _totalSamples, table = snapshot };
        File.WriteAllText(path, JsonConvert.SerializeObject(payload, Formatting.Indented));
    }

    public bool Load(string? overridePath = null)
    {
        string path = string.IsNullOrEmpty(overridePath) ? DefaultModelPath : overridePath!;
        if (!File.Exists(path)) return false;
        try
        {
            var raw = File.ReadAllText(path);
            var doc = JsonConvert.DeserializeObject<Dictionary<string, object>>(raw);
            if (doc == null) return false;
            int total = doc.TryGetValue("totalSamples", out var ts) ? Convert.ToInt32(ts) : 0;
            var tableObj = doc.TryGetValue("table", out var t) ? t : null;
            var table = tableObj == null
                ? new Dictionary<string, Dictionary<string, int>>()
                : JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(tableObj.ToString()!)
                  ?? new Dictionary<string, Dictionary<string, int>>();
            lock (_lock)
            {
                _table.Clear();
                foreach (var kv in table) _table[kv.Key] = kv.Value;
                _totalSamples = total;
            }
            Notify(nameof(TotalSamples));
            Notify(nameof(StateCount));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
