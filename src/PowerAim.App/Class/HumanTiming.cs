namespace PowerAim.Class;

/// <summary>
///     Shared timing helpers for the aim and trigger pipelines. Wraps
///     <see cref="AnonymizationCore"/> so action classes don't need a
///     direct dependency on the full anonymization surface.
/// </summary>
public static class HumanTiming
{
    /// <summary>
    ///     Gaussian timing jitter (σ≈4ms, ±12ms). Intended to be ADDED to a
    ///     fixed inter-frame or inter-trigger delay so wall-clock periodicity
    ///     is broken.
    /// </summary>
    public static int JitterMs() => AnonymizationCore.SampleTimingJitterMs();

    /// <summary>
    ///     Log-normal trigger reaction delay matching human FPS-game distributions
    ///     (mean≈220ms, σ≈50ms, clamped 60-600ms).
    /// </summary>
    public static TimeSpan ReactionDelay() => AnonymizationCore.SampleReactionTime();

    /// <summary>
    ///     Jitter factor for network polling intervals. Multiply by the base
    ///     interval in seconds before passing to Task.Delay.
    /// </summary>
    public static double NetworkFactor() => AnonymizationCore.SampleNetworkIntervalFactor();

    /// <summary>
    ///     Await a jittered delay: <paramref name="baseMs"/> ± Gaussian noise.
    ///     Cancellation-safe.
    /// </summary>
    public static Task DelayJitteredAsync(int baseMs, CancellationToken ct = default)
    {
        int jittered = Math.Max(0, baseMs + JitterMs());
        return Task.Delay(jittered, ct);
    }

    /// <summary>
    ///     Await a jittered delay: <paramref name="baseMs"/> ± Gaussian noise.
    ///     Cancellation-safe.
    /// </summary>
    public static Task DelayJitteredAsync(TimeSpan baseDelay, CancellationToken ct = default)
        => DelayJitteredAsync((int)baseDelay.TotalMilliseconds, ct);
}
