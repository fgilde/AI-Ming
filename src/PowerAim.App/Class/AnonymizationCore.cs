using System.Windows;

namespace PowerAim.Class;

/// <summary>
///     Stub. The original implementation (window identity spoofing, process-tree parent
///     spoofing, PEB ImagePath spoof, human-timing distributions, network header
///     randomization) came in from a third-party source we don't have rights to and don't
///     want to ship — anti-cheat-detection evasion is out of scope for this project.
///     <para>
///     Signatures are kept identical to the original so every call site compiles unchanged:
///     unused methods throw <see cref="NotImplementedException"/>, methods on the one
///     code path that was actually wired in (<see cref="HumanTiming"/> → the aim loop's
///     frame jitter) return a neutral/no-op value instead. Drop a real implementation
///     back into this file and everything upstream keeps working as-is.
///     </para>
/// </summary>
public static class AnonymizationCore
{
    public static void SpoofWindowIdentity(Window window)
        => throw new NotImplementedException("AnonymizationCore.SpoofWindowIdentity: stub, not shipped.");

    public static void HideFromShell(Window window)
        => throw new NotImplementedException("AnonymizationCore.HideFromShell: stub, not shipped.");

    public static void SpoofPebImagePath()
        => throw new NotImplementedException("AnonymizationCore.SpoofPebImagePath: stub, not shipped.");

    /// <summary>Stub — no-op. Real version sampled a log-normal human reaction delay.</summary>
    public static TimeSpan SampleReactionTime() => TimeSpan.Zero;

    /// <summary>Stub — no-op. Real version added Gaussian jitter to the aim loop's frame delay.</summary>
    public static int SampleTimingJitterMs() => 0;

    /// <summary>Stub — neutral factor (1.0 = no jitter). Real version randomized network polling intervals.</summary>
    public static double SampleNetworkIntervalFactor() => 1.0;

    public static IReadOnlyList<(int dx, int dy)> HumanizeMouseDelta(int dx, int dy)
        => throw new NotImplementedException("AnonymizationCore.HumanizeMouseDelta: stub, not shipped.");

    public static string RandomUserAgent()
        => throw new NotImplementedException("AnonymizationCore.RandomUserAgent: stub, not shipped.");

    public static bool LaunchWithSpoofedParent(string exePath, string? workingDir = null)
        => throw new NotImplementedException("AnonymizationCore.LaunchWithSpoofedParent: stub, not shipped.");
}
