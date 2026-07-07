using System.IO;

namespace PowerAim.AILogic;

/// <summary>One probed native dependency of the CUDA/TensorRT stack (present + where, vs. what's needed).</summary>
public sealed record NativeDependency(string Component, string Dll, bool Present, string? FoundIn, string Required);

/// <summary>
///     Read-only inspector for the CUDA/TensorRT inference stack, backing the setup wizard's "what's
///     active / what's there / what's missing" view. Everything here is best-effort DLL probing —
///     the ground truth for "does it actually run" is the active execution provider (from the live
///     session) and a reload-with-TensorRT attempt, both surfaced by the wizard.
///     <para>
///     Target stack for our ONNX Runtime (1.24.x): CUDA 12.x runtime, cuDNN 9, TensorRT 10.10.
///     </para>
/// </summary>
public static class CudaDiagnostics
{
    // Versioned native names for our target stack. CUDA 12 → cudart64_12.dll; cuDNN 9 → cudnn64_9.dll;
    // TensorRT 10 → nvinfer_10.dll (major-versioned, so any 10.x satisfies it).
    private const string CudartDll = "cudart64_12.dll";
    private const string CudnnDll = "cudnn64_9.dll";
    private const string TensorRtDll = "nvinfer_10.dll";

    /// <summary>The active execution provider of the running session, or null when no model is loaded.</summary>
    public static string? ActiveProvider =>
        AIManager.Instance?.PredictionLogic?.ExecutionProvider.ToString();

    /// <summary>True on the CUDA/GPU build (the DirectML build can't target CUDA/TensorRT at all).</summary>
    public static bool IsCudaBuild => TensorRtRuntime.SupportedInThisBuild();

    /// <summary>Probe the three native pieces TensorRT needs. Order = install order (CUDA → cuDNN → TRT).</summary>
    public static IReadOnlyList<NativeDependency> Collect()
    {
        return
        [
            Probe("CUDA runtime", CudartDll, "12.x"),
            Probe("cuDNN", CudnnDll, "9.x"),
            Probe("TensorRT", TensorRtDll, "10.10.0.31"),
        ];
    }

    private static NativeDependency Probe(string component, string dll, string required)
    {
        var found = FindDll(dll);
        return new NativeDependency(component, dll, found != null, found, required);
    }

    /// <summary>Resolve a native DLL the way the loader would: app dir, our TensorRT drop-in dir, the
    /// CUDA toolkit's bin, then everything on PATH. Returns the containing directory, or null.</summary>
    public static string? FindDll(string dll)
    {
        foreach (var dir in ProbeDirs())
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try { if (File.Exists(Path.Combine(dir.Trim(), dll))) return dir.Trim(); }
            catch { /* malformed PATH entry — skip */ }
        }
        return null;
    }

    private static IEnumerable<string> ProbeDirs()
    {
        yield return AppContext.BaseDirectory;
        yield return TensorRtRuntime.LocalRuntimeDir;

        var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
        if (!string.IsNullOrEmpty(cudaPath)) yield return Path.Combine(cudaPath, "bin");

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
            foreach (var p in path.Split(Path.PathSeparator))
                yield return p;
    }
}
