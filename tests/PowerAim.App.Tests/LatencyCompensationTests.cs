using System.Drawing;
using PowerAim.AI.Aiming;

namespace PowerAim.App.Tests;

public class LatencyCompensationTests
{
    private static readonly Rectangle Box = new(0, 0, 640, 640);
    private const double FrameMs = 1000.0 / 60;

    /// <summary>
    ///     Closed loop where every move reaches the captured image <paramref name="delayFrames"/> frames
    ///     later (capture + inference latency). Returns the error per frame.
    /// </summary>
    private static List<double> Run(double compensationMs, int delayFrames = 3, double targetVelocity = 0, int frames = 90)
    {
        double now = 0, camera = 0, target = 100;
        var pending = new Queue<(int Due, int Dx)>();
        int frame = 0;
        var controller = new AimController((dx, _) => pending.Enqueue((frame + delayFrames, dx)), () => now);
        var errors = new List<double>();
        for (; frame < frames; frame++)
        {
            while (pending.Count > 0 && pending.Peek().Due <= frame) camera += pending.Dequeue().Dx;
            double error = target - camera;
            errors.Add(error);
            controller.MoveTo(320 + error, 320, Box, FrameMs / 1000, 0.5, deadzonePx: 0, maxStepCounts: 1000,
                pixelsPerCount: 1, latencyCompensationMs: compensationMs);
            now += FrameMs;
            target += targetVelocity * FrameMs / 1000;
        }
        return errors;
    }

    [Fact]
    public void Latency_makes_the_uncompensated_loop_overshoot()
        => Assert.True(Run(0).Min() < -20);

    [Fact]
    public void Compensation_removes_the_overshoot()
    {
        var errors = Run(2.5 * FrameMs);

        Assert.True(errors.Min() > -10, $"overshoot {errors.Min():F1}");
        Assert.True(Math.Abs(errors[^1]) < 2);
    }

    [Fact]
    public void Compensation_follows_a_moving_target_without_swinging_past_it()
    {
        // Without a target lead a moving target keeps a small constant lag either way; compensation
        // turns the uncompensated swing through the target into a steady follow.
        var compensated = Run(2.5 * FrameMs, targetVelocity: 120).Skip(30).ToList();

        Assert.True(Run(0, targetVelocity: 120).Min() < -20);
        Assert.All(compensated, e => Assert.InRange(e, 0, 12));
        Assert.True(compensated.Max() - compensated.Min() <= 3);
    }

    [Fact]
    public void Zero_compensation_is_the_plain_controller()
    {
        var a = new List<int>();
        var b = new List<int>();
        var plain = new AimController((dx, _) => a.Add(dx));
        var zero = new AimController((dx, _) => b.Add(dx), () => 0);
        for (int i = 0; i < 5; i++)
        {
            plain.MoveTo(400, 320, Box, 1.0 / 60, 0.5, 0, 1000, 1);
            zero.MoveTo(400, 320, Box, 1.0 / 60, 0.5, 0, 1000, 1, latencyCompensationMs: 0);
        }

        Assert.Equal(a, b);
    }
}
