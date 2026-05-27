using PowerAim.Config;

namespace PowerAim;

/// <summary>
///     Shows the transient "X is now on/off" notice when a toggle is flipped via its global keybind.
///     Used by settings toggles (<c>AddToggleWithKeyBind</c>), the trigger list, and the mapping
///     profile list. Honors the <see cref="ToggleState.ShowToggleNotifications"/> setting and always
///     marshals to the UI thread (keybind handlers can run off the dispatcher).
/// </summary>
public static class Notifier
{
    public static void Notify(string? label, bool isOn)
    {
        if (AppConfig.Current?.ToggleState is not { ShowToggleNotifications: true }) return;
        if (string.IsNullOrWhiteSpace(label)) return;

        var fmt = isOn ? Locale.ToggleTurnedOnFormat : Locale.ToggleTurnedOffFormat;
        var message = string.Format(fmt, label);

        Notify(message);
    }

    public static void Notify(string message, int duration = 1800)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;
        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try { new global::Visuality.NoticeBar(message, duration).Show(); }
            catch { /* notice bar is best-effort feedback only */ }
        }));
    }
}
