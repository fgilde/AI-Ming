using System.Drawing;
using PowerAim.AI;

namespace PowerAim.App.Tests;

public class CaptureCopyTests
{
    [Fact]
    public void Center_copy_is_independent_of_the_capture_buffer()
    {
        using var capture = new GdiScreenCapture();
        Assert.Null(capture.CopyLastCaptureCenter(32)); // no frame yet

        capture.Capture(new Rectangle(0, 0, 64, 64));
        using var copy = capture.CopyLastCaptureCenter(32)!;

        // A differently sized capture replaces (disposes) the internal buffer; the copy must survive.
        capture.Capture(new Rectangle(0, 0, 48, 48));
        Assert.Equal((32, 32), (copy.Width, copy.Height));
        Assert.Null(capture.CopyLastCaptureCenter(64)); // larger than the current frame
    }
}

[Collection(nameof(DxgiScreenCaptureTests))]
public class DxgiCaptureCopyTests
{
    [Fact]
    public void Center_copy_comes_from_the_last_frame()
    {
        if (!DxgiScreenCapture.IsSupported())
            return; // no desktop duplication on this machine (e.g. headless CI)

        using var capture = new DxgiScreenCapture();
        Bitmap? copy = null;
        // A fresh duplication may time out before its first desktop frame; until then there is nothing to copy.
        for (int attempt = 0; attempt < 20 && copy == null; attempt++)
        {
            capture.Capture(new Rectangle(0, 0, 64, 64)).Dispose();
            copy = capture.CopyLastCaptureCenter(32);
        }

        Assert.NotNull(copy);
        Assert.Equal((32, 32), (copy.Width, copy.Height));
        copy.Dispose();
    }
}
