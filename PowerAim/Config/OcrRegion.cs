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
    public string Name
    {
        get;
        set => SetField(ref field, value);
    } = "Region";

    public int X
    {
        get;
        set => SetField(ref field, value);
    }

    public int Y
    {
        get;
        set => SetField(ref field, value);
    }

    public int Width
    {
        get;
        set => SetField(ref field, Math.Max(8, value));
    } = 100;

    public int Height
    {
        get;
        set => SetField(ref field, Math.Max(8, value));
    } = 40;

    public bool Enabled
    {
        get;
        set => SetField(ref field, value);
    } = true;

    public OcrRegionKind Kind
    {
        get;
        set => SetField(ref field, value);
    } = OcrRegionKind.Number;

    /// <summary>
    ///     Invert the binarized image before passing to OCR. Useful for white-on-dark HUDs where
    ///     Tesseract trained on black-on-white expects the opposite polarity.
    /// </summary>
    public bool Invert
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>Threshold (0..255) used when pre-binarizing the capture.</summary>
    public int Threshold
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0, 255));
    } = 140;
}
