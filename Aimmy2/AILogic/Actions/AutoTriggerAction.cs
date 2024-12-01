using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Aimmy2.AILogic.Actions;
using Aimmy2.AILogic;
using Aimmy2.Config;
using Aimmy2.InputLogic;
using Aimmy2.Types;
using InputLogic;
using System.Windows.Input;


public class AutoTriggerAction : BaseAction
{
    private Prediction? _lastPrediction;
    private List<CancellationTokenSource> _autoTriggerCtsList = new();
    private Dictionary<ActionTrigger, DateTime> _triggerCooldowns = new(); // Track cooldowns for each trigger
    private Dictionary<ActionTrigger, KeyPressState> _triggerKeyStates = new(); // Store current key states for each trigger

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
        _lastPrediction = predictions.MinBy(p => p.Confidence);

        if (AppConfig.Current.ToggleState.AutoTrigger)
        {
            foreach (var trigger in AppConfig.Current.Triggers)
            {
                if (trigger is { Enabled: true, IsValid: true } && (!trigger.NeedsDetection || _lastPrediction != null))
                {
                    var triggerCts = new CancellationTokenSource();
                    _autoTriggerCtsList.Add(triggerCts);
                    _ = Task.Run(() => HandleTriggerAsync(trigger, _lastPrediction, triggerCts.Token), triggerCts.Token);
                }
            }
        }

        return Task.CompletedTask;
    }

    private async Task HandleTriggerAsync(ActionTrigger trigger, Prediction? prediction, CancellationToken cancellationToken)
    {
        if (TriggerIsOnCooldown(trigger))
        {
            return; // Skip this trigger if it is still in cooldown
        }

        if (TriggerKeysStateCorrect(trigger))
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
            var keyState = DetermineKeyState(trigger, prediction);

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



    private KeyPressState? DetermineKeyState(ActionTrigger trigger, Prediction? prediction)
    {
        if (!trigger.NeedsDetection)
            return KeyPressState.DownAndUp;

        if (trigger.ChargeMode)
        {
            if (PredictionIsIntersecting(trigger.ExecutionIntersectionCheck, trigger.ExecutionIntersectionArea, prediction))
                return KeyPressState.Up;

            if (PredictionIsIntersecting(trigger.BeginIntersectionCheck, trigger.BeginIntersectionArea, prediction))
                return KeyPressState.Down;
        }
        else
        {
            if (PredictionIsIntersecting(trigger.ExecutionIntersectionCheck, trigger.ExecutionIntersectionArea, prediction))
                return KeyPressState.DownAndUp;
        }

        return null;
    }

    private bool TriggerKeysStateCorrect(ActionTrigger trigger)
    {
        return (trigger.TriggerKeysOperator == KeyOperator.And ? AllKeysAreUnsetOrHold(trigger.TriggerKeys) : AnyKeyIsHold(trigger.TriggerKeys) )
            && (!HasValidKey(trigger.AntiTriggerKeys) || (trigger.AntiTriggerKeysOperator == KeyOperator.And ? AllKeysAreNotHold(trigger.AntiTriggerKeys) : AnyKeysIsNotHold(trigger.AntiTriggerKeys)));
    }
    
    private bool PredictionIsIntersecting(TriggerCheck check, RelativeRect area, Prediction? prediction = null)
    {
        prediction ??= _lastPrediction;
        if (prediction == null)
        {
            return false;
        }
        return check == TriggerCheck.None
               || (check == TriggerCheck.HeadIntersectingCenter && prediction.IsIntersectingCenter(area))
               || (check == TriggerCheck.IntersectingCenter && prediction.IsIntersectingCenter());
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
