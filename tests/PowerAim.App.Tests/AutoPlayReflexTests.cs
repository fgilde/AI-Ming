using System.Drawing;
using PowerAim.AI;
using PowerAim.AI.AutoPlay;

namespace PowerAim.App.Tests;

public class AutoPlayPerceptionTests
{
    private static Prediction Box(float x, float y, float w = 40, float h = 80) =>
        new() { TranslatedRectangle = new RectangleF(x - w / 2, y - h / 2, w, h), Confidence = 0.9f };

    [Fact]
    public void Nearest_enemy_is_measured_from_the_capture_area_centre_in_pixels()
    {
        var seen = AutoPlayPerception.Perceive([Box(1500, 540), Box(1060, 500)], new Size(1920, 1080));

        Assert.Equal(2, seen.EnemyCount);
        Assert.NotNull(seen.Nearest);
        Assert.Equal(100, seen.Nearest!.Dx, 3);   // right of the crosshair
        Assert.Equal(-40, seen.Nearest.Dy, 3);    // above it
        Assert.Equal(80f / 1080f, seen.NearestSize, 4);
    }

    [Fact]
    public void Nothing_visible_means_no_nearest_enemy()
        => Assert.Null(AutoPlayPerception.Perceive([], new Size(1920, 1080)).Nearest);
}

public class AutoPlayReflexTests
{
    private static readonly Size Area = new(1920, 1080);
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);

    private static AutoPlayPerception Enemy(float dx, float dy, float height = 200) =>
        new(1, new EnemyView(dx, dy, height / 2, height), Area);

    private static AutoPlayPerception Nobody => new(0, null, Area);

    private static AutoPlayIntent Intent(string mode, string? dir = null) => new(mode, dir, null, T0);

    [Fact]
    public void Enemy_to_the_right_turns_the_mouse_right_and_strafes_right()
    {
        var reflex = new AutoPlayReflex(new Random(1));
        var cmd = reflex.Tick(Enemy(300, 0), AutoPlayIntent.Default, default, T0);

        Assert.True(cmd.MouseDx > 0);
        Assert.Contains(ControlRole.MoveRight, cmd.Hold);
        Assert.Contains(ControlRole.Aim, cmd.Hold);
        Assert.True(cmd.InCombat);
    }

    [Fact]
    public void Fires_once_the_aim_settled_on_a_centred_enemy()
    {
        var reflex = new AutoPlayReflex(new Random(1));
        var first = reflex.Tick(Enemy(10, 5), AutoPlayIntent.Default, default, T0);
        Assert.DoesNotContain(ControlRole.Shoot, first.Hold); // just spotted: let the aim settle

        var later = reflex.Tick(Enemy(10, 5), AutoPlayIntent.Default, default, T0.AddMilliseconds(200));
        Assert.Contains(ControlRole.Shoot, later.Hold);
    }

    [Fact]
    public void Leaves_mouse_and_trigger_to_the_other_features_when_they_own_them()
    {
        var reflex = new AutoPlayReflex(new Random(1));
        var ctx = new ReflexContext(AimHandledExternally: true, FireHandledExternally: true);
        reflex.Tick(Enemy(300, 40), AutoPlayIntent.Default, ctx, T0);
        var cmd = reflex.Tick(Enemy(300, 40), AutoPlayIntent.Default, ctx, T0.AddMilliseconds(300));

        Assert.Equal(0, cmd.MouseDy);
        Assert.DoesNotContain(ControlRole.Shoot, cmd.Hold);
        Assert.Contains(ControlRole.MoveRight, cmd.Hold); // still moves
    }

    [Fact]
    public void Range_comes_from_box_size_far_pushes_close_backs_off()
    {
        var far = new AutoPlayReflex(new Random(1)).Tick(Enemy(0, 0, height: 60), AutoPlayIntent.Default, default, T0);
        Assert.Contains(ControlRole.MoveForward, far.Hold);

        var close = new AutoPlayReflex(new Random(1)).Tick(Enemy(0, 0, height: 700), AutoPlayIntent.Default, default, T0);
        Assert.Contains(ControlRole.MoveBackward, close.Hold);
    }

    [Fact]
    public void Retreat_intent_backs_off_from_an_enemy()
    {
        var cmd = new AutoPlayReflex(new Random(1)).Tick(Enemy(0, 0, height: 60), Intent("retreat"), default, T0);
        Assert.Contains(ControlRole.MoveBackward, cmd.Hold);
        Assert.DoesNotContain(ControlRole.MoveForward, cmd.Hold);
    }

    [Fact]
    public void Explores_in_the_models_direction_and_sprints()
    {
        int forwardLeft = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var cmd = new AutoPlayReflex(new Random(seed)).Tick(Nobody, Intent("explore", "left"), default, T0);
            Assert.Contains(ControlRole.Sprint, cmd.Hold);
            if (cmd.Hold.Contains(ControlRole.MoveLeft) && cmd.Hold.Contains(ControlRole.MoveForward)) forwardLeft++;
        }
        Assert.True(forwardLeft >= 20, $"followed the hint {forwardLeft}/40 times"); // 70 % by design
    }

    [Fact]
    public void Hold_intent_stands_still()
    {
        var cmd = new AutoPlayReflex(new Random(1)).Tick(Nobody, Intent("hold"), default, T0);
        Assert.DoesNotContain(ControlRole.MoveForward, cmd.Hold);
        Assert.DoesNotContain(ControlRole.Sprint, cmd.Hold);
    }

    [Fact]
    public void Keeps_firing_briefly_after_the_enemy_vanishes_then_stops()
    {
        var reflex = new AutoPlayReflex(new Random(1));
        reflex.Tick(Enemy(0, 0), AutoPlayIntent.Default, default, T0);
        var firing = reflex.Tick(Enemy(0, 0), AutoPlayIntent.Default, default, T0.AddMilliseconds(200));
        Assert.Contains(ControlRole.Shoot, firing.Hold);

        var blink = reflex.Tick(Nobody, AutoPlayIntent.Default, default, T0.AddMilliseconds(250));
        Assert.Contains(ControlRole.Shoot, blink.Hold);

        var gone = reflex.Tick(Nobody, AutoPlayIntent.Default, default, T0.AddMilliseconds(700));
        Assert.DoesNotContain(ControlRole.Shoot, gone.Hold);
    }

    [Fact]
    public void Low_ammo_reloads_with_cooldown_and_low_health_forces_retreat()
    {
        var reflex = new AutoPlayReflex(new Random(1));
        var ctx = new ReflexContext(Ammo: 2, Health: 20);
        var cmd = reflex.Tick(Enemy(0, 0, height: 60), Intent("engage"), ctx, T0);

        Assert.Contains(ControlRole.Reload, cmd.Tap);
        Assert.Contains(ControlRole.MoveBackward, cmd.Hold);
        Assert.Equal("retreat", reflex.Effective(Intent("engage"), T0.AddSeconds(1)).Mode);

        var again = reflex.Tick(Enemy(0, 0), Intent("engage"), ctx, T0.AddSeconds(1));
        Assert.DoesNotContain(ControlRole.Reload, again.Tap);
    }

    [Fact]
    public void Stuck_turns_away_hard_and_jumps()
    {
        var reflex = new AutoPlayReflex(new Random(3));
        var cmd = reflex.Tick(Nobody, Intent("explore", "forward"), new ReflexContext(Stuck: true), T0);

        Assert.Contains(ControlRole.Jump, cmd.Tap);
        Assert.True(Math.Abs(cmd.MouseDx) >= 20, $"turned {cmd.MouseDx}");
        Assert.True(cmd.Hold.Contains(ControlRole.MoveLeft) || cmd.Hold.Contains(ControlRole.MoveRight));
    }
}
