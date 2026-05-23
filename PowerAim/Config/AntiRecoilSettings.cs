namespace PowerAim.Config;

public class AntiRecoilSettings : BaseSettings
{
    private int _holdTime = 10;
    private int _fireRate = 200;
    private int _yRecoil = 10;
    private int _xRecoil = 0;
    private double _autoStrength = 0.85;
    private bool _useImageBasedAntiRecoil = false;

    public int HoldTime
    {
        get => _holdTime;
        set => SetField(ref _holdTime, value);
    }

    public int FireRate
    {
        get => _fireRate;
        set => SetField(ref _fireRate, value);
    }

    /// <summary>Legacy manual Y-recoil compensation. Unused by the new auto-detect mode.</summary>
    public int YRecoil
    {
        get => _yRecoil;
        set => SetField(ref _yRecoil, value);
    }

    /// <summary>Legacy manual X-recoil compensation. Unused by the new auto-detect mode.</summary>
    public int XRecoil
    {
        get => _xRecoil;
        set => SetField(ref _xRecoil, value);
    }

    /// <summary>
    ///     Strength of the experimental image-based anti-recoil. 0 = off, 1.0 = full
    ///     compensation, 0.85 = natural feel. Range [0, 1.5]. Only used when
    ///     <see cref="UseImageBasedAntiRecoil"/> is enabled.
    /// </summary>
    public double AutoStrength
    {
        get => _autoStrength;
        set => SetField(ref _autoStrength, value);
    }

    /// <summary>
    ///     <b>BETA.</b> Switches the engine from the legacy pattern-based anti-recoil (manual
    ///     X/Y recoil sliders, fire-rate timing) to the experimental image-based path
    ///     (phase-correlation + EMA-baseline). When this is on, the legacy sliders have no
    ///     effect and only <see cref="AutoStrength"/> applies.
    /// </summary>
    public bool UseImageBasedAntiRecoil
    {
        get => _useImageBasedAntiRecoil;
        set => SetField(ref _useImageBasedAntiRecoil, value);
    }
}