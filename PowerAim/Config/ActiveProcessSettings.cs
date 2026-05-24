using System.Collections.ObjectModel;

namespace PowerAim.Config;

/// <summary>
///     Settings around Auto-Pause and Per-Game Auto-Profile switching, both of which depend on
///     the currently focused process. See <see cref="PowerAim.Class.AutoPauseManager"/> and
///     <see cref="PowerAim.Class.WindowFocusWatcher"/>.
/// </summary>
public class ActiveProcessSettings : BaseSettings
{
    private bool _autoPauseOnFocusLoss = true;
    private bool _autoSwitchProfile = true;
    private ObservableCollection<string> _gameProcessPatterns = new();

    /// <summary>
    ///     When true, the AI loop pauses while the foreground window is a recognised non-game
    ///     (browser, terminal, our own app, …) or doesn't match one of the configured
    ///     <see cref="GameProcessPatterns"/>.
    /// </summary>
    public bool AutoPauseOnFocusLoss
    {
        get => _autoPauseOnFocusLoss;
        set => SetField(ref _autoPauseOnFocusLoss, value);
    }

    /// <summary>
    ///     When true, ActionTrigger / AutoPlayProfile entries with a non-empty
    ///     <c>MatchProcess</c> pattern only become <c>IsActive</c> while the foreground process
    ///     matches their pattern.
    /// </summary>
    public bool AutoSwitchProfile
    {
        get => _autoSwitchProfile;
        set => SetField(ref _autoSwitchProfile, value);
    }

    /// <summary>
    ///     User-defined whitelist of process-name patterns that count as "games". Wildcards
    ///     (<c>*</c>, <c>?</c>) and multi-pattern (<c>cs2|valorant</c>) are supported by
    ///     <see cref="PowerAim.Class.ProcessMatcher"/>. Empty list means "use the built-in
    ///     non-game fallback list".
    /// </summary>
    public ObservableCollection<string> GameProcessPatterns
    {
        get => _gameProcessPatterns;
        set => SetField(ref _gameProcessPatterns, value);
    }
}
