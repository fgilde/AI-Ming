using System.Collections.ObjectModel;
using System.ComponentModel;
using Nextended.Core;
using Nextended.Core.Extensions;
using PowerAim.Input;
using PowerAim.Types;
using Nextended.UI.Input;

namespace PowerAim.Config;

/// <summary>
///     A named aim profile. Bundles the full aim "feel" (responsiveness, aim region, smoothing,
///     tracking, calibration, legacy EMA/prediction) plus its own <see cref="AimKeyBindings"/> and an
///     optional OCR weapon filter. There is NO single-active radio: every <see cref="Enabled"/> profile
///     is live and DRIVES the aim while one of its own aim-keys is held (and its OCR condition, if any,
///     is met) — put head on one key and chest on another and they coexist.
///     <para>
///     The aiming pipeline reads the live global settings (<c>SliderSettings</c> / <c>ToggleState</c> /
///     <c>AISettings</c>); each frame the resolver (<see cref="AimSettings.ResolveEffectiveProfile"/>)
///     picks the engaged profile and <see cref="AI.AimProfileManager"/> copies its values into
///     those globals when it changes — so the globals are just internal plumbing the UI never edits
///     directly. <see cref="AimSettings.ActiveProfileId"/> is now only a migration/reconcile anchor.
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
        // React when the effective (currently-driving) profile changes so the list badge updates.
        // Static event → no manager instantiation here (safe during deserialization).
        AI.AimProfileManager.EffectiveProfileChanged -= OnEffectiveChange;
        AI.AimProfileManager.EffectiveProfileChanged += OnEffectiveChange;
    }

    private void OnEffectiveChange() => RaisePropertyChanged(nameof(IsEffective));

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
                RaisePropertyChanged(nameof(IsEffective));
            }
        }
    } = "";

    // -------------------------------------------------- Aim feel (mapped to globals by Apply) --

    /// <summary>Use the smart tracking pipeline (vs the legacy single-target path).</summary>
    public bool SmartAim { get; set => SetProperty(ref field, value); } = true;

    /// <summary>Pick targets by <see cref="TargetRule"/> (higher score first) instead of nearest to the crosshair.</summary>
    public bool UseTargetRule { get; set => SetProperty(ref field, value); }

    /// <summary>Target score, e.g. <c>-distance</c> or <c>head_hit * 2 - distance / 500</c>. See <see cref="AI.Rules.TargetScope"/>.</summary>
    public string TargetRule { get; set => SetProperty(ref field, value ?? ""); } = "-distance";

    /// <summary>The plain-language description <see cref="TargetRule"/> was written from.</summary>
    public string TargetRuleDescription { get; set => SetProperty(ref field, value ?? ""); } = "";

    /// <summary>
    ///     Per-60Hz-frame approach fraction of the target offset (in FOV-box pixels, resolution-
    ///     independent). Higher = snappier/firmer, lower = smoother. Matches the "Balanced" preset.
    /// </summary>
    public double Sensitivity { get; set => SetProperty(ref field, value); } = 0.45;

    /// <summary>
    ///     Extra gain multiplier on the per-frame move (issue #10). <c>1.0</c> = normal. Raise it when
    ///     even max <see cref="Sensitivity"/> is too slow (high-DPI mouse + low in-game sensitivity).
    ///     Mirrors to <see cref="AISettings.AimSpeedMultiplier"/>.
    /// </summary>
    public double AimSpeedMultiplier { get; set => SetProperty(ref field, value); } = 1.0;

    /// <summary>Aim at a random point inside the region (per engagement) instead of its centre.</summary>
    public bool RandomAimPoint { get; set => SetProperty(ref field, value); }

    /// <summary>Sub-rectangle inside the detection box to aim at (same model as the trigger head area).</summary>
    public RelativeRect AimRegion { get; set => SetProperty(ref field, value); } = RelativeRect.Default;

    /// <summary>Crosshair-to-target radius (px) within which the aim stops nudging.</summary>
    public double DeadzonePx { get; set => SetProperty(ref field, value); } = 3.0;

    /// <summary>How the aim point is smoothed (None / EMA / adaptive 1€). Mirrors to <see cref="AISettings.SmoothingMode"/>.</summary>
    public AimSmoothingMode SmoothingMode { get; set => SetProperty(ref field, value); } = AimSmoothingMode.OneEuro;

    /// <summary>1€ filter minimum cutoff (Hz) — only used in OneEuro smoothing mode.</summary>
    public double OneEuroMinCutoff { get; set => SetProperty(ref field, value); } = 1.0;

    /// <summary>1€ filter speed coefficient — only used in OneEuro smoothing mode.</summary>
    public double OneEuroBeta { get; set => SetProperty(ref field, value); } = 0.7;

    /// <summary>PID gains — only used in PID smoothing mode. Mirror to <see cref="AISettings.PidKp"/> etc.</summary>
    public double PidKp { get; set => SetProperty(ref field, value); } = 0.55;
    public double PidKi { get; set => SetProperty(ref field, value); } = 0.05;
    public double PidKd { get; set => SetProperty(ref field, value); } = 0.12;

    /// <summary>
    ///     How far (px) the aim keeps following the same enemy before re-acquiring another (issue #19).
    ///     Larger = stickier lock. Mirrors to <see cref="AISettings.StickyRadiusPx"/>.
    /// </summary>
    public double StickyRadiusPx { get; set => SetProperty(ref field, value); } = 160.0;

    /// <summary>Track targets across frames (stable identity + switch hysteresis) instead of plain sticky-nearest.</summary>
    public bool UseTargetTracking { get; set => SetProperty(ref field, value); }

    /// <summary>Frames a track may coast through dropped detections before it's dropped (tracking only).</summary>
    public int CoastFrames { get; set => SetProperty(ref field, value); } = 8;

    /// <summary>Consecutive frames a new target must be seen before the aim engages (tracking only). 0 = at once.</summary>
    public int MinTrackedFrames { get; set => SetProperty(ref field, value); } = 2;

    /// <summary>Time constant (ms) with which a coasting track's confidence fades (tracking only). 0 = off.</summary>
    public double TrackConfidenceDecayMs { get; set => SetProperty(ref field, value); }

    /// <summary>Consecutive frames a challenger must stay better before the aim switches targets (tracking only).</summary>
    public int SwitchFrames { get; set => SetProperty(ref field, value); } = 6;

    /// <summary>Margin a challenger must beat the held target by to be eligible for a switch (tracking only).</summary>
    public double SwitchMarginPct { get; set => SetProperty(ref field, value); } = 0.25;

    /// <summary>
    ///     Measured screen-pixels per mouse-count from the calibration wizard (0 = uncalibrated).
    ///     When set, the aim converts a target's pixel offset into exact mouse counts so the strength
    ///     slider feels identical in every game. Mirrors to <see cref="AISettings.CalibratedPixelsPerCount"/>.
    /// </summary>
    public double CalibratedPixelsPerCount { get; set => SetProperty(ref field, value); }

    /// <summary>Reaction time (ms): after a target appears the aim waits this long before it moves. 0 = at once.</summary>
    public double ReactionDelayMs { get; set => SetProperty(ref field, Math.Clamp(value, 0, 1000)); }

    /// <summary>Smart-path controller: classic proportional approach or the model-based V2 engine.</summary>
    public AI.Aiming.AimEngine AimEngine { get; set => SetProperty(ref field, value); } = AI.Aiming.AimEngine.Classic;

    /// <summary>Capture-to-output latency (ms) the smart aim compensates for; raise when the aim overshoots, lower when it lags. 0 = off.</summary>
    public double LatencyCompensationMs { get; set => SetProperty(ref field, value); }

    // ---- Aim point (read directly by AimingAction, not mirrored to globals) ----

    /// <summary>Per-class rules: ignore, minimum confidence, priority and anchor. Empty = every class alike.</summary>
    public ObservableCollection<ClassTargetSpec> ClassSpecs
    {
        get;
        set => SetProperty(ref field, value ?? new());
    } = new();

    /// <summary>Pull the aim point toward the box centre for small (distant) boxes, so far targets aren't overshot above the head.</summary>
    public bool DynamicOffset { get; set => SetProperty(ref field, value); }

    /// <summary>Box area in % of the FOV at or below which the box centre is aimed at.</summary>
    public double DynamicMinAreaPct { get; set => SetProperty(ref field, Math.Max(0, value)); } = 0.05;

    /// <summary>Box area in % of the FOV from which the full aim region / anchor applies.</summary>
    public double DynamicMaxAreaPct { get; set => SetProperty(ref field, Math.Max(0, value)); } = 1.5;

    /// <summary>Fixed shift of the aim point in FOV pixels, applied last (e.g. models that detect a name tag above the body).</summary>
    public int AdditionalOffsetX { get; set => SetProperty(ref field, value); }

    /// <summary>Fixed shift of the aim point in FOV pixels, applied last.</summary>
    public int AdditionalOffsetY { get; set => SetProperty(ref field, value); }

    /// <summary>With a pose model: aim at the head (nose/eyes) or chest (shoulders) keypoints.</summary>
    public PoseAimTarget PoseTarget { get; set => SetProperty(ref field, value); } = PoseAimTarget.Off;

    /// <summary>Keypoints below this confidence are ignored (the box is used instead).</summary>
    public double KeypointMinConfidence { get; set => SetProperty(ref field, Math.Clamp(value, 0, 1)); } = 0.5;

    // ---- Legacy-path feel (only used when SmartAim is off; mirrored to globals by Apply) ----

    /// <summary>Apply the legacy EMA smoothing to the mouse path (legacy path only). Mirrors <see cref="ToggleState.EMASmoothening"/>.</summary>
    public bool EmaSmoothing { get; set => SetProperty(ref field, value); }

    /// <summary>Legacy EMA blend factor 0..1. Mirrors <see cref="SliderSettings.EMASmoothening"/>.</summary>
    public double EmaSmoothingFactor { get; set => SetProperty(ref field, value); } = 0.5;

    /// <summary>Enable legacy velocity prediction (legacy path only). Mirrors <see cref="ToggleState.Predictions"/>.</summary>
    public bool Predictions { get; set => SetProperty(ref field, value); }

    /// <summary>Legacy prediction method. Mirrors <see cref="DropdownState.PredictionMethod"/>.</summary>
    public PredictionMethod PredictionMethod { get; set => SetProperty(ref field, value); } = PredictionMethod.KalmanFilter;

    /// <summary>Mouse path curve. Mirrors <see cref="DropdownState.MovementPathType"/>.</summary>
    public MovementPathType MovementPathType { get; set => SetProperty(ref field, value); } = MovementPathType.Bezier;

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
    ///     This profile's aim keys, with HOLD semantics: while any of them is held (and the profile is
    ///     <see cref="Enabled"/> + its OCR condition met) this profile DRIVES the aim — put "head" on one
    ///     key and "chest/legit" on another and switch instantly by which key you hold. A profile with no
    ///     valid key never aims (there is no global-key fallback any more); each profile should have its
    ///     own unique key. Like <see cref="DisengageRules"/> this is NOT mirrored to globals by
    ///     <see cref="Apply"/> — the effective-profile resolver reads it directly each frame.
    /// </summary>
    public ObservableCollection<StoredInputBinding> AimKeyBindings
    {
        get;
        set => SetProperty(ref field, value);
    } = new();

    /// <summary>
    ///     Off-gate: while any of these is held the profile does NOT aim, even with its aim key held
    ///     (hipfire, melee, vehicles, ladders). Read directly by the resolver like <see cref="AimKeyBindings"/>.
    /// </summary>
    public ObservableCollection<StoredInputBinding> OffGateBindings
    {
        get;
        set => SetProperty(ref field, value ?? new());
    } = new();

    /// <summary>
    ///     Whether this profile participates at all. Replaces the old single-active radio: every
    ///     enabled profile is "live" and engages independently whenever its own <see cref="AimKeyBindings"/>
    ///     is held (and its OCR condition, if any, is met). Lets the user keep a profile configured but
    ///     turn it off without deleting it. Default <c>true</c>.
    /// </summary>
    public bool Enabled { get; set => SetProperty(ref field, value); } = true;

    /// <summary>
    ///     Runtime-only: does this profile's OCR weapon condition currently match? Updated by the
    ///     750 ms OCR poll (<see cref="AI.AimProfileManager"/>); read by the per-frame resolver so
    ///     the expensive OCR text match isn't done on the aim thread. <c>true</c> when no OCR filter is set.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool OcrConditionMet { get; set; } = true;

    /// <summary>
    ///     Per-profile aim-disengage rules — OCR-driven conditions that pause aim assist while true
    ///     (e.g. "knife equipped", "not scoped"). Read directly by <see cref="AI.AimDisengage"/>
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

    /// <summary>True iff this profile has at least one valid aim key (required to ever engage / aim).</summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasAimKey => AimKeyBindings.Any(k => k is { IsValid: true });

    /// <summary>
    ///     True iff this profile is the one currently DRIVING the aim (its aim-key is held + OCR met,
    ///     and it won the per-frame resolve). Lets the list badge whichever profile is actually live.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEffective => AI.AimProfileManager.Instance.EffectiveProfileId == Id;

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
    ///     Copy this profile's values into the live global settings the aim pipeline reads. Called by
    ///     <see cref="AI.AimProfileManager"/> (<c>ApplyEffective</c> / <c>ReapplyIfEffective</c>)
    ///     whenever this profile becomes the effective (currently-driving) one or is edited while live.
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
        if (cfg.SliderSettings != null)
            cfg.SliderSettings.EMASmoothening = EmaSmoothingFactor;
        if (cfg.ToggleState != null)
        {
            cfg.ToggleState.RandomAimPoint = RandomAimPoint;
            cfg.ToggleState.EMASmoothening = EmaSmoothing;
            cfg.ToggleState.Predictions = Predictions;
        }
        if (cfg.DropdownState != null)
        {
            cfg.DropdownState.PredictionMethod = PredictionMethod;
            cfg.DropdownState.MovementPathType = MovementPathType;
        }
        if (cfg.AISettings != null)
        {
            var ai = cfg.AISettings;
            ai.SmartAimEnabled = SmartAim;
            ai.AimDeadzonePx = DeadzonePx;
            ai.SmoothingMode = SmoothingMode;
            ai.OneEuroMinCutoff = OneEuroMinCutoff;
            ai.OneEuroBeta = OneEuroBeta;
            ai.PidKp = PidKp;
            ai.PidKi = PidKi;
            ai.PidKd = PidKd;
            ai.UseTargetTracking = UseTargetTracking;
            ai.TrackMaxAgeFrames = CoastFrames;
            ai.MinTrackedFrames = MinTrackedFrames;
            ai.TrackConfidenceDecayMs = TrackConfidenceDecayMs;
            ai.SwitchFrames = SwitchFrames;
            ai.SwitchMarginPct = SwitchMarginPct;
            ai.CalibratedPixelsPerCount = CalibratedPixelsPerCount;
            ai.AimSpeedMultiplier = AimSpeedMultiplier;
            ai.StickyRadiusPx = StickyRadiusPx;
            ai.LatencyCompensationMs = LatencyCompensationMs;
            ai.AimEngine = AimEngine;
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
        if (cfg.ToggleState != null)
        {
            RandomAimPoint = cfg.ToggleState.RandomAimPoint;
            EmaSmoothing = cfg.ToggleState.EMASmoothening;
            Predictions = cfg.ToggleState.Predictions;
        }
        if (cfg.SliderSettings != null) EmaSmoothingFactor = cfg.SliderSettings.EMASmoothening;
        if (cfg.DropdownState != null)
        {
            PredictionMethod = cfg.DropdownState.PredictionMethod;
            MovementPathType = cfg.DropdownState.MovementPathType;
        }
        if (cfg.AISettings != null)
        {
            var ai = cfg.AISettings;
            SmartAim = ai.SmartAimEnabled;
            DeadzonePx = ai.AimDeadzonePx;
            SmoothingMode = ai.SmoothingMode;
            OneEuroMinCutoff = ai.OneEuroMinCutoff;
            OneEuroBeta = ai.OneEuroBeta;
            PidKp = ai.PidKp;
            PidKi = ai.PidKi;
            PidKd = ai.PidKd;
            UseTargetTracking = ai.UseTargetTracking;
            CoastFrames = ai.TrackMaxAgeFrames;
            MinTrackedFrames = ai.MinTrackedFrames;
            TrackConfidenceDecayMs = ai.TrackConfidenceDecayMs;
            SwitchFrames = ai.SwitchFrames;
            SwitchMarginPct = ai.SwitchMarginPct;
            CalibratedPixelsPerCount = ai.CalibratedPixelsPerCount;
            AimSpeedMultiplier = ai.AimSpeedMultiplier;
            StickyRadiusPx = ai.StickyRadiusPx;
            LatencyCompensationMs = ai.LatencyCompensationMs;
            AimEngine = ai.AimEngine;
        }
    }
}