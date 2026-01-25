using System.ComponentModel;
using System.Drawing;
using System.Text;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Aimmy2.InputLogic;

namespace Aimmy2.AILogic.Actions;

/// <summary>
/// Action that uses Ollama vision models to automatically play games.
/// Analyzes screen captures and decides which action to execute.
/// </summary>
public class AutoPlayGameAction : BaseAction
{
    private readonly IOllamaClient _ollamaClient;
    private DateTime _lastDecisionTime = DateTime.MinValue;
    private CancellationTokenSource? _cts;
    private bool _isProcessing;
    private string? _lastActionName;

    public AutoPlayGameAction()
    {
        _ollamaClient = new OllamaClient();
        AppConfig.Current.ToggleState.PropertyChanged += OnToggleStateChanged;
    }

    private void OnToggleStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppConfig.Current.ToggleState.AutoPlay))
        {
            if (!AppConfig.Current.ToggleState.AutoPlay)
            {
                CancelCurrentOperation();
            }
        }
    }

    public override bool Active => base.Active && AppConfig.Current.ToggleState.AutoPlay;

    public override async Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active || _isProcessing)
            return;

        var activeProfile = GetActiveProfile();
        if (activeProfile == null)
            return;

        // Rate limiting based on DecisionInterval
        var timeSinceLastDecision = (DateTime.Now - _lastDecisionTime).TotalSeconds;
        if (timeSinceLastDecision < activeProfile.DecisionInterval)
            return;

        // Check if Ollama is available
        if (!_ollamaClient.IsAvailable)
        {
            var available = await _ollamaClient.IsAvailableAsync();
            if (!available)
                return;
        }

        _isProcessing = true;
        _lastDecisionTime = DateTime.Now;

        try
        {
            _cts = new CancellationTokenSource();

            // Get current screen capture
            var capture = ImageCapture?.LastCapture;
            if (capture == null)
                return;

            // Build prompt
            var prompt = BuildPrompt(activeProfile, predictions);

            // Get decision from Ollama
            var response = await _ollamaClient.AnalyzeImageAsync(
                capture,
                prompt,
                activeProfile.OllamaModel);

            if (_cts.IsCancellationRequested)
                return;

            // Parse and execute action
            var actionToExecute = ParseActionFromResponse(response, activeProfile);
            if (actionToExecute != null)
            {
                _lastActionName = actionToExecute.Name;
                await ExecuteAutoPlayActionAsync(actionToExecute);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled, ignore
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AutoPlay error: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private AutoPlayProfile? GetActiveProfile()
    {
        return AppConfig.Current.AutoPlayProfiles.FirstOrDefault(p => p.IsActive);
    }

    private string BuildPrompt(AutoPlayProfile profile, Prediction[] predictions)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an AI game assistant controlling a game character.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(profile.GameContext))
        {
            sb.AppendLine("Game context:");
            sb.AppendLine(profile.GameContext);
            sb.AppendLine();
        }

        sb.AppendLine("Available actions:");
        foreach (var action in profile.Actions.Where(a => a.IsValid))
        {
            var description = string.IsNullOrWhiteSpace(action.Description)
                ? $"Execute {action.Name}"
                : action.Description;
            sb.AppendLine($"- {action.Name}: {description}");
        }
        sb.AppendLine("- idle: Do nothing");
        sb.AppendLine();

        // Include detection info if available
        if (predictions.Length > 0)
        {
            sb.AppendLine($"Detected {predictions.Length} object(s) in the scene.");
        }

        sb.AppendLine();
        sb.AppendLine("Look at the screenshot and decide what action to take.");
        sb.AppendLine("IMPORTANT: Respond with ONLY the action name, nothing else.");
        sb.AppendLine("Example response: move_left");

        return sb.ToString();
    }

    private AutoPlayAction? ParseActionFromResponse(string response, AutoPlayProfile profile)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        var cleanResponse = response.Trim().ToLowerInvariant();

        // Remove common prefixes that models might add
        cleanResponse = cleanResponse
            .Replace("action:", "")
            .Replace("answer:", "")
            .Replace("response:", "")
            .Trim();

        // Check for "idle" first
        if (cleanResponse == "idle" || cleanResponse.Contains("do nothing") || cleanResponse.Contains("wait"))
            return null;

        // Try exact match first
        var exactMatch = profile.Actions.FirstOrDefault(a =>
            a.IsValid && a.Name.Equals(cleanResponse, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
            return exactMatch;

        // Try contains match
        var containsMatch = profile.Actions.FirstOrDefault(a =>
            a.IsValid && (cleanResponse.Contains(a.Name.ToLowerInvariant()) ||
                         a.Name.ToLowerInvariant().Contains(cleanResponse)));
        if (containsMatch != null)
            return containsMatch;

        // Try word match (response contains action name as a word)
        var words = cleanResponse.Split([' ', ',', '.', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var wordMatch = profile.Actions.FirstOrDefault(a =>
            a.IsValid && words.Any(w => w.Equals(a.Name.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)));

        return wordMatch;
    }

    private async Task ExecuteAutoPlayActionAsync(AutoPlayAction action)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(action.Duration, 0.05));

        foreach (var key in action.Keys.Where(k => k.IsValid))
        {
            // Press down
            await InputSender.SendKeyAsync(key, KeyPressState.Down);
        }

        // Hold for duration
        await Task.Delay(duration);

        foreach (var key in action.Keys.Where(k => k.IsValid))
        {
            // Release
            await InputSender.SendKeyAsync(key, KeyPressState.Up);
        }
    }

    private void CancelCurrentOperation()
    {
        _cts?.Cancel();
        _isProcessing = false;
    }

    public override Task OnPause()
    {
        CancelCurrentOperation();
        return base.OnPause();
    }

    public override void Dispose()
    {
        CancelCurrentOperation();
        _ollamaClient.Dispose();
        AppConfig.Current.ToggleState.PropertyChanged -= OnToggleStateChanged;
        base.Dispose();
    }
}
