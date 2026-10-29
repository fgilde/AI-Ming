using PowerAim.AI.Aiming;
using PowerAim.Input.Gamepad;

namespace PowerAim.App.Tests;

/// <summary>Aim engine V2 driving a gamepad stick: deflection sets the view's turn speed.</summary>
public class StickAimTests
{
    private const double FrameMs = 1000.0 / 60;

    /// <summary>
    ///     A game turning the view at <paramref name="turnPxPerMs"/> at full deflection, with its own stick
    ///     deadzone and response curve; the analysed frame shows the world <paramref name="latencyMs"/> late.
    /// </summary>
    private sealed class Game(double turnPxPerMs, double deadzone, StickResponseCurve curve, double latencyMs,
        Func<double, double> targetX)
    {
        private readonly List<(double T, double Vx)> _segments = [(0, 0)];
        public double Now;
        public readonly List<double> TrueError = [];

        private double Cam(double t)
        {
            double x = 0;
            for (int i = 0; i < _segments.Count; i++)
            {
                double start = _segments[i].T, end = i + 1 < _segments.Count ? _segments[i + 1].T : double.MaxValue;
                if (t <= start) break;
                x += _segments[i].Vx * (Math.Min(t, end) - start);
            }
            return x;
        }

        /// <summary>The pad output: stick X in −1..1 (Y unused here).</summary>
        public void Stick(double sx)
        {
            double d = Math.Abs(sx);
            double speed = d <= deadzone ? 0 : turnPxPerMs * StickOutputShaper.Curve(Math.Min(1, (d - deadzone) / (1 - deadzone)), curve);
            _segments.Add((Now, Math.Sign(sx) * speed));
        }

        public void Run(AimControllerV2 c, double seconds, StickShapeOptions options, double assumedTurnPxPerMs, double configuredLatency)
        {
            var model = new StickModel(assumedTurnPxPerMs, options, (fx, fy) =>
            {
                var (sx, _) = StickOutputShaper.ShapeAi(fx, -fy, options);
                Stick(sx);
            });
            int frames = (int)(seconds * 1000 / FrameMs);
            for (int i = 0; i < frames; i++)
            {
                double tc = Now - latencyMs;
                c.Step(targetX(tc) - Cam(tc), 0, boxWidthPx: 4, pixelsPerCount: 1, latencyMs: configuredLatency, stick: model);
                Now += FrameMs;
                TrueError.Add(targetX(Now) - Cam(Now));
            }
        }
    }

    private static AimControllerV2 Controller(Game g) => new((_, _) => throw new InvalidOperationException("mouse path used"), () => g.Now);

    [Fact]
    public void Converges_on_a_static_target_with_latency()
    {
        var game = new Game(3.0, 0, StickResponseCurve.Linear, 50, _ => 120);
        game.Run(Controller(game), 3, StickShapeOptions.Default, 3.0, 50);

        Assert.True(game.TrueError.Min() > -12, $"overshoot {game.TrueError.Min():F1}");
        Assert.All(game.TrueError.TakeLast(60), e => Assert.InRange(e, -2, 2));
    }

    [Fact]
    public void A_game_deadzone_and_curve_are_compensated_when_configured()
    {
        var options = StickShapeOptions.Default with { GameDeadzonePct = 8, Curve = StickResponseCurve.Exponential };
        var game = new Game(3.0, 0.10, StickResponseCurve.Exponential, 50, _ => 120); // +2 % margin = the game's 10 %
        game.Run(Controller(game), 3, options, 3.0, 50);

        Assert.True(game.TrueError.Min() > -12, $"overshoot {game.TrueError.Min():F1}");
        Assert.All(game.TrueError.TakeLast(60), e => Assert.InRange(e, -2, 2));
    }

    [Fact]
    public void Learns_a_wrong_turn_speed()
    {
        var rng = new Random(3);
        var spots = Enumerable.Range(0, 100).Select(_ => rng.Next(-150, 151) * 1.0).ToArray();
        var game = new Game(4.8, 0, StickResponseCurve.Linear, 50, t => t < 0 ? spots[0] : spots[(int)(t / 400) % spots.Length]);
        var c = Controller(game);
        game.Run(c, 4, StickShapeOptions.Default, 3.0, 50); // configured 3 px/ms, real 4.8 → ×1.6

        Assert.InRange(c.GainCorrection, 1.6 * 0.85, 1.6 * 1.15);
    }

    [Fact]
    public void A_correct_turn_speed_is_kept()
    {
        var rng = new Random(3);
        var spots = Enumerable.Range(0, 100).Select(_ => rng.Next(-150, 151) * 1.0).ToArray();
        var game = new Game(3.0, 0, StickResponseCurve.Linear, 50, t => t < 0 ? spots[0] : spots[(int)(t / 400) % spots.Length]);
        var c = Controller(game);
        game.Run(c, 4, StickShapeOptions.Default, 3.0, 50);

        Assert.InRange(c.GainCorrection, 0.9, 1.1);
    }

    [Fact]
    public void On_target_the_stick_returns_to_neutral()
    {
        var game = new Game(3.0, 0, StickResponseCurve.Linear, 50, _ => 60);
        double last = double.NaN;
        var model = new StickModel(3.0, StickShapeOptions.Default, (fx, _) => last = fx);
        var c = Controller(game);
        game.Run(c, 2, StickShapeOptions.Default, 3.0, 50);
        c.Step(0, 0, 4, 1, 50, model);

        Assert.Equal(0, last);
    }
}
