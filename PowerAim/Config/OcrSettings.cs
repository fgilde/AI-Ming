using System.Collections.ObjectModel;

namespace PowerAim.Config;

/// <summary>
///     Configuration for the OCR HUD-reader. Hosts the user-defined regions plus global toggles
///     (engine enable, polling interval). The engine itself reads <see cref="Enabled"/> +
///     <see cref="Regions"/> directly from <see cref="AppConfig.Current"/>.
/// </summary>
public class OcrSettings : BaseSettings
{
    public bool Enabled
    {
        get;
        set => SetField(ref field, value);
    }

    public int IntervalMs
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 100, 5000));
    } = 500;

    /// <summary>
    ///     Optional override for the Tesseract data folder. Empty = use the default
    ///     (<c>%LocalAppData%/PowerAim/tessdata</c>).
    /// </summary>
    public string TessdataPath
    {
        get;
        set => SetField(ref field, value);
    } = "";

    public ObservableCollection<OcrRegion> Regions
    {
        get;
        set => SetField(ref field, value);
    } = new();

    // ---- Experimental recognition tweaks ----
    // All default to OFF so existing setups behave exactly as before. Each one targets a
    // specific failure mode (see XML comments) and can be flipped independently. Combine as
    // needed — they're orthogonal except where noted.

    /// <summary>
    ///     Use max-of-channels (HSV V) for grayscale instead of luminance-weighted Y. Y = 0.299R +
    ///     0.587G + 0.114B punishes saturated coloured HUD digits (a magenta "10" collapses to
    ///     Y ≈ 158 even though it's "bright" on screen). V keeps any saturated colour at 255 so a
    ///     single threshold tuned for white text also works for pink / red / green. For non-coloured
    ///     regions (R=G=B) V == Y bit-identically, so it's a near-strict superset. <b>Try this
    ///     first</b> if a coloured-text region misreads while a neighbouring white-text region works.
    /// </summary>
    public bool UseMaxChannelGrayscale
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>
    ///     Replace common letter↔digit confusions in raw OCR output before digit-only stripping:
    ///     <c>O o Q D → 0</c>, <c>l I i | ! ] → 1</c>, <c>Z z → 2</c>, <c>S s → 5</c>, <c>b → 6</c>,
    ///     <c>B → 8</c>, <c>g q → 9</c>. Default OFF because aggressive games-font substitution can
    ///     also "fix" actual letters into wrong digits. Turn on when an ammo / health counter
    ///     consistently reads as text-with-digits.
    /// </summary>
    public bool SubstituteLettersToDigits
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>
    ///     Stricter number extraction: reject the parse when the sanitised string has no actual
    ///     digit characters (e.g. "." or ".." from a stray speckle that survived thresholding —
    ///     currently parses cleanly as 0), and trim stray leading/trailing dots before parsing.
    ///     Default OFF for backwards compatibility; recommended ON.
    /// </summary>
    public bool StrictNumberParsing
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>
    ///     Sticky-last-valid timeout in milliseconds for <see cref="OcrRegionKind.Number"/> /
    ///     <see cref="OcrRegionKind.Health"/> regions. When > 0 and the current frame fails to
    ///     parse a number, the most recent valid reading is carried forward for up to this many
    ///     ms. Stops single bad frames (motion blur, muzzle flash, partial occlusion) from flickering
    ///     consumers between "value X" and "nothing". Cannot manufacture wrong values — only holds
    ///     real prior ones. 0 = off, recommended 1500–2000 ms.
    /// </summary>
    public int StickyLastValidMs
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0, 10000));
    }

    /// <summary>
    ///     Tell Tesseract the input is at 300 DPI via <c>user_defined_dpi=300</c>. Without this
    ///     the LSTM engine auto-rescales small inputs by heuristic, which usually hurts already-
    ///     upscaled HUD patches. Documented sweet spot is 300 — the actual pixel density doesn't
    ///     have to match. Default OFF for safety; flip ON if engine output looks blurry or wobbly.
    /// </summary>
    public bool UseUserDefinedDpi
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>
    ///     When a Number/Health region's primary OCR pass (user-configured fixed threshold) returns
    ///     no parseable number, retry once with Otsu's automatic threshold. <b>High risk of false
    ///     positives</b>: on noisy / saturated HUD regions Otsu picks a degenerate threshold and
    ///     Tesseract reads speckle blobs as 4+ digit numbers. Only enable for very stable HUDs
    ///     where the primary threshold is just slightly off and a re-binarisation fixes it.
    ///     Default OFF.
    /// </summary>
    public bool UseOtsuFallback
    {
        get;
        set => SetField(ref field, value);
    }
}
