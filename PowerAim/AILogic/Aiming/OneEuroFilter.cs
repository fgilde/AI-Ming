namespace PowerAim.AILogic.Aiming;

/// <summary>
///     The 1€ (One-Euro) filter — an adaptive low-pass for noisy interactive signals. It trades
///     jitter for lag based on signal speed: heavy smoothing when the value is slow-moving (kills
///     standstill jitter) and light smoothing during fast motion (keeps flicks responsive). This is
///     the right smoother for an aim cursor — a fixed-weight EMA can only pick one point on the
///     jitter↔lag curve, the 1€ filter moves along it automatically.
///     <para>Reference: Casiez, Roussel &amp; Vogel — https://gery.casiez.net/1euro/.</para>
/// </summary>
public sealed class OneEuroFilter
{
    private double _minCutoff;
    private double _beta;
    private readonly double _dCutoff;

    private double _xPrev;
    private double _dxPrev;
    private bool _hasPrev;

    /// <param name="minCutoff">Lowest cutoff frequency (Hz). Lower = more smoothing at rest. ~1.0.</param>
    /// <param name="beta">Speed coefficient. Higher = less lag during fast motion. ~0.7.</param>
    /// <param name="dCutoff">Cutoff for the derivative estimate. 1.0 is fine for most uses.</param>
    public OneEuroFilter(double minCutoff = 1.0, double beta = 0.7, double dCutoff = 1.0)
    {
        _minCutoff = minCutoff;
        _beta = beta;
        _dCutoff = dCutoff;
    }

    /// <summary>Live-update the tunables (so config slider changes take effect without a re-alloc).</summary>
    public void Configure(double minCutoff, double beta)
    {
        _minCutoff = minCutoff;
        _beta = beta;
    }

    public void Reset() => _hasPrev = false;

    /// <summary>Filter one sample. <paramref name="dt"/> is the time since the last sample in seconds.</summary>
    public double Filter(double x, double dt)
    {
        if (dt <= 0) dt = 1.0 / 60.0;
        if (!_hasPrev)
        {
            _xPrev = x;
            _dxPrev = 0;
            _hasPrev = true;
            return x;
        }

        double dx = (x - _xPrev) / dt;
        double aD = Alpha(_dCutoff, dt);
        double dxHat = aD * dx + (1 - aD) * _dxPrev;

        double cutoff = _minCutoff + _beta * System.Math.Abs(dxHat);
        double a = Alpha(cutoff, dt);
        double xHat = a * x + (1 - a) * _xPrev;

        _xPrev = xHat;
        _dxPrev = dxHat;
        return xHat;
    }

    private static double Alpha(double cutoff, double dt)
    {
        double tau = 1.0 / (2.0 * System.Math.PI * cutoff);
        return 1.0 / (1.0 + tau / dt);
    }
}

/// <summary>Convenience pair of <see cref="OneEuroFilter"/>s for a 2-D point (X and Y independent).</summary>
public sealed class OneEuroFilter2D
{
    private readonly OneEuroFilter _x;
    private readonly OneEuroFilter _y;

    public OneEuroFilter2D(double minCutoff = 1.0, double beta = 0.7)
    {
        _x = new OneEuroFilter(minCutoff, beta);
        _y = new OneEuroFilter(minCutoff, beta);
    }

    public void Configure(double minCutoff, double beta)
    {
        _x.Configure(minCutoff, beta);
        _y.Configure(minCutoff, beta);
    }

    public void Reset()
    {
        _x.Reset();
        _y.Reset();
    }

    public (double X, double Y) Filter(double x, double y, double dt) => (_x.Filter(x, dt), _y.Filter(y, dt));
}
