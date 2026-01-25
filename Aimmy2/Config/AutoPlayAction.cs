using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Aimmy2.InputLogic;
using Nextended.Core;
using Nextended.Core.Extensions;

namespace Aimmy2.Config;

/// <summary>
/// Represents a single action that can be executed during AutoPlay.
/// For example: "move_left", "jump", "attack"
/// </summary>
public class AutoPlayAction : EditableNotificationObject
{
    private string _name;
    private string _description;
    private ObservableCollection<StoredInputBinding> _keys = new();
    private double _duration = 0.1;

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
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
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
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// Keys/buttons to press when this action is executed
    /// </summary>
    public ObservableCollection<StoredInputBinding> Keys
    {
        get => _keys;
        set
        {
            if (_keys != null)
                _keys.CollectionChanged -= RaiseValidChange;
            if (SetProperty(ref _keys, value))
            {
                value.CollectionChanged += RaiseValidChange;
                RaiseValidChange(null, null);
            }
        }
    }

    private void RaiseValidChange(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(IsValid));
    }

    /// <summary>
    /// How long to hold the key(s) in seconds
    /// </summary>
    public double Duration
    {
        get => _duration;
        set => SetProperty(ref _duration, value);
    }

    /// <summary>
    /// Whether this action has valid configuration
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Keys.Any(k => k is { IsValid: true });

    /// <summary>
    /// Display text for UI
    /// </summary>
    public string DisplayText => IsValid ? $"{Name}: {string.Join(", ", Keys.Where(k => k.IsValid).Select(k => k.Key))}" : Name ?? "New Action";
}
