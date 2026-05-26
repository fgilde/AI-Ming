using System.Collections.ObjectModel;

namespace PowerAim.Config;

public class AntiRecoilSettings : BaseSettings
{
    public int HoldTime
    {
        get;
        set => SetField(ref field, value);
    } = 10;

    public int FireRate
    {
        get;
        set => SetField(ref field, value);
    } = 200;

    /// <summary>Legacy manual Y-recoil compensation. Unused by the new auto-detect mode.</summary>
    public int YRecoil
    {
        get;
        set => SetField(ref field, value);
    } = 10;

    /// <summary>Legacy manual X-recoil compensation. Unused by the new auto-detect mode.</summary>
    public int XRecoil
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>
    ///     Strength of the experimental image-based anti-recoil. 0 = off, 1.0 = full
    ///     compensation, 0.85 = natural feel. Range [0, 1.5]. Only used when
    ///     <see cref="UseImageBasedAntiRecoil"/> is enabled.
    /// </summary>
    public double AutoStrength
    {
        get;
        set => SetField(ref field, value);
    } = 0.85;

    /// <summary>
    ///     <b>BETA.</b> Switches the engine from the legacy pattern-based anti-recoil (manual
    ///     X/Y recoil sliders, fire-rate timing) to the experimental image-based path
    ///     (phase-correlation + EMA-baseline). When this is on, the legacy sliders have no
    ///     effect and only <see cref="AutoStrength"/> applies.
    /// </summary>
    public bool UseImageBasedAntiRecoil
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>
    ///     Library of recorded recoil patterns. Names should be unique — playback resolves by
    ///     <see cref="ActivePatternName"/>.
    /// </summary>
    public ObservableCollection<RecoilPattern> Patterns
    {
        get;
        set => SetField(ref field, value);
    } = new();

    /// <summary>
    ///     Name of the pattern that <c>RecoilPatternPlaybackAction</c> should replay while the user
    ///     fires. Empty string disables pattern playback.
    /// </summary>
    public string ActivePatternName
    {
        get;
        set => SetField(ref field, value);
    } = "";

    /// <summary>
    ///     Scale factor applied to each playback sample. <c>1.0</c> = exact recorded compensation,
    ///     lower values dampen the correction, higher values amplify it. Useful when sharing
    ///     patterns between players with different in-game sensitivities.
    /// </summary>
    public double PatternStrength
    {
        get;
        set => SetField(ref field, value);
    } = 1.0;

    /// <summary>
    ///     Master switch for pattern-based recoil playback. Independent of
    ///     <see cref="UseImageBasedAntiRecoil"/> — when both are enabled, pattern playback wins.
    /// </summary>
    public bool UsePatternRecoil
    {
        get;
        set => SetField(ref field, value);
    }
}
