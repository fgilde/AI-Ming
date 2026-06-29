using System.Windows;
using PowerAim.Config;

namespace PowerAim.UILibrary;

// The two fixed, non-editable tools. They subclass ToolDefinition so the unified Tools list treats
// them like any other row (start keybind + start button + expandable panel), but they aren't
// serialized — they're injected into the display list at runtime by ToolsList. Their RunAsync just
// delegates to the existing MainWindow launchers (marshalled to the UI thread, since a tool start can
// fire off the dispatcher from a global keybind). Stable ids keep their per-tool keybind (stored in
// BindingSettings under USER_TOOL_<Id>) across restarts even though the instances are recreated.

/// <summary>Built-in: toggles the on-screen magnifier (delegates to <c>MainWindow.ToggleMagnifier</c>).</summary>
public sealed class MagnifierTool : ToolDefinition
{
    public const string ToolId = "builtin:magnifier";

    public MagnifierTool()
    {
        Id = ToolId;
        Name = Locale.Magnifier;
    }

    public override bool IsBuiltIn => true;
    public override bool IsEditable => false;
    public override string Subtitle => Locale.ToolBuiltIn;

    public override Task RunAsync(CancellationToken ct)
    {
        Application.Current?.Dispatcher.Invoke(() => MainWindow.Instance?.ToggleMagnifier());
        return Task.CompletedTask;
    }
}

/// <summary>Built-in: opens the HWID spoofer (delegates to <c>MainWindow.OpenSpoofer</c>).</summary>
public sealed class HwidSpooferTool : ToolDefinition
{
    public const string ToolId = "builtin:hwid";

    public HwidSpooferTool()
    {
        Id = ToolId;
        Name = Locale.HwidSpoofer;
    }

    public override bool IsBuiltIn => true;
    public override bool IsEditable => false;
    public override string Subtitle => Locale.ToolBuiltIn;

    public override Task RunAsync(CancellationToken ct)
    {
        Application.Current?.Dispatcher.Invoke(() => MainWindow.Instance?.OpenSpoofer());
        return Task.CompletedTask;
    }
}
