using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace PowerAim.AILogic;

/// <summary>
///     Optional on-demand installer for the TensorRT runtime — the piece we can't bundle (it's ~1 GB
///     and NVIDIA-gated). Mirrors what NobleAIM does: check for the runtime, and if missing, fetch a
///     redistributable and drop the <c>nvinfer*</c> DLLs next to the app so ORT's TensorRT provider can
///     build engines.
///     <para>
///     The download source is intentionally NOT hard-coded to an invented URL. Host the TensorRT
///     runtime zip yourself and set <see cref="RedistZipUrl"/> (env var <c>POWERAIM_TRT_URL</c> or the
///     const below). When it's unset, <see cref="IsConfigured"/> is false and the UI guides the user to
///     NVIDIA's page + the folder to drop the DLLs into instead of pretending to auto-install.
///     </para>
/// </summary>
public static class TensorRtInstaller
{
    // Set this to a zip you host (or use the POWERAIM_TRT_URL env var). Every *.dll in the zip is
    // extracted flat into TensorRtRuntime.LocalRuntimeDir, so the exact archive layout doesn't matter.
    private const string DefaultRedistZipUrl = "";

    public const string VendorPage = "https://developer.nvidia.com/tensorrt";

    public static string? RedistZipUrl =>
        Environment.GetEnvironmentVariable("POWERAIM_TRT_URL") is { Length: > 0 } env
            ? env
            : (DefaultRedistZipUrl.Length > 0 ? DefaultRedistZipUrl : null);

    /// <summary>True when a download source is configured, so <see cref="InstallAsync"/> can run.</summary>
    public static bool IsConfigured => RedistZipUrl != null;

    /// <summary>
    ///     Download the configured redist zip and extract its DLLs into <see cref="TensorRtRuntime.LocalRuntimeDir"/>.
    ///     Streams to a temp file (no full in-memory buffering) and invalidates the runtime-present cache
    ///     on success. Throws if no URL is configured or the download/extract fails.
    /// </summary>
    public static async Task InstallAsync(CancellationToken ct = default)
    {
        var url = RedistZipUrl ?? throw new InvalidOperationException("No TensorRT redist URL configured.");
        var dir = TensorRtRuntime.LocalRuntimeDir;
        Directory.CreateDirectory(dir);

        var tmpZip = Path.Combine(Path.GetTempPath(), "poweraim_trt_" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
            using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = File.Create(tmpZip);
                await resp.Content.CopyToAsync(fs, ct);
            }

            using var archive = ZipFile.OpenRead(tmpZip);
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileName(entry.FullName);
                if (string.IsNullOrEmpty(name)) continue;
                entry.ExtractToFile(Path.Combine(dir, name), overwrite: true);
            }
        }
        finally
        {
            try { File.Delete(tmpZip); } catch { /* temp cleanup is best-effort */ }
        }

        TensorRtRuntime.Invalidate();
    }
}
