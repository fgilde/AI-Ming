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

    [Fact]
    public void A_box_past_the_screen_edge_comes_back_cropped_to_the_screen()
    {
        if (!DxgiScreenCapture.IsSupported())
            return;

        using var capture = new DxgiScreenCapture(); // fresh duplication: the first acquire always has a frame
        var screen = capture.CaptureArea;

        using var bmp = capture.Capture(new Rectangle(screen.Width - 20, 8, 64, 48));

        Assert.Equal(20, bmp.Width);
        Assert.Equal(48, bmp.Height);
    }

    [Fact]
    public void Region_copy_returns_the_pixels_of_that_region()
    {
        if (!DxgiScreenCapture.IsSupported())
            return;

        var colour = System.Drawing.Color.FromArgb(12, 200, 90);
        Bitmap? shot = null;
        var thread = new Thread(() =>
        {
            using var form = new System.Windows.Forms.Form
            {
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Bounds = new Rectangle(200, 200, 200, 150),
                BackColor = colour,
                TopMost = true,
                ShowInTaskbar = false,
            };
            form.Show();
            using var capture = new DxgiScreenCapture();
            // Straddles the window's left edge: 40 px of desktop, then 40 px of the window.
            var box = new Rectangle(160, 240, 80, 40);
            for (int i = 0; i < 30 && shot == null; i++)
            {
                form.BackColor = i % 2 == 0 ? colour : System.Drawing.Color.FromArgb(13, 200, 90); // forces a new frame
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(40);
                var bmp = capture.Capture(box);
                if (Near(bmp.GetPixel(60, 20), colour)) shot = bmp; else bmp.Dispose();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.NotNull(shot);
        using (shot)
        {
            Assert.True(Near(shot!.GetPixel(79, 0), colour));   // right half: inside the window
            Assert.True(Near(shot.GetPixel(40, 39), colour));
            Assert.False(Near(shot.GetPixel(10, 20), colour));  // left half: the desktop next to it
        }
    }

    private static bool Near(System.Drawing.Color a, System.Drawing.Color b)
        => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) <= 6;
}
