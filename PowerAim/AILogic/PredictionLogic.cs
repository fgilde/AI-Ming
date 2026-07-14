using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using System.Buffers;
using System.Drawing;
using System.IO;
using System.Windows;
using PowerAim.AILogic.Contracts;
using PowerAim.Extensions;
using PowerAim.Visuality;
using PowerAim.Other;
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
    private string _inputName = "images"; // the model's real input name, read at load (not all are "images")
    private readonly RunOptions? _modeloptions = new();

    private int _imageSize = DefaultImageSize;
    private int _numDetections = MathUtil.CalculateNumDetections(DefaultImageSize);

    // Reused across frames so a FOV ≠ model-input resolution doesn't allocate a fresh Bitmap + Graphics
    // every frame. Predict runs sequentially (the AI loop awaits it), so a single instance is safe.
    private Bitmap? _resizeBuffer;
    private int _numClasses = 1;
    private bool _isDynamicModel;
    private readonly Dictionary<int, string> _modelClasses = new() { { 0, "Enemy" } };

    public OnnxExecutionProvider ExecutionProvider { get; private set; }

    /// <inheritdoc />
    public bool IsLoaded => _onnxModel != null;

    /// <inheritdoc />
    public string? LoadError { get; private set; }

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
        // Start from the user's execution-provider preference (Auto = the full TensorRT → CUDA →
        // DirectML → CPU chain, which self-filters to whatever this build/machine can actually run).
        // The chain inside the factory already falls back, so the outer retry here is just a safety net
        // for a hard negotiation failure — retry once from DirectML so an AMD box never dead-ends.
        var preferred = MapPreference(AppConfig.Current?.AISettings?.PreferredExecutionProvider
                                      ?? ExecutionProviderPreference.Auto);
        try
        {
            LoadModel(sessionOptions, modelPath, preferred);
            LoadError = null;
        }
        catch (Exception firstEx)
        {
            // Primary provider failed (e.g. CUDA / TensorRT engine build) — surface it and retry once
            // from DirectML so an AMD / iGPU box never dead-ends. Never fail silently.
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                new NoticeBar(string.Format(Locale.CudaLoadFailedFormat, firstEx.Message), 4000).Show()));
            try
            {
                LoadModel(sessionOptions, modelPath, OnnxExecutionProvider.DirectML);
                LoadError = null;
            }
            catch (Exception dmlEx)
            {
                LoadError = dmlEx.Message;
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    new NoticeBar(string.Format(Locale.ErrorStartingModelFormat, dmlEx.Message), 6000).Show()));
                _onnxModel?.Dispose();
                _onnxModel = null;
            }
        }
        // NOTE: FileManager.CurrentlyLoadingModel is intentionally NOT reset here — FileManager owns that
        // flag in a finally block so it is cleared even if construction throws before reaching this point.
    }

    /// <summary>Map the user preference to the chain entry point. Auto enters at TensorRT so the chain
    /// tries every accelerator top-down; unavailable ones are skipped (gated / CanWork).</summary>
    private static OnnxExecutionProvider MapPreference(ExecutionProviderPreference pref) => pref switch
    {
        ExecutionProviderPreference.Cuda => OnnxExecutionProvider.Cuda,
        ExecutionProviderPreference.Tensorrt => OnnxExecutionProvider.TensorRT,
        ExecutionProviderPreference.DirectML => OnnxExecutionProvider.DirectML,
        ExecutionProviderPreference.Cpu => OnnxExecutionProvider.Cpu,
        _ => OnnxExecutionProvider.TensorRT, // Auto → full chain
    };

    private void LoadModel(SessionOptions? sessionOptions, string modelPath, OnnxExecutionProvider provider)
    {
        // Dispose any prior session so model-switching at runtime doesn't leak ORT handles.
        _onnxModel?.Dispose();
        _onnxModel = null;

        // Make a user-installed TensorRT runtime (dropped into LocalRuntimeDir by the setup wizard)
        // discoverable to ORT's TensorRT provider before we build the session.
        TensorRtRuntime.EnsureOnPath();

        // Read the user-selected GPU adapter (default 0 = primary). Lets users push inference onto
        // a secondary card so the game's GPU isn't bottlenecked by detection workloads.
        int deviceId = AppConfig.Current?.AISettings?.InferenceGpuDeviceId ?? 0;

        // Precision. Two distinct FP16 paths with deliberately different defaults:
        //  • TensorRT applies FP16 safely (it profiles kernels and only uses half where it's a win), so
        //    Auto lets TensorRT run FP16.
        //  • CUDA/DirectML get FP16 only by loading an actual FP16 model, and that is a REGRESSION on
        //    older GPUs (Pascal / pre-RDNA) whose FP16 throughput is a fraction of FP32. So we swap to
        //    the FP16 model file ONLY on an explicit FP16 choice — never in Auto — to avoid making it
        //    slower on hardware without real FP16 acceleration.
        var precision = AppConfig.Current?.AISettings?.Precision ?? ModelPrecision.Auto;
        bool preferFp16Trt = precision != ModelPrecision.Fp32;   // TensorRT fp16 (Auto + Fp16)
        bool useFp16Model = precision == ModelPrecision.Fp16;     // model swap only when explicitly chosen
        string effectiveModelPath = ResolveModelPath(modelPath, useFp16Model);
        string cacheDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerAim", "trt-cache");

        // Heads-up before the (potentially long, silent) TensorRT engine build, so the user doesn't
        // think the app froze — the first build for a given model can take a couple of minutes; it's
        // cached afterwards. Only when TensorRT is actually being attempted and its runtime is present.
        if (provider == OnnxExecutionProvider.TensorRT && TensorRtRuntime.IsAvailable())
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                new NoticeBar(Locale.TensorRtBuilding, 8000).Show()));
        }

        var loaded = OnnxModelSessionFactory.Load(effectiveModelPath, provider, sessionOptions, deviceId, preferFp16Trt, cacheDir);
        _onnxModel = loaded.Session;
        _outputNames = loaded.OutputNames;
        _inputName = loaded.InputName;
        ExecutionProvider = loaded.ExecutionProvider;
        _isDynamicModel = loaded.IsDynamicInput;

        // Transparency: if we didn't get the requested provider (e.g. TensorRT silently fell back to
        // CUDA because nvinfer isn't installed, or DirectML dropped to CPU), say so — but stay quiet for
        // an expected Auto fallback that still landed on a GPU.
        if (!string.IsNullOrEmpty(loaded.Diagnostic))
        {
            var pref = AppConfig.Current?.AISettings?.PreferredExecutionProvider ?? ExecutionProviderPreference.Auto;
            if (pref != ExecutionProviderPreference.Auto || ExecutionProvider == OnnxExecutionProvider.Cpu)
            {
                var extra = "";
                if (ExecutionProvider == OnnxExecutionProvider.Cpu)
                {
                    // A CPU fallback is the slow / jittery case (issues #12 & #20). Append a native-DLL
                    // presence probe (so a missing native self-diagnoses) plus the concrete remedy for
                    // THIS build variant.
                    extra = "  " + NativePresenceHint() + "  → "
                          + (ApplicationConstants.IsCudaBuild ? Locale.CpuFallbackHintCuda : Locale.CpuFallbackHintDirectMl);
                }
                var msg = string.Format(Locale.ProviderFallbackFormat, loaded.Diagnostic + extra);
                Application.Current?.Dispatcher.BeginInvoke(new Action(() => new NoticeBar(msg, 12000).Show()));
            }
        }

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
        // Trust the model's ACTUAL output tensor over counts derived from class metadata / image size,
        // so multi-class and non-640 models parse correctly instead of only warning.
        AdaptToOutputShape();

        ValidateOnnxShape();
    }

    /// <summary>
    ///     For a fixed-shape model, adopt the class count and anchor count straight from the model's
    ///     output tensor. A YOLOv8 detection head is channels-first <c>[1, 4+numClasses, numDetections]</c>
    ///     (e.g. <c>[1,5,8400]</c> single-class, <c>[1,6,8400]</c> two-class). Reading them from the model
    ///     means a multi-class model, or one exported at a non-640 resolution, "just works" without
    ///     relying on a <c>names</c> table or the configured image size lining up. Layouts we can't parse
    ///     (transposed <c>[1,N,C]</c> or NMS-fused outputs) are left for <see cref="ValidateOnnxShape"/> to
    ///     flag clearly.
    /// </summary>
    private void AdaptToOutputShape()
    {
        if (_onnxModel == null || _isDynamicModel) return;
        foreach (var meta in _onnxModel.OutputMetadata.Values)
        {
            var d = meta.Dimensions;
            // Channels-first detection head: [1, C, N], small channel count C, larger anchor count N.
            if (d.Length == 3 && d[0] == 1 && d[1] > 4 && d[2] > d[1])
            {
                _numClasses = d[1] - 4;
                _numDetections = d[2];
                return;
            }
        }
    }

    /// <summary>
    ///     Pick a "&lt;name&gt;.fp16.onnx" sibling when FP16 is wanted and one exists. Such a model must be
    ///     converted with FP32 I/O kept (keep_io_types) so the existing float preprocessing feeds it
    ///     unchanged — only the internal compute is half precision. Falls back to the original path.
    /// </summary>
    private static string ResolveModelPath(string modelPath, bool preferFp16)
    {
        if (!preferFp16 || string.IsNullOrWhiteSpace(modelPath)) return modelPath;
        try
        {
            var dir = Path.GetDirectoryName(modelPath) ?? "";
            var name = Path.GetFileNameWithoutExtension(modelPath);
            var ext = Path.GetExtension(modelPath);
            var fp16 = Path.Combine(dir, name + ".fp16" + ext);
            if (File.Exists(fp16)) return fp16;
        }
        catch { /* fall back to the original path */ }
        return modelPath;
    }

    /// <summary>
    ///     Presence probe for the native runtime DLLs next to the exe, appended to the provider-fallback
    ///     notice so a "dropped to CPU" is instantly diagnosable: a missing native (typical of a
    ///     self-contained single-file publish that didn't lay the DLL down) vs. a runtime/config failure.
    /// </summary>
    private static string NativePresenceHint()
    {
        try
        {
            string dir = AppContext.BaseDirectory;
            string Mark(string n) => File.Exists(Path.Combine(dir, n)) ? "✓" : "✗";
            return $"[DirectML.dll {Mark("DirectML.dll")}, onnxruntime.dll {Mark("onnxruntime.dll")}, providers_shared {Mark("onnxruntime_providers_shared.dll")}]";
        }
        catch { return ""; }
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
        if (_onnxModel == null || _isDynamicModel) return;

        int[] expectedShape = [1, 4 + _numClasses, _numDetections];
        var outputs = _onnxModel.OutputMetadata.Values;
        if (outputs.All(m => m.Dimensions.SequenceEqual(expectedShape))) return;

        // AdaptToOutputShape() already adopts any channels-first [1, 4+nc, N] head, so reaching here means
        // a layout the parser can't read (transposed [1, N, C], NMS-fused, or a mismatched input size).
        // Report the ACTUAL vs expected shape so the user knows to re-export — instead of silently
        // producing wrong / empty detections.
        string actual = string.Join(" ", outputs.Select(m => "[" + string.Join("x", m.Dimensions) + "]"));
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            new NoticeBar(
                string.Format(Locale.OutputShapeMismatchFormat, string.Join("x", expectedShape), actual),
                15000).Show()));
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
            if (frame.Width != _imageSize || frame.Height != _imageSize)
            {
                // Reuse a persistent resize buffer instead of allocating a Bitmap + Graphics per frame.
                if (_resizeBuffer == null || _resizeBuffer.Width != _imageSize || _resizeBuffer.Height != _imageSize)
                {
                    _resizeBuffer?.Dispose();
                    _resizeBuffer = new Bitmap(_imageSize, _imageSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                }
                using (var g = Graphics.FromImage(_resizeBuffer))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(frame, new Rectangle(0, 0, _imageSize, _imageSize));
                }
                modelFrame = _resizeBuffer; // reused → must NOT be disposed
            }

            // Build the input tensor from a pooled buffer for the square/32bpp fast path so the ~4.9 MB
            // (640²·3) float array isn't allocated + GC'd every frame. Returned right after Run(), which
            // is synchronous, so ONNX is done reading it by then. Non-square/legacy pixel formats keep
            // the plain allocating path.
            int w = modelFrame.Width, h = modelFrame.Height;
            int tensorLen = 3 * w * h;
            float[]? rented = null;
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;
            try
            {
                Tensor<float> inputTensor;
                var pf = modelFrame.PixelFormat;
                if (w == h && pf is System.Drawing.Imaging.PixelFormat.Format32bppArgb
                        or System.Drawing.Imaging.PixelFormat.Format32bppPArgb
                        or System.Drawing.Imaging.PixelFormat.Format32bppRgb)
                {
                    rented = ArrayPool<float>.Shared.Rent(tensorLen);
                    modelFrame.ToFloatArrayInto(rented);
                    inputTensor = new DenseTensor<float>(rented.AsMemory(0, tensorLen), [1, 3, h, w]);
                }
                else
                {
                    inputTensor = new DenseTensor<float>(modelFrame.ToFloatArray(), [1, 3, h, w]);
                }

                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, inputTensor) };
                results = _onnxModel.Run(inputs, _outputNames, _modeloptions);
            }
            finally
            {
                // Run is fully synchronous, so ORT is done reading the input buffer here.
                if (rented != null) ArrayPool<float>.Shared.Return(rented);
            }
            // Free the native ONNX output buffers deterministically at end-of-frame instead of leaving
            // them to the finalizer (they were never disposed before).
            using var onnxResults = results;

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
