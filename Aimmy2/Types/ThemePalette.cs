using Aimmy2.Config;
using System.Windows.Media;

namespace Aimmy2.Types;

public class ThemePalette
{
    public ThemePalette(string name)
    {
        Name = name;
    }

    public string Name;
    public bool IsLight;
    public Color MainColor;
    public Color AccentColor;
    public Color EffectColor;

    public Color Surface1Color;
    public Color Surface2Color;
    public Color Surface3Color;
    public Color StrokeColor;
    public Color StrokeStrongColor;
    public Color TextPrimaryColor;
    public Color TextSecondaryColor;
    public Color TextTertiaryColor;
    public Color AccentForegroundColor = Colors.White;
    public Color HoverOverlayColor;
    public Color PressedOverlayColor;
    public Color DangerColor = Color.FromArgb(255, 232, 17, 35);
    public Color SuccessColor = Color.FromArgb(255, 16, 137, 62);

    public override string ToString() => Name;

    public static ThemePalette ThemeForActive
    {
        get
        {
            var active = All.FirstOrDefault(x => x.Name == AppConfig.Current.ActiveThemeName && x.IsLight == IsCurrentLight)
                         ?? All.FirstOrDefault(x => x.Name == AppConfig.Current.ActiveThemeName)
                         ?? (IsCurrentLight ? GreenPaletteLight : GreenPalette);
            return active;
        }
    }

    private static bool IsCurrentLight => ApplicationConstants.Theme?.IsLight ?? false;

    // ---- Dark variants (default) ----------------------------------------------------------------
    public static ThemePalette PurplePalette = MakeDark("Purple",
        accent: Color.FromArgb(255, 142, 90, 224),
        effect: Color.FromArgb(255, 211, 173, 247),
        mainTint: Color.FromArgb(255, 18, 3, 56));

    public static ThemePalette DarkPalette = MakeDark("Dark",
        accent: Color.FromArgb(255, 96, 205, 255),
        effect: Color.FromArgb(255, 173, 173, 173),
        mainTint: Color.FromArgb(255, 26, 26, 26));

    public static ThemePalette AquaPalette = MakeDark("Aqua",
        accent: Color.FromArgb(255, 0, 178, 255),
        effect: Color.FromArgb(255, 173, 211, 247),
        mainTint: Color.FromArgb(255, 8, 36, 60));

    public static ThemePalette GreenPalette = MakeDark("Green",
        accent: Color.FromArgb(255, 73, 215, 132),
        effect: Color.FromArgb(255, 173, 247, 211),
        mainTint: Color.FromArgb(255, 6, 40, 22));

    public static ThemePalette RedPalette = MakeDark("Red",
        accent: Color.FromArgb(255, 232, 86, 116),
        effect: Color.FromArgb(255, 247, 173, 211),
        mainTint: Color.FromArgb(255, 56, 3, 18));

    public static ThemePalette BluePalette = MakeDark("Blue",
        accent: Color.FromArgb(255, 76, 145, 255),
        effect: Color.FromArgb(255, 173, 211, 247),
        mainTint: Color.FromArgb(255, 6, 18, 56));

    // ---- Light variants -------------------------------------------------------------------------
    public static ThemePalette PurplePaletteLight = MakeLight("Purple",
        accent: Color.FromArgb(255, 121, 70, 196),
        effect: Color.FromArgb(255, 187, 142, 232),
        mainTint: Color.FromArgb(255, 244, 240, 252));

    public static ThemePalette DarkPaletteLight = MakeLight("Dark",
        accent: Color.FromArgb(255, 0, 120, 212),
        effect: Color.FromArgb(255, 96, 96, 96),
        mainTint: Color.FromArgb(255, 243, 243, 243));

    public static ThemePalette AquaPaletteLight = MakeLight("Aqua",
        accent: Color.FromArgb(255, 0, 145, 215),
        effect: Color.FromArgb(255, 150, 200, 240),
        mainTint: Color.FromArgb(255, 240, 248, 252));

    public static ThemePalette GreenPaletteLight = MakeLight("Green",
        accent: Color.FromArgb(255, 16, 137, 62),
        effect: Color.FromArgb(255, 130, 210, 165),
        mainTint: Color.FromArgb(255, 240, 248, 243));

    public static ThemePalette RedPaletteLight = MakeLight("Red",
        accent: Color.FromArgb(255, 196, 43, 80),
        effect: Color.FromArgb(255, 240, 170, 195),
        mainTint: Color.FromArgb(255, 252, 240, 244));

    public static ThemePalette BluePaletteLight = MakeLight("Blue",
        accent: Color.FromArgb(255, 0, 90, 200),
        effect: Color.FromArgb(255, 130, 175, 230),
        mainTint: Color.FromArgb(255, 240, 245, 252));

    private static ThemePalette MakeDark(string name, Color accent, Color effect, Color mainTint)
    {
        return new ThemePalette(name)
        {
            IsLight = false,
            MainColor = mainTint,
            AccentColor = accent,
            EffectColor = effect,
            Surface1Color = Color.FromArgb(255, 23, 23, 23),
            Surface2Color = Color.FromArgb(255, 32, 32, 32),
            Surface3Color = Color.FromArgb(255, 44, 44, 44),
            StrokeColor = Color.FromArgb(36, 255, 255, 255),
            StrokeStrongColor = Color.FromArgb(70, 255, 255, 255),
            TextPrimaryColor = Color.FromArgb(255, 255, 255, 255),
            TextSecondaryColor = Color.FromArgb(220, 255, 255, 255),
            TextTertiaryColor = Color.FromArgb(140, 255, 255, 255),
            HoverOverlayColor = Color.FromArgb(18, 255, 255, 255),
            PressedOverlayColor = Color.FromArgb(28, 0, 0, 0),
            AccentForegroundColor = Colors.White
        };
    }

    private static ThemePalette MakeLight(string name, Color accent, Color effect, Color mainTint)
    {
        return new ThemePalette(name)
        {
            IsLight = true,
            MainColor = mainTint,
            AccentColor = accent,
            EffectColor = effect,
            Surface1Color = Color.FromArgb(255, 243, 243, 243),
            Surface2Color = Color.FromArgb(255, 255, 255, 255),
            Surface3Color = Color.FromArgb(255, 250, 250, 250),
            StrokeColor = Color.FromArgb(36, 0, 0, 0),
            StrokeStrongColor = Color.FromArgb(70, 0, 0, 0),
            TextPrimaryColor = Color.FromArgb(255, 26, 26, 26),
            TextSecondaryColor = Color.FromArgb(190, 0, 0, 0),
            TextTertiaryColor = Color.FromArgb(120, 0, 0, 0),
            HoverOverlayColor = Color.FromArgb(14, 0, 0, 0),
            PressedOverlayColor = Color.FromArgb(28, 0, 0, 0),
            AccentForegroundColor = Colors.White
        };
    }

    public static ThemePalette[] All
    {
        get
        {
            return typeof(ThemePalette).GetFields()
                .Where(f => f.FieldType == typeof(ThemePalette))
                .Select(f => (ThemePalette)f.GetValue(null)!)
                .ToArray();
        }
    }

    public static ThemePalette[] ByMode(bool isLight)
        => All.Where(p => p.IsLight == isLight).ToArray();
}
