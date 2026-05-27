using System.Windows;
using PowerAim.Config;
using PowerAim.Types;
using Microsoft.Win32;

namespace PowerAim.Theme;

public static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static event EventHandler? Applied;

    public static void Initialize()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General ||
                e.Category == UserPreferenceCategory.VisualStyle ||
                e.Category == UserPreferenceCategory.Color)
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(Apply));
            }
        };
        AppConfig.ConfigLoaded += (_, _) => Apply();
        Apply();
    }

    public static bool ResolveIsLight()
    {
        var mode = AppConfig.Current?.ThemeMode ?? AppThemeMode.System;
        return mode switch
        {
            AppThemeMode.Light => true,
            AppThemeMode.Dark => false,
            _ => IsSystemLight()
        };
    }

    public static bool IsSystemLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i) return i == 1;
        }
        catch
        {
        }
        return false;
    }

    public static void Apply()
    {
        if (AppConfig.Current == null) return;
        var globalActive = AppConfig.Current.ToggleState?.GlobalActive ?? false;
        var accent = globalActive ? AppConfig.Current.ActiveAccentColorValue : AppConfig.Current.AccentColorValue;
        ApplicationConstants.Theme = ThemePalette.FromAccent(accent, ResolveIsLight());
        Applied?.Invoke(null, EventArgs.Empty);
    }
}
