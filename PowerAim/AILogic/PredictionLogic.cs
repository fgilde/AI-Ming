using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using System.Drawing;
using System.IO;
using System.Windows;
using PowerAim.AILogic.Contracts;
using PowerAim.Extensions;
using Visuality;
using Other;
using PowerAim.Config;
using PowerAim;
using Supercluster.KDTree;

namespace PowerAim.AILogic;

/// <summary>
///     ONNX inference pipeline. Detects the model's input resolution and class count at load time,
///     so the same code path now supports both fixed-shape YOLOv8 exports (e.g. 1×5×8400 for a
///     single-class 640px model) and dynamic-axis exports plus multi-class models
/// </summary>
public class PredictionLogic : IPredictionLogic
{
    /// <summary>
    ///     Last instance's negotiated image size — mirrored as a static so legacy call sites
    ///     (<c>AIManager.cs</c> capture box, <c>Prediction.IsIntersectingCenter</c>) can keep
    ///     using <c>PredictionLogic.CurrentImageSize</c> without taking a hard dependency on the
    ///     instance. Falls back to <see cref="DefaultImageSize"/> until a model is loaded.
    /// </summary>
    public static int CurrentImageSize { get; private set; } = DefaultImageSize;

    /// <summary>Conventional YOLOv8 default before any model is loaded.</summary>
    public const int DefaultImageSize = 640;

    // Preserved for backwards compatibility with any external code still reading the old constant.
    // Surfaces the same value as <see cref="CurrentImageSize"/>.
    public static int IMAGE_SIZE => CurrentImageSize;

    private DateTime lastSavedTime = DateTime.MinValue;
    private InferenceSession? _onnxModel;
    private List<string> _outputNames = new();
    private readonly RunOptions? _modeloptions = new();

    private int _imageSize = DefaultImageSize;
    private int _numDetections = MathUtil.CalculateNumDetections(DefaultImageSize);
    private int _numClasses = 1;
    private bool _isDynamicModel;
    private readonly Dictionary<int, string> _modelClasses = new() { { 0, "Enemy" } };

    public OnnxExecutionProvider ExecutionProvider { get; private set; }

    /// <inheritdoc />
    public int ImageSize => _imageSize;

    /// <inheritdoc />
    public int NumClasses => _numClasses;

    /// <inheritdoc />
    public IReadOnlyDictionary<int, string> ModelClasses => _modelClasses;

    /// <summary>
    ///     Indicates that the loaded model declares symbolic input dimensions and therefore relies
    ///     on the configured <see cref="SliderSettings.ImageSize"/> value at runtime.
    /// </summary>
    public bool IsDynamicModel => _isDynamicModel;

    public PredictionLogic(string modelPath, SessionOptions? sessionOptions = null)
    {
        InitializeModel(sessionOptions, modelPath);
    }

    private void InitializeModel(SessionOptions? sessionOptions, string modelPath)
    {
        // Try CUDA first (preferred), then the OnnxHelper fallback chain inside the factory will
        // walk down to DirectML / CPU. If CUDA negotiation itself throws we retry once with
        // DirectML as the explicit preference so we don't spuriously fail on AMD machines.
        try
        {
            LoadModel(sessionOptions, modelPath, OnnxExecutionProvider.Cuda);
        }
        catch (Exception cudaEx)
        {
            try
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    new NoticeBar(string.Format(Locale.CudaLoadFailedFormat, cudaEx.Message), 4000).Show()));
                LoadModel(sessionOptions, modelPath, OnnxExecutionProvider.DirectML);
            }
            catch (Exception dmlEx)
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    new NoticeBar(string.Format(Locale.ErrorStartingModelFormat, dmlEx.Message), 5000).Show()));
                _onnxModel?.Dispose();
                _onnxModel = null;
            }
        }
        FileManager.CurrentlyLoadingModel = false;
    }

    private void LoadModel(SessionOptions? sessionOptions, string modelPath, OnnxExecutionProvider provider)
    {
        // Dispose any prior session so model-switching at runtime doesn't leak ORT handles.
        _onnxModel?.Dispose();
        _onnxModel = null;

        // Read the user-selected GPU adapter (default 0 = primary). Lets users push inference onto
        // a secondary card so the game's GPU isn't bottlenecked by detection workloads.
        int deviceId = AppConfig.Current?.AISettings?.InferenceGpuDeviceId ?? 0;
        var loaded = OnnxModelSessionFactory.Load(modelPath, provider, sessionOptions, deviceId);
        _onnxModel = loaded.Session;
        _outputNames = loaded.OutputNames;
        ExecutionProvider = loaded.ExecutionProvider;
        _isDynamicModel = loaded.IsDynamicInput;

        // Determine the runtime image size: prefer model metadata for fixed models; use the
        // configured override (SliderSettings.ImageSize) for dynamic-axis ONNX models.
        int configuredSize = AppConfig.Current?.SliderSettings?.ImageSize ?? DefaultImageSize;
        int detected = loaded.InputImageSize;
        _imageSize = (detected > 0 && !_isDynamicModel) ? detected : (configuredSize > 0 ? configuredSize : DefaultImageSize);
        CurrentImageSize = _imageSize;

        // Keep the persisted config in sync when we auto-adopted a fixed model size, so the
        // capture path (which sizes the detection box from the config / static IMAGE_SIZE) and
        // the AI both agree.
        if (!_isDynamicModel && detected > 0 && AppConfig.Current?.SliderSettings != null
            && AppConfig.Current.SliderSettings.ImageSize != detected)
        {
            AppConfig.Current.SliderSettings.ImageSize = detected;
        }

        LoadClasses();

        _numDetections = MathUtil.CalculateNumDetections(_imageSize);

        ValidateOnnxShape();
    }

    /// <summary>
    ///     Parse the YOLOv8 <c>names</c> custom metadata into <see cref="_modelClasses"/> and
    ///     update <see cref="NumClasses"/>. Falls back to the legacy single-class
    ///     <c>{ 0: "Enemy" }</c> table when the metadata is absent or malformed.
    /// </summary>
    private void LoadClasses()
    {
        if (_onnxModel == null) return;

        _modelClasses.Clear();

        try
        {
            var metadata = _onnxModel.ModelMetadata;
            if (metadata != null
                && metadata.CustomMetadataMap.TryGetValue("names", out string? value)
                && !string.IsNullOrEmpty(value))
            {
                JObject data = JObject.Parse(value);
                foreach (var item in data)
                {
                    if (int.TryParse(item.Key, out int classId) && item.Value?.Type == JTokenType.String)
                    {
                        _modelClasses[classId] = item.Value.ToString();
                    }
                }
            }
        }
        catch
        {
            // Ignore — model just doesn't expose a class table. We'll treat it as single-class.
        }

        if (_modelClasses.Count == 0)
        {
            _modelClasses[0] = "Enemy";
            _numClasses = 1;
        }
        else
        {
            _numClasses = _modelClasses.Keys.Max() + 1;
        }
    }

    /// <summary>
    ///     Validates that the model's output tensor matches what the parser expects. Fixed models
    ///     are required to declare <c>[1, 4+numClasses, numDetections]</c>; dynamic models are
    ///     accepted unconditionally because their shape is only known at run time.
    /// </summary>
    private void ValidateOnnxShape()
    {
        if (_onnxModel == null) return;
        if (_isDynamicModel) return;

        int[] expectedShape = [1, 4 + _numClasses, _numDetections];
        var outputMetadata = _onnxModel.OutputMetadata;
        bool ok = outputMetadata.Values.All(metadata => metadata.Dimensions.SequenceEqual(expectedShape));
        if (!ok)
        {
            // Permit the legacy single-class shape [1,5,N] as well, even if our derived numClasses
            // came back as 1 (the formula yields the same expected shape — this branch is for
            // models whose metadata advertises a different anchor count we didn't predict).
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                new NoticeBar(
                        string.Format(Locale.OutputShapeMismatchFormat, string.Join("x", expectedShape)),
                        15000)
                    .Show()
            ));
        }
    }


    public Task<Prediction[]> Predict(Bitmap frame, Rectangle detectionBox)
    {
        return Task.Run(() =>
        {
            // Return ALL detections (nearest-to-centre first), capped for sanity. The old value of
            // 1 meant every consumer — overlay/ESP, triggers, AutoPlay context AND the aim — only
            // ever saw the single closest box. The smart-aim tracker needs every detection to keep
            // stable per-target identities, and ESP showing all enemies is the expected behaviour.
            int maxResultCount = 64;

            if (frame == null || _onnxModel == null) return Array.Empty<Prediction>();

            // Dynamic models may need to re-fit if the user changed the image size at runtime.
            int configuredSize = AppConfig.Current?.SliderSettings?.ImageSize ?? _imageSize;
            if (_isDynamicModel && configuredSize > 0 && configuredSize != _imageSize)
            {
                _imageSize = configuredSize;
                _numDetections = MathUtil.CalculateNumDetections(_imageSize);
                CurrentImageSize = _imageSize;
            }

            // The captured patch is FOV-sized (see AIManager) and may differ from the model input
            // resolution (_imageSize). Downscale/upscale it to a square _imageSize bitmap before
            // building the tensor. When they're equal (the default — FOV == model input) we skip
            // the resize entirely and feed the captured frame as-is, identical to before.
            int captureSize = frame.Width;
            Bitmap modelFrame = frame;
            bool resizedFrame = false;
            if (frame.Width != _imageSize || frame.Height != _imageSize)
            {
                modelFrame = new Bitmap(_imageSize, _imageSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(modelFrame))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(frame, new Rectangle(0, 0, _imageSize, _imageSize));
                }
                resizedFrame = true;
            }

            float[] inputArray = modelFrame.ToFloatArray();

            Tensor<float> inputTensor = new DenseTensor<float>(inputArray, [1, 3, modelFrame.Height, modelFrame.Width]);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };
            var results = _onnxModel.Run(inputs, _outputNames, _modeloptions);
            if (resizedFrame) modelFrame.Dispose();

            var outputTensor = results[0].AsTensor<float>();

            // FOV now sizes the capture region itself, so the whole captured frame already IS the
            // field of view — no additional in-frame clipping. Accept the full model-space extent.
            float fovMinX = 0f;
            float fovMaxX = _imageSize;
            float fovMinY = 0f;
            float fovMaxY = _imageSize;

            float minConfidence = (float)AppConfig.Current.SliderSettings.AIMinimumConfidence / 100.0f;
            IReadOnlyCollection<int>? allowedClassIds = ResolveAllowedClassIds();
            IReadOnlyList<DetectionMaskRegion>? ignoreRegions = ResolveIgnoreRegions();

            var filtered = PredictionFilter.CreatePredictions(
                outputTensor,
                detectionBox,
                _imageSize,
                _numDetections,
                _numClasses,
                _modelClasses,
                minConfidence,
                allowedClassIds,
                fovMinX, fovMaxX, fovMinY, fovMaxY,
                ignoreRegions);

            if (filtered.Count == 0)
            {
                return [];
            }

            var (kdPoints, kdPredictions) = PrepareKDTreeData(filtered);
            if (kdPoints.Count == 0 || kdPredictions.Count == 0)
            {
                return [];
            }

            var tree = new KDTree<double, Prediction>(2, kdPoints.ToArray(), kdPredictions.ToArray(), Normalizer.SquaredDouble);

            double[] centerPoint = [_imageSize / 2.0, _imageSize / 2.0];
            var allNearest = tree.NearestNeighbors(centerPoint, Math.Min(kdPredictions.Count, maxResultCount)).Select(n => n.Item2).ToArray();

            // prediction.Rectangle is in model space (0.._imageSize). The captured region is
            // captureSize wide, so scale model→capture pixels before offsetting by the box origin
            // to land on absolute screen coords. scale == 1 when FOV == model input (the default).
            float captureScale = captureSize / (float)_imageSize;
            foreach (var prediction in allNearest)
            {
                float translatedXMin = prediction.Rectangle.X * captureScale + detectionBox.Left;
                float translatedYMin = prediction.Rectangle.Y * captureScale + detectionBox.Top;
                prediction.TranslatedRectangle = new RectangleF(translatedXMin, translatedYMin,
                    prediction.Rectangle.Width * captureScale, prediction.Rectangle.Height * captureScale);
            }

            // Replay capture only for the primary (nearest-to-centre) target — saving every box
            // each tick would hammer the replay buffer now that we return the whole detection set.
            if (allNearest.Length > 0) _ = SaveFrameAsync(frame, allNearest[0]);

            return allNearest;
        });
    }

    /// <summary>
    ///     Read the AI settings and return either <c>null</c> (accept every class) or a snapshot of
    ///     the allow-list. A snapshot is taken so the hot path doesn't race with config edits.
    /// </summary>
    private static IReadOnlyCollection<int>? ResolveAllowedClassIds()
    {
        var ai = AppConfig.Current?.AISettings;
        if (ai == null || ai.TargetClassFilterMode == TargetClassFilterMode.AllClasses) return null;
        if (ai.TargetClassIds == null || ai.TargetClassIds.Count == 0) return null;
        return ai.TargetClassIds.ToArray();
    }

    /// <summary>
    ///     Snapshot the enabled ignore-regions so the hot path doesn't race with UI edits. Returns
    ///     <c>null</c> when no enabled regions exist, which keeps the parser on its fast path.
    /// </summary>
    private static IReadOnlyList<DetectionMaskRegion>? ResolveIgnoreRegions()
    {
        var ai = AppConfig.Current?.AISettings;
        if (ai?.IgnoreRegions == null || ai.IgnoreRegions.Count == 0) return null;
        var snapshot = new List<DetectionMaskRegion>(ai.IgnoreRegions.Count);
        foreach (var r in ai.IgnoreRegions)
        {
            if (r != null && r.Enabled) snapshot.Add(r);
        }
        return snapshot.Count == 0 ? null : snapshot;
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

                            // YOLO label format: <class-id> <x> <y> <w> <h>
                            File.WriteAllText(labelPath, $"{DoLabel.ClassId} {x} {y} {width} {height}");
                        }
                    });
                }
                catch (Exception e)
                {
                    new NoticeBar(string.Format(Locale.CollectDataFailedFormat, e.Message), 6000).Show();
                }
            }
        }
    }

    private static (List<double[]>, List<Prediction>) PrepareKDTreeData(List<Prediction> predictions)
    {
        var kdPoints = new List<double[]>(predictions.Count);
        foreach (var p in predictions)
        {
            float xCenter = p.Rectangle.X + p.Rectangle.Width / 2f;
            float yCenter = p.Rectangle.Y + p.Rectangle.Height / 2f;
            kdPoints.Add([xCenter, yCenter]);
        }
        return (kdPoints, predictions);
    }
}
