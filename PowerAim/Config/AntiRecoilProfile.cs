using System.ComponentModel;
using PowerAim.InputLogic;
using Nextended.Core;
using Nextended.Core.Extensions;

namespace PowerAim.Config;

/// <summary>
///     Engine variant for an <see cref="AntiRecoilProfile"/>. Mirrors the three old
///     <c>AntiRecoilSettings</c> code paths so the playback / action classes can dispatch on a single
///     enum instead of three independent bools.
/// </summary>
public enum AntiRecoilMode
{
    /// <summary>Fixed per-tick X/Y compensation (the original sliders).</summary>
    Legacy = 0,
    /// <summary>Experimental phase-correlation + EMA-baseline image-based mode.</summary>
    ImageBased = 1,
    /// <summary>Recorded recoil-pattern playback (looked up by name in the shared library).</summary>
    PatternPlayback = 2,
}

/// <summary>
///     A named anti-recoil profile. Combines a recoil engine choice + its parameters with optional
///     activation conditions (a keybind that toggles it active, an OCR weapon-name match that
///     auto-switches to it). Only ONE profile is ever active at a time — see
///     <see cref="AntiRecoilSettings.ActiveProfileId"/>.
///     <para>
///     Replaces the old monolithic <c>AntiRecoilSettings</c> sliders (HoldTime, FireRate, X/YRecoil,
///     AutoStrength, UsePatternRecoil, ActivePatternName, PatternStrength). Existing config files
///     get migrated to a single seeded profile on first load — see
///     <see cref="AntiRecoilSettings.MigrateLegacyIfNeeded"/>.
///     </para>
/// </summary>
public class AntiRecoilProfile : EditableNotificationObject
{
    private string _matchProcess = "";
    private string _ocrRegionName = "";
    private string _weaponMatch = "";

    public AntiRecoilProfile()
    {
        Id = Guid.NewGuid().ToFormattedId();
        AppConfig.ConfigLoaded += (_, _) => DetectChanges();
        // Important for runtime-added profiles: ConfigLoaded only fires on config Load, so a
        // profile created via "Add" in the UI would never get its sibling-notification
        // subscriptions wired up. Call DetectChanges immediately too — it's idempotent (the
        // -= / += pattern handles re-runs).
        DetectChanges();
    }

    private void DetectChanges()
    {
        if (AppConfig.Current?.ToggleState != null)
        {
            AppConfig.Current.ToggleState.PropertyChanged -= OnToggleStateChange;
            AppConfig.Current.ToggleState.PropertyChanged += OnToggleStateChange;
        }
        if (AppConfig.Current?.AntiRecoilSettings != null)
        {
            AppConfig.Current.AntiRecoilSettings.PropertyChanged -= OnSettingsChange;
            AppConfig.Current.AntiRecoilSettings.PropertyChanged += OnSettingsChange;
        }
    }

    private void OnToggleStateChange(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToggleState.AntiRecoil) or nameof(ToggleState.GlobalActive))
            RaisePropertyChanged(nameof(IsActive));
    }

    private void OnSettingsChange(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AntiRecoilSettings.ActiveProfileId))
        {
            RaisePropertyChanged(nameof(IsActive));
            RaisePropertyChanged(nameof(IsActiveSelfWrite));
        }
    }

    protected override void RaisePropertyChanged(string propertyName)
    {
        if (propertyName is not nameof(Description))
            RaisePropertyChanged(nameof(Description));
        base.RaisePropertyChanged(propertyName);
    }

    /// <summary>Stable identifier for the radio activation. Lives in <see cref="AntiRecoilSettings.ActiveProfileId"/>.</summary>
    public string Id { get; set; }

    /// <summary>Free-text label for the picker / notification ("AK-47", "Vandal", "Sniper").</summary>
    public string Name
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RaisePropertyChanged(nameof(IsValid));
                RaisePropertyChanged(nameof(IsActive));
            }
        }
    }

    /// <summary>Which engine variant this profile drives.</summary>
    public AntiRecoilMode Mode
    {
        get;
        set => SetProperty(ref field, value);
    } = AntiRecoilMode.Legacy;

    /// <summary>
    ///     Optional process-name pattern (wildcard / pipe-separated) that scopes auto-switching to
    ///     specific games. Empty (default) = active in every process. Same semantics as
    ///     <c>ActionTrigger.MatchProcess</c> / <c>AutoPlayProfile.MatchProcess</c>.
    /// </summary>
    public string MatchProcess
    {
        get => _matchProcess;
        set => SetProperty(ref _matchProcess, value ?? "");
    }

    /// <summary>
    ///     Name of an OCR region from <see cref="OcrSettings.Regions"/>. When set together with
    ///     <see cref="WeaponMatch"/> and <see cref="AutoSwitchOnOcr"/>, the profile manager polls
    ///     that region's recognised text and activates this profile when the substring matches.
    /// </summary>
    public string OcrRegionName
    {
        get => _ocrRegionName;
        set => SetProperty(ref _ocrRegionName, value ?? "");
    }

    /// <summary>Substring (case-insensitive) the OCR region's value must contain to auto-activate.</summary>
    public string WeaponMatch
    {
        get => _weaponMatch;
        set => SetProperty(ref _weaponMatch, value ?? "");
    }

    /// <summary>Master switch for the OCR-driven auto-activation. Keybind activation works regardless.</summary>
    public bool AutoSwitchOnOcr
    {
        get;
        set => SetProperty(ref field, value);
    }

    // -------------------------------------------------- Legacy mode parameters --

    public int HoldTime
    {
        get;
        set => SetProperty(ref field, value);
    } = 10;

    public int FireRate
    {
        get;
        set => SetProperty(ref field, value);
    } = 200;

    public int YRecoil
    {
        get;
        set => SetProperty(ref field, value);
    } = 10;

    public int XRecoil
    {
        get;
        set => SetProperty(ref field, value);
    }

    // ----------------------------------------------- ImageBased mode parameters --

    /// <summary>Strength of the image-based anti-recoil. Range [0, 1.5]. Only used when Mode == ImageBased.</summary>
    public double AutoStrength
    {
        get;
        set => SetProperty(ref field, value);
    } = 0.85;

    // ------------------------------------------ PatternPlayback mode parameters --

    /// <summary>Name of the entry in <see cref="AntiRecoilSettings.Patterns"/>. Empty = nothing to play.</summary>
    public string PatternName
    {
        get;
        set => SetProperty(ref field, value);
    } = "";

    /// <summary>Per-profile multiplier applied to every recorded sample during playback. 1.0 = exact.</summary>
    public double PatternStrength
    {
        get;
        set => SetProperty(ref field, value);
    } = 1.0;

    // -------------------------------------------------------------- Computed --

    /// <summary>A profile is valid when it has a name and (for PatternPlayback) a referenced pattern.</summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        (Mode != AntiRecoilMode.PatternPlayback || !string.IsNullOrWhiteSpace(PatternName));

    /// <summary>
    ///     True iff this is the currently-SELECTED profile. Independent of the master AntiRecoil
    ///     toggle — that's checked separately by each action class. This lets the user pre-select
    ///     a profile while AntiRecoil is off and have it pick up immediately when they switch
    ///     master on.
    /// </summary>
    public bool IsActive =>
        IsValid &&
        AppConfig.Current?.AntiRecoilSettings?.ActiveProfileId == Id;

    /// <summary>
    ///     Writable wrapper around <see cref="IsActive"/>. Setting it routes through
    ///     <see cref="AILogic.AntiRecoilProfileManager.SetActiveProfile"/> so the radio
    ///     behaviour + notifications fire from one place. Used by the list-row toggle binding.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsActiveSelfWrite
    {
        get => IsActive;
        set
        {
            var settings = AppConfig.Current?.AntiRecoilSettings;
            if (settings == null) return;
            var newId = value ? Id : (settings.ActiveProfileId == Id ? "" : settings.ActiveProfileId);
            AILogic.AntiRecoilProfileManager.Instance.SetActiveProfile(newId, notify: true);
            RaisePropertyChanged(nameof(IsActiveSelfWrite));
        }
    }

    public string Description
    {
        get
        {
            var mode = Mode switch
            {
                AntiRecoilMode.Legacy => "Legacy",
                AntiRecoilMode.ImageBased => "Image-based",
                AntiRecoilMode.PatternPlayback => string.IsNullOrEmpty(PatternName) ? "Pattern (none)" : $"Pattern: {PatternName}",
                _ => Mode.ToString(),
            };
            string suffix = string.IsNullOrWhiteSpace(WeaponMatch) ? "" : $" · OCR: \"{WeaponMatch}\"";
            return $"{Name ?? "New Profile"} — {mode}{suffix}";
        }
    }
}
