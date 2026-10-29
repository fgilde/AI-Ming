using System.Drawing;
using PowerAim.AI;
using PowerAim.Config;

namespace PowerAim.App.Tests;

[Collection(nameof(TestConfig))]
public class AimOverlayTests
{
    [Fact]
    public void Model_points_map_to_the_screen_like_the_boxes()
    {
        // Model box (100,100,40,80) is drawn at twice the size, offset by the capture box origin (500,300).
        var reference = new Prediction { Rectangle = new RectangleF(100, 100, 40, 80), TranslatedRectangle = new RectangleF(700, 500, 80, 160) };

        Assert.Equal(new PointF(740, 540), AimOverlay.ToScreen(120, 120, reference));
    }

    [Fact]
    public void A_published_point_expires()
    {
        AimOverlay.Publish(new PointF(10, 20), new PointF(12, 20), nowMs: 1000);

        Assert.True(AimOverlay.TryGet(1100, out var aim, out var predicted));
        Assert.Equal((new PointF(10, 20), new PointF(12, 20)), (aim, predicted));
        Assert.False(AimOverlay.TryGet(1300, out _, out _));

        AimOverlay.Clear();
        Assert.False(AimOverlay.TryGet(1000, out _, out _));
    }

    [Fact]
    public void The_overlay_draws_the_aim_point_only_when_switched_on()
    {
        TestConfig.Ensure();
        var toggles = AppConfig.Current.ToggleState;
        bool was = toggles.ShowPredictedAim;
        try
        {
            AimOverlay.Publish(new PointF(40, 40), new PointF(40, 40), Environment.TickCount64);

            toggles.ShowPredictedAim = false;
            Assert.Equal(Color.Black.ToArgb(), Render().GetPixel(40, 40).ToArgb());

            toggles.ShowPredictedAim = true;
            Assert.NotEqual(Color.Black.ToArgb(), Render().GetPixel(40, 40).ToArgb());
        }
        finally
        {
            toggles.ShowPredictedAim = was;
            AimOverlay.Clear();
        }

        static Bitmap Render()
        {
            var bmp = new Bitmap(80, 80);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Black);
            PowerAim.Other.PredictionDrawer.DrawPredictions(g, Array.Empty<Prediction>());
            return bmp;
        }
    }
}
