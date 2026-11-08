using System.Drawing;
using PowerAim.AI;

namespace PowerAim.App.Tests;

/// <summary>
///     Third-person games: the player's own character sits next to the crosshair, reaches out of the bottom
///     of the captured area and stays put relative to it while the view turns. Shapes taken from a The
///     Division 2 recording (640 px model space).
/// </summary>
public class SelfFilterTests
{
    private const int Size = 640;
    private static readonly RectangleF Me = new(30, 268, 350, 372);       // body, cut off at the bottom
    private static readonly RectangleF MyHead = new(140, 268, 100, 105);  // inside the body box
    private static readonly RectangleF Enemy = new(445, 166, 115, 254);

    private static Prediction P(RectangleF r) => new() { Rectangle = r, Confidence = 0.9f };

    private static List<Prediction> Frame(SelfFilter f, double ms, params RectangleF[] boxes)
        => f.Apply(boxes.Select(P).ToList(), Size, ms);

    private static RectangleF Jitter(RectangleF r, int i) => new(r.X + (i % 3) - 1, r.Y + (i % 2), r.Width, r.Height);

    [Fact]
    public void My_own_character_is_dropped_once_it_stayed_put_and_my_head_with_it()
    {
        var f = new SelfFilter();
        List<Prediction> kept = [];
        for (int i = 0; i <= 30; i++) kept = Frame(f, i * 16.7, Jitter(Me, i), Jitter(MyHead, i), Enemy);

        Assert.Equal([Enemy], kept.Select(p => p.Rectangle));
    }

    [Fact]
    public void Nothing_is_dropped_before_the_character_stayed_put_long_enough()
    {
        var f = new SelfFilter();
        var kept = Frame(f, 0, Me, Enemy);
        kept = Frame(f, 100, Me, Enemy);

        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void A_close_enemy_moving_across_the_bottom_is_kept()
    {
        var f = new SelfFilter();
        List<Prediction> kept = [];
        for (int i = 0; i <= 40; i++)
            kept = Frame(f, i * 16.7, new RectangleF(40 + i * 8, 300, 200, 340));

        Assert.Single(kept);
    }

    [Fact]
    public void A_small_box_at_the_bottom_is_not_taken_for_me()
    {
        var f = new SelfFilter();
        var crouched = new RectangleF(400, 560, 60, 80);   // an enemy's head and shoulders behind cover
        List<Prediction> kept = [];
        for (int i = 0; i <= 40; i++) kept = Frame(f, i * 16.7, crouched);

        Assert.Single(kept);
    }

    [Fact]
    public void After_a_pose_change_I_am_recognised_again_at_once()
    {
        var f = new SelfFilter();
        for (int i = 0; i <= 30; i++) Frame(f, i * 16.7, Me);
        // Into cover: the camera swings in and the box jumps (no IoU with the last frame's box).
        var inCover = new RectangleF(0, 289, 330, 351);
        Frame(f, 600, Enemy);
        var kept = Frame(f, 617, inCover, Enemy);

        Assert.Equal([Enemy], kept.Select(p => p.Rectangle));
    }

    [Fact]
    public void A_box_filling_the_whole_capture_does_not_hide_the_enemies_inside_it()
    {
        var f = new SelfFilter();
        var closeUp = new RectangleF(0, 0, 640, 640);   // the camera inside my shoulder
        List<Prediction> kept = [];
        for (int i = 0; i <= 30; i++) kept = Frame(f, i * 16.7, closeUp, Enemy);

        Assert.Contains(Enemy, kept.Select(p => p.Rectangle));
    }

    [Fact]
    public void The_remembered_place_expires()
    {
        var f = new SelfFilter();
        for (int i = 0; i <= 30; i++) Frame(f, i * 16.7, Me);
        Frame(f, 600, Enemy);

        // Much later a big enemy stands right there for a moment: no longer taken for me.
        var kept = Frame(f, 600 + SelfFilter.MemoryMs + 100, Me);

        Assert.Single(kept);
    }
}
