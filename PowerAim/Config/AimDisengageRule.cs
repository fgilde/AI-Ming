namespace PowerAim.Config;

/// <summary>
///     A per-game rule that pauses aim assist while a HUD OCR reading matches — e.g. region "state"
///     <i>contains</i> "scoped", or "weapon" <i>contains</i> "knife". If any enabled rule whose
///     <see cref="MatchProcess"/> applies to the focused game is satisfied, aiming is suppressed.
///     Only consulted while the OCR engine is enabled.
/// </summary>
public class AimDisengageRule
{
    public bool Enabled { get; set; } = true;

    /// <summary>Name of the OCR region (see <see cref="OcrSettings.Regions"/>) to read.</summary>
    public string RegionName { get; set; } = "";

    public OcrComparison Comparison { get; set; } = OcrComparison.Contains;

    /// <summary>Target value: a number for numeric comparisons, free text for Contains/Equals.</summary>
    public string Value { get; set; } = "";

    /// <summary>Optional process-name pattern (wildcard / pipe). Empty = applies to every game.</summary>
    public string MatchProcess { get; set; } = "";

    public AimDisengageRule Clone() => new()
    {
        Enabled = Enabled,
        RegionName = RegionName,
        Comparison = Comparison,
        Value = Value,
        MatchProcess = MatchProcess
    };
}
