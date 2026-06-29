using PowerAim.Config;

namespace PowerAim.InputLogic.Tools;

/// <summary>
///     Starts a <see cref="ToolDefinition"/> once (fire-once). Keeps one
///     <see cref="CancellationTokenSource"/> per tool id so a re-press while a tool is still running
///     cancels the in-flight run and starts a fresh one (cancel-and-restart) rather than overlapping.
///     Built-in tools complete instantly, so for them each press simply runs again.
/// </summary>
public static class ToolLauncher
{
    private static readonly Dictionary<string, CancellationTokenSource> Running = new();
    private static readonly object Gate = new();

    public static void Launch(ToolDefinition? tool)
    {
        if (tool is null || !tool.Enabled) return;

        CancellationTokenSource cts;
        lock (Gate)
        {
            // Cancel any still-running instance of this exact tool, then replace it. The old run's
            // finally only removes the dictionary entry if it still owns it (ReferenceEquals), so
            // replacing here is race-safe.
            if (Running.TryGetValue(tool.Id, out var existing))
                existing.Cancel();
            cts = new CancellationTokenSource();
            Running[tool.Id] = cts;
        }

        _ = RunAsync(tool, cts);
    }

    /// <summary>Stop a running tool without starting a new one (used when a tool is deleted/disabled).</summary>
    public static void Stop(string toolId)
    {
        lock (Gate)
            if (Running.TryGetValue(toolId, out var cts))
                cts.Cancel();
    }

    private static async Task RunAsync(ToolDefinition tool, CancellationTokenSource cts)
    {
        try
        {
            await tool.RunAsync(cts.Token);
        }
        catch (OperationCanceledException) { /* re-press / stop — expected */ }
        catch (Exception ex)
        {
            Notifier.Notify($"{tool.Name}: {ex.Message}");
        }
        finally
        {
            lock (Gate)
                if (Running.TryGetValue(tool.Id, out var owner) && ReferenceEquals(owner, cts))
                    Running.Remove(tool.Id);
            cts.Dispose();
        }
    }
}
