using System.Drawing;
using Microsoft.ML.OnnxRuntime.Tensors;
using PowerAim.Config;

namespace PowerAim.AI;

/// <summary>How a detection model lays out its main output tensor.</summary>
internal enum ModelOutputKind
{
    /// <summary><c>[1, 4+nc, N]</c> box + class scores (YOLOv8/11 style).</summary>
    YoloV8Raw,
    /// <summary><c>[1, N, 5+nc]</c> box + objectness + class scores.</summary>
    YoloV5Raw,
    /// <summary><c>[1, N, 6]</c> x1 y1 x2 y2 score class, NMS already in the graph.</summary>
    EndToEnd,
    /// <summary>Box + class scores + keypoints (x, y[, conf]) per anchor.</summary>
    Pose,
    /// <summary>Box + class scores + mask coefficients; protos in a second output.</summary>
    Segment,
}

/// <summary>Resolved output format: kind, class count and keypoint shape.</summary>
internal readonly record struct OutputFormat(ModelOutputKind Kind, int NumClasses, int KeypointCount, int KeypointDims);

/// <summary>
///     Pure parser for YOLOv8 ONNX output tensors. Supports both channels-first
///     <c>[1, 4+nc, N]</c> and ultralytics channels-last <c>[1, N, 4+nc]</c> layouts.
///     Bounds are taken from the live tensor so a wrong <c>numDetections</c> cannot IndexOutOfRange.
/// </summary>
internal static class PredictionFilter
{
    /// <param name="outputTransposed">
    ///     <c>true</c> when the tensor is <c>[1, N, C]</c> (ultralytics default export).
    ///     <c>false</c> for classic <c>[1, C, N]</c>.
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
        bool outputTransposed = false,
        ModelOutputKind kind = ModelOutputKind.YoloV8Raw,
        int keypointCount = 0,
        int keypointDims = 3,
        float ignoreMinOverlap = 0,
        bool circularFov = false)
    {
        bool classFilter = allowedClassIds != null && allowedClassIds.Count > 0;
        bool maskFilter = ignoreRegions != null && ignoreRegions.Count > 0;

        // Derive safe iteration bounds from the actual tensor — never trust metadata alone.
        var dims = outputTensor.Dimensions;
        int nFromTensor;
        int cFromTensor;
        if (dims.Length >= 3)
        {
            if (outputTransposed)
            {
                nFromTensor = dims[1];
                cFromTensor = dims[2];
            }
            else
            {
                cFromTensor = dims[1];
                nFromTensor = dims[2];
            }
        }
        else
        {
            return new List<Prediction>();
        }

        bool endToEnd = kind == ModelOutputKind.EndToEnd;
        if (endToEnd)
        {
            // End-to-end exports are always rows of x1 y1 x2 y2 score class: [1, N, 6].
            outputTransposed = true;
            nFromTensor = dims[1];
            cFromTensor = dims[2];
        }
        // Auto-correct layout if the caller guessed wrong but the shape is unambiguous.
        else if (!outputTransposed && cFromTensor > nFromTensor && nFromTensor > 4 && nFromTensor < 512)
        {
            outputTransposed = true;
            (nFromTensor, cFromTensor) = (cFromTensor, nFromTensor);
        }

        int safeN = Math.Min(numDetections > 0 ? numDetections : nFromTensor, nFromTensor);
        if (safeN <= 0) return new List<Prediction>();

        // x y w h + at least one score channel, else there is nothing to read.
        int channels = cFromTensor;
        if (channels < 5) return new List<Prediction>();
        // YOLOv5 heads carry an objectness channel before the class scores.
        int scoreStart = kind == ModelOutputKind.YoloV5Raw ? 5 : 4;
        int safeNc = Math.Min(Math.Max(numClasses, 1), Math.Max(1, channels - scoreStart));
        // Pose heads append keypointCount * keypointDims values after the class scores.
        int kpStart = scoreStart + safeNc;
        int kpCount = kind == ModelOutputKind.Pose && keypointDims >= 2
                      && kpStart + keypointCount * keypointDims <= channels ? keypointCount : 0;

        float At(int anchor, int channel) => outputTransposed
            ? outputTensor[0, anchor, channel]   // [batch, anchor, channel]
            : outputTensor[0, channel, anchor];  // [batch, channel, anchor]

        var predictions = new List<Prediction>(Math.Min(safeN, 64));

        for (int i = 0; i < safeN; i++)
        {
            float xCenter, yCenter, width, height, bestConfidence;
            int bestClassId = 0;

            if (endToEnd)
            {
                float x1 = At(i, 0), y1 = At(i, 1), x2 = At(i, 2), y2 = At(i, 3);
                width = x2 - x1;
                height = y2 - y1;
                if (width <= 0 || height <= 0) continue; // padding rows
                xCenter = x1 + width / 2f;
                yCenter = y1 + height / 2f;
                bestConfidence = At(i, 4);
                bestClassId = Math.Max(0, (int)MathF.Round(At(i, 5)));
            }
            else
            {
                xCenter = At(i, 0);
                yCenter = At(i, 1);
                width = At(i, 2);
                height = At(i, 3);
                if (safeNc <= 1)
                {
                    bestConfidence = At(i, scoreStart);
                }
                else
                {
                    bestConfidence = 0f;
                    for (int classId = 0; classId < safeNc; classId++)
                    {
                        float c = At(i, scoreStart + classId);
                        if (c > bestConfidence)
                        {
                            bestConfidence = c;
                            bestClassId = classId;
                        }
                    }
                }
                if (scoreStart == 5) bestConfidence *= At(i, 4);
            }

            if (bestConfidence < minConfidence) continue;
            if (classFilter && !allowedClassIds!.Contains(bestClassId)) continue;

            float xMin = xCenter - width / 2f;
            float yMin = yCenter - height / 2f;
            float xMax = xCenter + width / 2f;
            float yMax = yCenter + height / 2f;

            // Clip to the field of view instead of dropping: a target at the frame edge stays visible.
            xMin = Math.Max(xMin, fovMinX);
            yMin = Math.Max(yMin, fovMinY);
            xMax = Math.Min(xMax, fovMaxX);
            yMax = Math.Min(yMax, fovMaxY);
            if (xMax - xMin < 1 || yMax - yMin < 1) continue;
            xCenter = (xMin + xMax) / 2f;
            yCenter = (yMin + yMax) / 2f;
            width = xMax - xMin;
            height = yMax - yMin;

            if (circularFov)
            {
                // Only detections whose centre lies inside the circle inscribed in the frame.
                float r = Math.Min(fovMaxX - fovMinX, fovMaxY - fovMinY) / 2f;
                float dx = xCenter - (fovMinX + fovMaxX) / 2f, dy = yCenter - (fovMinY + fovMaxY) / 2f;
                if (dx * dx + dy * dy > r * r) continue;
            }

            if (maskFilter)
            {
                bool masked = false;
                for (int m = 0; m < ignoreRegions!.Count; m++)
                {
                    // The centre inside a zone always suppresses; with a minimum overlap set, enough overlap does too.
                    if (ignoreRegions[m].Contains(xCenter, yCenter, imageSize)
                        || (ignoreMinOverlap > 0
                            && ignoreRegions[m].OverlapFraction(new RectangleF(xMin, yMin, width, height), imageSize) >= ignoreMinOverlap))
                    {
                        masked = true;
                        break;
                    }
                }
                if (masked) continue;
            }

            Keypoint[]? keypoints = null;
            if (kpCount > 0)
            {
                keypoints = new Keypoint[kpCount];
                for (int k = 0; k < kpCount; k++)
                {
                    int ch = kpStart + k * keypointDims;
                    keypoints[k] = new Keypoint(At(i, ch), At(i, ch + 1), keypointDims >= 3 ? At(i, ch + 2) : 1f);
                }
            }

            predictions.Add(new Prediction
            {
                Keypoints = keypoints,
                Rectangle = new RectangleF(xMin, yMin, width, height),
                Confidence = bestConfidence,
                ClassId = bestClassId,
                ClassName = modelClasses.TryGetValue(bestClassId, out var n) ? n : $"Class_{bestClassId}",
                CenterXTranslated = xCenter / imageSize,
                CenterYTranslated = yCenter / imageSize
            });
        }

        // Class-aware NMS: suppress overlapping duplicates so aim/tracker see one box per object.
        // End-to-end models already ran NMS in the graph.
        return endToEnd ? predictions : NonMaxSuppression(predictions, iouThreshold: 0.45f);
    }

    /// <summary>
    ///     Greedy class-aware NMS. Sort by confidence descending; suppress same-class boxes with
    ///     IoU &gt;= threshold. Keeps cross-class overlaps (head vs body) intact.
    /// </summary>
    internal static List<Prediction> NonMaxSuppression(List<Prediction> predictions, float iouThreshold = 0.45f)
    {
        if (predictions.Count <= 1) return predictions;

        predictions.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
        var keep = new List<Prediction>(Math.Min(predictions.Count, 64));
        var suppressed = new bool[predictions.Count];

        for (int i = 0; i < predictions.Count; i++)
        {
            if (suppressed[i]) continue;
            var a = predictions[i];
            keep.Add(a);
            if (keep.Count >= 64) break;

            var ra = a.Rectangle;
            for (int j = i + 1; j < predictions.Count; j++)
            {
                if (suppressed[j]) continue;
                var b = predictions[j];
                if (a.ClassId != b.ClassId) continue;
                if (IoU(ra, b.Rectangle) >= iouThreshold)
                    suppressed[j] = true;
            }
        }
        return keep;
    }

    private static float IoU(RectangleF a, RectangleF b)
    {
        float x1 = Math.Max(a.Left, b.Left);
        float y1 = Math.Max(a.Top, b.Top);
        float x2 = Math.Min(a.Right, b.Right);
        float y2 = Math.Min(a.Bottom, b.Bottom);
        float inter = Math.Max(0f, x2 - x1) * Math.Max(0f, y2 - y1);
        if (inter <= 0f) return 0f;
        float uni = a.Width * a.Height + b.Width * b.Height - inter;
        return uni > 0f ? inter / uni : 0f;
    }
}