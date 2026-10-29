using System.Windows;

namespace PowerAim.UI.Visuality;

/// <summary>
///     Shown at startup before the virtual pad exists: games read the lowest XInput slot and Windows hands
///     slots out by arrival, so the physical pad has to come after the virtual one. Same look and
///     "show on startup" switch as <see cref="KnownIssuesDialog"/>.
/// </summary>
public sealed class GamepadOrderHintDialog : KnownIssuesDialog
{
    public GamepadOrderHintDialog() : base(Locale.GamepadOrderHint)
    {
        Header = Locale.GamepadOrderHintTitle;
        Title = Locale.GamepadOrderHintTitle;
        ApplyButton.Content = Locale.Continue;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Height = 560;
    }

    /// <summary>Modal, so the user can unplug the pad before the app goes on and creates the virtual one.</summary>
    public static void ShowIfWanted()
    {
        var hint = new GamepadOrderHintDialog();
        if (hint.GetWindowSettings()?.ShouldShow == false)
        {
            hint.Close();
            return;
        }
        hint.ShowDialog();
    }
}
