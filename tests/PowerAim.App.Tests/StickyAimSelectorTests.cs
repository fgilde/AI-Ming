using System.Drawing;
using PowerAim.AI;

namespace PowerAim.App.Tests;

public class StickyAimSelectorTests
{
    private static Prediction At(float x, float y, float size = 40, float confidence = 0.9f) => new()
    {
        CenterXTranslated = x,
        CenterYTranslated = y,
        Confidence = confidence,
        Rectangle = new RectangleF(x - size / 2, y - size / 2, size, size),
    };

    private static Prediction? Select(StickyAimSelector selector, params Prediction[] candidates)
        => selector.SelectTarget(candidates, candidates.MaxBy(p => p.Confidence), true, 80, 100);

    [Fact]
    public void Lost_target_is_coasted_briefly_with_fading_confidence_then_released()
    {
        var selector = new StickyAimSelector();
        var held = At(100, 100);
        Select(selector, held);

        var ghosts = Enumerable.Range(0, 3).Select(_ => Select(selector)).ToList();

        Assert.All(ghosts, g => Assert.NotNull(g));
        Assert.True(ghosts[0]!.Confidence > ghosts[1]!.Confidence && ghosts[1]!.Confidence > ghosts[2]!.Confidence);
        Assert.Equal(100, held.CenterXTranslated); // the real detection is never moved
        Assert.Null(Select(selector));
    }

    [Fact]
    public void Switches_to_a_new_enemy_instead_of_aiming_at_the_old_position()
    {
        var selector = new StickyAimSelector();
        Select(selector, At(100, 100));

        var other = At(400, 300);
        var selected = Select(selector, other);

        Assert.Same(other, selected);
    }

    [Fact]
    public void Keeps_following_the_same_enemy_as_it_moves()
    {
        var selector = new StickyAimSelector();
        Select(selector, At(100, 100));

        var moved = At(110, 102);
        var rival = At(400, 300, confidence: 0.95f);

        Assert.Same(moved, Select(selector, moved, rival));
    }
}
