using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Drawing;
using System.IO;
using System.Windows;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Extensions;
using Visuality;
using Other;
using Aimmy2.Config;
using Supercluster.KDTree;

namespace Aimmy2.AILogic;


public class PredictionLogic : IPredictionLogic
{
    private DateTime lastSavedTime = DateTime.MinValue;
    public const int IMAGE_SIZE = 640;
    private const int NUM_DETECTIONS = 8400; // Standard for OnnxV8 model (Shape: 1x5x8400)
    private InferenceSession _onnxModel;
    private List<string> _outputNames;
    private readonly RunOptions? _modeloptions = new();

    public OnnxExecutionProvider ExecutionProvider { get; private set; }

    public PredictionLogic(string modelPath, SessionOptions? sessionOptions = null)
    {
        InitializeModel(sessionOptions ?? OnnxHelper.CreateDefaultSessionOptions(), modelPath);
    }

    private void InitializeModel(SessionOptions sessionOptions, string modelPath)
    {
        LoadModel(sessionOptions, modelPath, OnnxExecutionProvider.Cuda);
        FileManager.CurrentlyLoadingModel = false;
    }

    private void LoadModel(SessionOptions sessionOptions, string modelPath, OnnxExecutionProvider provider)
    {
        try
        {
            ExecutionProvider = sessionOptions.SetExecutionProvider(provider);

            _onnxModel = new InferenceSession(modelPath, sessionOptions);
            _outputNames = [.._onnxModel.OutputMetadata.Keys];

            // Validate the onnx model output shape (ensure model is OnnxV8)
            ValidateOnnxShape();
        }
        catch (Exception ex)
        {
            _= Application.Current.Dispatcher.BeginInvoke(new Action(() => new NoticeBar($"Error starting the model: {ex.Message}", 5000).Show()));
            _onnxModel?.Dispose();
        }
    }

    private void ValidateOnnxShape()
    {
        var expectedShape = new int[] { 1, 5, NUM_DETECTIONS };
        if (_onnxModel != null)
        {
            var outputMetadata = _onnxModel.OutputMetadata;
            if (!outputMetadata.Values.All(metadata => metadata.Dimensions.SequenceEqual(expectedShape)))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    new NoticeBar(
                            $"Output shape does not match the expected shape of {string.Join("x", expectedShape)}.\n\nThis model will not work with Aimmy, please use an YOLOv8 model converted to ONNXv8."
                            , 15000)
                        .Show()
                ));
            }
        }
    }


    public Task<Prediction[]> Predict(Bitmap frame, Rectangle detectionBox)
    {
        return Task.Run(() =>
        {
            int maxResultCount = 1;

            if (frame == null || _onnxModel == null) return [];

            float[] inputArray = frame.ToFloatArray();

            Tensor<float> inputTensor = new DenseTensor<float>(inputArray, [1, 3, frame.Height, frame.Width]);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };
            var results = _onnxModel.Run(inputs, _outputNames, _modeloptions);

            var outputTensor = results[0].AsTensor<float>();

            float FovSize = (float)AppConfig.Current.SliderSettings.ActualFovSize;
            float fovMinX = (IMAGE_SIZE - FovSize) / 2.0f;
            float fovMaxX = (IMAGE_SIZE + FovSize) / 2.0f;
            float fovMinY = (IMAGE_SIZE - FovSize) / 2.0f;
            float fovMaxY = (IMAGE_SIZE + FovSize) / 2.0f;

            var (kdPoints, kdPredictions) = PrepareKDTreeData(outputTensor, detectionBox, fovMinX, fovMaxX, fovMinY, fovMaxY);

            if (kdPoints.Count == 0 || kdPredictions.Count == 0)
            {
                return [];
            }

            var tree = new KDTree<double, Prediction>(2, kdPoints.ToArray(), kdPredictions.ToArray(), Normalizer.SquaredDouble);

            var centerPoint = new[] { IMAGE_SIZE / 2.0, IMAGE_SIZE / 2.0 };
            var allNearest = tree.NearestNeighbors(centerPoint, Math.Min(kdPredictions.Count, maxResultCount)).Select(n => n.Item2).ToArray();

            foreach (var prediction in allNearest)
            {
                float translatedXMin = prediction.Rectangle.X + detectionBox.Left;
                float translatedYMin = prediction.Rectangle.Y + detectionBox.Top;
                prediction.TranslatedRectangle = new RectangleF(translatedXMin, translatedYMin, prediction.Rectangle.Width, prediction.Rectangle.Height);

                _ = SaveFrameAsync(frame, prediction);
            }

            return allNearest;
        });
    }


    private async Task SaveFrameAsync(Bitmap frame, Prediction? DoLabel)
    {
        if (AppConfig.Current.ToggleState.CollectDataWhilePlaying)
        {
            if ((DateTime.Now - lastSavedTime).TotalMilliseconds >= 500)
            {
                lastSavedTime = DateTime.Now;
                string uuid = Guid.NewGuid().ToString();

                try
                {
                    await Task.Run(() =>
                    {
                        frame.Save(Path.Combine("bin", "images", $"{uuid}.jpg"));

                        if (AppConfig.Current.ToggleState.AutoLabelData && DoLabel != null)
                        {
                            var labelPath = Path.Combine("bin", "labels", $"{uuid}.txt");

                            float x = (DoLabel!.Rectangle.X + DoLabel.Rectangle.Width / 2) / frame.Width;
                            float y = (DoLabel!.Rectangle.Y + DoLabel.Rectangle.Height / 2) / frame.Height;
                            float width = DoLabel.Rectangle.Width / frame.Width;
                            float height = DoLabel.Rectangle.Height / frame.Height;

                            File.WriteAllText(labelPath, $"0 {x} {y} {width} {height}");
                        }
                    });
                }
                catch (Exception e)
                {
                    new NoticeBar($"Collect Data isn't working, try again later. {e.Message}", 6000).Show();
                }
            }
        }
    }

    private (List<double[]>, List<Prediction>) PrepareKDTreeData(Tensor<float> outputTensor, Rectangle detectionBox, float fovMinX, float fovMaxX, float fovMinY, float fovMaxY)
    {
        float minConfidence = (float)AppConfig.Current.SliderSettings.AIMinimumConfidence / 100.0f; // Pre-compute minimum confidence

        var KDpoints = new List<double[]>();
        var KDpredictions = new List<Prediction>();

        for (int i = 0; i < NUM_DETECTIONS; i++)
        {
            float objectness = outputTensor[0, 4, i];
            if (objectness < minConfidence) continue;

            float x_center = outputTensor[0, 0, i];
            float y_center = outputTensor[0, 1, i];
            float width = outputTensor[0, 2, i];
            float height = outputTensor[0, 3, i];

            float x_min = x_center - width / 2;
            float y_min = y_center - height / 2;
            float x_max = x_center + width / 2;
            float y_max = y_center + height / 2;

            if (x_min < fovMinX || x_max > fovMaxX || y_min < fovMinY || y_max > fovMaxY) continue;

            RectangleF rect = new(x_min, y_min, width, height);
            Prediction prediction = new()
            {
                Rectangle = rect,
                Confidence = objectness,
                CenterXTranslated = (x_center - detectionBox.Left) / IMAGE_SIZE,
                CenterYTranslated = (y_center - detectionBox.Top) / IMAGE_SIZE
            };

            KDpoints.Add(new double[] { x_center, y_center });
            KDpredictions.Add(prediction);
        }

        return (KDpoints, KDpredictions);
    }
}