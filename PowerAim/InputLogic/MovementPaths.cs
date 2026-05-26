using System.Drawing;

namespace PowerAim.InputLogic;

/// <summary>
///     Mouse-movement path helpers. Adapted from upstream Babyhamsta/Aimmy (commits 8b030b7, 6c9128a)
///     into the fork's namespace. Used by <see cref="MouseManager"/> alongside the existing
///     Cubic-Bezier path as humanizing alternatives. <see cref="PerlinNoise"/> requires the
///     permutation table to be seeded once via <see cref="SeedPermutation"/>.
/// </summary>
public static class MovementPaths
{
    private static readonly int[] _permutation = new int[512];
    private static bool _seeded;

    /// <summary>Seed the Perlin permutation table. Deterministic if <paramref name="seed"/> is given.</summary>
    public static void SeedPermutation(int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;
        // Fisher-Yates
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++) _permutation[i] = p[i & 255];
        _seeded = true;
    }

    /// <summary>Standard cubic Bezier between two points with two control points.</summary>
    public static Point CubicBezier(Point start, Point end, Point control1, Point control2, double t)
    {
        double u = 1 - t;
        double tt = t * t;
        double uu = u * u;
        double x = uu * u * start.X + 3 * uu * t * control1.X + 3 * u * tt * control2.X + tt * t * end.X;
        double y = uu * u * start.Y + 3 * uu * t * control1.Y + 3 * u * tt * control2.Y + tt * t * end.Y;
        return new Point((int)x, (int)y);
    }

    /// <summary>Linear interpolation.</summary>
    public static Point Lerp(Point start, Point end, double t)
    {
        int x = (int)(start.X + (end.X - start.X) * t);
        int y = (int)(start.Y + (end.Y - start.Y) * t);
        return new Point(x, y);
    }

    /// <summary>Exponential easing — slow start, fast finish (default exponent 2).</summary>
    public static Point Exponential(Point start, Point end, double t, double exponent = 2.0)
    {
        double x = start.X + (end.X - start.X) * Math.Pow(t, exponent);
        double y = start.Y + (end.Y - start.Y) * Math.Pow(t, exponent);
        return new Point((int)x, (int)y);
    }

    /// <summary>
    ///     Adaptive: Lerp for short distances (below <paramref name="threshold"/>) and a cubic Bezier
    ///     with 1/3 + 2/3 control points for longer movements.
    /// </summary>
    public static Point Adaptive(Point start, Point end, double t, double threshold = 100.0)
    {
        double distance = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
        if (distance < threshold) return Lerp(start, end, t);

        var control1 = new Point(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3);
        var control2 = new Point(start.X + 2 * (end.X - start.X) / 3, start.Y + 2 * (end.Y - start.Y) / 3);
        return CubicBezier(start, end, control1, control2, t);
    }

    /// <summary>
    ///     Perlin-noise-perturbed linear interpolation — adds organic jitter perpendicular to the
    ///     direction of travel. Requires <see cref="SeedPermutation"/> to have been called.
    /// </summary>
    public static Point PerlinNoise(Point start, Point end, double t, double amplitude = 10.0, double frequency = 0.1)
    {
        if (!_seeded) SeedPermutation();

        double baseX = start.X + (end.X - start.X) * t;
        double baseY = start.Y + (end.Y - start.Y) * t;

        double noiseX = Noise(t * frequency, 0) * amplitude;
        double noiseY = Noise(t * frequency, 100) * amplitude;

        double perpX = -(end.Y - start.Y);
        double perpY = end.X - start.X;
        double perpLength = Math.Sqrt(perpX * perpX + perpY * perpY);
        if (perpLength > 0)
        {
            perpX /= perpLength;
            perpY /= perpLength;
        }

        double finalX = baseX + perpX * noiseX + noiseY * 0.3;
        double finalY = baseY + perpY * noiseX + noiseY * 0.3;
        return new Point((int)finalX, (int)finalY);
    }

    // -------- Perlin internals --------

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static double LerpD(double a, double b, double t) => a + t * (b - a);

    private static double Grad(int hash, double x, double y)
    {
        int h = hash & 15;
        double u = h < 8 ? x : y;
        double v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    private static double Noise(double x, double y)
    {
        int X = (int)Math.Floor(x) & 255;
        int Y = (int)Math.Floor(y) & 255;
        x -= Math.Floor(x);
        y -= Math.Floor(y);

        double u = Fade(x);
        double v = Fade(y);

        int A  = _permutation[X] + Y;
        int AA = _permutation[A];
        int AB = _permutation[A + 1];
        int B  = _permutation[X + 1] + Y;
        int BA = _permutation[B];
        int BB = _permutation[B + 1];

        return LerpD(
            LerpD(Grad(_permutation[AA], x, y),
                  Grad(_permutation[BA], x - 1, y), u),
            LerpD(Grad(_permutation[AB], x, y - 1),
                  Grad(_permutation[BB], x - 1, y - 1), u),
            v);
    }
}
