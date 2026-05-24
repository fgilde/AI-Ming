using PowerAim.Config;

namespace PowerAim.Class;

/// <summary>
///     Decides whether the AI loop should be paused based on the currently focused window.
///     <para>
///     <b>Goal:</b> stop the bot from moving the mouse / pressing keys while the user is doing
///     something else (alt-tabbed to Discord, configuring PowerAim itself, etc.) without
///     requiring the user to remember to flip GlobalActive every time.
///     </para>
///     <para>
///     <b>Strategy:</b>
///     <list type="number">
///       <item>If <see cref="ActiveProcessSettings.AutoPauseOnFocusLoss"/> is off → never pause.</item>
///       <item>If user has configured at least one <see cref="ActiveProcessSettings.GameProcessPatterns"/>
///             entry → only run when the focused process matches one of those patterns.</item>
///       <item>If no patterns are configured → pause when the focused process is in the
///             built-in <see cref="DefinitelyNotGames"/> list (browsers, terminals, devtools,
///             our own app, …), otherwise active.</item>
///     </list>
///     </para>
///     <para>
///     Purely a query helper — no events / no state mutation. Cheap to call every tick.
///     </para>
/// </summary>
public static class AutoPauseManager
{
    /// <summary>
    ///     Built-in list of process names that are obviously not games. Used when the user
    ///     hasn't configured a custom whitelist.
    /// </summary>
    public static readonly string[] DefinitelyNotGames =
    {
        // browsers
        "chrome", "firefox", "msedge", "opera", "brave", "vivaldi", "iexplore",
        // devtools
        "devenv", "code", "rider", "idea64", "studio64", "pycharm64", "phpstorm64",
        "notepad++", "notepad", "powershell", "pwsh", "windowsterminal", "cmd",
        // chat / productivity
        "slack", "discord", "teams", "zoom", "outlook", "thunderbird",
        // os shell
        "explorer", "taskmgr", "control", "snippingtool", "calc",
        // media
        "vlc", "mpc-hc64", "spotify",
    };

    /// <summary>
    ///     Should the AI action loop run right now? Returns <c>true</c> by default; only goes
    ///     <c>false</c> when the user has Auto-Pause enabled and the foreground process looks
    ///     like a non-game (or doesn't match the user's whitelist).
    /// </summary>
    public static bool ShouldBeActive()
    {
        var settings = AppConfig.Current?.ActiveProcessSettings;
        if (settings == null || !settings.AutoPauseOnFocusLoss) return true;

        var current = WindowFocusWatcher.Instance.CurrentProcessName;
        if (string.IsNullOrEmpty(current)) return true; // unknown foreground — give user the benefit of the doubt

        // If the user has configured a whitelist, only that whitelist counts.
        if (settings.GameProcessPatterns != null && settings.GameProcessPatterns.Count > 0)
        {
            foreach (var pattern in settings.GameProcessPatterns)
            {
                if (ProcessMatcher.Matches(pattern, current)) return true;
            }
            return false;
        }

        // No whitelist configured — fall back to the built-in non-game list.
        foreach (var blacklisted in DefinitelyNotGames)
        {
            if (string.Equals(current, blacklisted, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        // Also pause when our own app is foreground (user is configuring).
        if (IsOwnApp(current)) return false;

        return true;
    }

    private static bool IsOwnApp(string processName)
    {
        try
        {
            var self = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            return string.Equals(processName, self, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
