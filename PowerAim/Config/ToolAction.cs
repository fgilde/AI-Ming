using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Nextended.Core;
using PowerAim.InputLogic;

namespace PowerAim.Config;

/// <summary>Which mouse button a <see cref="ClickAction"/> presses (kept independent of WinForms in the model).</summary>
public enum ToolMouseButton { Left, Right, Middle }

/// <summary>
///     One step in a custom tool's action sequence. Polymorphic + serialized with a <c>$type</c>
///     discriminator exactly like <see cref="OcrConditionNode"/> (the proven pattern in this repo, so
///     System.Text.Json can round-trip the abstract list). The concrete subclasses are deliberately
///     plain data — execution lives in <c>ToolRunner</c> so the model stays serialization-only.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(MoveMouseAction), nameof(MoveMouseAction))]
[JsonDerivedType(typeof(ClickAction), nameof(ClickAction))]
[JsonDerivedType(typeof(SendKeysAction), nameof(SendKeysAction))]
[JsonDerivedType(typeof(RunExeAction), nameof(RunExeAction))]
[JsonDerivedType(typeof(DelayAction), nameof(DelayAction))]
public abstract class ToolAction : EditableNotificationObject
{
    /// <summary>Short label for the action-list row in the editor.</summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public abstract string DisplayText { get; }
}

/// <summary>Move the cursor. Coordinates are strings so they can embed option tokens like <c>{targetX}</c>.</summary>
public class MoveMouseAction : ToolAction
{
    public string X { get; set => SetProperty(ref field, value ?? "0"); } = "0";
    public string Y { get; set => SetProperty(ref field, value ?? "0"); } = "0";
    /// <summary>Relative delta (true, via the configured mouse backend) vs absolute screen pixels (false).</summary>
    public bool Relative { get; set => SetProperty(ref field, value); }

    public override string DisplayText => Relative ? $"Move mouse by {X}, {Y}" : $"Move mouse to {X}, {Y}";
}

/// <summary>Press a mouse button (down / up / down-and-up).</summary>
public class ClickAction : ToolAction
{
    public ToolMouseButton Button { get; set => SetProperty(ref field, value); } = ToolMouseButton.Left;
    public KeyPressState Mode { get; set => SetProperty(ref field, value); } = KeyPressState.DownAndUp;

    public override string DisplayText => $"{Mode} {Button} click";
}

/// <summary>Send key(s) — reuses the same multi-keybind (with record) control as ActionTrigger.</summary>
public class SendKeysAction : ToolAction
{
    public ObservableCollection<StoredInputBinding> Keys { get; set => SetProperty(ref field, value ?? new()); } = new();
    public KeyPressState Mode { get; set => SetProperty(ref field, value); } = KeyPressState.DownAndUp;

    /// <summary>Same Sequential/Simultaneous choice as ActionTrigger: send the keys one after another
    /// (honouring each key's recorded delay) or all at once.</summary>
    public TriggerExecutionMode ExecutionMode { get; set => SetProperty(ref field, value); } = TriggerExecutionMode.Simultaneous;

    public override string DisplayText => $"Send {Keys.Count} key(s)";
}

/// <summary>Launch an executable. <see cref="Path"/> and <see cref="Args"/> support <c>{Option}</c> tokens.</summary>
public class RunExeAction : ToolAction
{
    public string Path { get; set => SetProperty(ref field, value ?? ""); } = "";
    public string Args { get; set => SetProperty(ref field, value ?? ""); } = "";
    public bool AsAdmin { get; set => SetProperty(ref field, value); }
    public bool WaitForExit { get; set => SetProperty(ref field, value); }

    public override string DisplayText =>
        string.IsNullOrWhiteSpace(Path) ? "Run program" : $"Run {System.IO.Path.GetFileName(Path)}";
}

/// <summary>Wait. <see cref="Milliseconds"/> is a string so it can embed an option token.</summary>
public class DelayAction : ToolAction
{
    public string Milliseconds { get; set => SetProperty(ref field, value ?? "100"); } = "100";

    public override string DisplayText => $"Wait {Milliseconds} ms";
}
