using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

namespace Aimmy2.AILogic;

/// <summary>
///     Hot-path math helpers used by the inference pipeline.
///     Adapted from upstream Babyhamsta/Aimmy (commits 5ae44a4, 16d8173, f7bccf8) — namespace lifted to
///     <c>Aimmy2.AILogic</c> to match the fork's conventions and integrated with the fork's
///     <see cref="Prediction"/> class which already exposes screen-translated centers via the
///     <c>CenterXTranslated</c> / <c>CenterYTranslated</c> properties.
/// </summary>
public static class MathUtil
{
    public static readonly Func<double[], double[], double> L2Norm_Squared_Double = (x, y) =>
    {
        double dist = 0f;
        for (int i = 0; i < x.Length; i++)
            dist += (x[i] - y[i]) * (x[i] - y[i]);
        return dist;
    };

    /// <summary>Squared screen-space distance between two predictions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Prediction a, Prediction b)
    {
        float dx = a.CenterXTranslated - b.CenterXTranslated;
        float dy = a.CenterYTranslated - b.CenterYTranslated;
        return dx * dx + dy * dy;
    }

    /// <summary>
    ///     Score a candidate target against a predicted future position with confidence, size and lock
    ///     bonuses. Used by sticky-aim selection. Higher = better match.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CalculateTargetScore(
        Prediction candidate,
        Prediction? currentTarget,
        float predictedX,
        float predictedY,
        float currentLockScore,
        float maxLockScore,
        float threshold)
    {
        float dx = candidate.CenterXTranslated - predictedX;
        float dy = candidate.CenterYTranslated - predictedY;
        float distSq = dx * dx + dy * dy;

        float thresholdSq = threshold * threshold;
        float distanceScore = Math.Max(0f, 1f - (distSq / thresholdSq));

        float confidenceBonus = candidate.Confidence * 0.3f;

        float area = candidate.Rectangle.Width * candidate.Rectangle.Height;
        float sizeBonus = Math.Min(0.2f, area / 50000f);

        float lockBonus = (currentTarget != null && distanceScore > 0.3f)
            ? (currentLockScore / maxLockScore) * 0.5f
            : 0f;

        return distanceScore + confidenceBonus + sizeBonus + lockBonus;
    }

    /// <summary>YOLOv8 anchor-free detection count: (s/8)² + (s/16)² + (s/32)².</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateNumDetections(int imageSize)
    {
        int stride8 = imageSize / 8;
        int stride16 = imageSize / 16;
        int stride32 = imageSize / 32;
        return (stride8 * stride8) + (stride16 * stride16) + (stride32 * stride32);
    }

    // LUT = look up table.
    // Reference: https://stackoverflow.com/questions/1089235/where-can-i-find-a-byte-to-float-lookup-table
    private static readonly float[] _byteToFloatLut = CreateByteToFloatLut();

    private static float[] CreateByteToFloatLut()
    {
        var lut = new float[256];
        for (int i = 0; i < 256; i++) lut[i] = i / 255f;
        return lut;
    }

    /// <summary>
    ///     Fast bitmap → float tensor (CHW, BGR-order in upstream, mapped through a precomputed LUT
    ///     and processed in row-parallel chunks of 4 pixels). Caller supplies the destination buffer
    ///     to avoid per-frame allocation. <paramref name="imageSize"/> = both width and height.
    /// </summary>
    public static unsafe void BitmapToFloatArrayInPlace(Bitmap image, float[] result, int imageSize)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        if (result == null) throw new ArgumentNullException(nameof(result));

        int width = imageSize;
        int height = imageSize;
        int totalPixels = width * height;

        if (result.Length != 3 * totalPixels)
            throw new ArgumentException($"result must be length {3 * totalPixels}", nameof(result));

        var rect = new Rectangle(0, 0, width, height);
        var bmpData = image.LockBits(rect, ImageLockMode.ReadOnly, image.PixelFormat);
        try
        {
            byte* basePtr = (byte*)bmpData.Scan0;
            int stride = Math.Abs(bmpData.Stride);

            // 32bpp BGRA assumed (the fork captures into Format32bppArgb / PArgb).
            const int bytesPerPixel = 4;
            const int pixelsPerIteration = 4;

            int rOffset = 0;
            int gOffset = totalPixels;
            int bOffset = totalPixels * 2;

            fixed (float* dest = result)
            {
                float* rPtr = dest + rOffset;
                float* gPtr = dest + gOffset;
                float* bPtr = dest + bOffset;

                Parallel.For(0, height, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (y) =>
                {
                    byte* row = basePtr + (long)y * stride;
                    int rowStart = y * width;
                    int x = 0;
                    int widthLimit = width - pixelsPerIteration + 1;

                    for (; x < widthLimit; x += pixelsPerIteration)
                    {
                        int baseIdx = rowStart + x;
                        byte* p = row + (x * bytesPerPixel);

                        bPtr[baseIdx]     = _byteToFloatLut[p[0]];
                        gPtr[baseIdx]     = _byteToFloatLut[p[1]];
                        rPtr[baseIdx]     = _byteToFloatLut[p[2]];

                        bPtr[baseIdx + 1] = _byteToFloatLut[p[4]];
                        gPtr[baseIdx + 1] = _byteToFloatLut[p[5]];
                        rPtr[baseIdx + 1] = _byteToFloatLut[p[6]];

                        bPtr[baseIdx + 2] = _byteToFloatLut[p[8]];
                        gPtr[baseIdx + 2] = _byteToFloatLut[p[9]];
                        rPtr[baseIdx + 2] = _byteToFloatLut[p[10]];

                        bPtr[baseIdx + 3] = _byteToFloatLut[p[12]];
                        gPtr[baseIdx + 3] = _byteToFloatLut[p[13]];
                        rPtr[baseIdx + 3] = _byteToFloatLut[p[14]];
                    }

                    for (; x < width; x++)
                    {
                        int idx = rowStart + x;
                        byte* p = row + (x * bytesPerPixel);
                        bPtr[idx] = _byteToFloatLut[p[0]];
                        gPtr[idx] = _byteToFloatLut[p[1]];
                        rPtr[idx] = _byteToFloatLut[p[2]];
                    }
                });
            }
        }
        finally
        {
            image.UnlockBits(bmpData);
        }
    }

    /// <summary>
    ///     Allocating variant for callers that don't yet pool a buffer. Internally calls
    ///     <see cref="BitmapToFloatArrayInPlace"/>.
    /// </summary>
    public static float[] BitmapToFloatArray(Bitmap image, int imageSize)
    {
        var buf = new float[3 * imageSize * imageSize];
        BitmapToFloatArrayInPlace(image, buf, imageSize);
        return buf;
    }
}
