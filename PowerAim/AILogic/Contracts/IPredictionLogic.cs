using System.Drawing;

namespace PowerAim.AILogic.Contracts;

/// <summary>
///     Abstraction over the ONNX inference pipeline. Existing consumers should keep working with
///     just <see cref="Predict"/> + <see cref="ExecutionProvider"/>; the new members let advanced
///     callers (overlay, sticky aim, telemetry) read the active image size and class table without
///     hard-coding the legacy 640×640 constant.
/// </summary>
public interface IPredictionLogic
{
    Task<Prediction[]> Predict(Bitmap frame, Rectangle detectionBox);
    OnnxExecutionProvider ExecutionProvider { get; }

    /// <summary>True when an ONNX session actually loaded. False means every load attempt (incl. the
    /// DirectML fallback) failed — see <see cref="LoadError"/>. Callers must not treat the pipeline as
    /// ready when this is false.</summary>
    bool IsLoaded { get; }

    /// <summary>Human-readable reason the model failed to load, or null when it loaded fine.</summary>
    string? LoadError { get; }

    /// <summary>
    ///     Square input side length the model is currently running with. Reflects the value
    ///     detected from ONNX input metadata, or the configuration override for dynamic models.
    /// </summary>
    int ImageSize { get; }

    /// <summary>
    ///     Number of class channels in the loaded model (≥ 1).
    /// </summary>
    int NumClasses { get; }

    /// <summary>
    ///     Mapping from class id → human-readable name parsed from the ONNX <c>names</c>
    ///     metadata. Empty when the model didn't ship that metadata.
    /// </summary>
    IReadOnlyDictionary<int, string> ModelClasses { get; }
}
