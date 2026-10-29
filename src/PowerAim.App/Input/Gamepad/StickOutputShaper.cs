namespace PowerAim.Input.Gamepad;

/// <summary>How the game turns stick deflection (beyond its deadzone) into view speed.</summary>
public enum StickResponseCurve
{
    Linear,
    /// <summary>x² — soft near the centre, snappy at full deflection.</summary>
    Exponential,
    /// <summary>Smoothstep x²(3−2x).</summary>
    SCurve,
}

/// <param name="GameDeadzonePct">The game's stick deadzone (%). Corrections are remapped above it (+2 % margin) so small ones still turn the view.</param>
/// <param name="MaxOutputPct">Ceiling of the aim's stick deflection (%).</param>
/// <param name="Curve">The game's response curve; the aim deflects by its inverse so the view speed stays proportional.</param>
/// <param name="ManualOverridePct">How much the physical right stick takes over: blend = this % · its deflection.</param>
/// <param name="AntiStrafePct">0–50: counter the crosshair drift from strafing with the left stick, only toward the target.</param>
/// <param name="InvertAiY">Invert only the aim's vertical output (games with inverted look).</param>
public readonly record struct StickShapeOptions(
    double GameDeadzonePct,
    double MaxOutputPct,
    StickResponseCurve Curve,
    double ManualOverridePct,
    double AntiStrafePct,
    bool InvertAiY)
{
    public static StickShapeOptions Default => new(0, 100, StickResponseCurve.Linear, 0, 0, false);
}

/// <summary>Turns the aim's right-stick vector into the deflection sent to the virtual pad (unit-circle coordinates).</summary>
public static class StickOutputShaper
{
    public static double Curve(double x, StickResponseCurve curve) => curve switch
    {
        StickResponseCurve.Exponential => x * x,
        StickResponseCurve.SCurve => x * x * (3 - 2 * x),
        _ => x,
    };

    public static double InverseCurve(double y, StickResponseCurve curve)
    {
        y = Math.Clamp(y, 0, 1);
        switch (curve)
        {
            case StickResponseCurve.Exponential:
                return Math.Sqrt(y);
            case StickResponseCurve.SCurve:
                // Monotonic on [0,1] — bisect.
                double lo = 0, hi = 1;
                for (int i = 0; i < 30; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (Curve(mid, curve) < y) lo = mid; else hi = mid;
                }
                return (lo + hi) / 2;
            default:
                return y;
        }
    }

    /// <summary>
    ///     Deadzone remap, inverse response curve and output ceiling for the aim's vector. With the default
    ///     options the vector is returned unchanged (apart from the optional Y inversion).
    /// </summary>
    public static (double X, double Y) ShapeAi(double x, double y, StickShapeOptions o)
    {
        if (o.InvertAiY) y = -y;
        double dz = o.GameDeadzonePct > 0 ? Math.Min(o.GameDeadzonePct + 2, 99) / 100.0 : 0;
        double max = Math.Clamp(o.MaxOutputPct, 0, 100) / 100.0;
        if (dz == 0 && o.Curve == StickResponseCurve.Linear && max >= 1) return (x, y);

        double r = Math.Sqrt(x * x + y * y);
        if (r <= 0) return (0, 0);
        double m = dz + (1 - dz) * InverseCurve(Math.Min(r, 1), o.Curve);
        m = Math.Min(m, Math.Max(max, dz));
        return (x / r * m, y / r * m);
    }

    /// <summary>
    ///     Share of the game's full turn speed the shaped stick really produces for a wanted share <paramref name="f"/>,
    ///     taking the game's deadzone as configured: the +2 % safety margin turns it a bit more than wanted.
    /// </summary>
    public static double GameSpeedFraction(double f, StickShapeOptions o)
    {
        f = Math.Abs(f);
        if (f <= 0) return 0;
        var (m, _) = ShapeAi(f, 0, o with { InvertAiY = false });
        double g = Math.Clamp(o.GameDeadzonePct, 0, 99) / 100.0;
        return m <= g ? 0 : Curve(Math.Min(1, (m - g) / (1 - g)), o.Curve);
    }

    /// <summary>Highest share of the game's full turn speed the aim can reach under the output ceiling.</summary>
    public static double MaxSpeedFraction(StickShapeOptions o)
    {
        double dz = o.GameDeadzonePct > 0 ? Math.Min(o.GameDeadzonePct + 2, 99) / 100.0 : 0;
        double max = Math.Clamp(o.MaxOutputPct, 0, 100) / 100.0;
        if (max >= 1) return 1;
        return Curve(Math.Clamp((max - dz) / (1 - dz), 0, 1), o.Curve);
    }

    /// <summary>
    ///     Blend the shaped aim vector with the physical right stick (manual override), add the anti-strafe
    ///     push and keep the result inside the unit circle.
    /// </summary>
    public static (double X, double Y) Mix(double aiX, double aiY, double userX, double userY, double leftX, StickShapeOptions o)
    {
        double blend = Math.Clamp(Math.Sqrt(userX * userX + userY * userY), 0, 1) * Math.Clamp(o.ManualOverridePct, 0, 100) / 100.0;
        double x = aiX * (1 - blend) + userX * blend;
        double y = aiY * (1 - blend) + userY * blend;

        double s = Math.Clamp(o.AntiStrafePct, 0, 50);
        if (s > 0)
        {
            double push = -leftX * s * 0.03;
            if (push * aiX > 0) x += push;
        }

        double len = Math.Sqrt(x * x + y * y);
        if (len > 1) { x /= len; y /= len; }
        return (Math.Clamp(x, -1, 1), Math.Clamp(y, -1, 1));
    }
}

/// <summary>
///     Holds the latest aim vector and produces the right-stick value to send at any moment: the aim
///     (blended with the physical stick) while it is fresh, neutral once it is older than
///     <see cref="ReleaseAfterMs"/>. With manual override on, the physical stick is handed back after a
///     short neutral gap — if the "physical" reader is actually our own virtual pad it then echoes the
///     neutral value instead of locking onto the last aim output.
/// </summary>
public sealed class StickMixer
{
    public const double ReleaseAfterMs = 80;
    public const double PassThroughDelayMs = 20;

    private readonly object _gate = new();
    private double _aiX, _aiY;
    private double _aiAtMs = double.NegativeInfinity;
    private double _releasedAtMs = double.NegativeInfinity;

    public void SetAi(double x, double y, double nowMs)
    {
        lock (_gate)
        {
            _aiX = x;
            _aiY = y;
            _aiAtMs = nowMs;
            _releasedAtMs = double.NegativeInfinity;
        }
    }

    /// <summary>True while an aim vector younger than <see cref="ReleaseAfterMs"/> exists.</summary>
    public bool IsFresh(double nowMs)
    {
        lock (_gate) return nowMs - _aiAtMs < ReleaseAfterMs;
    }

    public (double X, double Y) Next(double nowMs, double userX, double userY, double leftX, StickShapeOptions o)
    {
        lock (_gate)
        {
            bool usesPhysical = o.ManualOverridePct > 0 || o.AntiStrafePct > 0;
            if (nowMs - _aiAtMs < ReleaseAfterMs)
                return usesPhysical ? StickOutputShaper.Mix(_aiX, _aiY, userX, userY, leftX, o) : (_aiX, _aiY);

            if (double.IsNegativeInfinity(_releasedAtMs)) _releasedAtMs = nowMs;
            if (o.ManualOverridePct > 0 && nowMs - _releasedAtMs >= PassThroughDelayMs)
                return (Math.Clamp(userX, -1, 1), Math.Clamp(userY, -1, 1));
            return (0, 0);
        }
    }
}

/// <summary>Stick patterns of the gamepad calibration tools, as functions of the elapsed seconds.</summary>
public static class StickTestPatterns
{
    /// <summary>
    ///     Holds the right stick exactly at the configured game deadzone. If the view still turns, the game's
    ///     deadzone is smaller than configured — lower the setting until it stays still.
    /// </summary>
    public static (double X, double Y, bool Bumper) DeadzoneCheck(double seconds, double gameDeadzonePct)
        => (Math.Clamp(gameDeadzonePct, 0, 100) / 100.0, 0, false);

    /// <summary>Half-deflection circle once per two seconds plus a short right-bumper press at the start of each turn.</summary>
    public static (double X, double Y, bool Bumper) OutputTest(double seconds)
    {
        double angle = seconds * Math.PI; // one turn per 2 s
        return (0.5 * Math.Cos(angle), 0.5 * Math.Sin(angle), seconds % 2 < 0.2);
    }
}
