using System.Drawing;

namespace PowerAim.AI;

internal static class CapturedFrame
{
    /// <summary>A copy of the centred <paramref name="size"/>² patch of <paramref name="frame"/>; null when there is no frame or it is smaller.</summary>
    public static Bitmap? CopyCenter(Bitmap? frame, int size)
    {
        if (frame == null || frame.Width < size || frame.Height < size)
            return null;
        var patch = new Rectangle((frame.Width - size) / 2, (frame.Height - size) / 2, size, size);
        return frame.Clone(patch, frame.PixelFormat);
    }
}
