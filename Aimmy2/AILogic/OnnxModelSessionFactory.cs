using Microsoft.ML.OnnxRuntime;
using Aimmy2.AILogic.Contracts;

namespace Aimmy2.AILogic;

/// <summary>
///     Creates an <see cref="InferenceSession"/> with the requested execution provider, falling back
///     through DirectML → CUDA → CPU as needed. The loader also inspects the ONNX input metadata so
///     callers can drive <c>IMAGE_SIZE</c> dynamically instead of hard-coding 640.
///     Adapted from upstream Babyhamsta/Aimmy (commits 3e153bc, 632cab6, c96fb70, 8a3139d) — extended
///     with input-shape detection (<see cref="OnnxModelLoadResult.InputImageSize"/> /
///     <see cref="OnnxModelLoadResult.IsDynamicInput"/>) so the fork can support multiple square
///     YOLOv8 input resolutions without recompiling.
/// </summary>
internal static class OnnxModelSessionFactory
{
    /// <summary>Default session options matching upstream's tuned defaults.</summary>
    public static SessionOptions CreateDefaultOptions() => new()
    {
        EnableCpuMemArena = true,
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
    /// <param name="modelPath">Path to the .onnx file.</param>
    /// <param name="preferredProvider">Preferred execution provider; falls back on failure.</param>
    /// <param name="sessionOptions">
    ///     Optional caller-owned options. When <c>null</c>, defaults from
    ///     <see cref="CreateDefaultOptions"/> are used and disposed internally.
    /// </param>
    public static OnnxModelLoadResult Load(
        string modelPath,
        OnnxExecutionProvider preferredProvider = OnnxExecutionProvider.Cuda,
        SessionOptions? sessionOptions = null)
    {
        SessionOptions options = sessionOptions ?? CreateDefaultOptions();
        bool ownsOptions = sessionOptions == null;

        InferenceSession? session = null;
        try
        {
            OnnxExecutionProvider actual = options.SetExecutionProvider(preferredProvider);
            session = new InferenceSession(modelPath, options);

            var (imageSize, isDynamic) = DetectInputImageSize(session);
            var outputNames = new List<string>(session.OutputMetadata.Keys);

            var result = new OnnxModelLoadResult(session, outputNames, actual, imageSize, isDynamic);
            session = null; // ownership transferred to result
            return result;
        }
        finally
        {
            session?.Dispose();
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

            // NCHW: [batch, channels, H, W]. Use H as canonical size for square models.
            if (dims.Length == 4 && dims[2] > 0)
            {
                return (dims[2], isDynamic);
            }
            if (isDynamic)
            {
                return (0, true);
            }
        }
        return (0, true);
    }
}

/// <summary>
///     Outcome of loading an ONNX model: the session itself, its output names, the execution
///     provider that ended up being used, and the detected input image size.
/// </summary>
/// <param name="Session">Owned ONNX inference session. The receiver is responsible for disposal.</param>
/// <param name="OutputNames">Cached list of output tensor names.</param>
/// <param name="ExecutionProvider">Execution provider actually negotiated by the fallback chain.</param>
/// <param name="InputImageSize">
///     Side length of the square NCHW input as declared by the model. <c>0</c> when the input shape
///     is fully symbolic — the caller should then use its configured override.
/// </param>
/// <param name="IsDynamicInput">
///     <c>true</c> when at least one input axis is symbolic (-1). For dynamic models the caller
///     must pick an image size and recompute <c>NUM_DETECTIONS</c> via
///     <see cref="MathUtil.CalculateNumDetections"/>.
/// </param>
internal sealed record OnnxModelLoadResult(
    InferenceSession Session,
    List<string> OutputNames,
    OnnxExecutionProvider ExecutionProvider,
    int InputImageSize,
    bool IsDynamicInput);
