using System.Collections.ObjectModel;
using System.Collections.Specialized;
using PowerAim.InputLogic;
using Nextended.Core;
using Nextended.Core.Extensions;

namespace PowerAim.Config;

/// <summary>
/// Defines how an action should be executed
/// </summary>
public enum AutoPlayActionType
{
    /// <summary>
    /// Action is held continuously until a different action is chosen
    /// </summary>
    Continuous,

    /// <summary>
    /// Action is executed once (tap) - like jump or reload
    /// </summary>
    Instant,

    /// <summary>
    /// Action can be combined with other actions (like sprint with movement)
    /// </summary>
    Modifier,

    /// <summary>
    /// Action toggles on/off - like crouch
    /// </summary>
    Toggle
}

/// <summary>
/// Represents a single action that can be executed during AutoPlay.
/// For example: "move_left", "jump", "attack"
/// </summary>
public class AutoPlayAction : EditableNotificationObject
{
    private StoredInputBinding[] _originalKeys;

    public AutoPlayAction()
    {
        Id = Guid.NewGuid().ToFormattedId();
    }

    public override void BeginEdit()
    {
        _originalKeys = Keys?.ToArray() ?? [];
        base.BeginEdit();
    }

    public override void CancelEdit()
    {
        base.CancelEdit();
        Keys = new ObservableCollection<StoredInputBinding>(_originalKeys);
    }

    protected override void RaisePropertyChanged(string propertyName)
    {
        if (propertyName is not nameof(DisplayText))
            RaisePropertyChanged(nameof(DisplayText));
        base.RaisePropertyChanged(propertyName);
    }

    /// <summary>
    /// Unique identifier for this action
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Name of the action (e.g., "move_left", "jump", "attack").
    /// This is what the LLM will output to trigger this action.
    /// </summary>
    public string Name
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RaisePropertyChanged(nameof(IsValid));
            }
        }
    }

    /// <summary>
    /// Description of what this action does.
    /// Used to provide context to the LLM.
    /// </summary>
    public string Description
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Keys/buttons to press when this action is executed
    /// </summary>
    public ObservableCollection<StoredInputBinding> Keys
    {
        get;
        set
        {
            if (field != null)
                field.CollectionChanged -= RaiseValidChange;
            if (SetProperty(ref field, value))
            {
                value.CollectionChanged += RaiseValidChange;
                RaiseValidChange(null, null);
            }
        }
    } = new();

    private void RaiseValidChange(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(IsValid));
    }

    /// <summary>
    /// How long to hold the key(s) in seconds (for Instant actions)
    /// </summary>
    public double Duration
    {
        get;
        set => SetProperty(ref field, value);
    } = 0.1;

    /// <summary>
    /// Type of action - determines how it's executed
    /// </summary>
    public AutoPlayActionType ActionType
    {
        get;
        set => SetProperty(ref field, value);
    } = AutoPlayActionType.Continuous;

    /// <summary>
    /// Whether this action has valid configuration
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Keys.Any(k => k is { IsValid: true });

    /// <summary>
    /// Display text for UI
    /// </summary>
    public string DisplayText => IsValid ? $"{Name}: {string.Join(", ", Keys.Where(k => k.IsValid).Select(k => k.Key))}" : Name ?? "New Action";
}
