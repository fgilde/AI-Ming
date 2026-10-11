using System.Drawing;
using Microsoft.ML.OnnxRuntime.Tensors;
using PowerAim.Config;

namespace PowerAim.AI;

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
        bool outputTransposed = false)
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

        // Auto-correct layout if the caller guessed wrong but the shape is unambiguous.
        if (!outputTransposed && cFromTensor > nFromTensor && nFromTensor > 4 && nFromTensor < 512)
        {
            outputTransposed = true;
            (nFromTensor, cFromTensor) = (cFromTensor, nFromTensor);
        }
        else if (outputTransposed && cFromTensor > nFromTensor && nFromTensor > 4 && nFromTensor < 512)
        {
            // Already consistent.
        }

        int safeN = Math.Min(numDetections > 0 ? numDetections : nFromTensor, nFromTensor);
        if (safeN <= 0) return new List<Prediction>();

        int channels = Math.Max(cFromTensor, 5);
        int safeNc = Math.Min(Math.Max(numClasses, 1), Math.Max(1, channels - 4));

        var predictions = new List<Prediction>(Math.Min(safeN, 64));

        for (int i = 0; i < safeN; i++)
        {
            float xCenter, yCenter, width, height, bestConfidence;
            int bestClassId = 0;

            if (outputTransposed)
            {
                // [batch, anchor, channel]
                xCenter = outputTensor[0, i, 0];
                yCenter = outputTensor[0, i, 1];
                width = outputTensor[0, i, 2];
                height = outputTensor[0, i, 3];
                if (safeNc <= 1)
                {
                    bestConfidence = channels > 4 ? outputTensor[0, i, 4] : 0f;
                }
                else
                {
                    bestConfidence = 0f;
                    for (int classId = 0; classId < safeNc; classId++)
                    {
                        int ch = 4 + classId;
                        if (ch >= channels) break;
                        float c = outputTensor[0, i, ch];
                        if (c > bestConfidence)
                        {
                            bestConfidence = c;
                            bestClassId = classId;
                        }
                    }
                }
            }
            else
            {
                // [batch, channel, anchor]
                xCenter = outputTensor[0, 0, i];
                yCenter = outputTensor[0, 1, i];
                width = outputTensor[0, 2, i];
                height = outputTensor[0, 3, i];
                if (safeNc <= 1)
                {
                    bestConfidence = channels > 4 ? outputTensor[0, 4, i] : 0f;
                }
                else
                {
                    bestConfidence = 0f;
                    for (int classId = 0; classId < safeNc; classId++)
                    {
                        int ch = 4 + classId;
                        if (ch >= channels) break;
                        float c = outputTensor[0, ch, i];
                        if (c > bestConfidence)
                        {
                            bestConfidence = c;
                            bestClassId = classId;
                        }
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