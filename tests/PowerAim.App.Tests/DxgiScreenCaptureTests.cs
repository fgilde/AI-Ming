using System.Drawing;
using PowerAim.AI;

namespace PowerAim.App.Tests;

// Desktop-duplication tests share one collection: they would otherwise run as two duplications at once.
[CollectionDefinition(nameof(DxgiScreenCaptureTests), DisableParallelization = true)]
[Collection(nameof(DxgiScreenCaptureTests))]
public class DxgiScreenCaptureTests
{
    [Fact]
    public void Unchanged_frame_comes_back_usable_and_capture_keeps_working()
    {
        if (!DxgiScreenCapture.IsSupported())
            return; // no desktop duplication on this machine (e.g. headless CI)

        using var capture = new DxgiScreenCapture();
        var box = new Rectangle(0, 0, 64, 64);
        capture.Capture(box).Dispose();
        Thread.Sleep(20); // past the frame cache

        // An off-screen box yields no new pixels, so the cached frame is handed back — the same branch
        // as a duplication timeout on a static screen. It used to be disposed before being returned.
        using var unchanged = capture.Capture(new Rectangle(100000, 100000, 64, 64));
        Assert.Equal(64, unchanged.Width);

        using var next = capture.Capture(box);
        Assert.Equal(64, next.Width);
    }
}
