using System.ComponentModel;
using Aimmy2.AILogic.Actions;
using Aimmy2.AILogic;
using Aimmy2.Config;
using Aimmy2.InputLogic;
using Aimmy2.Types;
using InputLogic;
using Aimmy2.InputLogic.Contracts;
public class AutoTriggerAction : BaseAction
{
    private Prediction? _lastPrediction;
    private List<CancellationTokenSource> _autoTriggerCtsList = new();
    private Dictionary<ActionTrigger, DateTime> _triggerCooldowns = new(); // Track cooldowns for each trigger

    public List<ActionTrigger> Triggers { get; set; } = new List<ActionTrigger>()
    {
        new ActionTrigger()
        {
            Enabled = true,
            TriggerKey = AppConfig.Current.BindingSettings.TriggerKey,
            Delay = AppConfig.Current.SliderSettings.AutoTriggerDelay,
            BreakTime = 5,
            TriggerKeyMin = AppConfig.Current.SliderSettings.TriggerKeyMin,
            IntersectionCheck = AppConfig.Current.DropdownState.TriggerCheck,
            IntersectionArea = RelativeRect.ParseOrDefault(AppConfig.Current.DropdownState.HeadArea)
        }
    };

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
    }

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        _lastPrediction = predictions.MinBy(p => p.Confidence);

        if (_lastPrediction != null && AppConfig.Current.ToggleState.AutoTrigger)
        {
            foreach (var trigger in Triggers)
            {
                if (trigger.Enabled)
                {
                    var triggerCts = new CancellationTokenSource();
                    _autoTriggerCtsList.Add(triggerCts);
                    _ = Task.Run(() => HandleTriggerAsync(trigger, _lastPrediction, triggerCts.Token), triggerCts.Token);
                }
            }
        }

        return Task.CompletedTask;
    }

    private async Task HandleTriggerAsync(ActionTrigger trigger, Prediction prediction, CancellationToken cancellationToken)
    {
        if (TriggerIsOnCooldown(trigger))
        {
            // Skip this trigger if it is still in cooldown
            return;
        }

        if (TriggerKeyUnsetOrHold(trigger.TriggerKey, trigger.TriggerKeyMin))
        {
            var delay = TimeSpan.FromSeconds(trigger.Delay);
            var breakTime = TimeSpan.FromSeconds(trigger.BreakTime);

            if (PredictionIsIntersecting(trigger.IntersectionCheck, trigger.IntersectionArea, prediction))
            {
                if (trigger.ChargeMode)
                {
                    if (!MouseManager.IsLeftDown)
                    {
                        await MouseManager.LeftDownUntil(() => Task.FromResult(TriggerKeyUnsetOrHold(trigger.TriggerKey, trigger.TriggerKeyMin) && PredictionIsIntersecting(trigger.IntersectionCheck, trigger.IntersectionArea, prediction)), delay, cancellationToken);
                    }
                }
                else
                {
                    await Task.Delay(delay, cancellationToken); // Delay for this specific trigger
                    await InputSender.SendKeyAsync(trigger.Action);
                   // await MouseManager.DoTriggerClick(); // Perform the action

                    // After execution, set the cooldown
                    SetTriggerCooldown(trigger, breakTime);
                }
            }
        }
    }

    private bool TriggerKeyUnsetOrHold(StoredInputBinding triggerKey, double triggerKeyMin)
    {
        return !triggerKey.IsValid || InputBindingManager.IsHoldingBindingFor(triggerKey, TimeSpan.FromSeconds(triggerKeyMin));
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

    // Check if the trigger is on cooldown
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

    // Set the cooldown for a trigger after it has been executed
    private void SetTriggerCooldown(ActionTrigger trigger, TimeSpan breakTime)
    {
        _triggerCooldowns[trigger] = DateTime.UtcNow.Add(breakTime); // Set cooldown end time
    }
}
