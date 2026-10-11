using System.Drawing;
using Microsoft.ML.OnnxRuntime.Tensors;
using PowerAim.Config;

namespace PowerAim.AILogic;

/// <summary>
///     Pure parser for YOLOv8 ONNX output tensors. Walks every candidate detection, runs
///     confidence + FOV + class filtering, and emits <see cref="Prediction"/> instances that match
///     the fork's existing API (including the <c>CenterXTranslated</c> / <c>CenterYTranslated</c>
///     screen-normalized coordinates used by sticky-aim and the KD-tree selection in
///     <see cref="PredictionLogic"/>).
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
    /// <param name="outputTransposed">
    ///     <c>true</c> for channels-last <c>[1, N, 4+nc]</c> (ultralytics default export),
    ///     <c>false</c> for channels-first <c>[1, 4+nc, N]</c>.
    /// </param>
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
        float fovMaxY,
        IReadOnlyList<DetectionMaskRegion>? ignoreRegions = null,
        bool outputTransposed = false)
    {
        bool classFilter = allowedClassIds != null && allowedClassIds.Count > 0;
        bool maskFilter = ignoreRegions != null && ignoreRegions.Count > 0;

        // Bound by the live tensor, not metadata, so a stale numDetections/numClasses can't index out of range.
        var dims = outputTensor.Dimensions;
        if (dims.Length < 3) return new List<Prediction>();
        int tensorAnchors = outputTransposed ? dims[1] : dims[2];
        int tensorChannels = outputTransposed ? dims[2] : dims[1];
        numDetections = Math.Min(numDetections, tensorAnchors);
        numClasses = Math.Min(numClasses, tensorChannels - 4);
        if (numClasses < 1) return new List<Prediction>();

        float At(int channel, int anchor) =>
            outputTransposed ? outputTensor[0, anchor, channel] : outputTensor[0, channel, anchor];

        var predictions = new List<Prediction>(Math.Min(numDetections, 64));

        for (int i = 0; i < numDetections; i++)
        {
            float xCenter = At(0, i);
            float yCenter = At(1, i);
            float width = At(2, i);
            float height = At(3, i);

            int bestClassId = 0;
            float bestConfidence;

            if (numClasses <= 1)
            {
                // Legacy fast path: channel 4 carries objectness directly.
                bestConfidence = At(4, i);
            }
            else
            {
                bestConfidence = 0f;
                for (int classId = 0; classId < numClasses; classId++)
                {
                    float c = At(4 + classId, i);
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

            // Ignore-region mask: drop detections whose centre falls inside any active mask. The
            // user uses these to suppress YOLO false-positives on HUDs (ammo / minimap / kill-feed).
            if (maskFilter)
            {
                bool masked = false;
                for (int m = 0; m < ignoreRegions!.Count; m++)
                {
                    if (ignoreRegions[m].Contains(xCenter, yCenter, imageSize))
                    {
                        masked = true;
                        break;
                    }
                }
                if (masked) continue;
            }

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
