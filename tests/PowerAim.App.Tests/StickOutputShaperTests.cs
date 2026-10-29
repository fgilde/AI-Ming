using PowerAim.Input.Gamepad;

namespace PowerAim.App.Tests;

public class StickOutputShaperTests
{
    private static double Len((double X, double Y) v) => Math.Sqrt(v.X * v.X + v.Y * v.Y);

    [Fact]
    public void Defaults_leave_the_ai_vector_unchanged()
    {
        Assert.Equal((0.3, -0.1), StickOutputShaper.ShapeAi(0.3, -0.1, StickShapeOptions.Default));
        Assert.Equal((0.3, -0.1), StickOutputShaper.Mix(0.3, -0.1, 0.8, 0.8, -1, StickShapeOptions.Default));
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(0.02)]
    [InlineData(0.5)]
    public void Every_correction_lands_above_the_game_deadzone(double r)
    {
        var o = StickShapeOptions.Default with { GameDeadzonePct = 5 };

        Assert.True(Len(StickOutputShaper.ShapeAi(r, 0, o)) >= 0.07);
    }

    [Fact]
    public void No_correction_stays_zero()
        => Assert.Equal((0.0, 0.0), StickOutputShaper.ShapeAi(0, 0, StickShapeOptions.Default with { GameDeadzonePct = 5 }));

    [Fact]
    public void Max_output_caps_the_deflection()
    {
        var o = StickShapeOptions.Default with { MaxOutputPct = 50 };

        Assert.True(Len(StickOutputShaper.ShapeAi(1, 1, o)) <= 0.5 + 1e-9);
        Assert.True(Len(StickOutputShaper.ShapeAi(0.9, 0, o with { GameDeadzonePct = 10 })) <= 0.5 + 1e-9);
    }

    [Fact]
    public void Full_manual_override_with_full_stick_is_the_physical_stick()
    {
        var o = StickShapeOptions.Default with { ManualOverridePct = 100 };
        var (x, y) = StickOutputShaper.Mix(0.6, 0.2, 0, -1, 0, o);

        Assert.Equal(0, x, 6);
        Assert.Equal(-1, y, 6);
    }

    [Fact]
    public void Manual_override_blends_by_stick_magnitude()
    {
        var o = StickShapeOptions.Default with { ManualOverridePct = 50 };
        // |user| = 0.5 → blend 0.25
        var (x, _) = StickOutputShaper.Mix(0.8, 0, 0.5, 0, 0, o);

        Assert.Equal(0.8 * 0.75 + 0.5 * 0.25, x, 6);
    }

    [Theory]
    [InlineData(StickResponseCurve.Linear)]
    [InlineData(StickResponseCurve.Exponential)]
    [InlineData(StickResponseCurve.SCurve)]
    public void Inverse_curve_undoes_the_curve(StickResponseCurve curve)
    {
        for (double x = 0; x <= 1.0001; x += 0.05)
            Assert.Equal(x, StickOutputShaper.InverseCurve(StickOutputShaper.Curve(x, curve), curve), 4);
    }

    [Fact]
    public void Ai_y_can_be_inverted_without_touching_the_user_stick()
    {
        var o = StickShapeOptions.Default with { InvertAiY = true, ManualOverridePct = 100 };
        var shaped = StickOutputShaper.ShapeAi(0, 0.4, o);
        Assert.Equal(-0.4, shaped.Y, 6);

        Assert.Equal(1, StickOutputShaper.Mix(0, 0, 0, 1, 0, o).Y, 6); // the user stick passes as is
    }

    [Fact]
    public void Anti_strafe_pushes_only_toward_the_target()
    {
        var o = StickShapeOptions.Default with { AntiStrafePct = 50 };
        // Strafing left (leftX = -1) drifts the crosshair right of a target that is to the right → push right.
        var toward = StickOutputShaper.Mix(0.2, 0, 0, 0, -1, o);
        Assert.True(toward.X > 0.2);

        // Same strafe while the target is to the left: the push would point away → not applied.
        var away = StickOutputShaper.Mix(-0.2, 0, 0, 0, -1, o);
        Assert.Equal(-0.2, away.X, 6);
    }

    [Fact]
    public void Output_never_leaves_the_unit_circle()
    {
        var o = StickShapeOptions.Default with { AntiStrafePct = 50, ManualOverridePct = 30 };
        var v = StickOutputShaper.Mix(0.9, 0.9, 0.2, 0.2, -1, o);

        Assert.True(Len(v) <= 1 + 1e-9);
    }

    [Fact]
    public void Mixer_sends_the_ai_vector_while_fresh_and_releases_it_after_80_ms()
    {
        var mixer = new StickMixer();
        mixer.SetAi(0.4, 0.1, nowMs: 0);

        Assert.Equal((0.4, 0.1), mixer.Next(50, 0.9, 0.9, 0, StickShapeOptions.Default));
        Assert.Equal((0.0, 0.0), mixer.Next(81, 0.9, 0.9, 0, StickShapeOptions.Default));
        // Default options never pass the physical stick (the virtual pad's own sync owns it otherwise).
        Assert.Equal((0.0, 0.0), mixer.Next(500, 0.9, 0.9, 0, StickShapeOptions.Default));
    }

    [Fact]
    public void Mixer_with_manual_override_hands_back_the_physical_stick_after_a_release_gap()
    {
        var o = StickShapeOptions.Default with { ManualOverridePct = 25 };
        var mixer = new StickMixer();
        mixer.SetAi(0.4, 0, nowMs: 0);

        Assert.Equal((0.0, 0.0), mixer.Next(90, 0.5, 0, 0, o));   // released to neutral first
        Assert.Equal((0.0, 0.0), mixer.Next(100, 0.5, 0, 0, o));  // still inside the gap
        Assert.Equal((0.5, 0.0), mixer.Next(115, 0.5, 0, 0, o));  // physical stick passes through
    }

    [Fact]
    public void Mixer_reading_its_own_output_settles_at_neutral()
    {
        var o = StickShapeOptions.Default with { ManualOverridePct = 25 };
        var mixer = new StickMixer();
        mixer.SetAi(0.6, 0, nowMs: 0);
        var last = mixer.Next(1, 0, 0, 0, o);

        // The "physical" input is our own virtual pad: it echoes whatever was sent last.
        for (double t = 2; t < 400; t += 2) last = mixer.Next(t, last.X, last.Y, 0, o);

        Assert.Equal((0.0, 0.0), last);
    }
}

public class StickTestPatternTests
{
    [Fact]
    public void Deadzone_check_holds_exactly_the_configured_deadzone_to_the_right()
    {
        foreach (var t in new[] { 0.0, 3.2, 14.9 })
            Assert.Equal((0.05, 0.0, false), StickTestPatterns.DeadzoneCheck(t, gameDeadzonePct: 5));
    }

    [Fact]
    public void Output_test_circles_the_stick_and_pulses_the_bumper_every_two_seconds()
    {
        var a = StickTestPatterns.OutputTest(0.1);
        var b = StickTestPatterns.OutputTest(1.0);
        Assert.Equal(0.5, Math.Sqrt(a.X * a.X + a.Y * a.Y), 6);
        Assert.Equal(0.5, Math.Sqrt(b.X * b.X + b.Y * b.Y), 6);
        Assert.NotEqual(Math.Sign(a.X), Math.Sign(b.X)); // half a turn later, the other side

        Assert.True(StickTestPatterns.OutputTest(0.1).Bumper);
        Assert.False(StickTestPatterns.OutputTest(1.0).Bumper);
        Assert.True(StickTestPatterns.OutputTest(2.1).Bumper);
    }
}
