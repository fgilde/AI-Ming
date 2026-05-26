using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;


namespace Core;


public class Constants
{
    public const float MinMagnificationFactor = 0.1f;
    public const float MaxMagnificationFactor = 100;

    public const string RepoOwner = "fgilde";
    public const string RepoName = "AI-Ming";

    /// <summary>Upstream repo we merge community models from in addition to <see cref="RepoOwner"/>/<see cref="RepoName"/>.</summary>
    public const string UpstreamRepoOwner = "Babyhamsta";
    public const string UpstreamRepoName = "Aimmy";

    /// <summary>
    ///     Central rotating brand-name pool. Used both for the rotating window title
    ///     (<see cref="ApplicationName"/>) and as the random AssemblyName the build script
    ///     stamps onto the produced .exe (see <c>build.ps1</c>). PowerAim is the canonical
    ///     brand and is included multiple times to bias the rotation towards it.
    /// </summary>
    public static readonly string[] Names =
    {
        // Canonical brand (weighted)
        "PowerAim",
        "Power Aim",
        "PowerAim",
        // Heritage names kept from the fork
        "AI-M ME Winehouse",
        "AI-Machine",
        "Aim A.I. Little Higher",
        "AIM-Botox",
        "Drunken AIrcher",
        "AI'll Be Back",
        "AIM-Possible",
        "A.I.migo",
        "AI-M King",
        "Mousemovement Machine",
        "Micro AI-mbot",
        // New additions
        "AimForge",
        "TriggerCraft",
        "Pixel Hunter",
        "Crosshair Whisperer",
        "Headshot Maestro",
        "Smart Reticle",
        "Targetron",
        "Aim Architect",
        "BulletBoss",
        "Auto-Lock-In",
        "Reticle Rogue",
        "FocalAim",
    };

    private static readonly string[] Infos =
    {
        "The only thing impossible is missing your target.",
        "For those who believe aiming high isn't high enough.",
        "Giving your aim that extra lift, without the needles.",
        "Perfect aim, even when you've had one too many.",
        "Your aim just got terminated.",
        "Mission accomplished, every single time.",
        "Your new best friend in hitting the bullseye.",
        "Now with 98% less missing.",
        "Your crosshair just found its soulmate.",
        "Built for legends. Used by questionable players.",
        "Turning panic shots into highlight clips.",
        "Precision so good, it feels illegal.",
        "Missed shots are now a personality issue.",
        "Aim smarter, not harder.",
        "Because spraying and praying is outdated.",
        "Your enemies called. They want a nerf.",
        "Every pixel counts.",
        "The shortest distance between you and victory.",
        "Even your excuses won't miss anymore.",
        "Warning: May cause sudden confidence.",
        "Lock. Aim. Regret nothing.",
        "Your monitor just became dangerous.",
        "For gamers with trust issues in their own aim.",
        "Less recoil. More ego.",
        "Click heads, not excuses.",
        "Your KD ratio's new best friend.",
        "Accuracy upgraded to absurd."
    };

    private static readonly string[] Slogans =
    {
        "AI'mpossible - Aim for the stars, even when sober.",
        "Aim A.I. Little Higher - Because the sky's just the beginning.",
        "AIM-Botox - Smooth and wrinkle-free aiming.",
        "Drunken AIrcher - Aim like nobody's watching.",
        "AI'll Be Back - Hasta la vista, missed shots.",
        "AIM-Possible - The odds are always in your favor.",
        "A.I.migo - Always by your side, and never missing.",
        "PowerAim - Precision Meets Power.",
        "Aim Like Tomorrow Depends On It.",
        "Built Different. Aimed Better.",
        "Zero Fear. Zero Misses.",
        "Lock In. Pop Off.",
        "Aim Beyond Human.",
        "Modern Problems Require Accurate Solutions.",
        "Pixel Perfect Destruction.",
        "Your Aim, But Evolved.",
        "Where Precision Becomes Unfair.",
        "Every Shot Tells A Story.",
        "Stay Calm And Click Heads.",
        "Elite Aim Starts Here.",
        "Aim Faster. Win Harder.",
        "Designed For The Impossible Flick.",
        "PowerAim - Because Missing Hurts.",
        "Confidence In Every Click.",
        "The Future Of Precision.",
        "Target Locked. Victory Loaded.",
        "More Than Aim. Pure Domination."
    };
    private static readonly Random random = new Random();

    public static bool IsDebug => Assembly.GetEntryAssembly()?.GetCustomAttribute<DebuggableAttribute>()?.IsJITTrackingEnabled ?? false;

    public static string ApplicationName => Names[random.Next(Names.Length)];
    public static string ApplicationInfo => Infos[random.Next(Infos.Length)];
    public static string ApplicationSlogan => Slogans[random.Next(Slogans.Length)];

}
