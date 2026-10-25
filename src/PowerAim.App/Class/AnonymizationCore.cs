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
        => WinAnonymizer.Anonymizer.Current.SpoofWindowIdentity(window);

    public static void HideFromShell(Window window)
        => WinAnonymizer.Anonymizer.Current.HideFromShell(window);

    public static void SpoofPebImagePath()
        => WinAnonymizer.Anonymizer.Current.SpoofPebImagePath();

    /// <summary>Stub — no-op. Real version sampled a log-normal human reaction delay.</summary>
    public static TimeSpan SampleReactionTime() => WinAnonymizer.Anonymizer.Current.SampleReactionTime();

    /// <summary>Stub — no-op. Real version added Gaussian jitter to the aim loop's frame delay.</summary>
    public static int SampleTimingJitterMs() => WinAnonymizer.Anonymizer.Current.SampleTimingJitterMs();

    /// <summary>Stub — neutral factor (1.0 = no jitter). Real version randomized network polling intervals.</summary>
    public static double SampleNetworkIntervalFactor() => WinAnonymizer.Anonymizer.Current.SampleNetworkIntervalFactor();

    public static IReadOnlyList<(int dx, int dy)> HumanizeMouseDelta(int dx, int dy)
        => WinAnonymizer.Anonymizer.Current.HumanizeMouseDelta(dx, dy);

    public static string RandomUserAgent()
        => WinAnonymizer.Anonymizer.Current.RandomUserAgent();

    public static bool LaunchWithSpoofedParent(string exePath, string? workingDir = null)
        => WinAnonymizer.Anonymizer.Current.LaunchWithSpoofedParent(exePath, workingDir);
}
