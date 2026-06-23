using System.Collections.ObjectModel;
using System.ComponentModel;
using Nextended.Core;
using Nextended.Core.Extensions;
using PowerAim.Types;

namespace PowerAim.Config;

/// <summary>
///     A named aim profile. Bundles the full aim "feel" (responsiveness, aim region, smart-tracking
///     tuning) plus optional activation conditions (a keybind that toggles it active, a process
///     filter, an OCR weapon-name match). Only ONE profile is active at a time — see
///     <see cref="AimSettings.ActiveProfileId"/>.
///     <para>
///     Activation is "apply-on-activate": <see cref="Apply"/> copies this profile's values into the
///     live global settings (<c>SliderSettings</c> / <c>ToggleState</c> / <c>AISettings</c>) that
///     the aiming pipeline already reads. So the pipeline needs no per-profile awareness — switching
///     a profile simply rewrites the settings it consumes. Mirrors the <see cref="AntiRecoilProfile"/>
///     UX (list + per-row hotkey + edit page) without a parallel runtime path.
///     </para>
/// </summary>
public class AimProfile : EditableNotificationObject
{
    private string _matchProcess = "";
    private string _ocrRegionName = "";
    private string _weaponMatch = "";

    public AimProfile()
    {
        Id = Guid.NewGuid().ToFormattedId();
        AppConfig.ConfigLoaded += (_, _) => DetectChanges();
        DetectChanges();
    }

    private void DetectChanges()
    {
        if (AppConfig.Current?.ToggleState != null)
        {
            AppConfig.Current.ToggleState.PropertyChanged -= OnToggleStateChange;
            AppConfig.Current.ToggleState.PropertyChanged += OnToggleStateChange;
        }
        if (AppConfig.Current?.AimSettings != null)
        {
            AppConfig.Current.AimSettings.PropertyChanged -= OnSettingsChange;
            AppConfig.Current.AimSettings.PropertyChanged += OnSettingsChange;
        }
    }

    private void OnToggleStateChange(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToggleState.AimAssist) or nameof(ToggleState.GlobalActive))
            RaisePropertyChanged(nameof(IsActive));
    }

    private void OnSettingsChange(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AimSettings.ActiveProfileId))
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

    public string Id { get; set; }

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
    } = "";

    // -------------------------------------------------- Aim feel (mapped to globals by Apply) --

    /// <summary>Use the smart tracking pipeline (vs the legacy single-target path).</summary>
    public bool SmartAim { get; set => SetProperty(ref field, value); } = true;

    /// <summary>Per-60Hz-frame approach fraction. Higher = snappier, lower = smoother.</summary>
    public double Sensitivity { get; set => SetProperty(ref field, value); } = 0.25;

    /// <summary>Aim at a random point inside the region (per engagement) instead of its centre.</summary>
    public bool RandomAimPoint { get; set => SetProperty(ref field, value); }

    /// <summary>Sub-rectangle inside the detection box to aim at (same model as the trigger head area).</summary>
    public RelativeRect AimRegion { get; set => SetProperty(ref field, value); } = RelativeRect.Default;

    public double DeadzonePx { get; set => SetProperty(ref field, value); } = 3.0;
    public int CoastFrames { get; set => SetProperty(ref field, value); } = 8;
    public int SwitchFrames { get; set => SetProperty(ref field, value); } = 6;
    public double SwitchMarginPct { get; set => SetProperty(ref field, value); } = 0.25;
    public double LeadTimeMs { get; set => SetProperty(ref field, value); }
    public bool UseOneEuro { get; set => SetProperty(ref field, value); } = true;
    public double OneEuroMinCutoff { get; set => SetProperty(ref field, value); } = 1.0;
    public double OneEuroBeta { get; set => SetProperty(ref field, value); } = 0.7;

    // -------------------------------------------------- Activation conditions --

    /// <summary>Optional process-name pattern scoping auto-activation to specific games. Empty = any.</summary>
    public string MatchProcess
    {
        get => _matchProcess;
        set => SetProperty(ref _matchProcess, value ?? "");
    }

    /// <summary>OCR region whose recognised text is checked against <see cref="WeaponMatch"/>.</summary>
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

    /// <summary>Master switch for OCR-driven auto-activation. Keybind activation works regardless.</summary>
    public bool AutoSwitchOnOcr { get; set => SetProperty(ref field, value); }

    /// <summary>
    ///     Per-profile aim-disengage rules — OCR-driven conditions that pause aim assist while true
    ///     (e.g. "knife equipped", "not scoped"). Read directly by <see cref="AILogic.AimDisengage"/>
    ///     for the active profile (not mirrored to globals by <see cref="Apply"/>, unlike the feel
    ///     fields — a rule list doesn't fit the single-value mirror model). New profiles start empty;
    ///     the migrated Default profile inherits the old global rules.
    /// </summary>
    public ObservableCollection<AimDisengageRule> DisengageRules
    {
        get;
        set => SetProperty(ref field, value);
    } = new();

    // -------------------------------------------------------------- Computed --

    public bool IsValid => !string.IsNullOrWhiteSpace(Name);

    /// <summary>True iff this is the currently-selected profile.</summary>
    public bool IsActive => IsValid && AppConfig.Current?.AimSettings?.ActiveProfileId == Id;

    /// <summary>Writable wrapper around <see cref="IsActive"/> for the list-row toggle binding.</summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsActiveSelfWrite
    {
        get => IsActive;
        set
        {
            var settings = AppConfig.Current?.AimSettings;
            if (settings == null) return;
            var newId = value ? Id : (settings.ActiveProfileId == Id ? "" : settings.ActiveProfileId);
            AILogic.AimProfileManager.Instance.SetActiveProfile(newId, notify: true);
            RaisePropertyChanged(nameof(IsActiveSelfWrite));
        }
    }

    public string Description
    {
        get
        {
            string mode = SmartAim ? "Smart" : "Legacy";
            string suffix = string.IsNullOrWhiteSpace(WeaponMatch) ? "" : $" · OCR: \"{WeaponMatch}\"";
            return $"{(string.IsNullOrWhiteSpace(Name) ? "New profile" : Name)} — {mode}{suffix}";
        }
    }

    /// <summary>
    ///     Copy this profile's values into the live global settings the aim pipeline reads. Called
    ///     by <see cref="AILogic.AimProfileManager.SetActiveProfile"/> whenever this profile becomes
    ///     active (hotkey, OCR auto-switch, manual toggle, or config load).
    /// </summary>
    public void Apply()
    {
        var cfg = AppConfig.Current;
        if (cfg == null) return;

        if (cfg.SliderSettings != null)
        {
            cfg.SliderSettings.MouseSensitivity = Sensitivity;
            cfg.SliderSettings.AimRegion = AimRegion;
        }
        if (cfg.ToggleState != null)
        {
            cfg.ToggleState.RandomAimPoint = RandomAimPoint;
        }
        if (cfg.AISettings != null)
        {
            var ai = cfg.AISettings;
            ai.SmartAimEnabled = SmartAim;
            ai.AimDeadzonePx = DeadzonePx;
            ai.TrackMaxAgeFrames = CoastFrames;
            ai.SwitchFrames = SwitchFrames;
            ai.SwitchMarginPct = SwitchMarginPct;
            ai.LeadTimeMs = LeadTimeMs;
            ai.UseOneEuro = UseOneEuro;
            ai.OneEuroMinCutoff = OneEuroMinCutoff;
            ai.OneEuroBeta = OneEuroBeta;
        }
    }

    /// <summary>Snapshot the current live global settings into this profile (used by "save current as profile").</summary>
    public void CaptureFromGlobals()
    {
        var cfg = AppConfig.Current;
        if (cfg == null) return;
        if (cfg.SliderSettings != null)
        {
            Sensitivity = cfg.SliderSettings.MouseSensitivity;
            AimRegion = cfg.SliderSettings.AimRegion;
        }
        if (cfg.ToggleState != null) RandomAimPoint = cfg.ToggleState.RandomAimPoint;
        if (cfg.AISettings != null)
        {
            var ai = cfg.AISettings;
            SmartAim = ai.SmartAimEnabled;
            DeadzonePx = ai.AimDeadzonePx;
            CoastFrames = ai.TrackMaxAgeFrames;
            SwitchFrames = ai.SwitchFrames;
            SwitchMarginPct = ai.SwitchMarginPct;
            LeadTimeMs = ai.LeadTimeMs;
            UseOneEuro = ai.UseOneEuro;
            OneEuroMinCutoff = ai.OneEuroMinCutoff;
            OneEuroBeta = ai.OneEuroBeta;
        }
    }
}
