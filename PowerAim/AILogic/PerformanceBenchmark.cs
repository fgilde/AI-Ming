using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using PowerAim.AILogic.Contracts;

namespace PowerAim.AILogic;

/// <summary>
///     Auto-benchmark for ONNX inference at different square input resolutions. Loads the model
///     once per candidate size, runs a small fixed number of warm-up frames plus a measurement
///     window, and reports the achieved FPS / per-frame latency. Picks the highest resolution
///     whose measured FPS still clears a "playable" threshold.
///
///     architecture: we drive the session via <see cref="OnnxModelSessionFactory"/> so the
///     execution-provider fallback chain (DirectML → CUDA → CPU) and dynamic input-shape
///     detection used elsewhere in the pipeline are reused unchanged.
///
///     UI integration is intentionally out of scope here — see <c>RunAsync</c>'s
///     <see cref="IProgress{T}"/> overload for progress reporting.
/// </summary>
public static class PerformanceBenchmark
{
    /// <summary>Default candidate resolutions probed when the caller doesn't supply a list.</summary>
    public static readonly IReadOnlyList<int> DefaultImageSizes = [320, 416, 512, 640, 800];

    /// <summary>How many frames to run before measurement begins (lets JIT / EP warm up).</summary>
    private const int WarmupFrames = 5;

    /// <summary>How many measurement frames per candidate size.</summary>
    private const int MeasurementFrames = 30;

    /// <summary>
    ///     Minimum acceptable FPS for a recommendation to be considered "playable". Sizes below
    ///     this threshold are still reported in <see cref="BenchmarkResult.Samples"/> but
    ///     <see cref="BenchmarkResult.RecommendedImageSize"/> will fall back to the fastest
    ///     measured size when nothing clears the bar.
    /// </summary>
    private const double PlayableFpsTarget = 60.0;

    /// <summary>
    ///     Run the benchmark. The returned <see cref="BenchmarkResult"/> always contains at least
    ///     one sample unless every candidate failed to load — in that case the call throws.
    /// </summary>
    /// <param name="modelPath">Path to a .onnx file. Must be a dynamic-axis model for multi-size
    ///     benchmarking to make sense; fixed-shape models will simply report a single sample at
    ///     the model's declared size.</param>
    /// <param name="imageSizes">Override the default candidate list. Sizes &lt;= 0 are ignored.</param>
    /// <param name="progress">Optional progress sink; ticked once per measurement frame and once
    ///     per size transition.</param>
    /// <param name="ct">Cancellation token; checked between frames.</param>
    public static Task<BenchmarkResult> RunAsync(
        string modelPath,
        IReadOnlyList<int>? imageSizes = null,
        IProgress<BenchmarkProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Model path is required.", nameof(modelPath));

        return Task.Run(() => RunCore(modelPath, imageSizes, progress, ct), ct);
    }

    private static BenchmarkResult RunCore(
        string modelPath,
        IReadOnlyList<int>? imageSizes,
        IProgress<BenchmarkProgress>? progress,
        CancellationToken ct)
    {
        var sizes = (imageSizes ?? DefaultImageSizes).Where(s => s > 0).Distinct().OrderBy(s => s).ToArray();
        if (sizes.Length == 0) sizes = DefaultImageSizes.ToArray();

        var samples = new List<BenchmarkSample>(sizes.Length);
        var notes = new List<string>();
        double totalFrames = sizes.Length * (double)MeasurementFrames;
        double framesDone = 0;

        for (int i = 0; i < sizes.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            int size = sizes[i];

            try
            {
                var sample = MeasureSize(modelPath, size, progress, sizes.Length, i, ref framesDone, totalFrames, ct);
                samples.Add(sample);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                notes.Add($"Size {size} failed: {ex.Message}");
                framesDone += MeasurementFrames; // keep progress monotonic
                progress?.Report(new BenchmarkProgress(size, sizes.Length, framesDone, totalFrames));
            }
        }

        if (samples.Count == 0)
            throw new InvalidOperationException(
                "Performance benchmark could not measure any image size. " +
                "Check that the model path is valid and at least one ONNX execution provider is available.\n" +
                string.Join("\n", notes));

        int recommended = PickRecommendedSize(samples, notes);
        return new BenchmarkResult(recommended, samples, string.Join("\n", notes));
    }

    private static BenchmarkSample MeasureSize(
        string modelPath,
        int imageSize,
        IProgress<BenchmarkProgress>? progress,
        int totalSizes,
        int sizeIndex,
        ref double framesDone,
        double totalFrames,
        CancellationToken ct)
    {
        var loaded = OnnxModelSessionFactory.Load(modelPath, OnnxExecutionProvider.Cuda);
        try
        {
            // Fixed-shape models effectively only have one valid size — overwrite our request so
            // the tensor we feed in matches reality (and the sample reflects what the user will
            // actually see at runtime).
            int actualSize = (!loaded.IsDynamicInput && loaded.InputImageSize > 0)
                ? loaded.InputImageSize
                : imageSize;

            int totalFloats = 3 * actualSize * actualSize;
            var inputBuffer = new float[totalFloats];
            FillSyntheticTensor(inputBuffer);

            var inputTensor = new DenseTensor<float>(inputBuffer, [1, 3, actualSize, actualSize]);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };

            using var runOptions = new RunOptions();

            // Warm-up
            for (int w = 0; w < WarmupFrames; w++)
            {
                ct.ThrowIfCancellationRequested();
                using var _ = loaded.Session.Run(inputs, loaded.OutputNames, runOptions);
            }

            // Measurement
            var perFrameMs = new double[MeasurementFrames];
            var sw = new Stopwatch();
            sw.Start();
            var overall = Stopwatch.StartNew();
            for (int m = 0; m < MeasurementFrames; m++)
            {
                ct.ThrowIfCancellationRequested();
                sw.Restart();
                using (var _ = loaded.Session.Run(inputs, loaded.OutputNames, runOptions)) { }
                sw.Stop();
                perFrameMs[m] = sw.Elapsed.TotalMilliseconds;
                framesDone++;
                progress?.Report(new BenchmarkProgress(actualSize, totalSizes, framesDone, totalFrames));
            }
            overall.Stop();

            double avgInfMs = perFrameMs.Average();
            double avgFps = overall.Elapsed.TotalSeconds > 0
                ? MeasurementFrames / overall.Elapsed.TotalSeconds
                : 0;

            double gpu = TryReadGpuUtilization();

            return new BenchmarkSample(actualSize, avgFps, avgInfMs, gpu);
        }
        finally
        {
            loaded.Session.Dispose();
        }
    }

    private static void FillSyntheticTensor(float[] buffer)
    {
        // Deterministic mid-grey fill is enough to exercise the graph end-to-end while keeping
        // the benchmark reproducible. We deliberately avoid Random so subsequent runs report
        // comparable numbers on the same hardware.
        for (int i = 0; i < buffer.Length; i++) buffer[i] = 0.5f;
    }

    /// <summary>
    ///     Best-effort GPU utilization read via Windows performance counters. Returns <c>0</c>
    ///     when the counter category isn't available (non-Windows, no GPU, lack of permissions).
    /// </summary>
    private static double TryReadGpuUtilization()
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return 0;

            // The "GPU Engine" / "Utilization Percentage" counter exposes per-engine usage; we
            // aggregate the "engtype_3D" instances which represent the 3D / compute engines.
            if (!PerformanceCounterCategory.Exists("GPU Engine")) return 0;
            var category = new PerformanceCounterCategory("GPU Engine");
            string[] instances = category.GetInstanceNames();
            double total = 0;
            foreach (var inst in instances)
            {
                if (!inst.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)) continue;
                using var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, true);
                counter.NextValue();
                Thread.Sleep(50);
                total += counter.NextValue();
            }
            // Cap to 100 — multiple 3D engines can in theory exceed 100 in aggregate.
            return Math.Min(100, total);
        }
        catch
        {
            return 0;
        }
    }

    private static int PickRecommendedSize(IReadOnlyList<BenchmarkSample> samples, List<string> notes)
    {
        // Prefer the highest resolution that still hits the playable threshold; otherwise pick
        // the fastest-measured size and add an explanatory note.
        var playable = samples.Where(s => s.AvgFps >= PlayableFpsTarget).OrderByDescending(s => s.ImageSize).ToList();
        if (playable.Count > 0)
        {
            notes.Add($"Recommended {playable[0].ImageSize} — highest size hitting {PlayableFpsTarget:F0} FPS target ({playable[0].AvgFps:F1} FPS).");
            return playable[0].ImageSize;
        }

        var fallback = samples.OrderByDescending(s => s.AvgFps).First();
        notes.Add($"No size cleared {PlayableFpsTarget:F0} FPS — falling back to the fastest measured size ({fallback.ImageSize} @ {fallback.AvgFps:F1} FPS).");
        return fallback.ImageSize;
    }
}

/// <summary>
///     Outcome of <see cref="PerformanceBenchmark.RunAsync"/>. <see cref="Notes"/> contains
///     human-readable annotations (which size was picked and why; any per-size failures).
/// </summary>
public record BenchmarkResult(
    int RecommendedImageSize,
    IReadOnlyList<BenchmarkSample> Samples,
    string Notes);

/// <summary>
///     One measured (image-size, throughput) data point. <see cref="GpuUtilizationPct"/> is
///     best-effort and may be <c>0</c> when the perf counters aren't available.
/// </summary>
public record BenchmarkSample(
    int ImageSize,
    double AvgFps,
    double AvgInferenceMs,
    double GpuUtilizationPct);

/// <summary>
///     Progress payload emitted while the benchmark is running. <see cref="SamplesDone"/> and
///     <see cref="SamplesTotal"/> count individual measurement frames across all sizes so the UI
///     can render a single linear progress bar.
/// </summary>
public record BenchmarkProgress(
    int CurrentSize,
    int TotalSizes,
    double SamplesDone,
    double SamplesTotal);
