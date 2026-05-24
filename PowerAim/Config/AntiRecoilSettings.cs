using System.Collections.ObjectModel;

namespace PowerAim.Config;

public class AntiRecoilSettings : BaseSettings
{
    private int _holdTime = 10;
    private int _fireRate = 200;
    private int _yRecoil = 10;
    private int _xRecoil = 0;
    private double _autoStrength = 0.85;
    private bool _useImageBasedAntiRecoil = false;
    private ObservableCollection<RecoilPattern> _patterns = new();
    private string _activePatternName = "";
    private double _patternStrength = 1.0;
    private bool _usePatternRecoil = false;

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

    /// <summary>
    ///     Library of recorded recoil patterns. Names should be unique — playback resolves by
    ///     <see cref="ActivePatternName"/>.
    /// </summary>
    public ObservableCollection<RecoilPattern> Patterns
    {
        get => _patterns;
        set => SetField(ref _patterns, value);
    }

    /// <summary>
    ///     Name of the pattern that <c>RecoilPatternPlaybackAction</c> should replay while the user
    ///     fires. Empty string disables pattern playback.
    /// </summary>
    public string ActivePatternName
    {
        get => _activePatternName;
        set => SetField(ref _activePatternName, value);
    }

    /// <summary>
    ///     Scale factor applied to each playback sample. <c>1.0</c> = exact recorded compensation,
    ///     lower values dampen the correction, higher values amplify it. Useful when sharing
    ///     patterns between players with different in-game sensitivities.
    /// </summary>
    public double PatternStrength
    {
        get => _patternStrength;
        set => SetField(ref _patternStrength, value);
    }

    /// <summary>
    ///     Master switch for pattern-based recoil playback. Independent of
    ///     <see cref="UseImageBasedAntiRecoil"/> — when both are enabled, pattern playback wins.
    /// </summary>
    public bool UsePatternRecoil
    {
        get => _usePatternRecoil;
        set => SetField(ref _usePatternRecoil, value);
    }
}