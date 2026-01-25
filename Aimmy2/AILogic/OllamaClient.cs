using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;

namespace Aimmy2.AILogic;

/// <summary>
/// HTTP client for communicating with Ollama API.
/// Supports vision models for image analysis.
/// </summary>
public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
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

    public OllamaClient()
    {
        _httpClient = new HttpClient();
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
        _httpClient.Dispose();
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
