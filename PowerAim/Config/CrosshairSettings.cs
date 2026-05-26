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
