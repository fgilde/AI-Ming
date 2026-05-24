namespace PowerAim.Config;

/// <summary>
///     Semantic of the value the OCR engine is expected to extract from a region. Drives the
///     post-processing (digit-only vs. alphanumeric) and lets consumers reason about the value.
/// </summary>
public enum OcrRegionKind
{
    /// <summary>Free-text — no constraints on the recognized output.</summary>
    Text = 0,

    /// <summary>Numeric counter (ammo, money, score). Post-processed to discard non-digits.</summary>
    Number = 1,

    /// <summary>Health-style value: number, but often with a slash (e.g. "75/100").</summary>
    Health = 2,
}

/// <summary>
///     A named rectangular region anchored in <i>screen-pixel coordinates</i> that the
///     <c>OcrService</c> samples periodically. Stored in absolute pixels (not normalized) because
///     HUD elements live in fixed screen positions, not relative to the FOV box.
/// </summary>
public class OcrRegion : BaseSettings
{
    private string _name = "Region";
    private int _x = 0;
    private int _y = 0;
    private int _width = 100;
    private int _height = 40;
    private bool _enabled = true;
    private OcrRegionKind _kind = OcrRegionKind.Number;
    private bool _invert = false;
    private int _threshold = 140;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public int X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    public int Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    public int Width
    {
        get => _width;
        set => SetField(ref _width, Math.Max(8, value));
    }

    public int Height
    {
        get => _height;
        set => SetField(ref _height, Math.Max(8, value));
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    public OcrRegionKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    /// <summary>
    ///     Invert the binarized image before passing to OCR. Useful for white-on-dark HUDs where
    ///     Tesseract trained on black-on-white expects the opposite polarity.
    /// </summary>
    public bool Invert
    {
        get => _invert;
        set => SetField(ref _invert, value);
    }

    /// <summary>Threshold (0..255) used when pre-binarizing the capture.</summary>
    public int Threshold
    {
        get => _threshold;
        set => SetField(ref _threshold, Math.Clamp(value, 0, 255));
    }
}
