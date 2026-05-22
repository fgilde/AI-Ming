using System.Diagnostics;

namespace Aimmy2.AILogic;

/// <summary>
///     Lightweight inter-frame pacer for the AI inference loop. Tracks the time since the previous
///     frame and, when a positive cap is requested, sleeps just long enough to keep the achieved
///     throughput at or below <c>maxFps</c>. A cap of <c>0</c> (or negative) disables the limiter
///     and the helper becomes a no-op so callers can leave it wired in unconditionally.
///
///     The helper is intentionally non-thread-safe — a single instance belongs to a single loop.
///     Both an async (<see cref="WaitForNextFrameAsync"/>) and a sync (<see cref="Wait"/>) variant
///     are provided because the fork's <c>AIManager.AiLoop</c> mixes the two styles.
///
///     Ported alongside <see cref="PerformanceBenchmark"/> from upstream Babyhamsta/Aimmy.
/// </summary>
public sealed class FpsCapHelper
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    /// <summary>
    ///     Reset the stopwatch reference point. Useful when the AI loop pauses (toggle off) and
    ///     resumes — we don't want the first post-resume frame to count the entire pause as
    ///     "elapsed time" and skip its delay.
    /// </summary>
    public void Reset() => _sw.Restart();

    /// <summary>
    ///     Async pace. Awaits a <see cref="Task.Delay(TimeSpan, CancellationToken)"/> for the
    ///     remaining slot time. Returns immediately when the cap is <c>&lt;= 0</c>.
    /// </summary>
    public async Task WaitForNextFrameAsync(double maxFps, CancellationToken ct = default)
    {
        if (maxFps <= 0)
        {
            _sw.Restart();
            return;
        }

        var targetMs = 1000.0 / maxFps;
        var elapsed = _sw.Elapsed.TotalMilliseconds;
        var wait = targetMs - elapsed;
        if (wait > 0)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(wait), ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
        }
        _sw.Restart();
    }

    /// <summary>
    ///     Synchronous variant for callers that run on a dedicated <see cref="Thread"/> and don't
    ///     want to <c>await</c>. Uses <see cref="Thread.Sleep(int)"/> with the standard tradeoff:
    ///     timer resolution is millisecond-coarse.
    /// </summary>
    public void Wait(double maxFps)
    {
        if (maxFps <= 0)
        {
            _sw.Restart();
            return;
        }

        var targetMs = 1000.0 / maxFps;
        var elapsed = _sw.Elapsed.TotalMilliseconds;
        var wait = targetMs - elapsed;
        if (wait > 0)
        {
            Thread.Sleep((int)Math.Ceiling(wait));
        }
        _sw.Restart();
    }
}
