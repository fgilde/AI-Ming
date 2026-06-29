using System.Collections.ObjectModel;
using Nextended.Core;

namespace PowerAim.Config;

/// <summary>How a <see cref="ToolOption"/> is presented on the tool panel + how its value is interpreted.</summary>
public enum ToolOptionType { String, Number, Bool, Path, Enum }

/// <summary>
///     A user-defined parameter on a custom tool's panel. Its <see cref="Name"/> is the variable token
///     referenced inside action arguments as <c>{Name}</c> (e.g. an exe's args), and <see cref="Value"/>
///     is what the panel binds to. Built-in tools don't use these (they keep their own controls).
/// </summary>
public class ToolOption : EditableNotificationObject
{
    /// <summary>Variable token name — referenced in action args as <c>{Name}</c>. Should be a simple identifier.</summary>
    public string Name { get; set => SetProperty(ref field, value ?? ""); } = "";

    public ToolOptionType Type { get; set => SetProperty(ref field, value); } = ToolOptionType.String;

    public string DefaultValue { get; set => SetProperty(ref field, value ?? ""); } = "";

    /// <summary>Current value the panel edits; falls back to <see cref="DefaultValue"/> when empty.</summary>
    public string Value { get; set => SetProperty(ref field, value ?? ""); } = "";

    /// <summary>Choices for <see cref="ToolOptionType.Enum"/>.</summary>
    public ObservableCollection<string> EnumValues { get; set => SetProperty(ref field, value ?? new()); } = new();

    // Bounds for Number-type options (rendered as a slider).
    public double Min { get; set => SetProperty(ref field, value); }
    public double Max { get; set => SetProperty(ref field, value); } = 100;
    public double Step { get; set => SetProperty(ref field, value); } = 1;

    /// <summary>The value to substitute / use — the current value, or the default when unset.</summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public string EffectiveValue => string.IsNullOrEmpty(Value) ? DefaultValue : Value;
}
