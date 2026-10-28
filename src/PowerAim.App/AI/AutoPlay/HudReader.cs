using PowerAim.Config;

namespace PowerAim.AI.AutoPlay;

/// <summary>
///     Ammo and health from the OCR HUD reader: the first region whose name contains one of the aliases
///     ("ammo", "hp", …) with a confident numeric reading. Nothing when OCR is off or no such region exists.
/// </summary>
public static class HudReader
{
    public static readonly string[] AmmoAliases = ["ammo", "mag", "bullets", "magazine", "rounds"];
    public static readonly string[] HealthAliases = ["health", "hp", "armor", "armour", "shield"];

    public static (double? Ammo, double? Health) Read()
    {
        if (AppConfig.Current?.OcrSettings?.Enabled != true) return (null, null);
        var ocr = OcrService.Instance.Latest;
        return ocr.Count == 0 ? (null, null) : (Number(ocr, AmmoAliases), Number(ocr, HealthAliases));
    }

    public static double? Number(IReadOnlyDictionary<string, OcrResult> latest, string[] aliases)
    {
        foreach (var kv in latest)
            if (aliases.Any(a => kv.Key.Contains(a, StringComparison.OrdinalIgnoreCase)) &&
                kv.Value.Confidence >= 0.3f && kv.Value.Number.HasValue)
                return kv.Value.Number;
        return null;
    }
}
