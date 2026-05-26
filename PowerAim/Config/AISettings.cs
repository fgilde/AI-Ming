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
