using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace PowerAim.AILogic;

/// <summary>Where a setup run currently is, for progress reporting (localized by the UI, not here).</summary>
public enum TrtPhase { Locating, Downloading, Extracting, Done }

/// <summary>A setup progress tick: phase, 0..1 fraction, and byte counts for the download phase.</summary>
public readonly record struct TrtSetupProgress(TrtPhase Phase, double Fraction, long DoneBytes, long TotalBytes);

/// <summary>
///     On-demand TensorRT runtime setup. We target one exact NVIDIA redistributable
///     (<see cref="TensorPackageToUse"/>) that matches our ONNX Runtime's TRT 10.x requirement. The
///     flow is deliberately simple: if the package is already sitting next to the exe / in temp /
///     Downloads, set up from it; otherwise download it to temp, then set up. "Set up" = extract the
///     runtime DLLs into <see cref="TensorRtRuntime.LocalRuntimeDir"/> (which is put on PATH before a
///     session is built).
/// </summary>
public static class TensorRtInstaller
{
    /// <summary>The exact TensorRT redist we use — TRT 10.10 for CUDA 12.9 (satisfies ORT 1.24's TRT 10.x).</summary>
    public const string TensorPackageToUse = "TensorRT-10.10.0.31.Windows.win10.cuda-12.9.zip";

    /// <summary>NVIDIA's zip folder for this release; the full URL is this + <see cref="TensorPackageToUse"/>.</summary>
    private const string DownloadBaseUri =
        "https://developer.nvidia.com/downloads/compute/machine-learning/tensorrt/10.10.0/zip/";

    /// <summary>NVIDIA's TensorRT landing page — the manual fallback if the direct download is gated.</summary>
    public const string VendorPage = "https://developer.nvidia.com/tensorrt";

    /// <summary>Full download URL (base + package). Overridable via the <c>POWERAIM_TRT_URL</c> env var.</summary>
    public static string DownloadUri =>
        Environment.GetEnvironmentVariable("POWERAIM_TRT_URL") is { Length: > 0 } env
            ? env
            : DownloadBaseUri + TensorPackageToUse;

    /// <summary>Locate an already-present copy of the exact package (next to the exe, in temp, or Downloads).</summary>
    public static string? FindPackageZip()
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, Path.GetTempPath(), DownloadsDir() })
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                var p = Path.Combine(dir, TensorPackageToUse);
                if (File.Exists(p)) return p;
            }
            catch { /* unreadable dir — skip */ }
        }
        return null;
    }

    private static string? DownloadsDir()
    {
        var up = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(up) ? null : Path.Combine(up, "Downloads");
    }

    /// <summary>
    ///     One-shot setup: use an existing package if present, else download it to temp, then extract the
    ///     runtime DLLs. Reports progress (download %, extract) via <paramref name="progress"/>. Throws on
    ///     download/extract failure (e.g. a gated NVIDIA URL that returns HTML instead of a zip).
    /// </summary>
    public static async Task SetUpAsync(IProgress<TrtSetupProgress>? progress, CancellationToken ct = default)
    {
        progress?.Report(new(TrtPhase.Locating, 0, 0, 0));
        var zip = FindPackageZip();
        if (zip == null)
        {
            zip = Path.Combine(Path.GetTempPath(), TensorPackageToUse);
            if (!File.Exists(zip))
                await DownloadAsync(DownloadUri, zip, progress, ct);
        }

        progress?.Report(new(TrtPhase.Extracting, 0, 0, 0));
        Directory.CreateDirectory(TensorRtRuntime.LocalRuntimeDir);
        await Task.Run(() => ExtractDlls(zip, TensorRtRuntime.LocalRuntimeDir), ct);
        TensorRtRuntime.Invalidate();

        progress?.Report(new(TrtPhase.Done, 1, 0, 0));
    }

    private static async Task DownloadAsync(string url, string dest, IProgress<TrtSetupProgress>? progress, CancellationToken ct)
    {
        // No client timeout — a TensorRT zip is ~1 GB; the default 100 s would abort mid-download.
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        long total = resp.Content.Headers.ContentLength ?? -1;
        var part = dest + ".part";
        try
        {
            await using (var src = await resp.Content.ReadAsStreamAsync(ct))
            await using (var fs = File.Create(part))
            {
                var buffer = new byte[1 << 20]; // 1 MB
                long read = 0, lastReported = 0;
                int n;
                progress?.Report(new(TrtPhase.Downloading, 0, 0, total > 0 ? total : 0));
                while ((n = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, n), ct);
                    read += n;
                    // Throttle UI updates to every ~4 MB (and always the last one) so we don't flood.
                    if (read - lastReported >= 4L << 20)
                    {
                        lastReported = read;
                        double frac = total > 0 ? (double)read / total : 0;
                        progress?.Report(new(TrtPhase.Downloading, frac, read, total > 0 ? total : 0));
                    }
                }
            }
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(part, dest);
        }
        catch
        {
            try { if (File.Exists(part)) File.Delete(part); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    /// <summary>Extract every *.dll from the zip flat into <paramref name="destDir"/> (the archive's
    /// folder layout doesn't matter — we only want the runtime libs). Throws if it isn't a real zip.</summary>
    private static void ExtractDlls(string zipPath, string destDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        int extracted = 0;
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
            var name = Path.GetFileName(entry.FullName);
            if (string.IsNullOrEmpty(name)) continue;
            entry.ExtractToFile(Path.Combine(destDir, name), overwrite: true);
            extracted++;
        }
        if (extracted == 0)
            throw new InvalidDataException("No DLLs found in the TensorRT archive — the download may be invalid.");
    }
}
