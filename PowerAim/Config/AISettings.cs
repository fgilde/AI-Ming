using System.Collections.ObjectModel;

namespace PowerAim.Config;

/// <summary>
///     AI-pipeline configuration that doesn't fit cleanly into the existing slider / toggle /
///     dropdown buckets. Currently holds multi-class target filtering for ONNX models that emit
///     more than one class channel (YOLOv8 with multiple labels).
/// </summary>
public class AISettings : BaseSettings
{
    /// <summary>
    ///     When <see cref="TargetClassFilterMode"/> is <see cref="TargetClassFilterMode.SpecificIds"/>,
    ///     only detections whose <c>ClassId</c> appears in this list will be considered. Ignored in
    ///     <see cref="TargetClassFilterMode.AllClasses"/> mode.
    /// </summary>
    public ObservableCollection<int> TargetClassIds
    {
        get;
        set => SetField(ref field, value);
    } = new();

    /// <summary>
    ///     How <see cref="TargetClassIds"/> should be interpreted at inference time.
    /// </summary>
    public TargetClassFilterMode TargetClassFilterMode
    {
        get;
        set => SetField(ref field, value);
    } = TargetClassFilterMode.AllClasses;

    /// <summary>
    ///     When enabled the aiming logic holds a target between frames based on a composite score
    ///     (distance + confidence + size + lock bonus) and only switches when a clearly better
    ///     candidate appears. Reduces crosshair flicker between two overlapping detections. Default
    ///     <c>true</c>.
    /// </summary>
    public bool StickyAimEnabled
    {
        get;
        set => SetField(ref field, value);
    } = true;

    /// <summary>
    ///     Upper bound for the accumulated lock-score on the currently held target. Higher values
    ///     make the lock harder to break. Default <c>100</c>.
    /// </summary>
    public float StickyAimMaxLockScore
    {
        get;
        set => SetField(ref field, value);
    } = 100f;

    /// <summary>
    ///     Pixel radius (translated screen-space) within which the distance score contributes to a
    ///     candidate's overall score. Outside this radius a candidate gets <c>0</c> distance score
    ///     and only competes on confidence/size. Default <c>80</c>.
    /// </summary>
    public float StickyAimThreshold
    {
        get;
        set => SetField(ref field, value);
    } = 80f;

    /// <summary>
    ///     Rectangular regions in normalized image-space whose contents are ignored by the prediction
    ///     pipeline. Used to blank out HUDs (ammo counters, minimaps) that some models hallucinate
    ///     enemies into. Individual regions can be toggled via <see cref="DetectionMaskRegion.Enabled"/>
    ///     without removing them from the list.
    /// </summary>
    public ObservableCollection<DetectionMaskRegion> IgnoreRegions
    {
        get;
        set => SetField(ref field, value);
    } = new();

    // ============================================================ Smart aim ====
    // Layered aim path. Always-on core: pick the nearest detection, move a proportional fraction of
    // the remaining offset each frame (frame-rate-independent, optionally ratio-calibrated). On top
    // of that two OPT-IN quality layers that each default to the core behaviour when off and have no
    // closed-loop (ego-motion) coupling: adaptive aim-point smoothing (SmoothingMode) and stable
    // multi-target tracking with switch hysteresis (UseTargetTracking). Velocity LEAD is deliberately
    // absent — it is broken in this closed loop without ego-motion compensation.

    /// <summary>
    ///     Master switch for the smart aim path (nearest detection + frame-rate-independent
    ///     proportional move, with optional smoothing/tracking and pixel→count calibration). When off,
    ///     the legacy sticky-selector + lerp path runs instead. Default <c>true</c>.
    /// </summary>
    public bool SmartAimEnabled
    {
        get;
        set => SetField(ref field, value);
    } = true;

    /// <summary>Crosshair-to-target radius (screen px) within which the controller stops nudging. ~3.</summary>
    public double AimDeadzonePx
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.0, 50.0));
    } = 3.0;

    // ---- Opt-in layer 1a: aim-point smoothing (no closed-loop coupling) ----

    /// <summary>
    ///     How to smooth the aim point before moving. <see cref="AimSmoothingMode.None"/> = raw
    ///     detection; <see cref="AimSmoothingMode.Ema"/> = cheap fixed-weight EMA;
    ///     <see cref="AimSmoothingMode.OneEuro"/> = adaptive 1€ filter (heavy smoothing at rest, light
    ///     during flicks — the better choice for an aim cursor). All only smooth POSITION, never
    ///     extrapolate, so none are affected by the closed-loop ego-motion problem. Default
    ///     <see cref="AimSmoothingMode.OneEuro"/> — restores the adaptive-filter behaviour that the
    ///     pre-simplification default (the old <c>UseOneEuro=true</c>) had, so configs that predate the
    ///     enum (where the key is dropped on load) keep their 1€ feel instead of silently dropping to EMA.
    /// </summary>
    public AimSmoothingMode SmoothingMode
    {
        get;
        set => SetField(ref field, value);
    } = AimSmoothingMode.OneEuro;

    /// <summary>1€ filter minimum cutoff (Hz). Lower = smoother at rest. Only used in OneEuro mode. ~1.0.</summary>
    public double OneEuroMinCutoff
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.1, 10.0));
    } = 1.0;

    /// <summary>1€ filter speed coefficient. Higher = less lag during fast flicks. Only used in OneEuro mode. ~0.7.</summary>
    public double OneEuroBeta
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.0, 5.0));
    } = 0.7;

    // ---- Opt-in layer 1b: stable multi-target tracking + switch hysteresis (ego-motion-immune) ----
    // Uses the SORT-style tracker for identity/association ONLY; the aim point is always taken from
    // the raw last detection (TargetTrack.LastDetectionBox), never the velocity-extrapolated box, so
    // the assist's own view-pan can never bias the crosshair.

    /// <summary>
    ///     Track targets across frames (stable identity in crowds) and keep the aim on the same enemy
    ///     with switch hysteresis, instead of always snapping to whoever is nearest. When off the
    ///     simpler sticky-nearest selection runs. Default <c>false</c> (core behaviour).
    /// </summary>
    public bool UseTargetTracking
    {
        get;
        set => SetField(ref field, value);
    } = false;

    /// <summary>Frames a track may coast (no matching detection) before it's dropped — bridges 1-3 dropped YOLO frames. ~8.</summary>
    public int TrackMaxAgeFrames
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 1, 60));
    } = 8;

    /// <summary>Consecutive frames a challenger must stay clearly better before the aim switches targets. ~6.</summary>
    public int SwitchFrames
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 1, 60));
    } = 6;

    /// <summary>A challenger must beat the held target by this fraction to be eligible for a switch. ~0.25.</summary>
    public double SwitchMarginPct
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.0, 2.0));
    } = 0.25;

    /// <summary>
    ///     Measured screen-pixels per unit of raw mouse input (from the calibration wizard). When
    ///     &gt; 0 the aim converts a target's pixel offset into EXACT mouse counts, so the strength
    ///     slider feels the same in every game. 0 = uncalibrated (pixel offset used 1:1 as counts).
    /// </summary>
    public double CalibratedPixelsPerCount
    {
        get;
        set => SetField(ref field, Math.Max(0.0, value));
    } = 0.0;

    /// <summary>
    ///     Extra gain on top of the sensitivity approach fraction (issue #10). <c>1.0</c> = normal.
    ///     Raise it when even max sensitivity is too slow — e.g. a very high-DPI mouse paired with a low
    ///     in-game sensitivity, where one mouse count barely turns the view so the computed counts fall
    ///     short. It multiplies the per-frame move (values &gt; 1 intentionally overshoot in raw counts,
    ///     which such a low effective sensitivity absorbs). Mirrored from the active <see cref="AimProfile"/>.
    /// </summary>
    public double AimSpeedMultiplier
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.1, 20.0));
    } = 1.0;

    /// <summary>
    ///     DXGI adapter index the ONNX inference session should run on, as reported by
    ///     <see cref="AILogic.GpuAdapterEnumerator"/>. Used directly as ORT's DirectML deviceId; for CUDA
    ///     it is translated to the NVIDIA-only CUDA ordinal at session-build time
    ///     (<see cref="AILogic.GpuAdapterEnumerator.DxgiIndexToCudaOrdinal"/>), since CUDA numbers NVIDIA
    ///     GPUs in their own space (issue #12). Default <c>0</c> = primary GPU; setting this to a
    ///     secondary card lets the user keep inference off the GPU the game runs on (reduces input lag on
    ///     single-GPU systems with an integrated/secondary adapter, or balances load on dual-GPU rigs).
    /// </summary>
    public int InferenceGpuDeviceId
    {
        get;
        set => SetField(ref field, value);
    } = 0;
}

/// <summary>
///     Strategy used to filter multi-class ONNX predictions before they reach the targeting logic.
/// </summary>
public enum TargetClassFilterMode
{
    /// <summary>Accept every class the model emits (legacy behaviour).</summary>
    AllClasses = 0,

    /// <summary>Only accept detections whose class id is listed in <c>TargetClassIds</c>.</summary>
    SpecificIds = 1
}

/// <summary>How the aim point is smoothed before the proportional move (see <see cref="AISettings.SmoothingMode"/>).</summary>
public enum AimSmoothingMode
{
    /// <summary>No smoothing — aim straight at the raw detection.</summary>
    None = 0,

    /// <summary>Cheap fixed-weight exponential moving average.</summary>
    Ema = 1,

    /// <summary>Adaptive 1€ filter — heavy smoothing at rest, light during fast motion.</summary>
    OneEuro = 2
}
