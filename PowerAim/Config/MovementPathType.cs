namespace PowerAim.Config;

/// <summary>
///     Selects which interpolation curve <see cref="PowerAim.InputLogic.MovementPaths"/> uses
///     when moving the crosshair from current to target position.
/// </summary>
public enum MovementPathType
{
    /// <summary>Cubic Bezier with 1/3 and 2/3 control points — original behaviour.</summary>
    Bezier,

    /// <summary>Straight linear interpolation. Fastest, no easing.</summary>
    Lerp,

    /// <summary>Slow start, fast finish (exponent 2 by default).</summary>
    Exponential,

    /// <summary>Lerp for short distances, Bezier for longer ones (threshold 100 px).</summary>
    Adaptive,

    /// <summary>Lerp with Perlin-noise-perturbed jitter perpendicular to the direction of travel.</summary>
    PerlinNoise
}
