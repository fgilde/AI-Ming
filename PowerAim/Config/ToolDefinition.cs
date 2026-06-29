using System.Collections.ObjectModel;
using Nextended.Core;
using Nextended.Core.Extensions;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Tools;

namespace PowerAim.Config;

/// <summary>
///     A tool shown on the Tools page. Built-in tools (Magnifier, HWID spoofer) and user-built custom
///     tools share this base so the list, per-tool start keybind, start button and expander panel are
///     uniform; only <see cref="IsEditable"/> gates whether the user can edit/delete it. A tool runs
///     once per start (key or button) via <see cref="RunAsync"/>.
/// </summary>
public abstract class ToolDefinition : EditableNotificationObject
{
    protected ToolDefinition() => Id = Guid.NewGuid().ToFormattedId();

    public string Id { get; set; }

    public string Name
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? ""))
                RaisePropertyChanged(nameof(IsValid));
        }
    } = "";

    /// <summary>Disabled tools stay in the list but their keybind/start are ignored.</summary>
    public bool Enabled { get; set => SetProperty(ref field, value); } = true;

    // The per-tool start keybind is NOT a field here — it is stored in BindingSettings under
    // USER_TOOL_<Id> via the list row's AKeyChanger (same pattern as aim profiles / triggers), which
    // is why built-in tools use STABLE ids (set in their ctor) so their keybind survives restarts even
    // though the built-in instances themselves aren't serialized.

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public abstract bool IsBuiltIn { get; }

    /// <summary>Custom tools are editable; built-ins are not (Edit/Delete hidden, panel read-only).</summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public abstract bool IsEditable { get; }

    public bool IsValid => !string.IsNullOrWhiteSpace(Name);

    /// <summary>Runtime-only: whether this row's panel is expanded in the Tools list.</summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsExpanded { get; set => SetProperty(ref field, value); }

    /// <summary>Secondary line under the name in the Tools list (e.g. "Built-in" / action count).</summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public virtual string Subtitle => "";

    /// <summary>Run the tool once. Built-ins delegate to their launcher; custom tools run their sequence.</summary>
    public abstract Task RunAsync(CancellationToken ct);
}

/// <summary>
///     A user-built tool: an ordered sequence of <see cref="Actions"/> plus user-defined
///     <see cref="Options"/> (variables usable in action args as <c>{Name}</c>). This is the only
///     <see cref="ToolDefinition"/> kind that is persisted (in <see cref="AppConfig.UserTools"/>);
///     built-ins are injected into the display list at runtime.
/// </summary>
public class CustomTool : ToolDefinition
{
    public override bool IsBuiltIn => false;
    public override bool IsEditable => true;

    public override string Subtitle => string.Format(Locale.ToolActionCountFormat, Actions.Count);

    // ?? new() so an explicit "Options": null / "Actions": null in a hand-edited or foreign config
    // can't leave a null collection that NREs the panel, runner and editor.
    public ObservableCollection<ToolOption> Options { get; set => SetProperty(ref field, value ?? new()); } = new();
    public ObservableCollection<ToolAction> Actions { get; set => SetProperty(ref field, value ?? new()); } = new();

    public override Task RunAsync(CancellationToken ct) => ToolRunner.RunAsync(this, ct);
}
