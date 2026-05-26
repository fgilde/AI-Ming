using System.Collections.ObjectModel;

namespace PowerAim.Config;

/// <summary>
///     Settings around Auto-Pause and Per-Game Auto-Profile switching, both of which depend on
///     the currently focused process. See <see cref="PowerAim.Class.AutoPauseManager"/> and
///     <see cref="PowerAim.Class.WindowFocusWatcher"/>.
/// </summary>
public class ActiveProcessSettings : BaseSettings
{
    /// <summary>
    ///     When true, the AI loop pauses while the foreground window is a recognised non-game
    ///     (browser, terminal, our own app, …) or doesn't match one of the configured
    ///     <see cref="GameProcessPatterns"/>.
    /// </summary>
    public bool AutoPauseOnFocusLoss
    {
        get;
        set => SetField(ref field, value);
    } = true;

    /// <summary>
    ///     When true, ActionTrigger / AutoPlayProfile entries with a non-empty
    ///     <c>MatchProcess</c> pattern only become <c>IsActive</c> while the foreground process
    ///     matches their pattern.
    /// </summary>
    public bool AutoSwitchProfile
    {
        get;
        set => SetField(ref field, value);
    } = true;

    /// <summary>
    ///     User-defined whitelist of process-name patterns that count as "games". Wildcards
    ///     (<c>*</c>, <c>?</c>) and multi-pattern (<c>cs2|valorant</c>) are supported by
    ///     <see cref="PowerAim.Class.ProcessMatcher"/>. Empty list means "use the built-in
    ///     non-game fallback list".
    /// </summary>
    public ObservableCollection<string> GameProcessPatterns
    {
        get;
        set => SetField(ref field, value);
    } = new();
}
