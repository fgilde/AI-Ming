using System.Drawing;
using Microsoft.ML.OnnxRuntime.Tensors;
using Aimmy2.Config;

namespace Aimmy2.AILogic;

/// <summary>
///     Pure parser for YOLOv8 ONNX output tensors. Walks every candidate detection, runs
///     confidence + FOV + class filtering, and emits <see cref="Prediction"/> instances that match
///     the fork's existing API (including the <c>CenterXTranslated</c> / <c>CenterYTranslated</c>
///     screen-normalized coordinates used by sticky-aim and the KD-tree selection in
///     <see cref="PredictionLogic"/>).
///     Ported from upstream Babyhamsta/Aimmy (commit c249de6) — class-id filtering uses the
///     fork's <see cref="AppConfig"/>-driven settings instead of upstream's free-text selector.
/// </summary>
internal static class PredictionFilter
{
    /// <summary>
    ///     Parse a YOLOv8 output tensor of shape <c>[1, 4 + numClasses, numDetections]</c> and
    ///     return the surviving candidates. The single-class fast path treats the 5th channel
    ///     directly as objectness so legacy single-class ONNXv8 models behave identically to the
    ///     old <c>[1,5,8400]</c> code path.
    /// </summary>
    /// <param name="outputTensor">Raw YOLO output tensor.</param>
    /// <param name="detectionBox">Screen-space rectangle the bitmap was captured from.</param>
    /// <param name="imageSize">Side length the model was given (e.g. 640).</param>
    /// <param name="numDetections">Total candidate count = (s/8)² + (s/16)² + (s/32)².</param>
    /// <param name="numClasses">Number of class channels in the output (≥ 1).</param>
    /// <param name="modelClasses">id → class-name map sourced from ONNX metadata.</param>
    /// <param name="minConfidence">Minimum confidence threshold (0..1).</param>
    /// <param name="allowedClassIds">
    ///     Optional whitelist of class IDs to accept. <c>null</c> or empty = no filter (any class).
    /// </param>
    /// <param name="fovMinX">FOV clip — minimum X (image-space).</param>
    /// <param name="fovMaxX">FOV clip — maximum X (image-space).</param>
    /// <param name="fovMinY">FOV clip — minimum Y (image-space).</param>
    /// <param name="fovMaxY">FOV clip — maximum Y (image-space).</param>
    public static List<Prediction> CreatePredictions(
        Tensor<float> outputTensor,
        Rectangle detectionBox,
        int imageSize,
        int numDetections,
        int numClasses,
        IReadOnlyDictionary<int, string> modelClasses,
        float minConfidence,
        IReadOnlyCollection<int>? allowedClassIds,
        float fovMinX,
        float fovMaxX,
        float fovMinY,
        float fovMaxY)
    {
        bool classFilter = allowedClassIds != null && allowedClassIds.Count > 0;
        var predictions = new List<Prediction>(Math.Min(numDetections, 64));

        for (int i = 0; i < numDetections; i++)
        {
            float xCenter = outputTensor[0, 0, i];
            float yCenter = outputTensor[0, 1, i];
            float width = outputTensor[0, 2, i];
            float height = outputTensor[0, 3, i];

            int bestClassId = 0;
            float bestConfidence;

            if (numClasses <= 1)
            {
                // Legacy fast path: channel 4 carries objectness directly.
                bestConfidence = outputTensor[0, 4, i];
            }
            else
            {
                bestConfidence = 0f;
                for (int classId = 0; classId < numClasses; classId++)
                {
                    float c = outputTensor[0, 4 + classId, i];
                    if (c > bestConfidence)
                    {
                        bestConfidence = c;
                        bestClassId = classId;
                    }
                }
            }

            if (bestConfidence < minConfidence) continue;
            if (classFilter && !allowedClassIds!.Contains(bestClassId)) continue;

            float xMin = xCenter - width / 2f;
            float yMin = yCenter - height / 2f;
            float xMax = xCenter + width / 2f;
            float yMax = yCenter + height / 2f;

            if (xMin < fovMinX || xMax > fovMaxX || yMin < fovMinY || yMax > fovMaxY) continue;

            // Match the fork's existing translation convention (see PredictionLogic prior to the
            // multi-class refactor): centers are stored as image-space coords offset by the
            // detection-box origin and normalized by image size.
            predictions.Add(new Prediction
            {
                Rectangle = new RectangleF(xMin, yMin, width, height),
                Confidence = bestConfidence,
                ClassId = bestClassId,
                ClassName = modelClasses.TryGetValue(bestClassId, out var n) ? n : $"Class_{bestClassId}",
                CenterXTranslated = (xCenter - detectionBox.Left) / imageSize,
                CenterYTranslated = (yCenter - detectionBox.Top) / imageSize
            });
        }

        return predictions;
    }
}
