using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Aimmy2.Types;
using Core;

namespace Aimmy2;

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

    private static ThemePalette _theme = ThemePalette.DarkPalette;
    public static Visibility DebugVisibility => IsDebug ? Visibility.Visible : Visibility.Collapsed;
    public static string ApplicationVersionStr => $"v{ApplicationVersion}";
    public static Version? ApplicationVersion => typeof(ApplicationConstants).Assembly.GetName().Version;

    public const string DefaultModel = "default.onnx";
    public const string ShowOnly = "";
    public const bool EasyMode = false;

    public static Visibility EasyModeHidden => EasyMode ? Visibility.Collapsed : Visibility.Visible;
    public static string[] DisabledFeatures => EasyMode ? ["AimAssist", "AntiRecoil", "ASP2", "AimConfig", "ARConfig"] : [];

    public static ThemePalette Theme
    {
        get => _theme;
        set
        {
            _theme = value;
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
    }

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
