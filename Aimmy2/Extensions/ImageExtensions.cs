using System.Drawing.Imaging;
using System.Drawing;
using Aimmy2.AILogic;

namespace Aimmy2.Extensions;

public static class ImageExtensions
{
    /// <summary>
    ///     Convert a bitmap to a CHW float tensor. Square bitmaps go through the fast LUT-based
    ///     <see cref="MathUtil.BitmapToFloatArrayInPlace"/> path; non-square bitmaps fall back to the
    ///     legacy scalar loop below. Allocates a fresh buffer on each call — for hot paths use
    ///     <see cref="ToFloatArrayInto"/> with a pooled buffer.
    /// </summary>
    public static float[] ToFloatArray(this Bitmap image)
    {
        if (image.Width == image.Height
            && (image.PixelFormat == PixelFormat.Format32bppArgb
                || image.PixelFormat == PixelFormat.Format32bppPArgb
                || image.PixelFormat == PixelFormat.Format32bppRgb))
        {
            return MathUtil.BitmapToFloatArray(image, image.Width);
        }
        return LegacyToFloatArray(image);
    }

    /// <summary>
    ///     Zero-allocation variant: writes into <paramref name="buffer"/> which must already be sized
    ///     <c>3 * image.Width * image.Height</c>. Returns the buffer for convenience.
    /// </summary>
    public static float[] ToFloatArrayInto(this Bitmap image, float[] buffer)
    {
        if (image.Width != image.Height)
            throw new ArgumentException("ToFloatArrayInto requires a square bitmap", nameof(image));
        MathUtil.BitmapToFloatArrayInPlace(image, buffer, image.Width);
        return buffer;
    }

    /// <summary>Original scalar implementation — kept as a fallback for 24bpp / non-square cases.</summary>
    private static float[] LegacyToFloatArray(Bitmap image)
    {
        int height = image.Height;
        int width = image.Width;
        float[] result = new float[3 * height * width];
        float multiplier = 1.0f / 255.0f;

        Rectangle rect = new(0, 0, width, height);
        BitmapData bmpData = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        int stride = bmpData.Stride;
        int offset = stride - width * 3;

        try
        {
            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0.ToPointer();
                int baseIndex = 0;
                for (int i = 0; i < height; i++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        result[baseIndex] = ptr[2] * multiplier;
                        result[height * width + baseIndex] = ptr[1] * multiplier;
                        result[2 * height * width + baseIndex] = ptr[0] * multiplier;
                        ptr += 3;
                        baseIndex++;
                    }
                    ptr += offset;
                }
            }
        }
        finally
        {
            image.UnlockBits(bmpData);
        }

        return result;
    }
}
