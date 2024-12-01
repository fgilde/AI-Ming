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

    private static readonly string[] Names =
    {
        "AI-M ME Winehouse",
        "AI-Machine",
        "Aim A.I. Little Higher",
        "AIM-Botox",
        "Drunken AIrcher",
        "AI'll Be Back",
        "AIM-Possible",
        "A.I.migo",
        "AI-M King",
        "Aimmy",
        "Mousemovement Machine",
        "Micro AI-mbot",
    };

    private static readonly string[] Infos =
    {
        "The only thing impossible is missing your target.",
        "For those who believe aiming high isn't high enough.",
        "Giving your aim that extra lift, without the needles.",
        "Perfect aim, even when you've had one too many.",
        "Your aim just got terminated.",
        "Mission accomplished, every single time.",
        "Your new best friend in hitting the bullseye."
    };

    private static readonly string[] Slogans =
    {
        "AI'mpossible - Aim for the stars, even when sober.",
        "Aim A.I. Little Higher - Because the sky's just the beginning.",
        "AIM-Botox - Smooth and wrinkle-free aiming.",
        "Drunken AIrcher - Aim like nobody's watching.",
        "AI'll Be Back - Hasta la vista, missed shots.",
        "AIM-Possible - The odds are always in your favor.",
        "A.I.migo - Always by your side, and never missing."
    };
    private static readonly Random random = new Random();

    public static bool IsDebug => Assembly.GetEntryAssembly()?.GetCustomAttribute<DebuggableAttribute>()?.IsJITTrackingEnabled ?? false;

    public static string ApplicationName => Names[random.Next(Names.Length)];
    public static string ApplicationInfo => Infos[random.Next(Infos.Length)];
    public static string ApplicationSlogan => Slogans[random.Next(Slogans.Length)];

}
