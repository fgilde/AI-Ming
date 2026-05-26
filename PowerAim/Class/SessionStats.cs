using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PowerAim.Class;

/// <summary>
///     Per-session metrics collector. All inference-loop and action-system writes funnel through
///     this singleton, and the Debug-Overlay + Stats sidebar page read from it.
///     <para>
///     Designed to be lock-free in the hot path: counters use <see cref="System.Threading.Interlocked"/>,
///     latest-value gauges are simple field writes (no atomicity required since they're only
///     surfaced via <see cref="INotifyPropertyChanged"/> on a UI polling timer).
///     </para>
///     <para>
///     Resets on <see cref="Reset"/>; <see cref="StartedAt"/> tracks session start.
///     </para>
/// </summary>
public sealed class SessionStats : INotifyPropertyChanged
{
    public static SessionStats Instance { get; } = new();

    // -------- Counters --------
    private long _shotsFired;
    private long _detectionsTotal;
    private long _framesProcessed;
    private long _tacticalActionsUsed;

    public long ShotsFired         => System.Threading.Interlocked.Read(ref _shotsFired);
    public long DetectionsTotal    => System.Threading.Interlocked.Read(ref _detectionsTotal);
    public long FramesProcessed    => System.Threading.Interlocked.Read(ref _framesProcessed);
    public long TacticalActionsUsed => System.Threading.Interlocked.Read(ref _tacticalActionsUsed);

    // -------- Gauges (latest values) --------

    /// <summary>Time the last <c>Predict()</c> call took, in milliseconds.</summary>
    public double LastInferenceMs { get; set => SetField(ref field, value); }

    /// <summary>Rolling-average inference loop FPS over a short window.</summary>
    public double InstantFps { get; set => SetField(ref field, value); }

    /// <summary>How many enemies the last frame saw.</summary>
    public int LastDetectionCount { get; set => SetField(ref field, value); }

    /// <summary>Most recent strategic intent from the Ollama layer ("default" when idle).</summary>
    public string LastIntent { get; set => SetField(ref field, value ?? ""); } = "";

    /// <summary>Name of the currently active ActionTrigger (if any).</summary>
    public string ActiveTriggerName { get; set => SetField(ref field, value ?? ""); } = "";

    /// <summary>Name of the currently active AutoPlay profile (if any).</summary>
    public string ActiveProfileName { get; set => SetField(ref field, value ?? ""); } = "";

    /// <summary>Session start (UTC). Set on <see cref="Reset"/>.</summary>
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;

    public TimeSpan Duration => DateTime.UtcNow - StartedAt;

    /// <summary>Rough hit-rate estimate: detections ÷ shots fired, clamped 0-1.</summary>
    public double HitRateEstimate
    {
        get
        {
            var shots = ShotsFired;
            if (shots <= 0) return 0;
            // Detections aren't hits, but they correlate. Treat (detections seen this session
            // divided by shots) as a coarse proxy. Hard to do better without per-game OCR.
            return Math.Clamp((double)DetectionsTotal / Math.Max(1, shots * 4), 0, 1);
        }
    }

    // -------- Mutators (hot-path callers) --------

    public void IncrementShots()              => System.Threading.Interlocked.Increment(ref _shotsFired);
    public void IncrementFrames()             => System.Threading.Interlocked.Increment(ref _framesProcessed);
    public void IncrementTacticalActions()    => System.Threading.Interlocked.Increment(ref _tacticalActionsUsed);
    public void AddDetections(int count)
    {
        if (count > 0) System.Threading.Interlocked.Add(ref _detectionsTotal, count);
    }

    public void Reset()
    {
        System.Threading.Interlocked.Exchange(ref _shotsFired, 0);
        System.Threading.Interlocked.Exchange(ref _detectionsTotal, 0);
        System.Threading.Interlocked.Exchange(ref _framesProcessed, 0);
        System.Threading.Interlocked.Exchange(ref _tacticalActionsUsed, 0);
        LastInferenceMs = 0;
        InstantFps = 0;
        LastDetectionCount = 0;
        LastIntent = "";
        StartedAt = DateTime.UtcNow;
        // Counters use Interlocked and don't fire PropertyChanged on every tick — UI polls.
    }

    // -------- INotifyPropertyChanged --------
    public event PropertyChangedEventHandler? PropertyChanged;
    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
