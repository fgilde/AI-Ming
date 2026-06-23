using InputLogic;
using PowerAim.Types;

namespace PowerAim.Config;

public class SliderSettings : BaseSettings
{
    /// <summary>
    ///     Maximum inference frames-per-second the AI loop is allowed to run at. <c>0</c> means
    ///     unlimited (the loop runs as fast as <see cref="PredictionLogic" /> will let it). Useful
    ///     for capping GPU/CPU load and pairing well with the
    ///     <see cref="PowerAim.AILogic.PerformanceBenchmark" /> recommendations.
    /// </summary>
    public double MaxInferenceFPS { get; set => SetField(ref field, value); } = 0;

    /// <summary>
    ///     Square ONNX input size the AI runs at. For fixed-size models this is auto-detected from
    ///     the model's input metadata at load time and snapped to the model's declared dimension.
    ///     For dynamic-axis ONNX models this is the user-selected runtime resolution
    ///     (160/256/320/416/512/640 are the supported strides for YOLOv8 anchor-free).
    /// </summary>
    public int ImageSize { get; set => SetField(ref field, value); } = 640;

    public string SuggestedModel { get; set => SetField(ref field, value); } = "";

    public double ActualFovSize =>
        AppConfig.Current.ToggleState.DynamicFOV &&
        InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind))
            ? DynamicFOVSize
            : FOVSize;

    public double FOVSize { get; set => SetField(ref field, value); } = 640;

    public double DynamicFOVSize { get; set => SetField(ref field, value); } = 200;

    // Smart-aim semantics: this is the per-60Hz-frame approach fraction — HIGHER = snappier,
    // LOWER = smoother (the inverse of the old 1-sensitivity lerp, and now meaningful all the way
    // down to 0.0001 for high-DPI mice — GitHub issue #10). 0.25 ≈ a smooth-but-responsive default.
    public double MouseSensitivity { get; set => SetField(ref field, value); } = 0.25;

    public double MouseJitter { get; set => SetField(ref field, value); } = 6;

    /// <summary>
    ///     Visual aim region — a sub-rectangle inside the detected bounding box (normalized 0..1
    ///     coords, same model as the trigger "head area"). The aim point is taken from this region:
    ///     its centre by default, or a random point within it when
    ///     <see cref="ToggleState.RandomAimPoint"/> is enabled. Default mirrors the trigger default
    ///     (an upper-centre "head" box). Replaces the old X/Y offset + percentage sliders.
    /// </summary>
    public RelativeRect AimRegion { get; set => SetField(ref field, value); } = RelativeRect.Default;

    public double EMASmoothening { get; set => SetField(ref field, value); } = 0.5;

    public double FirePressDelay { get; set => SetField(ref field, value); } = 0.1;

    public double AIMinimumConfidence { get; set => SetField(ref field, value); } = 45;

    public float MagnificationStepFactor { get; set => SetField(ref field, value); } = 0.5f;

    public float MagnificationFactor { get; set => SetField(ref field, value); } = 2;

    public double MagnifierWindowHeight { get; set => SetField(ref field, value); } = 250;

    public double MagnifierWindowWidth { get; set => SetField(ref field, value); } = 350;

    public double GamepadMinimumLT { get; set => SetField(ref field, value); } = 0.7;

    public double GamepadMinimumRT { get; set => SetField(ref field, value); } = 0.7;

    public int AIConfidenceFontSize { get; set => SetField(ref field, value); } = 20;

    public double CornerRadius { get; set => SetField(ref field, value); }

    public double BorderThickness { get; set => SetField(ref field, value); } = 1;

    internal override void OnPropertyChanged(string? propertyName = null)
    {
        if (propertyName is nameof(FOVSize) or nameof(DynamicFOVSize))
            OnPropertyChanged(nameof(ActualFovSize));

        base.OnPropertyChanged(propertyName);
    }
}
