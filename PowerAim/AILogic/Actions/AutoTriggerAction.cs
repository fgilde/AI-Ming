using System.ComponentModel;
using PowerAim.AILogic.Actions;
using PowerAim.AILogic;
using PowerAim.Config;
using PowerAim.InputLogic;
using PowerAim.Types;


public class AutoTriggerAction : BaseAction
{
    private Prediction[] _lastPredictions = [];
    private readonly List<CancellationTokenSource> _autoTriggerCtsList = [];
    private readonly Dictionary<ActionTrigger, DateTime> _triggerCooldowns = []; // Track cooldowns for each trigger
    private readonly Dictionary<ActionTrigger, KeyPressState> _triggerKeyStates = []; // Store current key states for each trigger

    public AutoTriggerAction()
    {
        AppConfig.Current.ToggleState.PropertyChanged += CheckChange;
    }

    private void CheckChange(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppConfig.Current.ToggleState.AutoTrigger))
        {
            CancelAllTriggers();
        }
    }

    private void CancelAllTriggers()
    {
        foreach (var cts in _autoTriggerCtsList)
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        }
        _autoTriggerCtsList.Clear();
        _triggerKeyStates.Clear();
    }

    public override Task ExecuteAsync(Prediction[] predictions)
    {

        _lastPredictions = predictions;

        if (AppConfig.Current.ToggleState.AutoTrigger)
        {
            foreach (var trigger in AppConfig.Current.Triggers)
            {
                if (trigger is { Enabled: true, IsValid: true } && (!trigger.NeedsDetection || _lastPredictions.Length > 0))
                {
                    var triggerCts = new CancellationTokenSource();
                    _autoTriggerCtsList.Add(triggerCts);
                    _ = Task.Run(() => HandleTriggerAsync(trigger, triggerCts.Token), triggerCts.Token);
                }
            }
        }

        return Task.CompletedTask;
    }

    private async Task HandleTriggerAsync(ActionTrigger trigger, CancellationToken cancellationToken)
    {
        if (TriggerIsOnCooldown(trigger))
        {
            return; // Skip this trigger if it is still in cooldown
        }

        if (TriggerKeysStateCorrect(trigger) && OcrConditionsMet(trigger) && !AntiOcrConditionBlocks(trigger))
        {
            if (trigger.ChargeMode)
            {
                cancellationToken.Register(() => // On cancel ensure key is released
                {
                    if(_triggerKeyStates.TryGetValue(trigger, out var currentState) && currentState == KeyPressState.Down)
                    {
                        _= SendActionsAsync(trigger, KeyPressState.Up, false, TriggerExecutionMode.Simultaneous); // Cancel always simultaneous and as fast as possible
                    }
                });
            }

            var delay = TimeSpan.FromSeconds(trigger.Delay);
            var breakTime = TimeSpan.FromSeconds(Math.Max(trigger.BreakTime, trigger.ChargeMode ? .2 : 0)); // on chargemode, breaktime should be at least a half second
            var keyState = DetermineKeyState(trigger);

            if (keyState != null)
            {
                
                if (!_triggerKeyStates.TryGetValue(trigger, out var currentState) || currentState != keyState.Value || keyState.Value == KeyPressState.DownAndUp)
                {
                    await Task.Delay(delay, cancellationToken); // Delay for this specific trigger
                    await SendActionsAsync(trigger, keyState.Value, true);

                    // Update the state to the current one
                    _triggerKeyStates[trigger] = keyState.Value;

                    // If the key was released (Up), remove it from the state tracking
                    if (keyState.Value == KeyPressState.Up)
                    {
                        _triggerKeyStates.Remove(trigger);
                    }

                    SetTriggerCooldown(trigger, breakTime);
                }
            }
        }
    }

    private async Task SendActionsAsync(ActionTrigger trigger, KeyPressState keyState, bool waitMinTime, TriggerExecutionMode? modeOverwrite = null)
    {
        if (trigger.ExecutionMode == TriggerExecutionMode.Simultaneous || modeOverwrite == TriggerExecutionMode.Simultaneous)
        {
            await Task.WhenAll(trigger.Actions.Select(async action =>
            {
                if (waitMinTime && action.MinTime > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(action.MinTime));
                }
                await InputSender.SendKeyAsync(action, keyState);
            }));
        }
        else
        {
            foreach (var action in trigger.Actions)
            {
                if (waitMinTime && action.MinTime > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(action.MinTime));
                }
                await InputSender.SendKeyAsync(action, keyState);
            }
        }
    }



    private KeyPressState? DetermineKeyState(ActionTrigger trigger)
    {
        if (!trigger.NeedsDetection)
            return KeyPressState.DownAndUp;

        if (trigger.ChargeMode)
        {
            if (PredictionIsIntersecting(trigger.ExecutionIntersectionCheck, trigger.ExecutionIntersectionArea))
                return KeyPressState.Up;

            if (PredictionIsIntersecting(trigger.BeginIntersectionCheck, trigger.BeginIntersectionArea))
                return KeyPressState.Down;
        }
        else
        {
            if (PredictionIsIntersecting(trigger.ExecutionIntersectionCheck, trigger.ExecutionIntersectionArea))
                return KeyPressState.DownAndUp;
        }

        return null;
    }

    /// <summary>
    ///     Gate the trigger on its optional OCR conditions. Only enforced while the OCR engine is on;
    ///     all conditions must hold (AND). A region with no reading yet blocks the trigger.
    /// </summary>
    private static bool OcrConditionsMet(ActionTrigger trigger)
    {
        var tree = trigger.OcrConditionTree;
        if (tree == null || tree.IsEmpty) return true;          // no gate configured
        if (AppConfig.Current.OcrSettings is not { Enabled: true }) return true; // OCR off → don't block
        return tree.Evaluate(OcrService.Instance.Latest);
    }

    /// <summary>
    ///     Anti-OCR gate — the trigger is blocked when this tree evaluates to true. Mirrors
    ///     <c>AntiTriggerKeys</c> for OCR. Returns false when no gate is configured (default),
    ///     when OCR itself is off, or when the gate evaluates to false (i.e. the blocker doesn't
    ///     hold). Returns true only when the blocker actively holds.
    /// </summary>
    private static bool AntiOcrConditionBlocks(ActionTrigger trigger)
    {
        var tree = trigger.AntiOcrConditionTree;
        if (tree == null || tree.IsEmpty) return false;
        if (AppConfig.Current.OcrSettings is not { Enabled: true }) return false;
        return tree.Evaluate(OcrService.Instance.Latest);
    }

    private bool TriggerKeysStateCorrect(ActionTrigger trigger)
    {
        return (trigger.TriggerKeysOperator == KeyOperator.And ? AllKeysAreUnsetOrHold(trigger.TriggerKeys) : AnyKeyIsHold(trigger.TriggerKeys) )
            && (!HasValidKey(trigger.AntiTriggerKeys) || (trigger.AntiTriggerKeysOperator == KeyOperator.And ? AllKeysAreNotHold(trigger.AntiTriggerKeys) : AnyKeysIsNotHold(trigger.AntiTriggerKeys)));
    }
    
    // Issue #17: scan ALL current detections — fire when ANY target satisfies the check — rather than
    // testing one single (least-confident) prediction. This re-couples the trigger to whatever target
    // is actually under the crosshair, including the one the aim assist just pulled in.
    private bool PredictionIsIntersecting(TriggerCheck check, RelativeRect area)
    {
        if (check == TriggerCheck.None)
            return true;

        foreach (var prediction in _lastPredictions)
        {
            if (check == TriggerCheck.HeadIntersectingCenter && prediction.IsIntersectingCenter(area))
                return true;
            if (check == TriggerCheck.IntersectingCenter && prediction.IsIntersectingCenter())
                return true;
        }

        return false;
    }

    public override Task OnPause()
    {
        CancelAllTriggers();
        return base.OnPause();
    }

    private bool TriggerIsOnCooldown(ActionTrigger trigger)
    {
        if (_triggerCooldowns.TryGetValue(trigger, out var cooldownEnd))
        {
            if (DateTime.UtcNow < cooldownEnd)
            {
                return true; // Trigger is still on cooldown
            }
        }

        return false;
    }

    private void SetTriggerCooldown(ActionTrigger trigger, TimeSpan breakTime)
    {
        _triggerCooldowns[trigger] = DateTime.UtcNow.Add(breakTime); // Set cooldown end time
    }
}
