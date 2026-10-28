using System.ComponentModel;

namespace PowerAim.Config;

public enum TriggerCheck
{
    [Description("None")]
    None,
    [Description("Intersecting Center")]
    IntersectingCenter,
    [Description("Head Intersecting Center")]
    HeadIntersectingCenter,

    /// <summary>Fires when <see cref="ActionTrigger.HitRule"/> holds for any target (e.g. "head_hit &gt;= 0.8").</summary>
    [Description("Rule / description")]
    Rule
}