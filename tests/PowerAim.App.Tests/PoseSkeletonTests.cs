using System.Drawing;
using PowerAim.AI;

namespace PowerAim.App.Tests;

public class PoseSkeletonTests
{
    private static Prediction Pose(params (int I, float X, float Y, float C)[] set)
    {
        var k = new Keypoint[17];
        foreach (var (i, x, y, c) in set) k[i] = new Keypoint(x, y, c);
        // Model box (100,100,40,80) shown at twice the size, offset by the detection box origin (500,300).
        return new Prediction
        {
            Rectangle = new RectangleF(100, 100, 40, 80),
            TranslatedRectangle = new RectangleF(700, 500, 80, 160),
            Keypoints = k,
        };
    }

    [Fact]
    public void Shoulder_line_is_mapped_like_the_box()
    {
        var segments = PoseSkeleton.Segments(Pose((5, 110, 120, 0.9f), (6, 130, 120, 0.9f)), 0.5f).ToList();

        var (a, b) = Assert.Single(segments);
        Assert.Equal(new PointF(720, 540), a);
        Assert.Equal(new PointF(760, 540), b);
    }

    [Fact]
    public void Unconfident_keypoints_draw_no_bones()
        => Assert.Empty(PoseSkeleton.Segments(Pose((5, 110, 120, 0.9f), (6, 130, 120, 0.2f)), 0.5f));

    [Fact]
    public void Models_without_keypoints_draw_nothing()
        => Assert.Empty(PoseSkeleton.Segments(new Prediction { Rectangle = new RectangleF(0, 0, 10, 10) }, 0.5f));

    [Fact]
    public void The_overlay_draws_the_skeleton()
    {
        TestConfig.Ensure();
        // Keypoints in model space; the drawn box is the same rectangle (scale 1, no offset).
        var k = new Keypoint[17];
        k[5] = new Keypoint(30, 40, 0.9f);
        k[6] = new Keypoint(90, 40, 0.9f);
        var p = new Prediction { Rectangle = new RectangleF(20, 20, 80, 100), TranslatedRectangle = new RectangleF(20, 20, 80, 100), Keypoints = k };

        using var bmp = new Bitmap(120, 140);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            PowerAim.Other.PredictionDrawer.DrawPredictions(g, [p]);
        }

        Assert.NotEqual(Color.Black.ToArgb(), bmp.GetPixel(60, 40).ToArgb());   // middle of the shoulder line
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(60, 70).ToArgb());      // empty inside of the box
    }
}
