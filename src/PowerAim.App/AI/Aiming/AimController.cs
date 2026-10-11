using System.Drawing;
using PowerAim.Input;

namespace PowerAim.AI.Aiming;

/// <summary>
/// Proportional aim controller — snappy real-time follow.
/// Changes vs stock:
/// - Default max step 400 (was 150) so large errors close in one/two frames
/// - At high strength (≥0.85) uses near-linear full correction (robotic)
/// - Accumulator still kills sub-pixel jitter
/// </summary>
public sealed class AimController
{
    private double _accumX;
    private double _accumY;

    public void Reset()
    {
        _accumX = 0;
        _accumY = 0;
    }

    public void MoveTo(
        double targetX,
        double targetY,
        Rectangle area,
        double dt,
        double strength,
        double deadzonePx,
        double maxStepCounts,
        double pixelsPerCount,
        double speedMultiplier = 1.0)
    {
        if (dt <= 0) dt = 1.0 / 60.0;

        double centerX = area.Width / 2.0;
        double centerY = area.Height / 2.0;

        double errPxX = targetX - centerX;
        double errPxY = targetY - centerY;

        double distPx = Math.Sqrt(errPxX * errPxX + errPxY * errPxY);
        if (distPx <= deadzonePx)
        {
            _accumX = 0;
            _accumY = 0;
            return;
        }

        double cntX = pixelsPerCount > 0 ? errPxX / pixelsPerCount : errPxX;
        double cntY = pixelsPerCount > 0 ? errPxY / pixelsPerCount : errPxY;

        double s = Math.Clamp(strength, 0.0001, 1.0);

        // High strength → direct (near-instant) correction; low strength → exponential approach.
        double gain;
        if (s >= 0.85)
        {
            gain = Math.Clamp(s * Math.Min(dt * 60.0, 1.0), 0.5, 1.0);
        }
        else
        {
            gain = 1.0 - Math.Pow(1.0 - s, dt * 60.0);
        }

        if (speedMultiplier > 0 && speedMultiplier != 1.0)
            gain *= speedMultiplier;

        double moveX = cntX * gain + _accumX;
        double moveY = cntY * gain + _accumY;

        int outX = (int)Math.Round(moveX);
        int outY = (int)Math.Round(moveY);
        _accumX = moveX - outX;
        _accumY = moveY - outY;

        // Caller often passes 150; floor at 400 so long flicks aren't chopped every frame.
        double stepCap = Math.Max(maxStepCounts, 400);
        outX = Math.Clamp(outX, (int)-stepCap, (int)stepCap);
        outY = Math.Clamp(outY, (int)-stepCap, (int)stepCap);

        if (outX == 0 && outY == 0) return;
        InputSender.Move(outX, outY);
    }
}