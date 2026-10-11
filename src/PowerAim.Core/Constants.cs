using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;


namespace PowerAim.Core;


public class Constants
{
    public const float MinMagnificationFactor = 0.1f;
    public const float MaxMagnificationFactor = 100;

    // ---- Local content directories (relative to the app base dir) ----------------------------
    public const string BinBasePath = "bin";
    public const string ModelsBasePath = "bin\\models";
    public const string ConfigBasePath = "bin\\configs";
    public const string AntiRecoilConfigBasePath = "bin\\anti_recoil_configs";
    public const string ImagesBasePath = "bin\\images";
    public const string LabelsBasePath = "bin\\labels";

    public const string ModelFileFilter = "*.onnx";
    public const string ConfigFileFilter = "*.cfg";
    public const string ModelFileExtension = ".onnx";
    public const string ConfigFileExtension = ".cfg";


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
    // Core identity
    "PowerAim",
    "PixelCraft",
    "Pixel Craft",
    "PixelCraft",

    // Legacy-inspired names
    "Precision House",
    "Clicksmith",
    "Reticle Works",
    "Target Theory",
    "Flick Factory",
    "Crosshair Club",
    "Precision Pilot",
    "Vector Vision",
    "Mouse Maestro",
    "Reflex Foundry",
    "Cursor Command",

    // New additions
    "Pixel Ranger",
    "Reticle Runner",
    "Flick Master",
    "Target Scholar",
    "Crosshair Sage",
    "Precision Lab",
    "Vector Hunter",
    "Click Commander",
    "Reflex Rogue",
    "Pixel Sniper",
    "Target Forge",
    "Cursor Knight",
    "Flick Architect",
    "Precision Prime",
    "Reticle Elite",
    "Vector Vault",
};

    private static readonly string[] Infos =
    {
    "Precision built for players who refuse to settle.",
    "Turn every movement into a confident decision.",
    "Sharper control for faster reactions.",
    "Made for clean clicks and confident plays.",
    "Your setup just gained another level of finesse.",
    "Consistency when every fraction of a second matters.",
    "A refined edge for competitive play.",
    "Less hesitation. More confidence.",
    "Where smooth movement meets sharp execution.",
    "Built for players who take every detail seriously.",
    "Turn frantic moments into controlled plays.",
    "Precision that feels natural from the first click.",
    "Make every movement count.",
    "Play cleaner, react faster, stay composed.",
    "Because controlled movement beats unnecessary chaos.",
    "Your crosshair deserves better.",
    "Every pixel matters when the pressure is on.",
    "Short movements. Big results.",
    "Even difficult moments feel more manageable.",
    "Warning: Confidence levels may increase.",
    "Stay focused. Stay consistent.",
    "Your screen just became your playground.",
    "For players who demand consistency.",
    "Smooth movement. Sharp results.",
    "Clean clicks without the clutter.",
    "Built around consistency and control.",
    "Precision taken to another level.",
    "Designed for players who notice every detail.",
};

    private static readonly string[] Slogans =
    {
    "PixelCraft - Precision Starts Here.",
    "Precision Without Compromise.",
    "Built For Cleaner Plays.",
    "Sharper Moves. Stronger Results.",
    "Stay Calm. Stay Precise.",
    "Every Click Has Purpose.",
    "Where Control Meets Confidence.",
    "Crafted For Competitive Play.",
    "Move Smooth. React Fast.",
    "Consistency Is The Difference.",
    "Make Every Pixel Count.",
    "Refined Movement. Refined Results.",
    "Confidence In Every Click.",
    "Precision For The Modern Player.",
    "Turn Focus Into Results.",
    "Clean Movement. Clear Decisions.",
    "Built Around Precision.",
    "Your Play. Refined.",
    "Control The Moment.",
    "Sharper Reactions Start Here.",
    "Precision Is A Practice.",
    "Designed For Clutch Moments.",
    "Less Hesitation. More Execution.",
    "Stay Focused. Play Cleaner.",
    "Every Movement Matters.",
    "Control Meets Competition.",
    "Precision That Feels Natural.",
    "Take Control Of Every Moment.",
};
    private static readonly Random random = new Random();

    public static bool IsDebug => Assembly.GetEntryAssembly()?.GetCustomAttribute<DebuggableAttribute>()?.IsJITTrackingEnabled ?? false;

    public static string ApplicationName => Names[random.Next(Names.Length)];
    public static string ApplicationInfo => Infos[random.Next(Infos.Length)];
    public static string ApplicationSlogan => Slogans[random.Next(Slogans.Length)];

}