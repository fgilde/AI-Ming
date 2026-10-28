using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PowerAim.AI.AutoPlay;

/// <summary>
///     Notices the bot walking into a wall: it holds a movement key, yet the centre of the view barely
///     changes for <see cref="StuckAfter"/>. Fed a tiny grey thumbnail a few times per second.
/// </summary>
public sealed class StuckDetector
{
    public const int Side = 16;

    /// <summary>Mean absolute grey difference (0..255) below which two thumbnails count as "the same view".</summary>
    public const double StillThreshold = 4.0;

    public static readonly TimeSpan StuckAfter = TimeSpan.FromSeconds(1.2);

    private byte[]? _last;
    private DateTime? _stillSince;

    public bool IsStuck { get; private set; }

    /// <param name="thumbnail"><see cref="Side"/>² grey values, see <see cref="Thumbnail"/>.</param>
    /// <param name="moving">The bot is holding a movement key right now.</param>
    public bool Update(byte[] thumbnail, bool moving, DateTime now)
    {
        bool still = _last != null && MeanDifference(_last, thumbnail) < StillThreshold;
        _last = thumbnail;
        if (!moving || !still) _stillSince = null;
        else _stillSince ??= now;
        IsStuck = _stillSince is { } since && now - since >= StuckAfter;
        return IsStuck;
    }

    public void Reset()
    {
        _last = null;
        _stillSince = null;
        IsStuck = false;
    }

    public static double MeanDifference(byte[] a, byte[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return double.MaxValue;
        long sum = 0;
        for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]);
        return sum / (double)a.Length;
    }

    /// <summary>Scales <paramref name="frame"/> to <see cref="Side"/>×<see cref="Side"/> grey values.</summary>
    public static byte[] Thumbnail(Bitmap frame)
    {
        using var small = new Bitmap(Side, Side, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.DrawImage(frame, new Rectangle(0, 0, Side, Side));
        }
        var data = small.LockBits(new Rectangle(0, 0, Side, Side), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bgra = new byte[data.Stride * Side];
            Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);
            var grey = new byte[Side * Side];
            for (int y = 0; y < Side; y++)
                for (int x = 0; x < Side; x++)
                {
                    int i = y * data.Stride + x * 4;
                    grey[y * Side + x] = (byte)((bgra[i] * 29 + bgra[i + 1] * 150 + bgra[i + 2] * 77) >> 8);
                }
            return grey;
        }
        finally { small.UnlockBits(data); }
    }
}
