namespace Aimmy2.Config;

/// <summary>
/// Settings for Ollama integration.
/// Ollama is a local LLM server that can run vision models.
/// </summary>
public class OllamaSettings : BaseSettings
{
    private string _baseUrl = "http://localhost:11434";
    private int _timeoutSeconds = 30;
    private double _temperature = 0.3;
    private int _maxTokens = 100;
    private int _imageMaxSize = 512;
    private int _imageQuality = 70;

    /// <summary>
    /// Base URL of the Ollama server.
    /// Default: http://localhost:11434
    /// </summary>
    public string BaseUrl
    {
        get => _baseUrl;
        set => SetField(ref _baseUrl, value);
    }

    /// <summary>
    /// Request timeout in seconds.
    /// Increase for slower models or hardware.
    /// </summary>
    public int TimeoutSeconds
    {
        get => _timeoutSeconds;
        set => SetField(ref _timeoutSeconds, value);
    }

    /// <summary>
    /// LLM temperature (0.0 - 1.0).
    /// Lower = more deterministic, higher = more creative.
    /// For gaming, lower values (0.1-0.3) are recommended.
    /// </summary>
    public double Temperature
    {
        get => _temperature;
        set => SetField(ref _temperature, value);
    }

    /// <summary>
    /// Maximum tokens in response.
    /// For action names, 50-100 is sufficient.
    /// </summary>
    public int MaxTokens
    {
        get => _maxTokens;
        set => SetField(ref _maxTokens, value);
    }

    /// <summary>
    /// Maximum image dimension (width/height) for compression.
    /// Smaller = faster but less detail.
    /// </summary>
    public int ImageMaxSize
    {
        get => _imageMaxSize;
        set => SetField(ref _imageMaxSize, value);
    }

    /// <summary>
    /// JPEG quality for image compression (1-100).
    /// Lower = smaller file but worse quality.
    /// </summary>
    public int ImageQuality
    {
        get => _imageQuality;
        set => SetField(ref _imageQuality, value);
    }
}
