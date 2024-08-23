using System.ComponentModel;

namespace Aimmy2.Config;

public enum DetectionAreaType
{
    [Description("Closest to Center")]
    ClosestToCenter,

    [Description("Closest to Mouse")]
    ClosestToMouse,
}