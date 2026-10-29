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

    [Fact]
    public void A_detection_is_aimable_only_after_the_configured_consecutive_frames()
    {
        var tracker = new TargetTracker { MinHits = 3 };

        Assert.Empty(tracker.Update([Box(300, 280)], 1.0 / 60));
        Assert.Empty(tracker.Update([Box(300, 280)], 1.0 / 60));
        Assert.Single(tracker.Update([Box(300, 280)], 1.0 / 60));
    }

    [Fact]
    public void A_flickering_false_positive_never_becomes_aimable()
    {
        var tracker = new TargetTracker { MinHits = 2 };
        for (int i = 0; i < 6; i++)
        {
            Assert.Empty(tracker.Update([Box(300, 280)], 1.0 / 60));
            Assert.Empty(tracker.Update([], 1.0 / 60));
        }
    }

    [Fact]
    public void Zero_confirmation_frames_aims_at_once()
        => Assert.Single(new TargetTracker { MinHits = 0 }.Update([Box(300, 280)], 1.0 / 60));

    [Fact]
    public void A_one_frame_dropout_keeps_the_lock()
    {
        var tracker = new TargetTracker { MinHits = 3 };
        var selector = new TargetSelector();
        IReadOnlyList<TargetTrack> tracks = [];
        for (int i = 0; i < 5; i++) tracks = tracker.Update([Box(300, 280), Box(420, 280)], 1.0 / 60);
        int locked = selector.Select(tracks, 320, 320, 0.25, 6, 160)!.Id;

        tracks = tracker.Update([Box(420, 280)], 1.0 / 60);       // locked target missing for a frame
        Assert.Equal(locked, selector.Select(tracks, 320, 320, 0.25, 6, 160)!.Id);

        tracks = tracker.Update([Box(300, 280), Box(420, 280)], 1.0 / 60);
        Assert.Equal(locked, selector.Select(tracks, 320, 320, 0.25, 6, 160)!.Id);
    }

    [Fact]
    public void Confidence_decays_while_coasting()
    {
        var tracker = new TargetTracker { ConfidenceDecayMs = 100 };
        for (int i = 0; i < 3; i++) tracker.Update([Box(300, 280)], 0.01);

        var coasting = Assert.Single(tracker.Update([], 0.1));

        Assert.Equal(0.9 * Math.Exp(-1), coasting.Confidence, 2);
    }

    [Fact]
    public void A_track_whose_confidence_decayed_away_is_dropped()
    {
        var tracker = new TargetTracker { ConfidenceDecayMs = 50, MaxAgeFrames = 30 };
        for (int i = 0; i < 3; i++) tracker.Update([Box(300, 280)], 0.01);

        Assert.Empty(tracker.Update([], 0.25)); // 0.9·e^-5 ≈ 0.006 < MinConfidence
    }

    [Fact]
    public void Without_decay_a_coasting_track_keeps_its_confidence()
    {
        var tracker = new TargetTracker();
        for (int i = 0; i < 3; i++) tracker.Update([Box(300, 280)], 0.01);

        Assert.Equal(0.9f, Assert.Single(tracker.Update([], 0.1)).Confidence);
    }
}
