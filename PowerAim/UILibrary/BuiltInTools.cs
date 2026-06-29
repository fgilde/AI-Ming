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

/// <summary>Built-in: the custom crosshair overlay. Start toggles it on/off; the panel holds its
/// appearance settings (shape, size, colours, detection flash). Toggling
/// <see cref="ToggleState.ShowCrosshairOverlay"/> shows/hides the overlay via its own setter.</summary>
public sealed class CrosshairTool : ToolDefinition
{
    public const string ToolId = "builtin:crosshair";

    public CrosshairTool()
    {
        Id = ToolId;
        Name = Locale.ToolCrosshair;
    }

    public override bool IsBuiltIn => true;
    public override bool IsEditable => false;
    public override string Subtitle => Locale.ToolBuiltIn;

    public override Task RunAsync(CancellationToken ct)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var ts = AppConfig.Current?.ToggleState;
            if (ts != null) ts.ShowCrosshairOverlay = !ts.ShowCrosshairOverlay;
        });
        return Task.CompletedTask;
    }
}

/// <summary>Built-in: Anti-AFK. Start toggles a background loop that nudges the mouse on an interval so
/// the session isn't flagged idle (no AFK-kick / queue drop). Delegates to <c>AntiAfkService</c>.</summary>
public sealed class AntiAfkTool : ToolDefinition
{
    public const string ToolId = "builtin:antiafk";

    public AntiAfkTool()
    {
        Id = ToolId;
        Name = Locale.ToolAntiAfk;
    }

    public override bool IsBuiltIn => true;
    public override bool IsEditable => false;
    public override string Subtitle => Locale.ToolBuiltIn;

    public override Task RunAsync(CancellationToken ct)
    {
        PowerAim.InputLogic.Tools.AntiAfkService.Toggle();
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
