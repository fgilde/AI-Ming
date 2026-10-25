using System.Drawing;
using PowerAim.AI;
using PowerAim.AI.Aiming;

namespace PowerAim.App.Tests;

public class TargetTrackerTests
{
    private static Prediction Box(float x, float y, float w = 40, float h = 80) => new()
    {
        Confidence = 0.9f,
        Rectangle = new RectangleF(x, y, w, h),
        CenterXTranslated = x + w / 2,
        CenterYTranslated = y + h / 2,
    };

    [Fact]
    public void Fast_pan_keeps_the_same_track_id()
    {
        var tracker = new TargetTracker();
        IReadOnlyList<TargetTrack> tracks = [];
        for (int i = 0; i < 4; i++)
            tracks = tracker.Update([Box(300, 280)], 1.0 / 60);
        int id = Assert.Single(tracks).Id;

        // The whole view jumps by more than a box width (fast pan): the boxes no longer overlap.
        tracks = tracker.Update([Box(350, 280)], 1.0 / 60);

        Assert.Contains(tracks, t => t.Id == id && t.MissedFrames == 0);
    }

    [Fact]
    public void A_far_detection_is_not_grabbed()
    {
        var tracker = new TargetTracker();
        IReadOnlyList<TargetTrack> tracks = [];
        for (int i = 0; i < 4; i++)
            tracks = tracker.Update([Box(100, 100)], 1.0 / 60);
        int id = Assert.Single(tracks).Id;

        tracks = tracker.Update([Box(500, 400)], 1.0 / 60);

        Assert.DoesNotContain(tracks, t => t.Id == id && t.MissedFrames == 0);
    }
}
