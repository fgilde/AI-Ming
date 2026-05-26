using System.ComponentModel;

namespace PowerAim.Config;

public enum TriggerCheck
{
    [Description("None")]
    None,
    [Description("Intersecting Center")]
    IntersectingCenter,
    [Description("Head Intersecting Center")]
    HeadIntersectingCenter
}