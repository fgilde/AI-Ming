using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Forms;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Contracts;
using PowerAim.Other;
using PowerAim.Types;
using Nextended.Core;
using Nextended.Core.Extensions;

namespace PowerAim.Config;

public class ActionTrigger : EditableNotificationObject
{
    private string _matchProcess = "";

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
    private OcrTriggerCondition[] _originalOcrConditions;

    public override void BeginEdit()
    {
        _originalActions = Actions?.ToArray() ?? [];
        _originalKeys = TriggerKeys?.ToArray() ?? [];
        _originalAntiKeys = AntiTriggerKeys?.ToArray() ?? [];
        // Deep-copy: the editor mutates condition rows in place, so a shallow snapshot wouldn't revert.
        _originalOcrConditions = OcrConditions?.Select(c => c.Clone()).ToArray() ?? [];
        base.BeginEdit();
    }

    public override void CancelEdit()
    {
        base.CancelEdit();
        TriggerKeys = new ObservableCollection<StoredInputBinding>(_originalKeys);
        AntiTriggerKeys = new ObservableCollection<StoredInputBinding>(_originalAntiKeys);
        Actions = new ObservableCollection<StoredInputBinding>(_originalActions);
        OcrConditions = new ObservableCollection<OcrTriggerCondition>(_originalOcrConditions);
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
        get;
        set => SetProperty(ref field, value);
    }

    public KeyOperator AntiTriggerKeysOperator
    {
        get;
        set => SetProperty(ref field, value);
    }

    public TriggerExecutionMode ExecutionMode
    {
        get;
        set => SetProperty(ref field, value);
    } = TriggerExecutionMode.Simultaneous;

    public bool NeedsDetection
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    /// <summary>
    /// Name of your trigger
    /// </summary>
    public string Name
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
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
        get;
        set
        {
            if (SetProperty(ref field, value))
                RaisePropertyChanged(nameof(IsActive));
        }
    }

    /// <summary>
    ///When charge mode is enabled the <see cref="Action"/> will we pressed down when a prediction is available and released when the prediction is intersecting based on your <see cref="ExecutionIntersectionCheck"/>
    /// </summary>
    public bool ChargeMode
    {
        get;
        set => SetProperty(ref field, value);
    }


    /// <summary>
    /// Keys that will ensure not hold before trigger is executed
    /// </summary>
    public ObservableCollection<StoredInputBinding> Actions
    {
        get;
        set
        {
            if(field != null)
                field.CollectionChanged -= RaiseValidChange;
            if (SetProperty(ref field, value))
            {
                value.CollectionChanged += RaiseValidChange;
                RaiseValidChange(null, null);
            }
        }
    } = [MouseButtons.Left, GamepadSlider.RightTrigger];

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
        get;
        set => SetProperty(ref field, value);
    } = new();

    /// <summary>
    /// Keys that will ensure not hold before trigger is executed
    /// </summary>
    public ObservableCollection<StoredInputBinding> AntiTriggerKeys
    {
        get;
        set => SetProperty(ref field, value);
    } = new();

    /// <summary>
    ///     Optional OCR gates: the trigger only fires while every condition holds against the live
    ///     <see cref="PowerAim.AILogic.OcrService.Latest"/> reading (e.g. ammo &gt; 5). Only enforced
    ///     when the OCR engine is enabled; empty = no OCR gating.
    /// </summary>
    public ObservableCollection<OcrTriggerCondition> OcrConditions
    {
        get;
        set => SetProperty(ref field, value);
    } = new();

    /// <summary>
    /// Delay before trigger is executed
    /// </summary>
    public double Delay
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// If the trigger was executed it needs to wait for the break time before it will be executed again
    /// </summary>
    public double BreakTime
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Intersection check for the detected object before trigger is executed
    /// </summary>
    public TriggerCheck BeginIntersectionCheck
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Intersection area for the detected object before trigger is executed only if <see cref="ExecutionIntersectionCheck"/> is set to <see cref="TriggerCheck.HeadIntersectingCenter"/>
    /// </summary>
    public RelativeRect BeginIntersectionArea
    {
        get;
        set => SetProperty(ref field, value);
    }


    /// <summary>
    /// Intersection check for the detected object before trigger is executed
    /// </summary>
    public TriggerCheck ExecutionIntersectionCheck
    {
        get;
        set => SetProperty(ref field, value);
    } = TriggerCheck.HeadIntersectingCenter;

    /// <summary>
    /// Intersection area for the detected object before trigger is executed only if <see cref="ExecutionIntersectionCheck"/> is set to <see cref="TriggerCheck.HeadIntersectingCenter"/>
    /// </summary>
    public RelativeRect ExecutionIntersectionArea
    {
        get;
        set => SetProperty(ref field, value);
    } = RelativeRect.Default;

    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Actions.Any(a => a is { IsValid: true });
    /// <summary>
    ///     Optional process-name pattern (wildcard / pipe-separated) that scopes this trigger
    ///     to a specific game. Empty (default) = active in every process. Matched via
    ///     <see cref="PowerAim.Class.ProcessMatcher"/> against the currently focused process.
    ///     Only consulted when <see cref="ActiveProcessSettings.AutoSwitchProfile"/> is on.
    /// </summary>
    public string MatchProcess
    {
        get => _matchProcess;
        set => SetProperty(ref _matchProcess, value ?? "");
    }

    /// <summary>
    ///     True when the trigger should fire right now: must be valid, enabled, AutoTrigger
    ///     toggle on, and (if Auto-Switch-Profile is enabled) the foreground process must
    ///     match <see cref="MatchProcess"/>.
    /// </summary>
    public bool IsActive =>
        IsValid &&
        Enabled &&
        AppConfig.Current.ToggleState.AutoTrigger &&
        ProcessMatchesIfFilterEnabled();

    private bool ProcessMatchesIfFilterEnabled()
    {
        var settings = AppConfig.Current.ActiveProcessSettings;
        if (settings == null || !settings.AutoSwitchProfile) return true;
        if (string.IsNullOrWhiteSpace(_matchProcess)) return true; // no filter on this entry
        return PowerAim.Class.ProcessMatcher.Matches(_matchProcess, PowerAim.Class.WindowFocusWatcher.Instance.CurrentProcessName);
    }
    public string Description => this.GetDescription();

}
