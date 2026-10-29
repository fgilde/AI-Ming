using System.Collections.Concurrent;
using System.ComponentModel;
using PowerAim.AI.Actions;
using PowerAim.AI;
using PowerAim.AI.Aiming;
using PowerAim.AI.Rules;
using PowerAim.Config;
using PowerAim.Input;
using PowerAim.Types;


public class AutoTriggerAction : BaseAction
{
    private Prediction[] _lastPredictions = [];
    // One in-flight handler per trigger, all cancellable through a single long-lived CTS. The old
    // shape spawned a fresh Task + CancellationTokenSource per trigger EVERY FRAME and only cleared
    // the CTS list on pause/toggle — hundreds of leaked CTS per second, plus concurrent handlers for
    // the same trigger (double-fires whenever trigger.Delay outlived a frame). The state maps are
    // concurrent because handlers run on thread-pool threads.
    private readonly ConcurrentDictionary<ActionTrigger, Task> _runningTriggers = [];
    private readonly ConcurrentDictionary<ActionTrigger, DateTime> _triggerCooldowns = []; // Track cooldowns for each trigger
    private readonly ConcurrentDictionary<ActionTrigger, KeyPressState> _triggerKeyStates = []; // Store current key states for each trigger
    private CancellationTokenSource _cancellation = new();
    // Per-trigger "check held long enough" state, stepped every frame (dwell + hold-while-true triggers).
    private readonly ConcurrentDictionary<ActionTrigger, DwellGate> _dwellGates = [];
    private readonly ConcurrentDictionary<ActionTrigger, bool> _armed = [];
    private readonly System.Diagnostics.Stopwatch _dwellClock = System.Diagnostics.Stopwatch.StartNew();

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
        var old = _cancellation;
        _cancellation = new CancellationTokenSource();
        try { old.Cancel(); } finally { old.Dispose(); }
        _runningTriggers.Clear();

        // Release any charge-mode key still held down. (Previously done via one CTS registration per
        // handler per frame — same effect, minus the per-frame registration pile-up.)
        foreach (var kv in _triggerKeyStates)
        {
            if (kv.Value == KeyPressState.Down)
                _ = SendActionsAsync(kv.Key, KeyPressState.Up, false, TriggerExecutionMode.Simultaneous); // Cancel always simultaneous and as fast as possible
        }
        _triggerKeyStates.Clear();
    }

    public override Task ExecuteAsync(Prediction[] predictions)
    {

        _lastPredictions = predictions;
        UpdateRuleTargets(predictions);

        if (AppConfig.Current.ToggleState.AutoTrigger)
        {
            var token = _cancellation.Token;
            foreach (var trigger in AppConfig.Current.Triggers)
            {
                if (trigger.DwellMs > 0 || trigger.HoldWhileTrue) StepArmed(trigger);
                // IsActive = valid + enabled + the per-trigger game filter (MatchProcess).
                if (trigger.IsActive && (!trigger.NeedsDetection || _lastPredictions.Length > 0))
                {
                    // At most one running handler per trigger — skip while the previous one (e.g. still
                    // inside its Delay) hasn't finished; a later frame re-dispatches it fresh.
                    if (_runningTriggers.TryGetValue(trigger, out var running) && !running.IsCompleted)
                        continue;
                    _runningTriggers[trigger] = Task.Run(() => HandleTriggerAsync(trigger, token), token);
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
            // (Charge-mode "release key on cancel" now lives centrally in CancelAllTriggers — a per-call
            // token.Register here would pile up one registration per dispatch on the long-lived CTS.)

            var delay = TimeSpan.FromSeconds(trigger.Delay);
            var breakTime = TimeSpan.FromSeconds(Math.Max(trigger.BreakTime, trigger.ChargeMode ? .2 : 0)); // on chargemode, breaktime should be at least a half second
            if (trigger.HoldWhileTrue) breakTime = TimeSpan.Zero; // the break follows the release instead
            var keyState = DetermineKeyState(trigger);

            if (keyState != null)
            {
                
                if (!_triggerKeyStates.TryGetValue(trigger, out var currentState) || currentState != keyState.Value || keyState.Value == KeyPressState.DownAndUp)
                {
                    await Task.Delay(delay, cancellationToken); // Delay for this specific trigger
                    await SendActionsAsync(trigger, keyState.Value, true, cancellationToken: cancellationToken);

                    // Update the state to the current one
                    _triggerKeyStates[trigger] = keyState.Value;

                    // If the key was released (Up), remove it from the state tracking
                    if (keyState.Value == KeyPressState.Up)
                    {
                        _triggerKeyStates.TryRemove(trigger, out _);
                    }

                    SetTriggerCooldown(trigger, breakTime);
                }
            }
        }
    }

    private async Task SendActionsAsync(ActionTrigger trigger, KeyPressState keyState, bool waitMinTime, TriggerExecutionMode? modeOverwrite = null, CancellationToken cancellationToken = default)
    {
        if (trigger.ExecutionMode == TriggerExecutionMode.Simultaneous || modeOverwrite == TriggerExecutionMode.Simultaneous)
        {
            await Task.WhenAll(trigger.Actions.Select(async action =>
            {
                if (waitMinTime && action.MinTime > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(action.MinTime), cancellationToken);
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
                    await Task.Delay(TimeSpan.FromSeconds(action.MinTime), cancellationToken);
                }
                await InputSender.SendKeyAsync(action, keyState);
            }
        }
    }



    /// <summary>
    ///     Every frame: does the trigger's whole condition (keys, OCR, execution check) hold, and for at
    ///     least its dwell time? A hold-while-true trigger whose condition lapses is released right here,
    ///     so it lets go even when the target vanishes and no handler gets dispatched any more.
    /// </summary>
    private void StepArmed(ActionTrigger trigger)
    {
        bool condition = trigger.IsActive
                         && (!trigger.NeedsDetection || _lastPredictions.Length > 0)
                         && TriggerKeysStateCorrect(trigger) && OcrConditionsMet(trigger) && !AntiOcrConditionBlocks(trigger)
                         && (!trigger.NeedsDetection || PredictionIsIntersecting(trigger, trigger.ExecutionIntersectionCheck, trigger.ExecutionIntersectionArea));
        bool armed = _dwellGates.GetOrAdd(trigger, _ => new DwellGate())
            .Update(condition, _dwellClock.Elapsed.TotalMilliseconds, trigger.DwellMs);
        _armed[trigger] = armed;

        if (trigger.HoldWhileTrue && !armed
            && _triggerKeyStates.TryGetValue(trigger, out var state) && state == KeyPressState.Down
            && _triggerKeyStates.TryRemove(trigger, out _))
        {
            _ = SendActionsAsync(trigger, KeyPressState.Up, false, TriggerExecutionMode.Simultaneous);
            SetTriggerCooldown(trigger, TimeSpan.FromSeconds(trigger.BreakTime));
        }
    }

    private bool IsArmed(ActionTrigger trigger) => _armed.TryGetValue(trigger, out var armed) && armed;

    private KeyPressState? DetermineKeyState(ActionTrigger trigger)
    {
        if (trigger.HoldWhileTrue)
        {
            bool down = _triggerKeyStates.TryGetValue(trigger, out var state) && state == KeyPressState.Down;
            return IsArmed(trigger) && !down ? KeyPressState.Down : null; // the release happens in StepArmed
        }

        if (!trigger.NeedsDetection)
            return trigger.DwellMs <= 0 || IsArmed(trigger) ? KeyPressState.DownAndUp : null;

        if (trigger.ChargeMode)
        {
            if (PredictionIsIntersecting(trigger, trigger.ExecutionIntersectionCheck, trigger.ExecutionIntersectionArea))
                return KeyPressState.Up;

            if (PredictionIsIntersecting(trigger, trigger.BeginIntersectionCheck, trigger.BeginIntersectionArea))
                return KeyPressState.Down;
        }
        else
        {
            if (PredictionIsIntersecting(trigger, trigger.ExecutionIntersectionCheck, trigger.ExecutionIntersectionArea)
                && (trigger.DwellMs <= 0 || IsArmed(trigger)))
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
    private bool PredictionIsIntersecting(ActionTrigger trigger, TriggerCheck check, RelativeRect area)
    {
        if (check == TriggerCheck.None)
            return true;

        if (check == TriggerCheck.Rule)
            return RuleHolds(trigger, area);

        foreach (var prediction in _lastPredictions)
        {
            if (check == TriggerCheck.HeadIntersectingCenter && prediction.IsIntersectingCenter(area))
                return true;
            if (check == TriggerCheck.IntersectingCenter && prediction.IsIntersectingCenter())
                return true;
        }

        return false;
    }

    // ---- Rule triggers: tracked targets (for motion) + the rule over them --------------------------

    private readonly TargetTracker _tracker = new();
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private double _lastFrameAt;
    private volatile IReadOnlyList<RuleTarget> _ruleTargets = [];

    /// <summary>
    ///     Tracks the detections only while some trigger uses a rule: hit probabilities need each target's
    ///     velocity. Confirmed, currently-seen tracks carry it; detections no confirmed track covers yet
    ///     (a target that just appeared) count too, with zero velocity, so they aren't a frame late.
    /// </summary>
    private void UpdateRuleTargets(Prediction[] predictions)
    {
        if (!AppConfig.Current.ToggleState.AutoTrigger || !AppConfig.Current.Triggers.Any(t => t is { Enabled: true, UsesRule: true }))
        {
            _ruleTargets = [];
            return;
        }
        double now = _clock.Elapsed.TotalSeconds;
        double dt = Math.Clamp(now - _lastFrameAt, 0.001, 0.25);
        _lastFrameAt = now;
        var seen = _tracker.Update(predictions, dt).Where(t => t.MissedFrames == 0).ToList();
        var targets = seen.Select(RuleTarget.From).ToList();
        targets.AddRange(predictions.Where(p => !seen.Exists(t => t.LastDetectionBox == p.Rectangle)).Select(RuleTarget.From));
        _ruleTargets = targets;
    }

    private bool RuleHolds(ActionTrigger trigger, RelativeRect headArea)
    {
        var (rule, _) = TriggerRules.Compile(trigger.HitRule);
        if (rule == null) return false;
        int size = Math.Max(1, global::PowerAim.AI.PredictionLogic.CurrentImageSize);
        var context = new RuleContext(size, new System.Drawing.PointF(size / 2f, size / 2f), headArea,
            trigger.ShotLatencyMs / 1000.0, trigger.AimSpreadPx, _lastPredictions.Length, OcrNumber);
        return TriggerRules.AnyMatch(rule, _ruleTargets, context);
    }

    internal static double? OcrNumber(string region)
        => OcrService.Instance.Latest.FirstOrDefault(kv => string.Equals(kv.Key, region, StringComparison.OrdinalIgnoreCase)).Value?.Number;

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
