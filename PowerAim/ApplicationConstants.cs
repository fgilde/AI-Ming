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
#endif
            return false;
        }
    }

    public static Visibility DebugVisibility => IsDebug ? Visibility.Visible : Visibility.Collapsed;
    public static string ApplicationVersionStr => $"v{ApplicationVersion}";
    public static Version? ApplicationVersion => typeof(ApplicationConstants).Assembly.GetName().Version;

    public const string DefaultModel = "default.onnx";
    public const string ShowOnly = "";
    public const bool EasyMode = false;

    /// <summary>
    ///     URL the in-app Help button opens. Points to the GitHub Pages-hosted documentation.
    ///     Bundled docs live under <c>docs/</c> in the repo and are auto-published to this URL
    ///     by GitHub Pages on push. Update when the repo path changes.
    /// </summary>
    public const string DocsUrl = "https://fgilde.github.io/AI-Ming/";
    public static Uri DocsUri = new Uri(DocsUrl);

    // ---- Project / author links -----------------------------------------------------------
    public const string Copyright    = "© Florian Gilde";
    public static Uri AuthorUrl    = new Uri("https://www.gilde.org");
    public const string RepoOwner    = "fgilde";
    public const string RepoName     = "AI-Ming";
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
