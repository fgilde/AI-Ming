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

    // ============================================================ Smart aim pipeline ====
    // The tracker → selector → smoother → controller pipeline that replaces the per-frame
    // StickyAimSelector + stacked prediction/EMA. All gated behind SmartAimEnabled so the old
    // path stays available as a fallback. Defaults are tuned from the research write-up.

    /// <summary>
    ///     Master switch for the new aim pipeline (persistent SORT-style target tracking, switch
    ///     hysteresis, 1€ smoothing, frame-rate-independent damped control). When off, the legacy
    ///     sticky-selector + lerp path runs instead. Default <c>true</c>.
    /// </summary>
    public bool SmartAimEnabled
    {
        get;
        set => SetField(ref field, value);
    } = true;

    /// <summary>Frames a track may coast (no matching detection) before it's dropped. ~8 ≈ bridges 1-3 dropped YOLO frames.</summary>
    public int TrackMaxAgeFrames
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 1, 60));
    } = 8;

    /// <summary>Matched detections required before a track is "confirmed" and eligible for aiming. ~3 filters one-frame false positives.</summary>
    public int TrackMinHits
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 1, 10));
    } = 3;

    /// <summary>Minimum bounding-box IoU for a detection to associate to an existing track. ~0.2.</summary>
    public double TrackIoUThreshold
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.01, 0.9));
    } = 0.2;

    /// <summary>Alpha-beta position-correction gain (0..1). Higher = trusts detections more, less smoothing.</summary>
    public double TrackAlpha
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.05, 1.0));
    } = 0.5;

    /// <summary>Alpha-beta velocity-correction gain (0..1). Drives how fast the velocity estimate adapts.</summary>
    public double TrackBeta
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.0, 1.0));
    } = 0.2;

    /// <summary>A challenger track must beat the held one by this fraction to be considered for a switch. ~0.25.</summary>
    public double SwitchMarginPct
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.0, 2.0));
    } = 0.25;

    /// <summary>Consecutive frames a challenger must stay better before the aim actually switches targets. ~6.</summary>
    public int SwitchFrames
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 1, 60));
    } = 6;

    /// <summary>Crosshair-to-target radius (screen px) within which the controller stops nudging. ~3.</summary>
    public double AimDeadzonePx
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.0, 50.0));
    } = 3.0;

    /// <summary>Apply the 1€ filter to the aim point for adaptive jitter removal. Default <c>true</c>.</summary>
    public bool UseOneEuro
    {
        get;
        set => SetField(ref field, value);
    } = true;

    /// <summary>1€ filter minimum cutoff (Hz). Lower = smoother at rest. ~1.0.</summary>
    public double OneEuroMinCutoff
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.1, 10.0));
    } = 1.0;

    /// <summary>1€ filter speed coefficient. Higher = less lag during fast flicks. ~0.7.</summary>
    public double OneEuroBeta
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.0, 5.0));
    } = 0.7;

    /// <summary>
    ///     Velocity lead time (ms): aim ahead of the target by its estimated velocity to compensate
    ///     for input + render latency. 0 = aim at the current position. Keep small (~0-40 ms).
    /// </summary>
    public double LeadTimeMs
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.0, 200.0));
    } = 0.0;

    /// <summary>
    ///     DXGI adapter index the ONNX inference session should run on. Passed straight through to
    ///     <c>AppendExecutionProvider_CUDA(deviceId)</c> / <c>AppendExecutionProvider_DML(deviceId)</c>,
    ///     so the meaning matches whatever Windows reports through <see cref="AILogic.GpuAdapterEnumerator"/>.
    ///     Default <c>0</c> = primary GPU; setting this to a secondary card lets the user keep
    ///     inference off the GPU the game is running on (reduces input lag on single-GPU systems with
    ///     an integrated/secondary adapter, or just balances load on dual-GPU rigs).
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
