using System.Drawing;

namespace Aimmy2.AILogic.Contracts;

/// <summary>
/// Interface for Ollama API client.
/// Ollama is a local LLM server that supports vision models.
/// </summary>
public interface IOllamaClient : IDisposable
{
    /// <summary>
    /// Check if the Ollama server is available
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Get list of available models from the Ollama server
    /// </summary>
    Task<string[]> GetAvailableModelsAsync();

    /// <summary>
    /// Get list of available vision models (filtered for image support)
    /// </summary>
    Task<string[]> GetAvailableVisionModelsAsync();

    /// <summary>
    /// Analyze an image and return the LLM response
    /// </summary>
    /// <param name="image">The image to analyze</param>
    /// <param name="prompt">The prompt/question for the LLM</param>
    /// <param name="model">The model to use (e.g., "moondream", "llava")</param>
    /// <returns>The LLM response text</returns>
    Task<string> AnalyzeImageAsync(Bitmap image, string prompt, string model);

    /// <summary>
    /// Event fired when Ollama availability status changes
    /// </summary>
    event EventHandler<OllamaStatusEventArgs>? StatusChanged;

    /// <summary>
    /// Current availability status
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Last error message if not available
    /// </summary>
    string? LastError { get; }
}

/// <summary>
/// Event args for Ollama status changes
/// </summary>
public class OllamaStatusEventArgs : EventArgs
{
    public bool IsAvailable { get; set; }
    public string[] AvailableModels { get; set; } = [];
    public string? ErrorMessage { get; set; }
}
