namespace PowerAim.AI.Aiming;

/// <summary>
/// Adaptive dual-axis PID on aim-point position.
/// Base gains (Kp/Ki/Kd) are scaled each frame from the locked target's
/// overlay features: box size, confidence, and measured aim-point speed.
/// Far / fast / low-confidence → stronger P, weaker D; close / slow / high-confidence → softer P, stronger D.
/// </summary>
public sealed class PidAimSmoother
{
    private double _prevX, _prevY;
    private double _ix, _iy;
    private bool _has;
    private double _lastSpeed;

    public double Kp { get; set; } = 0.55;
    public double Ki { get; set; } = 0.05;
    public double Kd { get; set; } = 0.12;
    public double IntegralLimit { get; set; } = 80.0;
    public double OutputLimit { get; set; } = 400.0;

    /// <summary>When true (default), gains auto-adapt from target box / confidence / speed.</summary>
    public bool Adaptive { get; set; } = true;

    /// <summary>Last effective gains (for debug / overlay readout).</summary>
    public double LastKp { get; private set; }
    public double LastKi { get; private set; }
    public double LastKd { get; private set; }
    public double LastSpeed => _lastSpeed;

    public void Configure(double kp, double ki, double kd)
    {
        Kp = kp;
        Ki = ki;
        Kd = kd;
    }

    public void Reset()
    {
        _has = false;
        _ix = _iy = 0;
        _prevX = _prevY = 0;
        _lastSpeed = 0;
        LastKp = Kp;
        LastKi = Ki;
        LastKd = Kd;
    }

    /// <summary>Legacy entry — no target context; uses base gains only (still optional adaptive off).</summary>
    public (double x, double y) Filter(double targetX, double targetY, double dt)
        => Filter(targetX, targetY, dt, confidence: 1.0, boxWidth: 40, boxHeight: 40, fovSize: 640);

    /// <summary>
    /// Adaptive filter. <paramref name="confidence"/> is 0..1 from the detector;
    /// box size and FOV size put the target in the same space as the ESP overlay box.
    /// </summary>
    public (double x, double y) Filter(
        double targetX,
        double targetY,
        double dt,
        double confidence,
        double boxWidth,
        double boxHeight,
        double fovSize)
    {
        if (dt <= 1e-6) dt = 1.0 / 60.0;
        if (!_has)
        {
            _prevX = targetX;
            _prevY = targetY;
            _ix = _iy = 0;
            _has = true;
            _lastSpeed = 0;
            LastKp = Kp; LastKi = Ki; LastKd = Kd;
            return (targetX, targetY);
        }

        double ex = targetX - _prevX;
        double ey = targetY - _prevY;
        double errMag = System.Math.Sqrt(ex * ex + ey * ey);
        _lastSpeed = errMag / dt; // aim-point speed in units/sec

        double kp = Kp, ki = Ki, kd = Kd;
        if (Adaptive)
            (kp, ki, kd) = AdaptGains(errMag, _lastSpeed, confidence, boxWidth, boxHeight, fovSize);

        LastKp = kp; LastKi = ki; LastKd = kd;

        _ix = System.Math.Clamp(_ix + ex * dt, -IntegralLimit, IntegralLimit);
        _iy = System.Math.Clamp(_iy + ey * dt, -IntegralLimit, IntegralLimit);

        double dx = kp * ex + ki * _ix + kd * (ex / dt);
        double dy = kp * ey + ki * _iy + kd * (ey / dt);

        double frameScale = System.Math.Clamp(dt * 60.0, 0.25, 2.0);
        dx *= frameScale;
        dy *= frameScale;

        dx = System.Math.Clamp(dx, -OutputLimit, OutputLimit);
        dy = System.Math.Clamp(dy, -OutputLimit, OutputLimit);

        _prevX += dx;
        _prevY += dy;
        return (_prevX, _prevY);
    }

    /// <summary>
    /// Map overlay/target features → gain multipliers.
    /// Large error or fast motion → more P, less D (catch up).
    /// Small error, high confidence, large box (close) → less P, more D (settle, less jitter).
    /// </summary>
    private (double kp, double ki, double kd) AdaptGains(
        double errMag,
        double speed,
        double confidence,
        double boxW,
        double boxH,
        double fovSize)
    {
        double fov = System.Math.Max(64.0, fovSize);
        // Normalized distance of this frame's correction request (0 = on target, 1+ = far).
        double distN = System.Math.Clamp(errMag / (fov * 0.35), 0.0, 1.5);
        // Box fill of FOV — larger ≈ closer target in typical aim FOV crops.
        double boxN = System.Math.Clamp(System.Math.Max(boxW, boxH) / fov, 0.02, 0.8);
        double conf = System.Math.Clamp(confidence, 0.05, 1.0);
        // Speed relative to FOV per second.
        double speedN = System.Math.Clamp(speed / (fov * 2.5), 0.0, 1.5);

        // P: boost when far or fast; ease when close + confident.
        double kpMul = 0.75 + 0.55 * distN + 0.35 * speedN - 0.20 * conf * (1.0 - distN);
        // I: only meaningful near the target with stable detection; cut when far/fast.
        double kiMul = (0.25 + 0.90 * conf) * (1.0 - 0.70 * System.Math.Min(distN, 1.0)) * (1.0 - 0.50 * System.Math.Min(speedN, 1.0));
        // D: stronger when close (damp jitter/overshoot); weaker when flying across FOV.
        double kdMul = 0.55 + 0.70 * (1.0 - System.Math.Min(distN, 1.0)) * conf + 0.25 * boxN - 0.35 * System.Math.Min(speedN, 1.0);

        kpMul = System.Math.Clamp(kpMul, 0.35, 1.85);
        kiMul = System.Math.Clamp(kiMul, 0.0, 1.25);
        kdMul = System.Math.Clamp(kdMul, 0.25, 1.75);

        return (Kp * kpMul, Ki * kiMul, Kd * kdMul);
    }
}
