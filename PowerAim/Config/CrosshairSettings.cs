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
    private CrosshairShape _shape = CrosshairShape.Plus;
    private int _size = 16;
    private int _thickness = 2;
    private int _gap = 4;
    private string _color = "#FF8B5CF6";       // PowerAim accent purple
    private string _outlineColor = "#FF000000";
    private int _outlineThickness = 1;

    public CrosshairShape Shape
    {
        get => _shape;
        set => SetField(ref _shape, value);
    }

    /// <summary>Total size of the crosshair in pixels.</summary>
    public int Size
    {
        get => _size;
        set => SetField(ref _size, Math.Clamp(value, 4, 200));
    }

    public int Thickness
    {
        get => _thickness;
        set => SetField(ref _thickness, Math.Clamp(value, 1, 12));
    }

    /// <summary>Center gap (Plus + Cross shapes).</summary>
    public int Gap
    {
        get => _gap;
        set => SetField(ref _gap, Math.Clamp(value, 0, 50));
    }

    /// <summary>ARGB hex string e.g. <c>#FF8B5CF6</c>.</summary>
    public string Color
    {
        get => _color;
        set => SetField(ref _color, value ?? "#FFFFFFFF");
    }

    public string OutlineColor
    {
        get => _outlineColor;
        set => SetField(ref _outlineColor, value ?? "#FF000000");
    }

    public int OutlineThickness
    {
        get => _outlineThickness;
        set => SetField(ref _outlineThickness, Math.Clamp(value, 0, 6));
    }

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
