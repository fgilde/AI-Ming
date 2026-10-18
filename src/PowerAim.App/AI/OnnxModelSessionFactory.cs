using Microsoft.ML.OnnxRuntime;
using PowerAim.AI.Contracts;
using System.IO;

namespace PowerAim.AI;

/// <summary>
///     Creates an <see cref="InferenceSession"/> with the requested execution provider, falling back
///     through the configured chain. Inspects ONNX input metadata so callers can drive IMAGE_SIZE
///     dynamically instead of hard-coding 640.
/// </summary>
/// <remarks>
///     CRITICAL: SessionOptions must outlive the InferenceSession that was created from them.
///     ORT keeps a native reference to the options (arena, providers, thread pools). Disposing
///     options while the session is still alive causes freezes/crashes on Run() and often on
///     the first metadata touch after load — which is exactly what custom-model load was hitting.
///     Ownership of default-created options is transferred to the returned result.
/// </remarks>
internal static class OnnxModelSessionFactory
{
    /// <summary>Default session options tuned for real-time detection (DirectML-safe).</summary>
    public static SessionOptions CreateDefaultOptions() => new()
    {
        EnableCpuMemArena = true,
        // DirectML requires MemoryPattern=false + Sequential. Keep that as the universal default
        // so Auto/DML builds never probe or load with incompatible options.
        EnableMemoryPattern = false,
        GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        InterOpNumThreads = 1,
        IntraOpNumThreads = 4
    };

    /// <summary>
    ///     Load the ONNX model with the preferred execution provider, falling back through the
    ///     fork's standard chain (<see cref="OnnxHelper.SetExecutionProvider"/>). Inspects the input
    ///     metadata so the caller can configure tensor dimensions dynamically.
    /// </summary>
    /// <param name="modelPath">Absolute or relative path to the .onnx file.</param>
    /// <param name="preferredProvider">Preferred execution provider; falls back on failure.</param>
    /// <param name="sessionOptions">
    ///     Optional caller-owned options. When <c>null</c>, defaults from
    ///     <see cref="CreateDefaultOptions"/> are used and ownership is transferred to the result
    ///     (must be disposed with the session).
    /// </param>
    public static OnnxModelLoadResult Load(
        string modelPath,
        OnnxExecutionProvider preferredProvider = OnnxExecutionProvider.Cuda,
        SessionOptions? sessionOptions = null,
        int deviceId = 0,
        bool preferFp16 = false,
        string? cacheDir = null)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Model path is empty.", nameof(modelPath));

        // Resolve relative paths (e.g. "bin\\models\\foo.onnx") against the app base directory.
        // Selection handlers historically built relative paths; cwd is not guaranteed to be the
        // publish folder, so File.Exists / InferenceSession would fail or load the wrong file.
        if (!Path.IsPathRooted(modelPath))
        {
            modelPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, modelPath));
        }

        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"ONNX model not found: {modelPath}", modelPath);

        SessionOptions options = sessionOptions ?? CreateDefaultOptions();
        // When we created the options, the RESULT owns them for the lifetime of the session.
        // Caller-supplied options remain caller-owned (ownsOptions = false → result.SessionOptions null).
        bool ownsOptions = sessionOptions == null;

        InferenceSession? session = null;
        try
        {
            OnnxExecutionProvider actual = options.SetExecutionProvider(
                preferredProvider, deviceId, preferFp16, cacheDir, out var diagnostic);

            // Creating the session is where TensorRT builds (or loads) its engine. The FIRST build for a
            // given model/shape can take 30s–2min with ZERO output — which looks exactly like a hang
            // ("Initialized with provider TensorRT" is logged, then silence). Emit a start line, a
            // heartbeat every 15s with elapsed time, and a done line, so the log clearly shows whether
            // it's still progressing, genuinely stuck (heartbeats stop), or finished.
            Console.WriteLine(
                $"Building inference session with {actual} — this is where TensorRT builds/loads its engine (first time can be slow) ...");
            var buildSw = System.Diagnostics.Stopwatch.StartNew();
            bool isTrtBuild = actual == OnnxExecutionProvider.TensorRT;
            if (isTrtBuild) TensorRtBuildCoordinator.NotifyStarted(Path.GetFileName(modelPath));
            try
            {
                using (new System.Threading.Timer(
                           _ => Console.WriteLine(
                               $"  ... still building with {actual} — {buildSw.Elapsed.TotalSeconds:F0}s elapsed"),
                           null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15)))
                {
                    session = new InferenceSession(modelPath, options);
                }
            }
            finally
            {
                if (isTrtBuild) TensorRtBuildCoordinator.NotifyFinished();
            }
            buildSw.Stop();
            Console.WriteLine(
                $"Inference session ready with {actual} in {buildSw.Elapsed.TotalSeconds:F1}s.");

            var (imageSize, isDynamic) = DetectInputImageSize(session);
            var outputNames = new List<string>(session.OutputMetadata.Keys);
            // Feed the model by its ACTUAL input name — not every YOLO export calls it "images".
            var inputName = session.InputMetadata.Keys.FirstOrDefault() ?? "images";

            // Transfer session ownership to the result. Options ownership is transferred only when
            // we created them — they MUST NOT be disposed in the finally below.
            var result = new OnnxModelLoadResult(
                session,
                outputNames,
                inputName,
                actual,
                imageSize,
                isDynamic,
                diagnostic,
                ownsOptions ? options : null);
            session = null;
            ownsOptions = false; // result now owns options; do not dispose in finally
            return result;
        }
        finally
        {
            session?.Dispose();
            // Only dispose options we still own (load failed before transfer).
            if (ownsOptions) options.Dispose();
        }
    }

    /// <summary>
    ///     Probe the first 4-D input (NCHW) for its spatial size. Returns <c>(size, isDynamic)</c>
    ///     where <c>isDynamic == true</c> when any axis is symbolic (-1) — the caller should then
    ///     supply the runtime size from configuration.
    /// </summary>
    private static (int Size, bool IsDynamic) DetectInputImageSize(InferenceSession session)
    {
        foreach (var kvp in session.InputMetadata)
        {
            var dims = kvp.Value.Dimensions;
            bool isDynamic = dims.Any(d => d <= 0);

            if (dims.Length != 4)
            {
                if (isDynamic) return (0, true);
                continue;
            }

            // NCHW: [N, C, H, W] — C is typically 3.
            if (dims[1] > 0 && dims[1] <= 4 && dims[2] > 0)
                return (dims[2], isDynamic);

            // NHWC: [N, H, W, C]
            if (dims[3] > 0 && dims[3] <= 4 && dims[1] > 0)
                return (dims[1], isDynamic);

            // Fallback: largest spatial-looking dim among axes 1..3
            int spatial = 0;
            for (int i = 1; i < 4; i++)
                if (dims[i] > spatial) spatial = dims[i];
            if (spatial > 0)
                return (spatial, isDynamic);

            if (isDynamic)
                return (0, true);
        }
        return (0, true);
    }
}

/// <summary>
///     Outcome of loading an ONNX model: the session itself, optional owned SessionOptions,
///     output names, the execution provider that ended up being used, and the detected input size.
/// </summary>
/// <param name="Session">Owned ONNX inference session. The receiver is responsible for disposal.</param>
/// <param name="OutputNames">Cached list of output tensor names.</param>
/// <param name="InputName">Actual model input tensor name (not always "images").</param>
/// <param name="ExecutionProvider">Execution provider actually negotiated by the fallback chain.</param>
/// <param name="InputImageSize">
///     Side length of the square NCHW input as declared by the model. <c>0</c> when the input shape
///     is fully symbolic — the caller should then use its configured override.
/// </param>
/// <param name="IsDynamicInput">
///     <c>true</c> when at least one input axis is symbolic (-1).
/// </param>
/// <param name="Diagnostic">Optional fallback diagnostic string for the UI.</param>
/// <param name="OwnedSessionOptions">
///     SessionOptions created by the factory for this session. Must be disposed together with
///     <paramref name="Session"/>. Null when the caller supplied their own options.
/// </param>
internal sealed record OnnxModelLoadResult(
    InferenceSession Session,
    List<string> OutputNames,
    string InputName,
    OnnxExecutionProvider ExecutionProvider,
    int InputImageSize,
    bool IsDynamicInput,
    string? Diagnostic = null,
    SessionOptions? OwnedSessionOptions = null);
