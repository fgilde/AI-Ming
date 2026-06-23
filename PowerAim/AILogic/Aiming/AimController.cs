using System.Drawing;
using PowerAim.InputLogic;

namespace PowerAim.AILogic.Aiming;

/// <summary>
///     Turns a target screen position into a per-frame mouse/stick move. A proportional controller
///     with three fixes over the old <c>delta * (1 - sensitivity)</c> lerp:
///     <list type="bullet">
///       <item><b>Frame-rate independent:</b> the approach fraction is normalised to 60 Hz via
///             <c>1 - (1-s)^(dt·60)</c>, so the feel is identical at 60 and 144 fps instead of
///             scaling with the inference rate.</item>
///       <item><b>Sub-pixel accumulator:</b> SendInput is integer-only; dropping the fractional
///             remainder each frame is itself a source of micro-jitter, so we carry it over.</item>
///       <item><b>Deadzone:</b> once the crosshair is essentially on target, output nothing —
///             stops the residual tremor of constantly nudging a target you've already hit.</item>
///     </list>
///     Sensitivity semantics are now intuitive: <b>higher = snappier, lower = smoother</b> (the old
///     formula was inverted and flat at the low end — see GitHub issue #10).
/// </summary>
public sealed class AimController
{
    private double _accumX;
    private double _accumY;
    private readonly Random _rng = new();

    public void Reset()
    {
        _accumX = 0;
        _accumY = 0;
    }

    /// <param name="targetScreenX">Target X in screen pixels (absolute within the capture area).</param>
    /// <param name="targetScreenY">Target Y in screen pixels.</param>
    /// <param name="area">The capture area (its centre is the crosshair).</param>
    /// <param name="dt">Seconds since the previous move.</param>
    /// <param name="sensitivity">Approach fraction per 60 Hz frame (0..1). Higher = snappier.</param>
    /// <param name="deadzonePx">Skip output when the crosshair is within this radius of the target.</param>
    /// <param name="maxStepPx">Clamp per-frame movement to ± this many pixels.</param>
    /// <param name="jitterPx">Optional uniform random jitter added to the move (humanisation).</param>
    public void MoveTo(double targetScreenX, double targetScreenY, Rectangle area, double dt,
        double sensitivity, double deadzonePx, double maxStepPx, double jitterPx)
    {
        if (dt <= 0) dt = 1.0 / 60.0;

        double centerX = area.Width / 2.0;
        double centerY = area.Height / 2.0;
        double aspect = area.Height > 0 ? (double)area.Width / area.Height : 1.0;

        double errX = targetScreenX - centerX;
        double errY = (targetScreenY - centerY) * aspect;

        double dist = Math.Sqrt(errX * errX + errY * errY);
        if (dist <= deadzonePx)
        {
            // On target — stop nudging. Drop any tiny accumulated remainder so it can't drift.
            _accumX = 0;
            _accumY = 0;
            return;
        }

        // Frame-rate-independent proportional approach.
        double s = Math.Clamp(sensitivity, 0.0001, 1.0);
        double gain = 1.0 - Math.Pow(1.0 - s, dt * 60.0);

        double moveX = errX * gain + _accumX;
        double moveY = errY * gain + _accumY;

        if (jitterPx > 0)
        {
            moveX += _rng.NextDouble() * 2 * jitterPx - jitterPx;
            moveY += _rng.NextDouble() * 2 * jitterPx - jitterPx;
        }

        int outX = (int)Math.Round(moveX);
        int outY = (int)Math.Round(moveY);
        // Carry the sub-pixel remainder into the next frame.
        _accumX = moveX - outX;
        _accumY = moveY - outY;

        outX = Math.Clamp(outX, (int)-maxStepPx, (int)maxStepPx);
        outY = Math.Clamp(outY, (int)-maxStepPx, (int)maxStepPx);

        if (outX == 0 && outY == 0) return;
        InputSender.Move(outX, outY);
    }
}
