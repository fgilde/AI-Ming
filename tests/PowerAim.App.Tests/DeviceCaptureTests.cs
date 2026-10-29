using System.Diagnostics;
using System.Drawing;
using System.IO;
using OpenCvSharp;
using PowerAim.AI;

namespace PowerAim.App.Tests;

public class DeviceCaptureTests
{
    private static Mat Frame(int w, int h)
    {
        // Left half red, right half blue (BGR), a green row at the top.
        var m = new Mat(h, w, MatType.CV_8UC3, new Scalar(0, 0, 255));
        m[new OpenCvSharp.Rect(w / 2, 0, w - w / 2, h)].SetTo(new Scalar(255, 0, 0));
        m.Row(0).SetTo(new Scalar(0, 255, 0));
        return m;
    }

    [Fact]
    public void The_box_is_cut_out_of_the_frame()
    {
        using var frame = Frame(100, 60);
        using var bmp = DeviceCapture.Crop(frame, new Rectangle(40, 0, 20, 10), flipVertical: false);

        Assert.Equal((20, 10), (bmp.Width, bmp.Height));
        Assert.Equal(Color.FromArgb(0, 255, 0), Rgb(bmp.GetPixel(0, 0)));    // top row green
        Assert.Equal(Color.FromArgb(255, 0, 0), Rgb(bmp.GetPixel(2, 5)));    // left half red
        Assert.Equal(Color.FromArgb(0, 0, 255), Rgb(bmp.GetPixel(17, 5)));   // right half blue
    }

    [Fact]
    public void A_box_past_the_frame_edge_is_clipped()
    {
        using var frame = Frame(100, 60);
        using var bmp = DeviceCapture.Crop(frame, new Rectangle(90, 50, 30, 30), flipVertical: false);

        Assert.Equal((10, 10), (bmp.Width, bmp.Height));
    }

    [Fact]
    public void Vertical_flip_turns_the_frame_upside_down()
    {
        using var frame = Frame(100, 60);
        using var bmp = DeviceCapture.Crop(frame, new Rectangle(0, 50, 10, 10), flipVertical: true);

        Assert.Equal(Color.FromArgb(0, 255, 0), Rgb(bmp.GetPixel(0, 9)));   // the green top row is now the bottom
    }

    [Fact]
    public void Mode_probe_takes_the_first_mode_the_card_accepts()
    {
        // This card falls back to 1080p60 for anything it can't do.
        (int, int, double) Card((int W, int H, double Fps) m) => m is (1920, 1080, 60) or (1280, 720, 120) ? m : (1920, 1080, 60);

        Assert.Equal((1920, 1080, 60.0), DeviceCapture.ProbeMode(Card));
    }

    [Fact]
    public void Mode_probe_without_a_match_keeps_the_default()
        => Assert.Null(DeviceCapture.ProbeMode(_ => (640, 480, 30)));

    private static Color Rgb(Color c) => Color.FromArgb(c.R, c.G, c.B);

    /// <summary>A video file stands in for a capture card: the same reader thread, crop and frame hand-out.</summary>
    [Fact]
    public void Frames_are_read_continuously_from_a_video_source()
    {
        if (!ReplayVideo.EncoderAvailable()) return; // ffmpeg makes the stand-in video
        var dir = Path.Combine(Path.GetTempPath(), "poweraim-device-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var video = Path.Combine(dir, "source.avi");
        try
        {
            using (var p = Process.Start(new ProcessStartInfo("ffmpeg",
                       $"-y -loglevel error -f lavfi -i testsrc=size=320x240:rate=30 -t 3 -c:v mjpeg \"{video}\"")
                   { CreateNoWindow = true, UseShellExecute = false }))
                p!.WaitForExit(30_000);

            using var capture = new DeviceCapture(video, flipVertical: false);
            for (int i = 0; i < 100 && capture.CaptureArea.Width == 0; i++) Thread.Sleep(20);

            Assert.Equal(new Rectangle(0, 0, 320, 240), capture.CaptureArea);
            Assert.True(capture.CallerOwnsFrame);
            using var frame = capture.Capture(new Rectangle(100, 60, 64, 48));
            Assert.Equal((64, 48), (frame.Width, frame.Height));
            using var center = capture.CopyLastCaptureCenter(32);
            Assert.NotNull(center);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class VideoInputDevicesTests
{
    [Fact]
    public void Device_names_are_listed_without_opening_a_device()
    {
        var names = VideoInputDevices.Names(); // may be empty on machines without video inputs

        Assert.All(names, n => Assert.False(string.IsNullOrWhiteSpace(n)));
        if (names.Count > 0) Assert.Equal(0, VideoInputDevices.IndexOf(names[0]));
        Assert.Null(VideoInputDevices.IndexOf("no such device"));
    }

    [Fact]
    public void A_capture_device_source_round_trips()
    {
        var source = CaptureSource.Device(2, "Cam Link 4K", flipVertical: true);
        var json = System.Text.Json.JsonSerializer.Serialize(source);
        var back = System.Text.Json.JsonSerializer.Deserialize<CaptureSource>(json)!;

        Assert.Equal((CaptureTargetType.CaptureDevice, "Cam Link 4K", 2, true), (back.TargetType, back.Title, back.ProcessOrScreenId, back.FlipVertical));
    }
}
