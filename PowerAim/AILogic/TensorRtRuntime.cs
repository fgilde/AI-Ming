using System.IO;

namespace PowerAim.AILogic;

/// <summary>
///     Detects whether the NVIDIA TensorRT runtime (the <c>nvinfer*</c> native libraries) is present.
///     ONNX Runtime's TensorRT execution provider is registerable from the GPU package, but the actual
///     engine build needs the TensorRT runtime DLLs, which are NOT shipped with the app (they're ~1 GB
///     and NVIDIA-gated). So we gate the provider on this check: without it, the EP chain skips
///     TensorRT and lands on CUDA/DirectML instead of committing to a provider that would fail at
///     session-build time. It also backs the UI's "install TensorRT" prompt.
/// </summary>
public static class TensorRtRuntime
{
    // TensorRT 10.x renamed the versioned DLL to nvinfer_10.dll; older majors use nvinfer.dll.
    private static readonly string[] ProbeNames = ["nvinfer_10.dll", "nvinfer.dll"];

    private static bool? _cached;

    /// <summary>The directory we drop a downloaded TensorRT redist into (checked first, on PATH too).</summary>
    public static string LocalRuntimeDir =>
        Path.Combine(AppContext.BaseDirectory, "tensorrt");

    /// <summary>True when a TensorRT runtime DLL is resolvable. Cached; call <see cref="Invalidate"/> after an install.</summary>
    public static bool IsAvailable()
    {
        _cached ??= Probe();
        return _cached.Value;
    }

    public static void Invalidate() => _cached = null;

    /// <summary>
    ///     True when this build can even TARGET TensorRT — i.e. the ORT TensorRT provider PLUGIN
    ///     (<c>onnxruntime_providers_tensorrt.dll</c>) ships with it. Only the CUDA/GPU build has it; the
    ///     DirectML build does not. NOTE: this is the plugin that drives TensorRT, NOT the TensorRT
    ///     runtime itself (nvinfer, checked by <see cref="IsAvailable"/>). The UI offers the runtime
    ///     install only when the build supports TensorRT (this is true) AND the runtime is still missing
    ///     (<see cref="IsAvailable"/> is false).
    /// </summary>
    public static bool SupportedInThisBuild()
    {
        try { return File.Exists(Path.Combine(AppContext.BaseDirectory, "onnxruntime_providers_tensorrt.dll")); }
        catch { return false; }
    }

    private static bool Probe()
    {
        try
        {
            var dirs = new List<string> { AppContext.BaseDirectory, LocalRuntimeDir };
            var path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path)) dirs.AddRange(path.Split(Path.PathSeparator));

            foreach (var dir in dirs)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                foreach (var name in ProbeNames)
                {
                    try { if (File.Exists(Path.Combine(dir.Trim(), name))) return true; }
                    catch { /* bad PATH entry — ignore */ }
                }
            }
        }
        catch { /* environment probing is best-effort */ }
        return false;
    }
}
