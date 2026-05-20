using System.Windows;
using Aimmy2.Config;
using Aimmy2.Types;
using Microsoft.Win32;

namespace Aimmy2.Theme;

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
        var paletteName = globalActive ? AppConfig.Current.ActiveThemeName : AppConfig.Current.ThemeName;
        ApplicationConstants.Theme = Resolve(paletteName);
        Applied?.Invoke(null, EventArgs.Empty);
    }

    public static ThemePalette Resolve(string paletteName)
    {
        var wantLight = ResolveIsLight();
        var pool = ThemePalette.ByMode(wantLight);
        var match = pool.FirstOrDefault(p => string.Equals(p.Name, paletteName, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        var fallback = ThemePalette.All.FirstOrDefault(p =>
            string.Equals(p.Name, paletteName, StringComparison.OrdinalIgnoreCase) && p.IsLight == wantLight);
        return fallback ?? (wantLight ? ThemePalette.DarkPaletteLight : ThemePalette.DarkPalette);
    }
}
