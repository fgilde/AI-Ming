using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PowerAim.AILogic.Contracts;
using PowerAim.Config;

namespace PowerAim.AILogic;

/// <summary>
/// HTTP client for communicating with Ollama API.
/// Supports vision models for image analysis.
/// </summary>
public class OllamaClient : IOllamaClient
{
    /// <summary>
    ///     Process-wide HttpClient shared by every <see cref="OllamaClient"/> instance. Per the
    ///     canonical guidance (HttpClient is meant to be long-lived), this avoids socket exhaustion
    ///     and — more importantly here — prevents <see cref="ObjectDisposedException"/> when one
    ///     control disposes its short-lived OllamaClient while another async operation is still in
    ///     flight (we used to dispose the shared HttpClient via the per-instance Dispose).
    /// </summary>
    private static readonly HttpClient _httpClient = new();
    private bool _isAvailable;
    private string? _lastError;
    private string[] _availableModels = [];
    private DateTime _lastStatusCheck = DateTime.MinValue;
    private readonly TimeSpan _statusCheckInterval = TimeSpan.FromSeconds(30);

    private static readonly string[] KnownVisionModels =
    [
        "moondream", "llava", "bakllava", "llava-llama3", "llava-phi3",
        "minicpm-v", "qwen2-vl", "qwen2.5-vl", "internvl2"
    ];

    /// <summary>Curated vision-model suggestions shown in the AutoPlay profile editor.</summary>
    public static IReadOnlyList<string> RecommendedVisionModels => KnownVisionModels;

    /// <summary>URL of the official Ollama download page (Windows installer + docs).</summary>
    public const string DownloadUrl = "https://ollama.com/download";

    // =============================================================== Install / process ====

    /// <summary>
    ///     Locate the <c>ollama.exe</c> binary on the local machine. Probes the standard Windows
    ///     install path first (the Ollama installer drops it under <c>%LocalAppData%\Programs\Ollama</c>),
    ///     then Program Files, then the user's PATH via <c>where ollama</c>. Returns <c>null</c> if
    ///     none of those produces a hit.
    /// </summary>
    public static string? FindOllamaExecutable()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),         "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),      "Ollama", "ollama.exe"),
        ];
        foreach (var path in candidates)
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;

        // Fall back to PATH lookup via `where ollama` so unusual install locations still register.
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "where",
                Arguments = "ollama",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is not null)
            {
                p.WaitForExit(2000);
                var first = p.StandardOutput.ReadLine();
                if (!string.IsNullOrWhiteSpace(first) && File.Exists(first)) return first.Trim();
            }
        }
        catch { /* `where` not available / blocked — ignore */ }
        return null;
    }

    /// <summary>True if Ollama looks installed on this machine.</summary>
    public static bool IsInstalled => FindOllamaExecutable() is not null;

    /// <summary>
    ///     Launch <c>ollama serve</c> in the background so the local HTTP API becomes reachable
    ///     without the user having to drop to a terminal. Best-effort: returns the spawned process,
    ///     or <c>null</c> if the executable couldn't be found or starting it failed. The server
    ///     keeps running until the spawned process exits (e.g. when PowerAim exits, unless the
    ///     server was already running independently).
    /// </summary>
    public static System.Diagnostics.Process? TryStartServer()
    {
        var exe = FindOllamaExecutable();
        if (exe is null) return null;
        try
        {
            return System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            });
        }
        catch { return null; }
    }

    // =============================================================== Pull ====

    public sealed record PullProgress(string Status, double Percent);

    /// <summary>
    ///     POSTs to <c>/api/pull</c> and streams the NDJSON progress lines back as
    ///     <see cref="PullProgress"/> snapshots. Suitable for binding to a progress bar in the UI.
    /// </summary>
    public async Task PullModelAsync(string modelName, IProgress<PullProgress>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name required.", nameof(modelName));
        var settings = AppConfig.Current?.OllamaSettings;
        var baseUrl = settings?.BaseUrl ?? "http://localhost:11434";

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/pull")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { name = modelName, stream = true }),
                Encoding.UTF8, "application/json")
        };
        using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Pull request failed: HTTP {(int)resp.StatusCode}");

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                string status = root.TryGetProperty("status", out var s) ? (s.GetString() ?? "") : "";
                double pct = 0;
                if (root.TryGetProperty("total", out var tot) && tot.TryGetInt64(out var total) && total > 0 &&
                    root.TryGetProperty("completed", out var comp) && comp.TryGetInt64(out var done))
                    pct = Math.Clamp(100.0 * done / total, 0, 100);
                progress?.Report(new PullProgress(status, pct));
            }
            catch (JsonException) { /* skip malformed chunk */ }
        }
        // Refresh the local model cache so the new tag shows up immediately.
        await IsAvailableAsync();
    }

    public OllamaClient()
    {
        // HttpClient is process-wide static — see _httpClient field. No-op constructor.
    }

    public bool IsAvailable => _isAvailable;
    public string? LastError => _lastError;

    public event EventHandler<OllamaStatusEventArgs>? StatusChanged;

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var settings = AppConfig.Current?.OllamaSettings;
            var baseUrl = settings?.BaseUrl ?? "http://localhost:11434";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync($"{baseUrl}/api/tags", cts.Token);

            var wasAvailable = _isAvailable;
            _isAvailable = response.IsSuccessStatusCode;
            _lastError = _isAvailable ? null : $"HTTP {response.StatusCode}";

            if (_isAvailable)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaTagsResponse>(json);
                _availableModels = result?.Models?.Select(m => m.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToArray() ?? [];
            }
            else
            {
                _availableModels = [];
            }

            if (wasAvailable != _isAvailable)
            {
                OnStatusChanged();
            }

            _lastStatusCheck = DateTime.Now;
            return _isAvailable;
        }
        catch (Exception ex)
        {
            var wasAvailable = _isAvailable;
            _isAvailable = false;
            _lastError = ex.Message;
            _availableModels = [];

            if (wasAvailable != _isAvailable)
            {
                OnStatusChanged();
            }

            return false;
        }
    }

    public async Task<string[]> GetAvailableModelsAsync()
    {
        if (DateTime.Now - _lastStatusCheck > _statusCheckInterval)
        {
            await IsAvailableAsync();
        }
        return _availableModels;
    }

    public async Task<string[]> GetAvailableVisionModelsAsync()
    {
        var allModels = await GetAvailableModelsAsync();
        return allModels
            .Where(m => KnownVisionModels.Any(vm => m.ToLower().Contains(vm.ToLower())))
            .ToArray();
    }

    public async Task<string> AnalyzeImageAsync(Bitmap image, string prompt, string model)
    {
        try
        {
            var settings = AppConfig.Current?.OllamaSettings;
            var baseUrl = settings?.BaseUrl ?? "http://localhost:11434";
            var timeout = settings?.TimeoutSeconds ?? 30;
            var temperature = settings?.Temperature ?? 0.3;
            var maxTokens = settings?.MaxTokens ?? 100;

            // Compress and encode image
            var base64Image = CompressAndEncodeImage(image, settings);

            // Build request
            var request = new OllamaGenerateRequest
            {
                Model = model,
                Prompt = prompt,
                Images = [base64Image],
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = temperature,
                    NumPredict = maxTokens
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            var response = await _httpClient.PostAsync($"{baseUrl}/api/generate", content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _lastError = $"HTTP {response.StatusCode}";
                return "idle";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson);

            return result?.Response?.Trim() ?? "idle";
        }
        catch (OperationCanceledException)
        {
            _lastError = "Request timeout";
            return "idle";
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return "idle";
        }
    }

    private string CompressAndEncodeImage(Bitmap image, OllamaSettings? settings)
    {
        var maxSize = settings?.ImageMaxSize ?? 512;
        var quality = settings?.ImageQuality ?? 70;

        // Resize if needed
        Bitmap resized = image;
        if (image.Width > maxSize || image.Height > maxSize)
        {
            var scale = Math.Min((float)maxSize / image.Width, (float)maxSize / image.Height);
            var newWidth = (int)(image.Width * scale);
            var newHeight = (int)(image.Height * scale);
            resized = new Bitmap(image, newWidth, newHeight);
        }

        // Encode to JPEG with compression
        using var ms = new MemoryStream();
        var encoder = GetEncoder(ImageFormat.Jpeg);
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);

        if (encoder != null)
        {
            resized.Save(ms, encoder, encoderParams);
        }
        else
        {
            resized.Save(ms, ImageFormat.Jpeg);
        }

        if (resized != image)
        {
            resized.Dispose();
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        return ImageCodecInfo.GetImageDecoders().FirstOrDefault(codec => codec.FormatID == format.Guid);
    }

    private void OnStatusChanged()
    {
        StatusChanged?.Invoke(this, new OllamaStatusEventArgs
        {
            IsAvailable = _isAvailable,
            AvailableModels = _availableModels,
            ErrorMessage = _lastError
        });
    }

    public void Dispose()
    {
        // No per-instance resources — the HttpClient is process-wide static. Intentionally a no-op
        // so re-using a logically "disposed" OllamaClient (control reloaded after navigation) still
        // works and never hits ObjectDisposedException.
    }
}

#region Ollama API Models

internal class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public OllamaModel[]? Models { get; set; }
}

internal class OllamaModel
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("details")]
    public OllamaModelDetails? Details { get; set; }
}

internal class OllamaModelDetails
{
    [JsonPropertyName("family")]
    public string? Family { get; set; }

    [JsonPropertyName("parameter_size")]
    public string? ParameterSize { get; set; }
}

internal class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("images")]
    public string[]? Images { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
}

internal class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; }
}

internal class OllamaGenerateResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("response")]
    public string? Response { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }
}

#endregion
