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

    public void Reset()
    {
        _accumX = 0;
        _accumY = 0;
    }

    /// <param name="targetX">Target X in capture-box pixels (the box is square + centred on the crosshair).</param>
    /// <param name="targetY">Target Y in capture-box pixels.</param>
    /// <param name="area">The (square) capture box; its centre is the crosshair.</param>
    /// <param name="dt">Seconds since the previous move.</param>
    /// <param name="strength">Approach fraction per 60 Hz frame (0..1). Higher = snappier. &lt;1 never overshoots.</param>
    /// <param name="deadzonePx">Skip output when the crosshair is within this radius (pixels) of the target.</param>
    /// <param name="maxStepCounts">Clamp per-frame movement to ± this many mouse counts.</param>
    /// <param name="pixelsPerCount">
    ///     Calibration ratio (screen pixels per mouse count). &gt; 0 → convert the pixel error into exact
    ///     mouse counts (game-independent). 0 → treat 1 pixel ≈ 1 count (the strength slider then
    ///     absorbs the game's sensitivity).
    /// </param>
    /// <param name="speedMultiplier">
    ///     Extra gain on the per-frame move (issue #10). 1 = normal. &gt; 1 pushes past "full correction
    ///     per frame" for setups where even max strength is too slow (high-DPI mouse + low in-game
    ///     sensitivity, so a count barely moves the view). Intentionally allows overshoot in raw counts.
    /// </param>
    public void MoveTo(double targetX, double targetY, Rectangle area, double dt,
        double strength, double deadzonePx, double maxStepCounts, double pixelsPerCount, double speedMultiplier = 1.0)
    {
        if (dt <= 0) dt = 1.0 / 60.0;

        double centerX = area.Width / 2.0;
        double centerY = area.Height / 2.0;

        // Error to the target in capture-box pixels (square box → no X/Y aspect skew).
        double errPxX = targetX - centerX;
        double errPxY = targetY - centerY;

        double distPx = Math.Sqrt(errPxX * errPxX + errPxY * errPxY);
        if (distPx <= deadzonePx)
        {
            // On target — stop nudging. Drop any tiny accumulated remainder so it can't drift.
            _accumX = 0;
            _accumY = 0;
            return;
        }

        // Pixel error → mouse counts. Calibrated → exact; otherwise 1:1 (legacy behaviour).
        double cntX = pixelsPerCount > 0 ? errPxX / pixelsPerCount : errPxX;
        double cntY = pixelsPerCount > 0 ? errPxY / pixelsPerCount : errPxY;

        // Frame-rate-independent proportional approach: cover `strength` of the remaining distance per
        // 60 Hz frame. With strength < 1 this never overshoots — the closed loop converges smoothly.
        double s = Math.Clamp(strength, 0.0001, 1.0);
        double gain = 1.0 - Math.Pow(1.0 - s, dt * 60.0);

        // Issue #10: optional extra gain for setups where even gain==1 (full correction) is too slow.
        if (speedMultiplier > 0 && speedMultiplier != 1.0) gain *= speedMultiplier;

        double moveX = cntX * gain + _accumX;
        double moveY = cntY * gain + _accumY;

        int outX = (int)Math.Round(moveX);
        int outY = (int)Math.Round(moveY);
        // Carry the sub-pixel remainder into the next frame (kills integer-rounding micro-jitter).
        _accumX = moveX - outX;
        _accumY = moveY - outY;

        outX = Math.Clamp(outX, (int)-maxStepCounts, (int)maxStepCounts);
        outY = Math.Clamp(outY, (int)-maxStepCounts, (int)maxStepCounts);

        if (outX == 0 && outY == 0) return;
        InputSender.Move(outX, outY);
    }
}
