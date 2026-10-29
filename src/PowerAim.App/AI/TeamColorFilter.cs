using System.Drawing;

namespace PowerAim.AI;

/// <summary>What a colour match next to a box means.</summary>
public enum TeamColorMode
{
    Off,
    /// <summary>The colour marks teammates: drop boxes where it is found.</summary>
    SuppressMatching,
    /// <summary>The colour marks enemies: drop boxes where it is not found.</summary>
    KeepMatching,
}

/// <summary>HSV range: hue 0–360 (wraps when <see cref="HueMin"/> &gt; <see cref="HueMax"/>), saturation and value 0–100.</summary>
public readonly record struct HsvRange(double HueMin, double HueMax, double SatMin, double SatMax, double ValMin, double ValMax)
{
    public bool Contains(double h, double s, double v)
    {
        bool hue = HueMin <= HueMax ? h >= HueMin && h <= HueMax : h >= HueMin || h <= HueMax;
        return hue && s >= SatMin && s <= SatMax && v >= ValMin && v <= ValMax;
    }
}

/// <param name="ExtentPct">How far a sample strip reaches out from the box, in % of the box height (above/below) or width (left/right).</param>
/// <param name="MatchPct">Share (%) of sampled pixels that must be in range for the box to count as matching.</param>
public readonly record struct TeamColorOptions(
    TeamColorMode Mode, HsvRange Range, bool Above, bool Below, bool Left, bool Right, double ExtentPct, double MatchPct);

/// <summary>
///     Tells teammates from enemies by a colour next to their box (name tag, outline, marker): samples
///     strips around the box and checks which share of their pixels falls in the configured HSV range.
/// </summary>
public static class TeamColorFilter
{
    /// <param name="bgra">32-bit BGRA pixels (System.Drawing 32bpp ARGB memory layout).</param>
    public static bool ShouldDrop(ReadOnlySpan<byte> bgra, int stride, int width, int height, RectangleF box, TeamColorOptions o)
    {
        if (o.Mode == TeamColorMode.Off) return false;
        int match = 0, total = 0;
        foreach (var region in SampleRegions(box, o, width, height))
            Count(bgra, stride, region, o.Range, ref match, ref total);
        bool matching = total > 0 && match * 100.0 / total >= o.MatchPct;
        return o.Mode == TeamColorMode.SuppressMatching ? matching : !matching;
    }

    private static IEnumerable<Rectangle> SampleRegions(RectangleF box, TeamColorOptions o, int width, int height)
    {
        double ext = Math.Max(0, o.ExtentPct) / 100.0;
        var frame = new Rectangle(0, 0, width, height);
        RectangleF[] strips =
        [
            o.Above ? new RectangleF(box.X, box.Y - (float)(box.Height * ext), box.Width, (float)(box.Height * ext)) : RectangleF.Empty,
            o.Below ? new RectangleF(box.X, box.Bottom, box.Width, (float)(box.Height * ext)) : RectangleF.Empty,
            o.Left ? new RectangleF(box.X - (float)(box.Width * ext), box.Y, (float)(box.Width * ext), box.Height) : RectangleF.Empty,
            o.Right ? new RectangleF(box.Right, box.Y, (float)(box.Width * ext), box.Height) : RectangleF.Empty,
        ];
        foreach (var s in strips)
        {
            if (s.Width <= 0 || s.Height <= 0) continue;
            var r = Rectangle.Intersect(Rectangle.Round(s), frame);
            if (r.Width > 0 && r.Height > 0) yield return r;
        }
    }

    private static void Count(ReadOnlySpan<byte> bgra, int stride, Rectangle r, HsvRange range, ref int match, ref int total)
    {
        for (int y = r.Top; y < r.Bottom; y++)
        {
            int row = y * stride;
            for (int x = r.Left; x < r.Right; x++)
            {
                int o = row + x * 4;
                var (h, s, v) = ToHsv(bgra[o + 2], bgra[o + 1], bgra[o]);
                if (range.Contains(h, s, v)) match++;
                total++;
            }
        }
    }

    private static (double H, double S, double V) ToHsv(byte r8, byte g8, byte b8)
    {
        double r = r8 / 255.0, g = g8 / 255.0, b = b8 / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double h = d == 0 ? 0
            : max == r ? 60 * (((g - b) / d + 6) % 6)
            : max == g ? 60 * ((b - r) / d + 2)
            : 60 * ((r - g) / d + 4);
        return (h, max == 0 ? 0 : d / max * 100, max * 100);
    }
}
