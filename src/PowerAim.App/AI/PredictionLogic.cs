using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using System.Buffers;
using System.Drawing;
using System.IO;
using System.Windows;
using PowerAim.AI.Contracts;
using PowerAim.Extensions;
using PowerAim.UI.Visuality;
using PowerAim.Other;
using PowerAim.Config;
using PowerAim;
using Supercluster.KDTree;

namespace PowerAim.AI;

/// <summary>
///     ONNX inference pipeline. Detects the model's input resolution and class count at load time,
///     so the same code path now supports both fixed-shape YOLOv8 exports (e.g. 1×5×8400 for a
///     single-class 640px model) and dynamic-axis exports plus multi-class models
/// </summary>
public class PredictionLogic : IPredictionLogic, IDisposable
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
    /// <summary>SessionOptions owned by this instance (created by the factory). Must outlive the session.</summary>
    private SessionOptions? _ownedSessionOptions;
    /// <summary>Serializes Run vs Dispose so a model-switch never tears the session under an in-flight Run.</summary>
    private readonly object _sessionGate = new();
    private int _inflightRuns;
    private string? _modelPath;
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
    /// <summary>True when model output is [1, N, C] (ultralytics export) instead of [1, C, N].</summary>
    private bool _outputTransposed;
    private OutputFormat _outputFormat = new(ModelOutputKind.YoloV8Raw, 1, 0, 0);
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
        if (string.IsNullOrWhiteSpace(modelPath) || !System.IO.File.Exists(modelPath))
            throw new System.IO.FileNotFoundException($"ONNX model not found: {modelPath}", modelPath);

        _modelPath = modelPath;

        // Dispose any prior session so model-switching at runtime doesn't leak ORT handles.
        // Session first, then its owned options (options must outlive the session).
        try { _onnxModel?.Dispose(); } catch { /* ignore */ }
        _onnxModel = null;
        try { _ownedSessionOptions?.Dispose(); } catch { /* ignore */ }
        _ownedSessionOptions = null;

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
        // SessionOptions MUST outlive InferenceSession — disposing them early freezes/crashes ORT.
        try { _ownedSessionOptions?.Dispose(); } catch { /* ignore */ }
        _ownedSessionOptions = loaded.OwnedSessionOptions;
        _onnxModel = loaded.Session;
        _outputNames = loaded.OutputNames;
        _inputName = loaded.InputName;
        ExecutionProvider = loaded.ExecutionProvider;
        _isDynamicModel = loaded.IsDynamicInput;
        AppDebugLog.Info("ONNX", $"Session ready provider={ExecutionProvider} size={loaded.InputImageSize}", effectiveModelPath);

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
    ///     Adopt the class count, anchor count and layout straight from the model's output tensor
    ///     (see <see cref="ReadOutputLayout"/>): channels-first <c>[1, 4+numClasses, numDetections]</c>
    ///     (e.g. <c>[1,5,8400]</c>) or channels-last <c>[1, numDetections, 4+numClasses]</c>. Reading them from
    ///     the model means a multi-class model, or one exported at a non-640 resolution, "just works" without
    ///     relying on a <c>names</c> table or the configured image size lining up. Layouts we can't parse
    ///     (NMS-fused outputs) are left for <see cref="ValidateOnnxShape"/> to flag clearly.
    /// </summary>
    private void AdaptToOutputShape()
    {
        if (_onnxModel == null) return;
        _outputTransposed = false;
        _outputFormat = new OutputFormat(ModelOutputKind.YoloV8Raw, _numClasses, 0, 0);

        var outputs = _onnxModel.OutputMetadata.Values.Select(m => m.Dimensions).ToList();
        var main = outputs.FirstOrDefault();
        if (main != null && IsEndToEnd(main))
        {
            _numDetections = Math.Max(1, main[1]);
            _outputTransposed = true;
            _outputFormat = new OutputFormat(ModelOutputKind.EndToEnd, Math.Max(1, ReadLabelCount()), 0, 0);
            _numClasses = _outputFormat.NumClasses;
            return;
        }

        foreach (var dims in outputs)
        {
            if (ReadOutputLayout(dims, _numDetections) is not { } layout) continue;
            if (layout.NumDetections > 0) _numDetections = layout.NumDetections;
            _outputTransposed = layout.Transposed;
            string? kptShape = null;
            _onnxModel.ModelMetadata?.CustomMetadataMap?.TryGetValue("kpt_shape", out kptShape);
            _outputFormat = ResolveOutputFormat(layout.NumClasses + 4, ReadLabelCount(), kptShape,
                outputs.FirstOrDefault(d => d.Length == 4));
            _numClasses = _outputFormat.NumClasses;
            return;
        }
    }

    /// <summary>
    ///     True for an NMS-fused export: rows of <c>x1 y1 x2 y2 score class</c>, <c>[1, N, 6]</c> with
    ///     at most a few hundred rows (YOLOv10 / YOLO26 end-to-end heads).
    /// </summary>
    internal static bool IsEndToEnd(int[] d)
        => d.Length == 3 && d[0] <= 1 && d[2] == 6 && d[1] > 0 && d[1] < MinAnchorRows;

    /// <summary>
    ///     Tell the raw head variants apart. <paramref name="channels"/> = C of the main output,
    ///     <paramref name="labelCount"/> = class names the model declares (0 = unknown),
    ///     <paramref name="kptShape"/> = Ultralytics <c>kpt_shape</c> metadata (e.g. <c>[17, 3]</c>),
    ///     <paramref name="protoDims"/> = the 4-D mask-prototype output of a segmentation model.
    /// </summary>
    internal static OutputFormat ResolveOutputFormat(int channels, int labelCount, string? kptShape, int[]? protoDims)
    {
        if (protoDims is { Length: 4 } && protoDims[1] > 0 && channels - 4 - protoDims[1] >= 1)
            return new OutputFormat(ModelOutputKind.Segment, channels - 4 - protoDims[1], 0, 0);

        var kpt = (kptShape ?? "").Trim('[', ']', ' ').Split(',', StringSplitOptions.TrimEntries);
        if (kpt.Length == 2 && int.TryParse(kpt[0], out int k) && int.TryParse(kpt[1], out int kd)
            && k > 0 && kd is 2 or 3 && channels - 4 - k * kd >= 1)
            return new OutputFormat(ModelOutputKind.Pose, channels - 4 - k * kd, k, kd);

        if (labelCount > 0 && channels - 5 == labelCount)
            return new OutputFormat(ModelOutputKind.YoloV5Raw, labelCount, 0, 0);

        return new OutputFormat(ModelOutputKind.YoloV8Raw, Math.Max(1, channels - 4), 0, 0);
    }

    /// <summary>Number of class names declared by the model's <c>names</c> metadata or a labels file; 0 if none.</summary>
    private int ReadLabelCount()
    {
        try
        {
            if (_onnxModel?.ModelMetadata?.CustomMetadataMap?.TryGetValue("names", out string? value) == true
                && !string.IsNullOrWhiteSpace(value))
            {
                value = value.Trim();
                if (value.StartsWith('{')) return JObject.Parse(value).Count;
                if (value.StartsWith('[')) return JArray.Parse(value).Count;
            }
            var labels = LabelsFileCandidates().FirstOrDefault(File.Exists);
            if (labels != null)
                return File.ReadAllLines(labels).Count(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith('#'));
        }
        catch
        {
            // Unknown label count only disables the objectness heuristic.
        }
        return 0;
    }

    /// <summary>Fewer output rows than this is not an anchor grid (e.g. an NMS-fused export).</summary>
    private const int MinAnchorRows = 1000;

    /// <summary>
    ///     Reads a detection head's layout from its output dims (-1/0 = dynamic): channels-first
    ///     <c>[1, 4+nc, N]</c> or channels-last <c>[1, N, 4+nc]</c>. NumDetections is 0 when dynamic.
    ///     Null for anything else — including an NMS-fused export (<c>[1, 300, 6]</c> = x1 y1 x2 y2 score
    ///     class), which only looks like a 2-class channels-last head and is left to ValidateOnnxShape.
    ///     <paramref name="expectedAnchors"/> (the anchor count for the model's input size) also accepts
    ///     small-input heads below <see cref="MinAnchorRows"/>, e.g. 756 rows at 192 px.
    /// </summary>
    internal static (int NumClasses, int NumDetections, bool Transposed)? ReadOutputLayout(int[] d, int expectedAnchors = 0)
    {
        if (d.Length != 3) return null;
        bool dynamic = d.Any(x => x <= 0);
        bool AnchorRows(int n) => n >= MinAnchorRows || n == expectedAnchors;

        // Fixed channels-first: [1, C, N] with C = 4+nc small, N anchors large (classic YOLOv8).
        if (!dynamic && d[0] == 1 && d[1] > 4 && d[1] < 512 && d[2] > d[1])
            return (Math.Max(1, d[1] - 4), d[2], false);

        // Fixed channels-last: [1, N, C]. Anchor heads have thousands of rows (8400 at 640).
        if (!dynamic && d[0] == 1 && d[2] > 4 && d[2] < 512 && d[1] > d[2] && AnchorRows(d[1]))
            return (Math.Max(1, d[2] - 4), d[1], true);

        if (dynamic)
        {
            if (d[1] > 4 && d[1] < 512 && (d[2] <= 0 || d[2] > d[1]))
                return (Math.Max(1, d[1] - 4), Math.Max(0, d[2]), false);
            if (d[2] > 4 && d[2] < 512 && (d[1] <= 0 || (d[1] > d[2] && AnchorRows(d[1]))))
                return (Math.Max(1, d[2] - 4), Math.Max(0, d[1]), true);
        }
        return null;
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
    ///     Build the class-id → name table used by the Target Classes dialog and the filter.
    ///     Order of preference:
    ///     1) ONNX custom metadata <c>names</c> (Ultralytics dict JSON),
    ///     2) <c>bin/labels/labels.txt</c> (app labels dir; one name per line, id = line index),
    ///     3) class count inferred from the output tensor (<c>_numClasses</c> from AdaptToOutputShape),
    ///        with generic names Class_0..N-1 (or Enemy/Target when N==2).
    ///     Legacy single-class fallback remains { 0: "Enemy" } only when nothing else is known.
    /// </summary>
    private void LoadClasses()
    {
        if (_onnxModel == null) return;

        _modelClasses.Clear();

        // 1) Embedded Ultralytics-style names metadata.
        try
        {
            var metadata = _onnxModel.ModelMetadata;
            if (metadata != null
                && metadata.CustomMetadataMap.TryGetValue("names", out string? value)
                && !string.IsNullOrEmpty(value))
            {
                // Ultralytics may emit a JSON object {"0":"Enemy","1":"Target"} or a list ["Enemy","Target"].
                value = value.Trim();
                if (value.StartsWith('{'))
                {
                    JObject data = JObject.Parse(value);
                    foreach (var item in data)
                    {
                        if (int.TryParse(item.Key, out int classId) && item.Value != null)
                            _modelClasses[classId] = item.Value.ToString() ?? $"Class_{classId}";
                    }
                }
                else if (value.StartsWith('['))
                {
                    var arr = JArray.Parse(value);
                    for (int i = 0; i < arr.Count; i++)
                        _modelClasses[i] = arr[i]?.ToString() ?? $"Class_{i}";
                }
            }
        }
        catch
        {
            // Malformed metadata — try labels.txt / tensor shape next.
        }

        // 2) bin/labels/labels.txt (canonical) or model-local sidecar.
        if (_modelClasses.Count == 0)
            TryLoadClassesFromLabelsFile();

        // 3) Tensor already told us how many classes (AdaptToOutputShape ran before LoadClasses).
        //    Synthesize selectable entries so the dialog is not stuck on a single "Enemy".
        if (_modelClasses.Count == 0 && _numClasses > 1)
        {
            for (int i = 0; i < _numClasses; i++)
            {
                _modelClasses[i] = _numClasses == 2
                    ? (i == 0 ? "Enemy" : "Target")
                    : $"Class_{i}";
            }
        }

        if (_modelClasses.Count == 0)
        {
            _modelClasses[0] = "Enemy";
            _numClasses = 1;
        }
        else
        {
            // Prefer the larger of metadata count vs tensor-derived count so a 2-class head
            // is never collapsed to 1 just because names metadata was missing.
            int fromNames = _modelClasses.Keys.Max() + 1;
            if (_numClasses < fromNames) _numClasses = fromNames;
            // Fill any gaps (sparse ids) so the picker lists every id the head can emit.
            for (int i = 0; i < _numClasses; i++)
            {
                if (!_modelClasses.ContainsKey(i))
                    _modelClasses[i] = $"Class_{i}";
            }
        }
    }

    private List<string> LabelsFileCandidates()
    {
        var candidates = new List<string>
        {
            // Canonical location requested by the app layout.
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "bin", "labels", "labels.txt")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "bin", "labels", "classes.txt")),
        };

        // Optional model-local sidecars (secondary).
        if (!string.IsNullOrWhiteSpace(_modelPath))
        {
            string? dir = Path.GetDirectoryName(_modelPath);
            string stem = Path.GetFileNameWithoutExtension(_modelPath);
            if (!string.IsNullOrEmpty(dir))
            {
                candidates.Add(Path.Combine(dir, "labels.txt"));
                candidates.Add(Path.Combine(dir, stem + ".txt"));
                candidates.Add(Path.Combine(dir, stem + "_labels.txt"));
                candidates.Add(Path.Combine(dir, "classes.txt"));
            }
        }

        return candidates;
    }

    /// <summary>
    ///     Load class names from the app labels directory first:
    ///     <c>{BaseDirectory}/bin/labels/labels.txt</c> (one name per line; line index = class id).
    ///     Falls back to a sidecar next to the loaded model if the app labels file is missing.
    /// </summary>
    private void TryLoadClassesFromLabelsFile()
    {
        try
        {
            string? labelsPath = LabelsFileCandidates().FirstOrDefault(File.Exists);
            if (labelsPath == null) return;

            var lines = File.ReadAllLines(labelsPath)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .ToList();
            for (int i = 0; i < lines.Count; i++)
                _modelClasses[i] = lines[i];
        }
        catch
        {
            // Optional sidecars must never fail the model load.
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

        // Dynamic / symbolic outputs: nothing fixed to validate; runtime tensor drives the parser.
        if (_isDynamicModel) return;
        // End-to-end / objectness / pose / segment heads were recognised explicitly — their shape differs by design.
        if (_outputFormat.Kind != ModelOutputKind.YoloV8Raw) return;

        int[] expectedCf = [1, 4 + _numClasses, _numDetections];
        int[] expectedCl = [1, _numDetections, 4 + _numClasses];
        var outputs = _onnxModel.OutputMetadata.Values.ToList();
        if (outputs.Count == 0) return;

        bool ok = outputs.Any(m =>
        {
            var d = m.Dimensions;
            if (d.Length != 3) return false;
            if (d.SequenceEqual(expectedCf) || d.SequenceEqual(expectedCl)) return true;
            // Tolerate batch dim symbolic (-1) with matching C/N.
            if (d[0] <= 0)
            {
                if (d[1] == expectedCf[1] && d[2] == expectedCf[2]) return true;
                if (d[1] == expectedCl[1] && d[2] == expectedCl[2]) return true;
            }
            return false;
        });
        if (ok) return;

        // Soft warning only — do not abort load. Parser will use _outputTransposed + live dims.
        string actual = string.Join(" ", outputs.Select(m => "[" + string.Join("x", m.Dimensions) + "]"));
        string expected = _outputTransposed
            ? string.Join("x", expectedCl)
            : string.Join("x", expectedCf);
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            new NoticeBar(
                string.Format(Locale.OutputShapeMismatchFormat, expected, actual),
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
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue>? results = null;
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
                // Snapshot under the gate so Dispose cannot free the native session mid-Run.
                InferenceSession? session;
                lock (_sessionGate)
                {
                    session = _onnxModel;
                    if (session == null)
                        return Array.Empty<Prediction>();
                    System.Threading.Interlocked.Increment(ref _inflightRuns);
                }
                try
                {
                    results = session.Run(inputs, _outputNames, _modeloptions);
                }
                catch (ObjectDisposedException)
                {
                    // Model was switched while we were queued — treat as empty frame, not a crash.
                    return Array.Empty<Prediction>();
                }
                catch (Microsoft.ML.OnnxRuntime.OnnxRuntimeException) when (!ReferenceEquals(System.Threading.Volatile.Read(ref _onnxModel), session))
                {
                    // Session torn down mid-switch — treat as an empty frame. Any other inference failure
                    // propagates, so the AI loop counts it and tells the user when it persists.
                    return Array.Empty<Prediction>();
                }
                finally
                {
                    System.Threading.Interlocked.Decrement(ref _inflightRuns);
                }
            }
            finally
            {
                // Run is fully synchronous, so ORT is done reading the input buffer here.
                if (rented != null) ArrayPool<float>.Shared.Return(rented);
            }
            // Free the native ONNX output buffers deterministically at end-of-frame instead of leaving
            // them to the finalizer (they were never disposed before).
            if (results == null)
                return Array.Empty<Prediction>();
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
                ignoreRegions,
                outputTransposed: _outputTransposed,
                kind: _outputFormat.Kind,
                keypointCount: _outputFormat.KeypointCount,
                keypointDims: _outputFormat.KeypointDims);

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
                        string baseDir = AppContext.BaseDirectory;
                        string imagesDir = Path.Combine(baseDir, "bin", "images");
                        string labelsDir = Path.Combine(baseDir, "bin", "labels");
                        Directory.CreateDirectory(imagesDir);
                        Directory.CreateDirectory(labelsDir);
                        frame.Save(Path.Combine(imagesDir, $"{uuid}.jpg"));

                        if (AppConfig.Current?.ToggleState?.AutoLabelData == true && DoLabel != null)
                        {
                            var labelPath = Path.Combine(labelsDir, $"{uuid}.txt");

                            // Map model-space box into the saved frame's pixel space (FOV may differ from model input).
                            float scaleX = frame.Width / (float)Math.Max(1, _imageSize);
                            float scaleY = frame.Height / (float)Math.Max(1, _imageSize);
                            float bx = DoLabel!.Rectangle.X * scaleX;
                            float by = DoLabel.Rectangle.Y * scaleY;
                            float bw = DoLabel.Rectangle.Width * scaleX;
                            float bh = DoLabel.Rectangle.Height * scaleY;
                            float x = (bx + bw / 2f) / Math.Max(1, frame.Width);
                            float y = (by + bh / 2f) / Math.Max(1, frame.Height);
                            float width = bw / Math.Max(1, frame.Width);
                            float height = bh / Math.Max(1, frame.Height);

                            // YOLO label format: <class-id> <x> <y> <w> <h>
                            File.WriteAllText(labelPath, $"{DoLabel.ClassId} {x} {y} {width} {height}");
                        }
                    });
                }
                catch (Exception e)
                {
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        new NoticeBar(string.Format(Locale.CollectDataFailedFormat, e.Message), 6000).Show()));
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

    public void Dispose()
    {
        // Stop new Runs from taking the session, then wait briefly for any in-flight Run to finish
        // before freeing native ORT handles. This is the difference between a clean model-switch
        // and an AccessViolation / process crash when downloading or switching models.
        InferenceSession? session;
        SessionOptions? options;
        lock (_sessionGate)
        {
            session = _onnxModel;
            _onnxModel = null;
            options = _ownedSessionOptions;
            _ownedSessionOptions = null;
        }

        var spin = 0;
        while (System.Threading.Volatile.Read(ref _inflightRuns) > 0 && spin < 200)
        {
            System.Threading.Thread.Sleep(5);
            spin++;
        }

        try { session?.Dispose(); } catch { /* ignore dispose races */ }
        // Options after session — ORT requires options to outlive the session.
        try { options?.Dispose(); } catch { }
        try
        {
            _resizeBuffer?.Dispose();
            _resizeBuffer = null;
        }
        catch { }
        try
        {
            _modeloptions?.Dispose();
        }
        catch { }
    }
}