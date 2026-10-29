using PowerAim.AI.Aiming;

namespace PowerAim.App.Tests;

public class AimControllerV2Tests
{
    private const double FrameMs = 1000.0 / 60;

    /// <summary>
    ///     Closed loop with a game model: every count turns the view by <paramref name="realGain"/> px at
    ///     once, but the analysed frame shows the world <paramref name="trueLatencyMs"/> late.
    /// </summary>
    private sealed class Sim(double realGain, double trueLatencyMs, Func<double, (double X, double Y)> target)
    {
        private readonly List<(double T, double X, double Y)> _moves = [];
        public double Now;
        public readonly List<double> TrueErrorX = [];

        private (double X, double Y) Cam(double t)
        {
            double x = 0, y = 0;
            foreach (var m in _moves) if (m.T <= t) { x += m.X; y += m.Y; }
            return (x, y);
        }

        public AimControllerV2 Controller() =>
            new((dx, dy) => _moves.Add((Now, dx * realGain, dy * realGain)), () => Now);

        public void Run(AimControllerV2 c, double seconds, double configuredLatencyMs)
        {
            int frames = (int)(seconds * 1000 / FrameMs);
            for (int i = 0; i < frames; i++)
            {
                double tc = Now - trueLatencyMs;
                var cam = Cam(tc);
                var tgt = target(tc);
                c.Step(tgt.X - cam.X, tgt.Y - cam.Y, boxWidthPx: 4, pixelsPerCount: 1, latencyMs: configuredLatencyMs);
                var camNow = Cam(Now);
                TrueErrorX.Add(target(Now).X - camNow.X);
                Now += FrameMs;
            }
        }
    }

    /// <summary>A target that jumps to a new spot every 400 ms — keeps the loop correcting so the model can learn.</summary>
    private static Func<double, (double X, double Y)> Jumping()
    {
        var rng = new Random(7);
        var spots = Enumerable.Range(0, 200).Select(_ => (rng.Next(-120, 121) * 1.0, rng.Next(-60, 61) * 1.0)).ToArray();
        return t => t < 0 ? spots[0] : spots[(int)(t / 400) % spots.Length];
    }

    [Fact]
    public void Converges_on_a_static_target_without_overshoot_despite_three_frames_of_latency()
    {
        var sim = new Sim(1.0, 3 * FrameMs, _ => (100, 0));
        sim.Run(sim.Controller(), 2, configuredLatencyMs: 3 * FrameMs);

        Assert.True(sim.TrueErrorX.Min() > -10, $"overshoot {sim.TrueErrorX.Min():F1}");
        Assert.All(sim.TrueErrorX.TakeLast(60), e => Assert.InRange(e, -1, 1));
    }

    [Fact]
    public void Stays_still_once_on_target()
    {
        var sim = new Sim(1.0, 3 * FrameMs, _ => (100, 0));
        var c = sim.Controller();
        sim.Run(c, 1, 3 * FrameMs);
        int before = sim.TrueErrorX.Count;
        sim.Run(c, 1, 3 * FrameMs);

        var settled = sim.TrueErrorX.Skip(before).ToList();
        Assert.True(settled.Max() - settled.Min() <= 1, "the aim keeps hunting around the target");
    }

    [Fact]
    public void Learns_a_wrong_gain_within_two_seconds()
    {
        var sim = new Sim(1.8, 3 * FrameMs, Jumping());
        var c = sim.Controller();
        sim.Run(c, 2, 3 * FrameMs);

        Assert.InRange(c.GainCorrection, 1.8 * 0.85, 1.8 * 1.15);
    }

    [Fact]
    public void Moves_its_latency_toward_the_real_one()
    {
        var sim = new Sim(1.0, 80, Jumping());
        var c = sim.Controller();
        sim.Run(c, 10, configuredLatencyMs: 50);

        Assert.InRange(c.LatencyOffsetMs, 14, 40);
    }

    [Fact]
    public void Follows_a_moving_target_closer_than_without_lead()
    {
        // 0.12 px/ms ≈ 120 px/s sideways.
        var sim = new Sim(1.0, 3 * FrameMs, t => (100 + 0.12 * Math.Max(0, t), 0));
        sim.Run(sim.Controller(), 3, 3 * FrameMs);

        // Smith alone keeps a lag of about v·latency (6 px here); the lead takes off a good part of it.
        double lag = sim.TrueErrorX.TakeLast(60).Average(Math.Abs);
        Assert.True(lag < 4, $"lag {lag:F1}");
    }

    [Fact]
    public void A_correct_model_stays_put()
    {
        var sim = new Sim(1.0, 50, Jumping());
        var c = sim.Controller();
        sim.Run(c, 10, configuredLatencyMs: 50);

        Assert.InRange(c.GainCorrection, 0.9, 1.1);
        Assert.InRange(c.LatencyOffsetMs, -17, 17); // within one frame
    }

    [Fact]
    public void Learns_a_too_high_gain_assumption_as_well()
    {
        var sim = new Sim(0.6, 3 * FrameMs, Jumping());
        var c = sim.Controller();
        sim.Run(c, 2, 3 * FrameMs);

        Assert.InRange(c.GainCorrection, 0.6 * 0.85, 0.6 * 1.15);
    }

    [Fact]
    public void Jumping_targets_are_reached_without_swinging_past_them()
    {
        var sim = new Sim(1.0, 50, Jumping());
        sim.Run(sim.Controller(), 4, configuredLatencyMs: 50);

        // In the last 100 ms before each jump the aim rests inside the settle zone (22.5 % of the 4 px aim region, i.e. about a pixel).
        var settled = sim.TrueErrorX.Where((_, i) => i % 24 >= 18).ToList();
        Assert.All(settled, e => Assert.InRange(e, -1.5, 1.5));
    }
}
