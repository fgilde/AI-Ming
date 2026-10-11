namespace PowerAim;

/// <summary>
///     De-dupes keybind-driven toggles. A keybind control (<c>AKeyChanger</c>) can end up subscribed
///     to the global hook more than once (e.g. after a <c>CreateUI</c> rebuild leaves a stale row
///     subscribed), so one key press raises its event twice. The settings toggles already swallow
///     this via an <c>updating</c> flag; the trigger / mapping-profile lists route through
///     <c>ApplyBindingEnabled</c> with no such guard, which makes the binding toggle twice (net zero)
///     and the notice fire twice. This swallows the duplicate within a short window, keyed by the
///     toggled object so different triggers stay independent.
/// </summary>
public static class KeybindToggleGuard
{
    private static readonly Dictionary<object, DateTime> _last = new();
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(250);

    public static bool ShouldHandle(object key)
    {
        var now = DateTime.UtcNow;
        lock (_last)
        {
            if (_last.TryGetValue(key, out var t) && now - t < Window) return false;
            _last[key] = now;
            return true;
        }
    }
}
