using System.ComponentModel;

namespace PowerAim.Config;

public enum DetectionAreaType
{
    [Description("Closest to Center")]
    ClosestToCenter,

    [Description("Closest to Mouse")]
    ClosestToMouse,
}