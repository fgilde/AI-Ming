using System.Diagnostics;
using System.IO;
using System.Text;
using Vortice.DXGI;

namespace PowerAim.AILogic;

/// <summary>
///     Enumerates all DXGI graphics adapters in the system so the user can pick which GPU runs ONNX
///     inference. DXGI enumeration order is the same order ONNX Runtime uses internally for both
///     DirectML (<c>AppendExecutionProvider_DML(deviceId)</c>) and CUDA (<c>AppendExecutionProvider_CUDA(deviceId)</c>) —
///     adapter index 0 is whatever Windows considers the primary, 1 the next, and so on. Indices
///     returned here therefore map directly to ORT's <c>deviceId</c> parameter.
/// </summary>
public static class GpuAdapterEnumerator
{
    /// <summary>
    ///     One physical GPU as reported by DXGI. <see cref="DeviceId"/> is the index that ORT expects;
    ///     <see cref="Description"/> is a human-readable vendor + model string for the UI.
    /// </summary>
    public readonly record struct GpuAdapter(int DeviceId, string Description, long DedicatedVideoMemoryBytes)
    {
        /// <summary>Compact "NVIDIA RTX 4090 · 24 GB" style label for picker rows.</summary>
        public string DisplayLabel =>
            DedicatedVideoMemoryBytes >= 512L * 1024 * 1024 // ≥ 512 MB so iGPUs with stolen RAM still get a number
                ? $"{Description} · {DedicatedVideoMemoryBytes / (1024L * 1024 * 1024)} GB"
                : Description;
    }

    private static IReadOnlyList<GpuAdapter>? _cached;

    /// <summary>
    ///     Latest enumeration log. Useful for the user to share when adapters are missing. Cleared
    ///     and rebuilt on every <see cref="List"/> call so it always reflects the current attempt.
    /// </summary>
    public static string LastLog { get; private set; } = string.Empty;

    /// <summary>
    ///     Lists every DXGI adapter, except the Microsoft Basic Render / WARP software adapter.
    ///     iGPUs (Intel / AMD APUs) are kept because they are valid DirectML targets. Returns an
    ///     empty list when DXGI enumeration outright fails — the caller decides how to present that
    ///     (rather than us pretending a "Default GPU" placeholder is real, which masks the bug).
    /// </summary>
    public static IReadOnlyList<GpuAdapter> List(bool forceRefresh = false)
    {
        if (!forceRefresh && _cached is not null && _cached.Count > 0) return _cached;

        var result = new List<GpuAdapter>();
        var log = new StringBuilder();
        log.AppendLine($"[GpuEnum] starting at {DateTime.Now:HH:mm:ss.fff}");

        IDXGIFactory1? factory = null;
        try
        {
            factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            log.AppendLine($"[GpuEnum] factory created: {(factory is null ? "NULL" : factory.NativePointer)}");

            for (uint i = 0; i < 32; i++)
            {
                IDXGIAdapter1? adapter = null;
                try
                {
                    var hr = factory!.EnumAdapters1(i, out adapter);
                    log.AppendLine($"[GpuEnum]   idx={i} hr=0x{hr.Code:X8} success={hr.Success} adapter={(adapter is null ? "null" : "ok")}");
                    if (hr.Failure || adapter is null) break;

                    AdapterDescription1 desc;
                    try
                    {
                        desc = adapter.Description1;
                    }
                    catch (Exception descEx)
                    {
                        log.AppendLine($"[GpuEnum]   idx={i} Description1 threw: {descEx.Message}");
                        continue;
                    }

                    string description = string.IsNullOrWhiteSpace(desc.Description)
                        ? $"GPU #{i}"
                        : desc.Description.Trim();
                    bool isSoftware = ((int)desc.Flags & 0x2) != 0; // DXGI_ADAPTER_FLAG_SOFTWARE == 0x2
                    long vram = (long)(ulong)desc.DedicatedVideoMemory;

                    log.AppendLine($"[GpuEnum]   idx={i} desc=\"{description}\" vendor=0x{desc.VendorId:X4} device=0x{desc.DeviceId:X4} vram={vram} flags={(int)desc.Flags} software={isSoftware}");

                    if (!isSoftware)
                        result.Add(new GpuAdapter(DeviceId: (int)i, Description: description, DedicatedVideoMemoryBytes: vram));
                }
                catch (Exception adapterEx)
                {
                    log.AppendLine($"[GpuEnum]   idx={i} threw: {adapterEx.Message}");
                    break;
                }
                finally
                {
                    adapter?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            log.AppendLine($"[GpuEnum] FATAL: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            factory?.Dispose();
        }

        log.AppendLine($"[GpuEnum] enumeration finished with {result.Count} adapter(s)");
        LastLog = log.ToString();
        Debug.WriteLine(LastLog);

        try
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "PowerAim_GpuEnum.log"), LastLog);
        }
        catch { /* best-effort logging only */ }

        // Only cache successful enumerations — a transient failure shouldn't poison future calls.
        if (result.Count > 0) _cached = result;
        return result;
    }

    /// <summary>Forces re-enumeration on the next <see cref="List"/> call. Use after a driver change.</summary>
    public static void Invalidate() => _cached = null;
}
