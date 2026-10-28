using System.Drawing;
using PowerAim.Types;

namespace PowerAim.AI.Rules;

/// <summary>
///     Chance that a shot fired now lands inside a region, given the target's motion. The region is moved by
///     the target's velocity over <c>latency</c> (input lag + game tick), and the crosshair's error is a
///     normal distribution: <c>spread</c> px (weapon / jitter) plus uncertainty that grows with target speed.
/// </summary>
public static class HitProbability
{
    /// <param name="region">Where the shot must land, in model pixels (e.g. the head area of the box).</param>
    /// <param name="velocity">Target velocity, model pixels per second.</param>
    public static double Estimate(RectangleF region, PointF velocity, PointF crosshair, double latencySec, double spreadPx)
    {
        float dx = (float)(velocity.X * latencySec), dy = (float)(velocity.Y * latencySec);
        double travel = Math.Sqrt(dx * (double)dx + dy * (double)dy);
        double sigma = Math.Max(0.5, Math.Sqrt(spreadPx * spreadPx + Math.Pow(travel * 0.25, 2)));
        return Interval(region.Left + dx, region.Right + dx, crosshair.X, sigma)
             * Interval(region.Top + dy, region.Bottom + dy, crosshair.Y, sigma);
    }

    private static double Interval(double from, double to, double at, double sigma)
        => Math.Max(0, Phi((to - at) / sigma) - Phi((from - at) / sigma));

    /// <summary>Standard normal CDF (Abramowitz–Stegun 7.1.26 erf, error &lt; 1.5e-7).</summary>
    public static double Phi(double z)
    {
        double x = Math.Abs(z) / Math.Sqrt(2);
        double t = 1 / (1 + 0.3275911 * x);
        double erf = 1 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return z >= 0 ? 0.5 * (1 + erf) : 0.5 * (1 - erf);
    }

    /// <summary>The part of <paramref name="box"/> a <see cref="RelativeRect"/> (e.g. the head area) selects.</summary>
    public static RectangleF Area(RectangleF box, RelativeRect area) => new(
        box.X + box.Width * area.LeftMarginPercentage,
        box.Y + box.Height * area.TopMarginPercentage,
        box.Width * area.WidthPercentage,
        box.Height * area.HeightPercentage);
}

/// <summary>One detected (and, when tracked, moving) target as rules see it. All in model pixels.</summary>
public sealed record RuleTarget(RectangleF Box, PointF Velocity, float Confidence, string ClassName, int ClassId)
{
    public static RuleTarget From(Prediction p) => new(p.Rectangle, PointF.Empty, p.Confidence, p.ClassName, p.ClassId);

    public static RuleTarget From(AI.Aiming.TargetTrack t) =>
        new(t.Box, new PointF((float)t.Vx, (float)t.Vy), t.Confidence, t.ClassName, t.ClassId);
}

/// <summary>The shot being considered and the rest of the frame, shared by every target's evaluation.</summary>
public sealed record RuleContext(
    int ModelSize,
    PointF Crosshair,
    RelativeRect HeadArea,
    double LatencySec,
    double SpreadPx,
    int EnemyCount,
    Func<string, double?>? Ocr = null);

/// <summary>
///     The signals triggers and aim rules can use, computed lazily for one target. Unknown or unreadable
///     signals are NaN, which makes any comparison on them false.
/// </summary>
public sealed class TargetScope : IRuleScope
{
    /// <summary>Signal name → what it means; shown in the editors and handed to the rule-writing model.</summary>
    public static readonly IReadOnlyDictionary<string, string> Signals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["head_hit"] = "probability 0..1 that a shot fired now hits the target's head area (accounts for its motion)",
        ["body_hit"] = "probability 0..1 that a shot fired now hits anywhere on the target's box",
        ["head_distance"] = "pixels from the crosshair to the centre of the head area",
        ["distance"] = "pixels from the crosshair to the centre of the target",
        ["speed"] = "target speed in pixels per second",
        ["confidence"] = "detector confidence 0..1",
        ["size"] = "target height as a share of the view 0..1 (bigger = closer)",
        ["class"] = "class name text, compare like class == \"head\"",
        ["class_id"] = "class number",
        ["enemies"] = "number of detected targets",
        ["ocr.<region>"] = "number read by the OCR region with that name, e.g. ocr.health",
    };

    public static bool IsKnown(string name) =>
        Signals.ContainsKey(name) || (name.StartsWith("ocr.", StringComparison.OrdinalIgnoreCase) && name.Length > 4);

    private readonly RuleTarget _t;
    private readonly RuleContext _c;

    public TargetScope(RuleTarget target, RuleContext context)
    {
        _t = target;
        _c = context;
    }

    public RuleValue Get(string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "head_hit": return new(HitProbability.Estimate(HitProbability.Area(_t.Box, _c.HeadArea), _t.Velocity, _c.Crosshair, _c.LatencySec, _c.SpreadPx));
            case "body_hit": return new(HitProbability.Estimate(_t.Box, _t.Velocity, _c.Crosshair, _c.LatencySec, _c.SpreadPx));
            case "head_distance": return new(DistanceTo(HitProbability.Area(_t.Box, _c.HeadArea)));
            case "distance": return new(DistanceTo(_t.Box));
            case "speed": return new(Math.Sqrt(_t.Velocity.X * (double)_t.Velocity.X + _t.Velocity.Y * (double)_t.Velocity.Y));
            case "confidence": return new(_t.Confidence);
            case "size": return new(_c.ModelSize > 0 ? _t.Box.Height / _c.ModelSize : double.NaN);
            case "class": return new(double.NaN, _t.ClassName);
            case "class_id": return new(_t.ClassId);
            case "enemies": return new(_c.EnemyCount);
        }
        if (name.StartsWith("ocr.", StringComparison.OrdinalIgnoreCase) && _c.Ocr?.Invoke(name[4..]) is { } v)
            return new(v);
        return RuleValue.Missing;
    }

    private double DistanceTo(RectangleF r)
    {
        double dx = r.X + r.Width / 2 - _c.Crosshair.X, dy = r.Y + r.Height / 2 - _c.Crosshair.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
