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
///     jitter. Every press is tracked, so <see cref="ReleaseAll"/> leaves no key stuck down.
/// </summary>
public sealed class AutoPlayActuator
{
    private readonly IAutoPlayInput _input;
    private readonly Random _rng;
    private readonly HashSet<AutoPlayAction> _held = new();
    private readonly List<(AutoPlayAction Action, DateTime At)> _scheduled = new();

    public AutoPlayActuator(IAutoPlayInput? input = null, Random? rng = null)
    {
        _input = input ?? new InputSenderInput();
        _rng = rng ?? new Random();
    }

    public bool IsHeld(AutoPlayAction action) => _held.Contains(action);

    public void Apply(ReflexCommand cmd, AutoPlayActionMap map, AutoPlayProfile profile, DateTime now)
    {
        ReleaseDue(now);

        foreach (var role in AutoPlayActionMap.ReflexOwned)
        {
            var action = map.Get(role);
            if (action == null) continue;
            if (cmd.Hold.Contains(role)) Press(action, profile);
            else if (!IsScheduled(action)) Release(action, profile);
        }

        foreach (var role in cmd.Tap)
            if (map.Get(role) is { } action) Tap(action, now);

        Move(cmd.MouseDx, cmd.MouseDy, profile);
    }

    /// <summary>
    ///     Press now and release after the action's <see cref="AutoPlayAction.Duration"/> (at least 50 ms).
    ///     Used for every model-chosen action, whatever its type, so nothing a model triggers stays held.
    /// </summary>
    public void Tap(AutoPlayAction action, DateTime now)
    {
        if (!action.IsValid || !_held.Add(action)) return;
        Send(action, KeyPressState.Down);
        _scheduled.Add((action, now.AddSeconds(Math.Max(action.Duration, 0.05))));
    }

    public void ReleaseAll()
    {
        foreach (var action in _held) Send(action, KeyPressState.Up);
        _held.Clear();
        _scheduled.Clear();
    }

    private void ReleaseDue(DateTime now)
    {
        for (int i = _scheduled.Count - 1; i >= 0; i--)
        {
            if (_scheduled[i].At > now) continue;
            var action = _scheduled[i].Action;
            _scheduled.RemoveAt(i);
            if (_held.Remove(action)) Send(action, KeyPressState.Up);
        }
    }

    private bool IsScheduled(AutoPlayAction action) => _scheduled.Exists(s => s.Action == action);

    private void Press(AutoPlayAction action, AutoPlayProfile profile)
    {
        if (_held.Add(action)) SendJittered(action, KeyPressState.Down, profile);
    }

    private void Release(AutoPlayAction action, AutoPlayProfile profile)
    {
        if (_held.Remove(action)) SendJittered(action, KeyPressState.Up, profile);
    }

    private void Send(AutoPlayAction action, KeyPressState state)
    {
        foreach (var key in action.Keys.Where(k => k.IsValid)) _input.Send(key, state);
    }

    /// <summary>Hold/release with the profile's optional 0..N ms delay, off the tick thread.</summary>
    private void SendJittered(AutoPlayAction action, KeyPressState state, AutoPlayProfile profile)
    {
        int jitter = profile.KeyDelayJitterMs;
        if (jitter <= 0) { Send(action, state); return; }
        int delay = _rng.Next(0, jitter + 1);
        _ = Task.Run(async () =>
        {
            if (delay > 0) await Task.Delay(delay);
            Send(action, state);
        });
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
