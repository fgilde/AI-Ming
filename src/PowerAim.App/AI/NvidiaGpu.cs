using System.Diagnostics;
using System.Globalization;

namespace PowerAim.AI;

/// <summary>NVIDIA compute capability per CUDA ordinal, read from <c>nvidia-smi</c> (once per session).</summary>
public static class NvidiaGpu
{
    private static readonly Lazy<IReadOnlyList<double>> Caps = new(Query);

    /// <summary>Compute capability of the GPU with this CUDA ordinal, or null when unknown (no NVIDIA driver / tool).</summary>
    public static double? ComputeCapability(int ordinal)
        => ordinal >= 0 && ordinal < Caps.Value.Count ? Caps.Value[ordinal] : null;

    /// <summary>
    ///     TensorRT 10's fused end-to-end NMS path needs Volta (7.0) or newer; on Pascal such a model has to run
    ///     on CUDA instead. Unknown capability is not judged.
    /// </summary>
    internal static bool TensorRtUnsuitable(double? computeCapability, ModelOutputKind kind)
        => kind == ModelOutputKind.EndToEnd && computeCapability is < 7.0;

    /// <summary>"index, cap" lines (as printed by nvidia-smi) → capabilities in index order.</summary>
    internal static List<double> Parse(string csv)
    {
        var caps = new List<double>();
        foreach (var line in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var cap))
                caps.Add(cap);
        }
        return caps;
    }

    private static IReadOnlyList<double> Query()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("nvidia-smi", "--query-gpu=index,compute_cap --format=csv,noheader")
            {
                CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            });
            if (p == null) return [];
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return p.ExitCode == 0 ? Parse(output) : [];
        }
        catch
        {
            return []; // no NVIDIA driver
        }
    }
}
