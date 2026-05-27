using System.Windows.Media;

namespace PowerAim.Config;

/// <summary>Shape options for the custom crosshair overlay.</summary>
public enum CrosshairShape
{
    Dot,
    Cross,
    Plus,       // cross with center gap
    Circle,
    CircleDot,
    T
}

/// <summary>
///     User-tunable parameters for the topmost custom crosshair overlay
///     (<see cref="Visuality.CrosshairOverlay"/>). Default is a small accent-coloured plus.
/// </summary>
public class CrosshairSettings : BaseSettings
{
    public CrosshairShape Shape
    {
        get;
        set => SetField(ref field, value);
    } = CrosshairShape.Plus;

    /// <summary>Total size of the crosshair in pixels.</summary>
    public int Size
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 4, 200));
    } = 16;

    public int Thickness
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 1, 12));
    } = 2;

    /// <summary>Center gap (Plus + Cross shapes).</summary>
    public int Gap
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0, 50));
    } = 4;

    /// <summary>ARGB hex string e.g. <c>#FF8B5CF6</c>.</summary>
    public string Color
    {
        get;
        set => SetField(ref field, value ?? "#FFFFFFFF");
    } = "#FF8B5CF6";       // PowerAim accent purple

    public string OutlineColor
    {
        get;
        set => SetField(ref field, value ?? "#FF000000");
    } = "#FF000000";

    public int OutlineThickness
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0, 6));
    } = 1;

    // Computed WPF Brush — DO NOT serialize. WPF's Brush.Transform.Inverse chains back on itself
    // and System.Text.Json walks straight into an "object cycle detected" at depth 64. The
    // persisted state is the Color / OutlineColor hex strings above; the brushes are reconstituted
    // from them on every access. Both ignore attributes are present because BaseSettings.Save uses
    // System.Text.Json while other call sites in the codebase use Newtonsoft.
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public Brush ColorBrush => TryParseBrush(Color, Brushes.White);

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public Brush OutlineBrush => TryParseBrush(OutlineColor, Brushes.Black);

    /// <summary>
    ///     <see cref="Color"/>-typed view over the persisted <see cref="Color"/> hex string, so the
    ///     in-app <c>AColorChanger</c> picker can bind to a Color directly. Not serialized — the hex
    ///     string remains the source of truth.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public Color ColorValue
    {
        get => TryParseColor(Color, Colors.White);
        set => Color = $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public Color OutlineColorValue
    {
        get => TryParseColor(OutlineColor, Colors.Black);
        set => OutlineColor = $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    private static Color TryParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static Brush TryParseBrush(string hex, SolidColorBrush fallback)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
        catch { return fallback; }
    }
}
