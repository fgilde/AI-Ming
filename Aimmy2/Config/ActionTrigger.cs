using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Forms;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.Contracts;
using Aimmy2.Other;
using Aimmy2.Types;
using Nextended.Core;
using Nextended.Core.Extensions;

namespace Aimmy2.Config;

public class ActionTrigger : EditableNotificationObject
{
    private string _name;
    private bool _enabled;
    private bool _chargeMode;

    private ObservableCollection<StoredInputBinding> _actions = [MouseButtons.Left, GamepadSlider.RightTrigger];
    private ObservableCollection<StoredInputBinding> _antiTriggerKeys = new();
    private ObservableCollection<StoredInputBinding> _triggerKeys = new();
    private double _delay;
    private double _breakTime;
    private TriggerCheck _executionIntersectionCheck = TriggerCheck.HeadIntersectingCenter;
    private TriggerCheck _beginIntersectionCheck;
    private RelativeRect _beginIntersectionArea;
    private RelativeRect _executionIntersectionArea = RelativeRect.Default;
    private bool _needsDetection = true;
    private TriggerExecutionMode _executionMode = TriggerExecutionMode.Simultaneous;

    public ActionTrigger()
    {
        Id = Guid.NewGuid().ToFormattedId();
        AppConfig.ConfigLoaded += (sender, args) => DetectChanges();
    }

    private void DetectChanges()
    {
        if (AppConfig.Current?.ToggleState != null)
        {
            AppConfig.Current.ToggleState.PropertyChanged -= OnToogleStateChange;
            AppConfig.Current.ToggleState.PropertyChanged += OnToogleStateChange;
        }
    }

    private void OnToogleStateChange(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppConfig.Current.ToggleState.AutoTrigger) or nameof(AppConfig.Current.ToggleState.GlobalActive))
            RaisePropertyChanged(nameof(IsActive));
    }


    private StoredInputBinding[] _originalActions;
    private StoredInputBinding[] _originalKeys;
    private StoredInputBinding[] _originalAntiKeys;
    private KeyOperator _triggerKeysOperator;
    private KeyOperator _antiTriggerKeysOperator;

    public override void BeginEdit()
    {
        _originalActions = Actions?.ToArray() ?? [];
        _originalKeys = TriggerKeys?.ToArray() ?? [];
        _originalAntiKeys = AntiTriggerKeys?.ToArray() ?? [];
        base.BeginEdit();
    }

    public override void CancelEdit()
    {
        base.CancelEdit();
        TriggerKeys = new ObservableCollection<StoredInputBinding>(_originalKeys);
        AntiTriggerKeys = new ObservableCollection<StoredInputBinding>(_originalAntiKeys);
        Actions = new ObservableCollection<StoredInputBinding>(_originalActions);
    }

    protected override void RaisePropertyChanged(string propertyName)
    {
        if (propertyName is not nameof(Description))
            RaisePropertyChanged(nameof(Description));
        base.RaisePropertyChanged(propertyName);
    }

    public string Id { get; set; }

    public KeyOperator TriggerKeysOperator
    {
        get => _triggerKeysOperator;
        set => SetProperty(ref _triggerKeysOperator, value);
    }

    public KeyOperator AntiTriggerKeysOperator
    {
        get => _antiTriggerKeysOperator;
        set => SetProperty(ref _antiTriggerKeysOperator, value);
    }

    public TriggerExecutionMode ExecutionMode
    {
        get => _executionMode;
        set => SetProperty(ref _executionMode, value);
    }

    public bool NeedsDetection
    {
        get => _needsDetection;
        set => SetProperty(ref _needsDetection, value);
    }

    /// <summary>
    /// Name of your trigger
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
    /// Trigger is enabled
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
    ///When charge mode is enabled the <see cref="Action"/> will we pressed down when a prediction is available and released when the prediction is intersecting based on your <see cref="ExecutionIntersectionCheck"/>
    /// </summary>
    public bool ChargeMode
    {
        get => _chargeMode;
        set => SetProperty(ref _chargeMode, value);
    }


    /// <summary>
    /// Keys that will ensure not hold before trigger is executed
    /// </summary>
    public ObservableCollection<StoredInputBinding> Actions
    {
        get => _actions;
        set
        {
            if(_actions != null)
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
    /// Keys that needs to hold before trigger is executed
    /// </summary>
    public ObservableCollection<StoredInputBinding> TriggerKeys
    {
        get => _triggerKeys;
        set => SetProperty(ref _triggerKeys, value);
    }

    /// <summary>
    /// Keys that will ensure not hold before trigger is executed
    /// </summary>
    public ObservableCollection<StoredInputBinding> AntiTriggerKeys
    {
        get => _antiTriggerKeys;
        set => SetProperty(ref _antiTriggerKeys, value);
    }

    /// <summary>
    /// Delay before trigger is executed
    /// </summary>
    public double Delay
    {
        get => _delay;
        set => SetProperty(ref _delay, value);
    }

    /// <summary>
    /// If the trigger was executed it needs to wait for the break time before it will be executed again
    /// </summary>
    public double BreakTime
    {
        get => _breakTime;
        set => SetProperty(ref _breakTime, value);
    }

    /// <summary>
    /// Intersection check for the detected object before trigger is executed
    /// </summary>
    public TriggerCheck BeginIntersectionCheck
    {
        get => _beginIntersectionCheck;
        set => SetProperty(ref _beginIntersectionCheck, value);
    }

    /// <summary>
    /// Intersection area for the detected object before trigger is executed only if <see cref="ExecutionIntersectionCheck"/> is set to <see cref="TriggerCheck.HeadIntersectingCenter"/>
    /// </summary>
    public RelativeRect BeginIntersectionArea
    {
        get => _beginIntersectionArea;
        set => SetProperty(ref _beginIntersectionArea, value);
    }


    /// <summary>
    /// Intersection check for the detected object before trigger is executed
    /// </summary>
    public TriggerCheck ExecutionIntersectionCheck
    {
        get => _executionIntersectionCheck;
        set => SetProperty(ref _executionIntersectionCheck, value);
    }

    /// <summary>
    /// Intersection area for the detected object before trigger is executed only if <see cref="ExecutionIntersectionCheck"/> is set to <see cref="TriggerCheck.HeadIntersectingCenter"/>
    /// </summary>
    public RelativeRect ExecutionIntersectionArea
    {
        get => _executionIntersectionArea;
        set => SetProperty(ref _executionIntersectionArea, value);
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Actions.Any(a => a is { IsValid: true });
    public bool IsActive => IsValid && Enabled && /*AppConfig.Current.ToggleState.GlobalActive &&*/ AppConfig.Current.ToggleState.AutoTrigger;
    public string Description => this.GetDescription();

}