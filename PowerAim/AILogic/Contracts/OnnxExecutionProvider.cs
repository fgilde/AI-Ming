using Microsoft.ML.OnnxRuntime;

namespace PowerAim.AILogic.Contracts;

public enum OnnxExecutionProvider
{
    Cpu,
    DirectML,
    Cuda,
    TensorRT
}

internal static class OnnxHelper
{
    public static OnnxExecutionProvider SetExecutionProvider(this SessionOptions sessionOptions,
        OnnxExecutionProvider preferredProvider, int deviceId = 0, bool preferFp16 = false, string? cacheDir = null)
    {
        OnnxExecutionProvider[] fallbackOrder;

        switch (preferredProvider)
        {
            case OnnxExecutionProvider.TensorRT:
                fallbackOrder = [OnnxExecutionProvider.TensorRT, OnnxExecutionProvider.Cuda, OnnxExecutionProvider.DirectML, OnnxExecutionProvider.Cpu];
                break;
            case OnnxExecutionProvider.Cuda:
                fallbackOrder = [OnnxExecutionProvider.Cuda, OnnxExecutionProvider.DirectML, OnnxExecutionProvider.Cpu];
                break;
            case OnnxExecutionProvider.DirectML:
                fallbackOrder = [OnnxExecutionProvider.DirectML, OnnxExecutionProvider.Cpu];
                break;
            default:
                fallbackOrder = [OnnxExecutionProvider.Cpu];
                break;
        }

        foreach (var provider in fallbackOrder)
        {
            // The picker stores a DXGI adapter index; CUDA/TensorRT need the NVIDIA-only CUDA ordinal
            // instead (issue #12). -1 means the chosen adapter can't run this provider (e.g. an iGPU
            // under CUDA) → skip it and let the fallback chain try the next one.
            int providerDeviceId = ResolveDeviceId(provider, deviceId);
            if (providerDeviceId < 0)
            {
                Console.WriteLine($"Skipping provider {provider}: selected adapter (DXGI index {deviceId}) is not a valid {provider} device.");
                continue;
            }

            // Gate TensorRT on the runtime actually being installed. The provider registers fine from
            // the GPU package, but the engine build (at session creation) needs the nvinfer DLLs — so
            // without them we'd commit to TensorRT and only fail later, bypassing the CUDA fallback.
            if (provider == OnnxExecutionProvider.TensorRT && !PowerAim.AILogic.TensorRtRuntime.IsAvailable())
            {
                Console.WriteLine("Skipping provider TensorRT: runtime (nvinfer) not found.");
                continue;
            }

            try
            {
                if (CanWork(provider, providerDeviceId))
                {
                    AppendConfigured(sessionOptions, provider, providerDeviceId, preferFp16, cacheDir);
                    Console.WriteLine($"Initialized with provider {provider} on device {providerDeviceId} " +
                                      $"(requested DXGI adapter index {deviceId}, fp16={preferFp16}).");
                    return provider;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine($"Failed to initialize provider {provider}. Message {e.Message}");
            }
        }

        throw new InvalidOperationException("No suitable execution provider found.");
    }

    /// <summary>
    ///     Append the chosen provider with its full runtime options: TensorRT gets FP16 + an on-disk
    ///     engine/timing cache (the expensive engine build then happens once and is reused — the
    ///     "build accelerator"). CUDA/DirectML take their FP16 speedup from an FP16 model variant
    ///     (selected upstream in PredictionLogic), so there's no per-provider precision flag to set here.
    /// </summary>
    private static void AppendConfigured(SessionOptions options, OnnxExecutionProvider provider,
        int deviceId, bool preferFp16, string? cacheDir)
    {
        switch (provider)
        {
            case OnnxExecutionProvider.Cpu:
                options.AppendExecutionProvider_CPU();
                break;
            case OnnxExecutionProvider.DirectML:
                options.AppendExecutionProvider_DML(deviceId);
                break;
            case OnnxExecutionProvider.Cuda:
                options.AppendExecutionProvider_CUDA(deviceId);
                break;
            case OnnxExecutionProvider.TensorRT:
                var trt = new Dictionary<string, string>
                {
                    ["device_id"] = deviceId.ToString(),
                    ["trt_fp16_enable"] = preferFp16 ? "1" : "0",
                };
                if (!string.IsNullOrWhiteSpace(cacheDir))
                {
                    try { System.IO.Directory.CreateDirectory(cacheDir); } catch { /* fall through — cache just won't persist */ }
                    trt["trt_engine_cache_enable"] = "1";
                    trt["trt_engine_cache_path"] = cacheDir;
                    trt["trt_timing_cache_enable"] = "1";
                }
                using (var trtOpts = new OrtTensorRTProviderOptions())
                {
                    trtOpts.UpdateOptions(trt);
                    options.AppendExecutionProvider_Tensorrt(trtOpts);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider));
        }
    }

    /// <summary>
    ///     Map the picker's DXGI adapter index to the device id a given provider actually expects.
    ///     DirectML keys on the DXGI index directly; CUDA/TensorRT need the NVIDIA-only CUDA ordinal
    ///     (see <see cref="GpuAdapterEnumerator.DxgiIndexToCudaOrdinal"/>) and return -1 when the chosen
    ///     adapter isn't an NVIDIA GPU. CPU ignores the id entirely.
    /// </summary>
    private static int ResolveDeviceId(OnnxExecutionProvider provider, int dxgiIndex) => provider switch
    {
        OnnxExecutionProvider.Cuda or OnnxExecutionProvider.TensorRT
            => PowerAim.AILogic.GpuAdapterEnumerator.DxgiIndexToCudaOrdinal(dxgiIndex),
        OnnxExecutionProvider.DirectML => dxgiIndex,
        _ => 0, // CPU has no per-device id
    };

    public static SessionOptions AppendExecutionProvider(this SessionOptions options, OnnxExecutionProvider provider, int deviceId = 0)
    {
        // ORT's deviceId is the DXGI adapter index for DirectML and the CUDA ordinal for CUDA. CPU
        // and TensorRT either have no concept of a per-device id or take it via a different config
        // path, so deviceId is silently ignored for those two.
        switch (provider)
        {
            case OnnxExecutionProvider.Cpu:
                options.AppendExecutionProvider_CPU();
                break;
            case OnnxExecutionProvider.DirectML:
                options.AppendExecutionProvider_DML(deviceId);
                break;
            case OnnxExecutionProvider.Cuda:
                options.AppendExecutionProvider_CUDA(deviceId);
                break;
            case OnnxExecutionProvider.TensorRT:
                options.AppendExecutionProvider_Tensorrt();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider));
        }
        return options;
    }

    public static SessionOptions CreateDefaultSessionOptions()
    {
        return new SessionOptions
        {
            EnableCpuMemArena = true,
            EnableMemoryPattern = true,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_PARALLEL
        };
    }

    public static bool CanWork(OnnxExecutionProvider provider, int deviceId = 0)
    {
        try
        {
            CreateDefaultSessionOptions()
                .AppendExecutionProvider(provider, deviceId);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to initialize provider {provider} on device {deviceId}. Message {e.Message}");
            return false;
        }
    }
}