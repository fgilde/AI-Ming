using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using PowerAim.AILogic;

namespace PowerAim.Config;

/// <summary>Boolean combinator for an <see cref="OcrConditionGroup"/>.</summary>
public enum OcrLogicOp
{
    /// <summary>All children must evaluate <c>true</c>. Empty group → <c>true</c> (neutral element).</summary>
    And = 0,
    /// <summary>At least one child must evaluate <c>true</c>. Empty group → <c>false</c>.</summary>
    Or = 1,
}

/// <summary>
///     Base node of an OCR condition tree. The tree replaces the old flat
///     <see cref="OcrTriggerCondition"/> list with grouped AND/OR semantics so users can express
///     things like <c>(ammo &gt; 5 OR mag = "full") AND weapon contains "AK"</c>.
///     <para>
///     <see cref="Evaluate"/> is the single entry point any consumer should use; the abstract
///     <see cref="EvaluateInternal"/> below is the per-subclass implementation. Both the trigger
///     pipeline and the aim-disengage path share the same evaluator — no duplicated logic.
///     </para>
/// </summary>
// System.Text.Json polymorphism: discriminator "$type" tells the deserializer which concrete
// subclass to instantiate. Without these attributes STJ throws "Deserialization of interface or
// abstract types is not supported" for the abstract base — which also means previous saves
// (made before these attributes existed) wrote children as empty {} placeholders without any
// type info. AppConfig.Load has a defensive fallback that strips the broken trees and re-seeds
// from the legacy flat OcrConditions list on first load.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(OcrConditionLeaf),  nameof(OcrConditionLeaf))]
[JsonDerivedType(typeof(OcrConditionGroup), nameof(OcrConditionGroup))]
public abstract class OcrConditionNode
{
    /// <summary>
    ///     Evaluate against the latest OCR readings. Returns <c>true</c> when the condition is
    ///     satisfied. Implementations must be allocation-free on the hot path — this runs every
    ///     AI tick.
    /// </summary>
    public bool Evaluate(IReadOnlyDictionary<string, OcrResult>? latest)
    {
        if (latest == null) return false;
        return EvaluateInternal(latest);
    }

    protected abstract bool EvaluateInternal(IReadOnlyDictionary<string, OcrResult> latest);

    /// <summary>Deep copy — used by <c>BeginEdit</c> snapshots so Cancel rolls back nested edits.</summary>
    public abstract OcrConditionNode Clone();

    /// <summary>True when the node holds no meaningful configuration (used by the UI to hide empty roots).</summary>
    public abstract bool IsEmpty { get; }
}

/// <summary>
///     A single OCR comparison: pick a region, an operator, and a target value. Same shape as the
///     legacy <see cref="OcrTriggerCondition"/> — the new tree just wraps these.
/// </summary>
public sealed class OcrConditionLeaf : OcrConditionNode
{
    public string RegionName { get; set; } = "";
    public OcrComparison Comparison { get; set; } = OcrComparison.GreaterThan;
    public string Value { get; set; } = "";

    protected override bool EvaluateInternal(IReadOnlyDictionary<string, OcrResult> latest)
    {
        // A leaf with no region is treated as "no condition" — skipped (neutral), not failing.
        // This keeps half-edited rows from blocking everything while the user is mid-config.
        if (string.IsNullOrWhiteSpace(RegionName)) return true;
        if (!latest.TryGetValue(RegionName, out var reading)) return false;
        return OcrConditionEvaluator.Evaluate(Comparison, Value, reading);
    }

    public override OcrConditionNode Clone() => new OcrConditionLeaf
    {
        RegionName = RegionName,
        Comparison = Comparison,
        Value = Value,
    };

    public override bool IsEmpty => string.IsNullOrWhiteSpace(RegionName);
}

/// <summary>
///     A boolean grouping of child nodes — itself a node, so groups can nest. Default operator is
///     <see cref="OcrLogicOp.And"/> which matches the legacy flat-list semantics; switch to
///     <see cref="OcrLogicOp.Or"/> for at-least-one-matches.
/// </summary>
public sealed class OcrConditionGroup : OcrConditionNode
{
    public OcrLogicOp Op { get; set; } = OcrLogicOp.And;

    /// <summary>Children may be leaves or other groups (arbitrary nesting).</summary>
    public ObservableCollection<OcrConditionNode> Children { get; set; } = new();

    protected override bool EvaluateInternal(IReadOnlyDictionary<string, OcrResult> latest)
    {
        // Empty group = neutral element: AND → true (no constraint), OR → false (nothing matched).
        if (Children.Count == 0) return Op == OcrLogicOp.And;
        if (Op == OcrLogicOp.And)
        {
            foreach (var child in Children) if (!child.Evaluate(latest)) return false;
            return true;
        }
        else // OR
        {
            foreach (var child in Children) if (child.Evaluate(latest)) return true;
            return false;
        }
    }

    public override OcrConditionNode Clone()
    {
        var clone = new OcrConditionGroup { Op = Op };
        foreach (var c in Children) clone.Children.Add(c.Clone());
        return clone;
    }

    /// <summary>A group is empty when it has no children with meaningful configuration.</summary>
    public override bool IsEmpty
    {
        get
        {
            foreach (var c in Children) if (!c.IsEmpty) return false;
            return true;
        }
    }
}
