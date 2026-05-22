using System.Globalization;

namespace PowerAim.Localizations;

public static class Cultures
{
    public static IEnumerable<CultureInfo> All => new[]
    {
        new CultureInfo("en-US"),
        new CultureInfo("de-DE"),
        new CultureInfo("it-IT"),
        new CultureInfo("es-ES"),
        new CultureInfo("fr-FR"),
        new CultureInfo("tr-TR"),
        new CultureInfo("uk-UA"),
        new CultureInfo("ru-RU"),
        new CultureInfo("zh-CN"),
    };
}