using System.Drawing;
using PowerAim.AI.Aiming;

namespace PowerAim.App.Tests;

public class AimControllerTests
{
    private static readonly Rectangle Box = new(0, 0, 640, 640);
    private const double Center = 320;

    /// <summary>Closed loop: each move shrinks the remaining error. Returns the error left after 1/60 s.</summary>
    private static double ErrorAfterOneSixtiethSecond(double strength, int fps)
    {
        double error = 100;
        var controller = new AimController((dx, _) => error -= dx);
        for (int frame = 0; frame < fps / 60; frame++)
            controller.MoveTo(Center + error, Center, Box, 1.0 / fps, strength, deadzonePx: 0, maxStepCounts: 1000, pixelsPerCount: 1);
        return error;
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void Approach_speed_does_not_depend_on_the_frame_rate(double strength)
    {
        // `strength` of the distance per 60 Hz frame, however many inference frames that takes.
        double expected = 100 * (1 - strength);

        Assert.Equal(expected, ErrorAfterOneSixtiethSecond(strength, 60), precision: 0);
        Assert.Equal(expected, ErrorAfterOneSixtiethSecond(strength, 120), precision: 0);
        Assert.Equal(expected, ErrorAfterOneSixtiethSecond(strength, 240), precision: 0);
    }

    [Fact]
    public void Single_move_is_capped()
    {
        var moves = new List<int>();
        var controller = new AimController((dx, _) => moves.Add(dx));

        controller.MoveTo(Center + 600, Center, Box, 1.0 / 60, 1.0, deadzonePx: 0, maxStepCounts: 150, pixelsPerCount: 1);

        Assert.Equal([150], moves);
    }

    [Fact]
    public void Nothing_moves_inside_the_deadzone()
    {
        var moved = false;
        var controller = new AimController((_, _) => moved = true);

        controller.MoveTo(Center + 2, Center, Box, 1.0 / 60, 1.0, deadzonePx: 3, maxStepCounts: 150, pixelsPerCount: 1);

        Assert.False(moved);
    }
}
