namespace PowerAim.Config;

/// <summary>
/// Settings for Ollama integration.
/// Ollama is a local LLM server that can run vision models.
/// </summary>
public class OllamaSettings : BaseSettings
{
    /// <summary>
    /// Base URL of the Ollama server.
    /// Default: http://localhost:11434
    /// </summary>
    public string BaseUrl
    {
        get;
        set => SetField(ref field, value);
    } = "http://localhost:11434";

    /// <summary>
    /// Request timeout in seconds.
    /// Increase for slower models or hardware.
    /// </summary>
    public int TimeoutSeconds
    {
        get;
        set => SetField(ref field, value);
    } = 30;

    /// <summary>
    /// LLM temperature (0.0 - 1.0).
    /// Lower = more deterministic, higher = more creative.
    /// For gaming, lower values (0.1-0.3) are recommended.
    /// </summary>
    public double Temperature
    {
        get;
        set => SetField(ref field, value);
    } = 0.3;

    /// <summary>
    /// Maximum tokens in response.
    /// For action names, 50-100 is sufficient.
    /// </summary>
    public int MaxTokens
    {
        get;
        set => SetField(ref field, value);
    } = 100;

    /// <summary>
    /// Maximum image dimension (width/height) for compression.
    /// Smaller = faster but less detail.
    /// </summary>
    public int ImageMaxSize
    {
        get;
        set => SetField(ref field, value);
    } = 512;

    /// <summary>
    /// JPEG quality for image compression (1-100).
    /// Lower = smaller file but worse quality.
    /// </summary>
    public int ImageQuality
    {
        get;
        set => SetField(ref field, value);
    } = 70;
}
