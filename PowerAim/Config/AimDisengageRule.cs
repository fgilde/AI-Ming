namespace PowerAim.Config;

/// <summary>
///     A per-game rule that pauses aim assist while a HUD OCR reading matches — e.g. region "state"
///     <i>contains</i> "scoped", or "weapon" <i>contains</i> "knife". If any enabled rule whose
///     <see cref="MatchProcess"/> applies to the focused game evaluates to true, aiming is
///     suppressed. Only consulted while the OCR engine is enabled.
///     <para>
///     The condition lives in <see cref="ConditionTree"/> — a full
///     <see cref="OcrConditionGroup"/>, so users can express AND/OR combos like
///     <i>(weapon contains "AWP" OR weapon contains "Scout") AND state contains "scoped"</i>.
///     The legacy flat fields (<see cref="RegionName"/>, <see cref="Comparison"/>,
///     <see cref="Value"/>) are kept for JSON-config back-compat and migrated into the tree on
///     first load by <see cref="EnsureTreeMigrated"/>.
///     </para>
/// </summary>
public class AimDisengageRule
{
    public bool Enabled { get; set; } = true;

    /// <summary>Optional human label for the rule, shown in the editor list.</summary>
    public string Name { get; set; } = "";

    /// <summary>Optional process-name pattern (wildcard / pipe). Empty = applies to every game.</summary>
    public string MatchProcess { get; set; } = "";

    // -------------------------------------------------------------- LEGACY (pre-tree) fields --
    // Persisted for JSON back-compat: existing user configs deserialize cleanly. Once
    // EnsureTreeMigrated() has run they're not consulted any more — the tree drives evaluation.

    /// <summary>Legacy single-region field; superseded by <see cref="ConditionTree"/>.</summary>
    public string RegionName { get; set; } = "";

    /// <summary>Legacy single-comparison field; superseded by <see cref="ConditionTree"/>.</summary>
    public OcrComparison Comparison { get; set; } = OcrComparison.Contains;

    /// <summary>Legacy single-value field; superseded by <see cref="ConditionTree"/>.</summary>
    public string Value { get; set; } = "";

    // -------------------------------------------------------------------- NEW (tree) field --

    /// <summary>
    ///     Grouped condition for this rule. The rule fires (= aim pauses) when this tree evaluates
    ///     to true AND <see cref="Enabled"/> AND <see cref="MatchProcess"/> matches.
    /// </summary>
    public OcrConditionGroup ConditionTree { get; set; } = new();

    /// <summary>
    ///     One-shot migration: if the tree is empty but the legacy region field has content,
    ///     seed the tree with a single leaf carrying the legacy data. Idempotent — once the tree
    ///     has anything, this is a no-op.
    /// </summary>
    public void EnsureTreeMigrated()
    {
        if (!ConditionTree.IsEmpty) return;
        if (string.IsNullOrWhiteSpace(RegionName)) return;
        ConditionTree.Children.Add(new OcrConditionLeaf
        {
            RegionName = RegionName,
            Comparison = Comparison,
            Value = Value,
        });
    }

    public AimDisengageRule Clone() => new()
    {
        Enabled = Enabled,
        Name = Name,
        RegionName = RegionName,
        Comparison = Comparison,
        Value = Value,
        MatchProcess = MatchProcess,
        ConditionTree = (OcrConditionGroup)ConditionTree.Clone(),
    };
}
