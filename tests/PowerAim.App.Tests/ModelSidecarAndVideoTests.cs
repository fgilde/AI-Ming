using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PowerAim.AI;

namespace PowerAim.App.Tests;

public class ModelSidecarTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "poweraim-sidecar-" + Guid.NewGuid().ToString("N"));

    public ModelSidecarTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Sidecar_next_to_the_model_supplies_class_rules_and_notes()
    {
        var model = Path.Combine(_dir, "enemy.onnx");
        File.WriteAllText(model + ".json", """
            { "classSpecs": [ { "classId": 2, "priority": 0, "useAnchor": true, "anchorY": 0.25 },
                              { "classId": 1, "ignored": true } ],
              "notes": "In-game FOV 90" }
            """);

        var sidecar = ModelSidecar.Load(model);

        Assert.NotNull(sidecar);
        Assert.Equal("In-game FOV 90", sidecar!.Notes);
        var head = sidecar.ClassSpecs!.Single(s => s.ClassId == 2);
        Assert.Equal((0, true, 0.25f), (head.Priority, head.UseAnchor, head.AnchorY));
        Assert.True(sidecar.ClassSpecs!.Single(s => s.ClassId == 1).Ignored);
    }

    [Fact]
    public void Class_rules_take_the_model_recommendation_for_classes_without_a_rule()
    {
        var profile = new PowerAim.Config.AimProfile { Name = "p" };
        profile.ClassSpecs.Add(new PowerAim.Config.ClassTargetSpec { ClassId = 0, Priority = 5 });
        var sidecar = new ModelSidecar([new() { ClassId = 0, Priority = 1 }, new() { ClassId = 1, Ignored = true }], null);

        PowerAim.UI.Visuality.ClassRulesDialog.AddMissingRows(profile,
            new Dictionary<int, string> { [0] = "enemy", [1] = "team", [2] = "head" }, sidecar);

        Assert.Equal(5, profile.ClassSpecs.Single(s => s.ClassId == 0).Priority); // the user's rule stays
        Assert.True(profile.ClassSpecs.Single(s => s.ClassId == 1).Ignored);      // recommended
        Assert.Equal("team", profile.ClassSpecs.Single(s => s.ClassId == 1).Name);
        Assert.Equal(-1, profile.ClassSpecs.Single(s => s.ClassId == 2).Priority); // neutral
        Assert.False(sidecar.ClassSpecs![1].Name == "team"); // the sidecar itself is not modified
    }

    [Fact]
    public void No_or_broken_sidecar_is_no_sidecar()
    {
        var model = Path.Combine(_dir, "plain.onnx");
        Assert.Null(ModelSidecar.Load(model));

        File.WriteAllText(model + ".json", "{ not json");
        Assert.Null(ModelSidecar.Load(model));
    }
}

public class ReplayVideoTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "poweraim-video-" + Guid.NewGuid().ToString("N"));

    public ReplayVideoTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Exported_frames_become_an_mp4_when_ffmpeg_is_available()
    {
        for (int i = 0; i < 12; i++)
        {
            using var bmp = new Bitmap(65, 49); // odd size on purpose: the encoder needs even dimensions
            using (var g = Graphics.FromImage(bmp)) g.Clear(Color.FromArgb(i * 20, 80, 160));
            bmp.Save(Path.Combine(_dir, $"frame_{i:D4}.jpg"), ImageFormat.Jpeg);
        }

        var mp4 = ReplayVideo.TryEncode(_dir, framesPerSecond: 30, width: 65, height: 49);

        if (!ReplayVideo.EncoderAvailable())
        {
            Assert.Null(mp4);
            return;
        }
        Assert.NotNull(mp4);
        Assert.True(new FileInfo(mp4!).Length > 0);
    }

    [Theory]
    [InlineData(0, 10, 1.0, 9)]   // 10 frames, first to last 1 s apart → 9 intervals
    [InlineData(0, 61, 2.0, 30)]
    [InlineData(0, 2, 0.0, 30)]   // no time span → default
    public void Frame_rate_comes_from_the_capture_span(int first, int count, double seconds, double expected)
        => Assert.Equal(expected, ReplayVideo.FrameRate(count - first, seconds), 1);
}
