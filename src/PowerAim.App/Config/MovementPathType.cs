using System.ComponentModel;

namespace PowerAim.Config;

/// <summary>
///     Selects which interpolation curve <see cref="PowerAim.Input.MovementPaths"/> uses
///     when moving the crosshair from current to target position.
/// </summary>
public enum MovementPathType
{
    /// <summary>Cubic Bezier with 1/3 and 2/3 control points — original behaviour.</summary>
    [Description("Bezier (curved)")]
    Bezier,

    /// <summary>Straight linear interpolation. Fastest, no easing — robotic / snappy.</summary>
    [Description("Lerp (direct / snappy)")]
    Lerp,

    /// <summary>Slow start, fast finish (exponent 2 by default).</summary>
    [Description("Exponential")]
    Exponential,

    /// <summary>Lerp for short distances, Bezier for longer ones (threshold 100 px).</summary>
    [Description("Adaptive")]
    Adaptive,

    /// <summary>Lerp with Perlin-noise-perturbed jitter perpendicular to the direction of travel.</summary>
    [Description("Perlin noise")]
    PerlinNoise
}