using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Aimmy2.Types;
using Core;

namespace Aimmy2;

public class ApplicationConstants: Constants
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
    public static string ApplicationVersionStr => $"v{ApplicationVersion.ToString()}";
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
        }
    }

    public static event PropertyChangedEventHandler StaticPropertyChanged;

    private static void OnStaticPropertyChanged(string propertyName)
    {
        StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
    }

    //public static Color Foreground => GetForegroundFor(MainColor);
    public static Color Foreground => Colors.White; // TODO:

    private static Color GetForegroundFor(Color background)
    {
        var luminance = 1 - (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255;
        return luminance < 0.5 ? Colors.White : Colors.Black;
    }

    public static Color MainColor => Theme.MainColor;
    public static Color AccentColor => Theme.AccentColor;
    public static Color EffectColor => Theme.EffectColor;

}
