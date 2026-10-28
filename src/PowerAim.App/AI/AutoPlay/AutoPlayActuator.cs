using PowerAim.Config;
using PowerAim.Input;
using Nextended.UI.Input;

namespace PowerAim.AI.AutoPlay;

/// <summary>Where AutoPlay's key presses and mouse moves go. The default is <see cref="InputSender"/>.</summary>
public interface IAutoPlayInput
{
    void Send(StoredInputBinding key, KeyPressState state);
    void Move(int dx, int dy);
}

internal sealed class InputSenderInput : IAutoPlayInput
{
    public void Send(StoredInputBinding key, KeyPressState state) => _ = InputSender.SendKeyAsync(key, state);
    public void Move(int dx, int dy) => InputSender.Move(dx, dy);
}

/// <summary>
///     Turns a <see cref="ReflexCommand"/> into input: presses what should be held, releases what no longer
///     is, taps (press + scheduled release) and moves the mouse with the profile's sensitivity scale and
///     jitter. Nothing can stay down: a release goes to exactly the keys that were pressed (even if the action
///     was edited since), and a jittered release is queued behind its own press, never ahead of it.
///     Thread-safe: the AI thread drives it, the UI thread may call <see cref="ReleaseAll"/>.
/// </summary>
public sealed class AutoPlayActuator
{
    private readonly IAutoPlayInput _input;
    private readonly Random _rng;
    private readonly object _gate = new();
    private readonly Dictionary<AutoPlayAction, StoredInputBinding[]> _held = new();
    private readonly List<(AutoPlayAction Action, DateTime At)> _scheduled = new();
    private readonly Dictionary<AutoPlayAction, Task> _pending = new(); // jittered sends, per action, in order

    public AutoPlayActuator(IAutoPlayInput? input = null, Random? rng = null)
    {
        _input = input ?? new InputSenderInput();
        _rng = rng ?? new Random();
    }

    public bool IsHeld(AutoPlayAction action) { lock (_gate) return _held.ContainsKey(action); }

    public void Apply(ReflexCommand cmd, AutoPlayActionMap map, AutoPlayProfile profile, DateTime now)
    {
        lock (_gate)
        {
            ReleaseDue(now);

            foreach (var role in AutoPlayActionMap.ReflexOwned)
            {
                var action = map.Get(role);
                if (action == null) continue;
                if (cmd.Hold.Contains(role)) Press(action, profile.KeyDelayJitterMs);
                else if (!IsScheduled(action)) Release(action, profile.KeyDelayJitterMs);
            }

            foreach (var role in cmd.Tap)
                if (map.Get(role) is { } action) TapLocked(action, now);
        }
        Move(cmd.MouseDx, cmd.MouseDy, profile);
    }

    /// <summary>
    ///     Press now and release after the action's <see cref="AutoPlayAction.Duration"/> (at least 50 ms).
    ///     Used for every model-chosen action, whatever its type, so nothing a model triggers stays held.
    /// </summary>
    public void Tap(AutoPlayAction action, DateTime now)
    {
        lock (_gate) TapLocked(action, now);
    }

    /// <summary>Releases every key AutoPlay pressed, after any press still waiting on its jitter delay.</summary>
    public void ReleaseAll()
    {
        lock (_gate)
        {
            foreach (var action in _held.Keys.ToList()) Release(action, jitterMs: 0);
            _scheduled.Clear();
        }
    }

    private void TapLocked(AutoPlayAction action, DateTime now)
    {
        if (!action.IsValid || _held.ContainsKey(action)) return;
        Press(action, jitterMs: 0);
        _scheduled.Add((action, now.AddSeconds(Math.Max(action.Duration, 0.05))));
    }

    private void ReleaseDue(DateTime now)
    {
        for (int i = _scheduled.Count - 1; i >= 0; i--)
        {
            if (_scheduled[i].At > now) continue;
            var action = _scheduled[i].Action;
            _scheduled.RemoveAt(i);
            Release(action, jitterMs: 0);
        }
    }

    private bool IsScheduled(AutoPlayAction action) => _scheduled.Exists(s => s.Action == action);

    private void Press(AutoPlayAction action, int jitterMs)
    {
        if (_held.ContainsKey(action)) return;
        var keys = action.Keys.Where(k => k.IsValid).ToArray();
        if (keys.Length == 0) return;
        _held[action] = keys;
        Send(action, keys, KeyPressState.Down, jitterMs);
    }

    private void Release(AutoPlayAction action, int jitterMs)
    {
        if (_held.Remove(action, out var keys)) Send(action, keys, KeyPressState.Up, jitterMs);
    }

    /// <summary>
    ///     Sends now when nothing for this action is still waiting; otherwise (or with jitter) queues behind
    ///     the action's previous send, so its Down and Up always arrive in order.
    /// </summary>
    private void Send(AutoPlayAction action, StoredInputBinding[] keys, KeyPressState state, int jitterMs)
    {
        bool waiting = _pending.TryGetValue(action, out var previous) && !previous.IsCompleted;
        if (jitterMs <= 0 && !waiting)
        {
            foreach (var key in keys) _input.Send(key, state);
            return;
        }
        int delay = jitterMs > 0 ? _rng.Next(0, jitterMs + 1) : 0;
        _pending[action] = (previous ?? Task.CompletedTask).ContinueWith(async _ =>
        {
            if (delay > 0) await Task.Delay(delay);
            foreach (var key in keys) _input.Send(key, state);
        }, TaskScheduler.Default).Unwrap();
    }

    private void Move(int dx, int dy, AutoPlayProfile profile)
    {
        if (dx == 0 && dy == 0) return;
        double scale = profile.MouseSensScale;
        if (scale != 1.0)
        {
            dx = (int)Math.Round(dx * scale);
            dy = (int)Math.Round(dy * scale);
        }
        int jitter = profile.MouseJitterPx;
        if (jitter > 0)
        {
            dx += _rng.Next(-jitter, jitter + 1);
            dy += _rng.Next(-jitter, jitter + 1);
        }
        if (dx != 0 || dy != 0) _input.Move(dx, dy);
    }
}
