using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Nextended.Core;
using Nextended.Core.Extensions;

namespace Aimmy2.Config;

/// <summary>
/// Represents an AutoPlay profile containing actions and LLM configuration.
/// Similar to ActionTrigger but for AI-driven gameplay.
/// </summary>
public class AutoPlayProfile : EditableNotificationObject
{
    private string _name;
    private bool _enabled;
    private string _gameContext;
    private string _ollamaModel = "moondream";
    private double _decisionInterval = 1.0;
    private ObservableCollection<AutoPlayAction> _actions = new();

    private AutoPlayAction[] _originalActions;

    public AutoPlayProfile()
    {
        Id = Guid.NewGuid().ToFormattedId();
        AppConfig.ConfigLoaded += (sender, args) => DetectChanges();
    }

    private void DetectChanges()
    {
        if (AppConfig.Current?.ToggleState != null)
        {
            AppConfig.Current.ToggleState.PropertyChanged -= OnToggleStateChange;
            AppConfig.Current.ToggleState.PropertyChanged += OnToggleStateChange;
        }
    }

    private void OnToggleStateChange(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppConfig.Current.ToggleState.AutoPlay) or nameof(AppConfig.Current.ToggleState.GlobalActive))
            RaisePropertyChanged(nameof(IsActive));
    }

    public override void BeginEdit()
    {
        _originalActions = Actions?.ToArray() ?? [];
        base.BeginEdit();
    }

    public override void CancelEdit()
    {
        base.CancelEdit();
        Actions = new ObservableCollection<AutoPlayAction>(_originalActions);
    }

    protected override void RaisePropertyChanged(string propertyName)
    {
        if (propertyName is not nameof(Description))
            RaisePropertyChanged(nameof(Description));
        base.RaisePropertyChanged(propertyName);
    }

    /// <summary>
    /// Unique identifier for this profile
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Name of this profile
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                RaisePropertyChanged(nameof(IsValid));
                RaisePropertyChanged(nameof(IsActive));
            }
        }
    }

    /// <summary>
    /// Whether this profile is enabled
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetProperty(ref _enabled, value))
                RaisePropertyChanged(nameof(IsActive));
        }
    }

    /// <summary>
    /// Description of the game for the LLM to understand context.
    /// Example: "This is a 2D platformer. The player needs to avoid obstacles and collect coins."
    /// </summary>
    public string GameContext
    {
        get => _gameContext;
        set => SetProperty(ref _gameContext, value);
    }

    /// <summary>
    /// Ollama model to use for vision analysis.
    /// Recommended: "moondream" (fast), "llava:7b" (balanced), "qwen2.5-vl:7b" (quality)
    /// </summary>
    public string OllamaModel
    {
        get => _ollamaModel;
        set => SetProperty(ref _ollamaModel, value);
    }

    /// <summary>
    /// Minimum time in seconds between decisions.
    /// Lower = more responsive but higher load.
    /// Recommended: 0.5 - 2.0 seconds
    /// </summary>
    public double DecisionInterval
    {
        get => _decisionInterval;
        set => SetProperty(ref _decisionInterval, value);
    }

    /// <summary>
    /// Available actions for this profile
    /// </summary>
    public ObservableCollection<AutoPlayAction> Actions
    {
        get => _actions;
        set
        {
            if (_actions != null)
                _actions.CollectionChanged -= RaiseValidChange;
            if (SetProperty(ref _actions, value))
            {
                value.CollectionChanged += RaiseValidChange;
                RaiseValidChange(null, null);
            }
        }
    }

    private void RaiseValidChange(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(IsValid));
        RaisePropertyChanged(nameof(IsActive));
    }

    /// <summary>
    /// Whether this profile has valid configuration
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Actions.Any(a => a is { IsValid: true });

    /// <summary>
    /// Whether this profile is currently active (valid, enabled, and AutoPlay is on)
    /// </summary>
    public bool IsActive => IsValid && Enabled && AppConfig.Current.ToggleState.AutoPlay;

    /// <summary>
    /// Display description for UI
    /// </summary>
    public string Description => $"{Name ?? "New Profile"} ({Actions.Count(a => a.IsValid)} actions, {OllamaModel})";
}
