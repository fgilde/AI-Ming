using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using PowerAim.Types;
using Core;

namespace PowerAim;

public class ApplicationConstants : Constants
{
    public static bool IsCudaBuild
    {
        get
        {
#if IsCuda
            return true;
#else
            // Runtime fallback: the CUDA/GPU build is the only one that ships the ONNX Runtime TensorRT
            // provider plugin, so its presence identifies the build even if the compile symbol is unset.
            return AILogic.TensorRtRuntime.SupportedInThisBuild();
#endif
        }
    }

    /// <summary>"CUDA" or "DirectML" — the native build variant, for display (e.g. the window title).</summary>
    public static string BuildVariant => IsCudaBuild ? "CUDA" : "DirectML";

    public static Visibility DebugVisibility => IsDebug ? Visibility.Visible : Visibility.Collapsed;
    public static string ApplicationVersionStr => $"v{ApplicationVersion}";
    public static Version? ApplicationVersion => typeof(ApplicationConstants).Assembly.GetName().Version;

    public const string DefaultModel = "default.onnx";
    public const string ShowOnly = "";
    public const bool EasyMode = false;

    /// <summary>Marketing / download landing page (the GitHub Pages site root).</summary>
    public const string WebsiteUrl = "https://fgilde.github.io/AI-Ming/";

    /// <summary>
    ///     URL the in-app Help button opens. Points to the GitHub Pages-hosted <b>documentation</b>
    ///     home — NOT the site root, which is now the marketing landing page (<see cref="WebsiteUrl"/>).
    ///     The docs live under <c>docs/</c> in the repo and are auto-published by GitHub Pages on push.
    /// </summary>
    public const string DocsUrl = "https://fgilde.github.io/AI-Ming/documentation/";
    public static Uri DocsUri = new Uri(DocsUrl);

    // ---- Project / author links -----------------------------------------------------------
    public const string Copyright    = "© Florian Gilde";
    public static Uri AuthorUrl    = new Uri("https://www.gilde.org");
    // RepoOwner / RepoName are inherited from Core.Constants (single source of truth).
    public const string RepoUrl      = "https://github.com/" + RepoOwner + "/" + RepoName;
    public static Uri RepoUri = new Uri(RepoUrl);
    public const string ReleasesUrl  = RepoUrl + "/releases";
    public static Uri IssuesUrl    = new Uri(RepoUrl + "/issues");
    public const string ReleasesApiUrl = "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases";

    public static Visibility EasyModeHidden => EasyMode ? Visibility.Collapsed : Visibility.Visible;
    public static string[] DisabledFeatures => EasyMode ? ["AimAssist", "AntiRecoil", "ASP2", "AimConfig", "ARConfig"] : [];

    public static ThemePalette Theme
    {
        get => field;
        set
        {
            field = value;
            OnStaticPropertyChanged(nameof(Theme));
            OnStaticPropertyChanged(nameof(MainColor));
            OnStaticPropertyChanged(nameof(AccentColor));
            OnStaticPropertyChanged(nameof(EffectColor));
            OnStaticPropertyChanged(nameof(Surface1Color));
            OnStaticPropertyChanged(nameof(Surface2Color));
            OnStaticPropertyChanged(nameof(Surface3Color));
            OnStaticPropertyChanged(nameof(StrokeColor));
            OnStaticPropertyChanged(nameof(StrokeStrongColor));
            OnStaticPropertyChanged(nameof(TextPrimaryColor));
            OnStaticPropertyChanged(nameof(TextSecondaryColor));
            OnStaticPropertyChanged(nameof(TextTertiaryColor));
            OnStaticPropertyChanged(nameof(AccentForegroundColor));
            OnStaticPropertyChanged(nameof(HoverOverlayColor));
            OnStaticPropertyChanged(nameof(PressedOverlayColor));
            OnStaticPropertyChanged(nameof(Foreground));
            OnStaticPropertyChanged(nameof(IsLightTheme));
        }
    } = ThemePalette.DarkPalette;

    public static event PropertyChangedEventHandler? StaticPropertyChanged;

    private static void OnStaticPropertyChanged(string propertyName)
    {
        StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
    }

    public static Color Foreground => Theme.TextPrimaryColor;
    public static bool IsLightTheme => Theme.IsLight;

    public static Color MainColor => Theme.MainColor;
    public static Color AccentColor => Theme.AccentColor;
    public static Color EffectColor => Theme.EffectColor;
    public static Color Surface1Color => Theme.Surface1Color;
    public static Color Surface2Color => Theme.Surface2Color;
    public static Color Surface3Color => Theme.Surface3Color;
    public static Color StrokeColor => Theme.StrokeColor;
    public static Color StrokeStrongColor => Theme.StrokeStrongColor;
    public static Color TextPrimaryColor => Theme.TextPrimaryColor;
    public static Color TextSecondaryColor => Theme.TextSecondaryColor;
    public static Color TextTertiaryColor => Theme.TextTertiaryColor;
    public static Color AccentForegroundColor => Theme.AccentForegroundColor;
    public static Color HoverOverlayColor => Theme.HoverOverlayColor;
    public static Color PressedOverlayColor => Theme.PressedOverlayColor;
}
