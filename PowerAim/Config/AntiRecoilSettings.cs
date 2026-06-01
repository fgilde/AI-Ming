using System.Collections.ObjectModel;
using Nextended.Core;
using Nextended.Core.Extensions;

namespace PowerAim.Config;

public class AntiRecoilSettings : BaseSettings
{
    /// <summary>
    ///     The set of named profiles. Exactly one (or none) is "active" at any time — see
    ///     <see cref="ActiveProfileId"/>. Replaces the old monolithic per-mode settings (HoldTime,
    ///     X/Y Recoil, AutoStrength, PatternName, …) — those still exist as fields for backward
    ///     compatibility, are read once during <see cref="MigrateLegacyIfNeeded"/>, and are then
    ///     left alone.
    /// </summary>
    public ObservableCollection<AntiRecoilProfile> Profiles
    {
        get;
        set => SetField(ref field, value);
    } = new();

    /// <summary>
    ///     Id of the currently active <see cref="AntiRecoilProfile"/> (radio activation). Empty
    ///     string = nothing active; the engine actions become no-ops. Mutated by:
    ///     <list type="bullet">
    ///       <item>The keybind manager when the user presses a profile's hotkey.</item>
    ///       <item>The OCR watcher when a configured weapon-name substring matches.</item>
    ///       <item>The UI profile-list when the user manually activates one.</item>
    ///     </list>
    /// </summary>
    public string ActiveProfileId
    {
        get;
        set => SetField(ref field, value ?? "");
    } = "";

    /// <summary>
    ///     Migration flag. <c>0</c> = original schema (legacy fields populated, no Profiles).
    ///     <c>1</c> = profiles seeded from legacy fields. Bumped automatically by
    ///     <see cref="MigrateLegacyIfNeeded"/> on first load.
    /// </summary>
    public int SchemaVersion
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>
    ///     Seeds a single profile from the legacy settings the very first time this config is
    ///     loaded under the new schema. Idempotent — does nothing once <see cref="SchemaVersion"/>
    ///     reaches <c>1</c>. Called from <c>AppConfig</c> after deserialization.
    /// </summary>
    public void MigrateLegacyIfNeeded()
    {
        if (SchemaVersion >= 1) return;
        if (Profiles.Count > 0) { SchemaVersion = 1; return; }

        // Pick the mode that was effectively active in the legacy config so the user's behaviour
        // doesn't change after the migration.
        AntiRecoilMode mode;
        if (UsePatternRecoil && !string.IsNullOrEmpty(ActivePatternName)) mode = AntiRecoilMode.PatternPlayback;
        else if (UseImageBasedAntiRecoil) mode = AntiRecoilMode.ImageBased;
        else mode = AntiRecoilMode.Legacy;

        var seeded = new AntiRecoilProfile
        {
            Id = Guid.NewGuid().ToFormattedId(),
            Name = mode switch
            {
                AntiRecoilMode.PatternPlayback => string.IsNullOrEmpty(ActivePatternName) ? "Default" : ActivePatternName,
                AntiRecoilMode.ImageBased      => "Image-based",
                _                              => "Default",
            },
            Mode = mode,
            HoldTime = HoldTime,
            FireRate = FireRate,
            YRecoil = YRecoil,
            XRecoil = XRecoil,
            AutoStrength = AutoStrength,
            PatternName = ActivePatternName ?? "",
            PatternStrength = PatternStrength,
        };
        Profiles.Add(seeded);
        ActiveProfileId = seeded.Id;
        SchemaVersion = 1;
    }

    /// <summary>Returns the currently-active profile, or <c>null</c> when none is active.</summary>
    public AntiRecoilProfile? ActiveProfile
    {
        get
        {
            if (string.IsNullOrEmpty(ActiveProfileId)) return null;
            foreach (var p in Profiles)
                if (p.Id == ActiveProfileId) return p;
            return null;
        }
    }


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
