namespace PowerAim.Config;

/// <summary>How an <see cref="OcrTriggerCondition"/> compares the recognized region value to its target.</summary>
public enum OcrComparison
{
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    Equals,
    NotEquals,
    Contains,
    NotContains
}

/// <summary>
///     An optional gate on an <see cref="ActionTrigger"/> (and on aim, via the disengage list) that
///     checks a live OCR region reading — e.g. "only fire when region 'ammo' &gt; 5" or
///     "pause aim when region 'state' contains 'scoped'". Pure data; the evaluation lives in the
///     consuming action so this model stays free of any AILogic dependency.
/// </summary>
public class OcrTriggerCondition
{
    /// <summary>Name of the OCR region (see <see cref="OcrSettings.Regions"/>) to read.</summary>
    public string RegionName { get; set; } = "";

    public OcrComparison Comparison { get; set; } = OcrComparison.GreaterThan;

    /// <summary>Target value: a number for the numeric comparisons, free text for Contains/Equals.</summary>
    public string Value { get; set; } = "";

    public OcrTriggerCondition Clone() => new()
    {
        RegionName = RegionName,
        Comparison = Comparison,
        Value = Value
    };
}
