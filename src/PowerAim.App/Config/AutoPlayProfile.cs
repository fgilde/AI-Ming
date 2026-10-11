using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Nextended.Core;
using Nextended.Core.Extensions;

namespace PowerAim.Config;

/// <summary>
///     Which strategic layer biases AutoPlay's heuristic. Exactly one — the reflex layer (aim, shoot,
///     movement) always runs regardless.
/// </summary>
public enum AutoPlayDecisionBackend
{
    /// <summary>No strategic layer at all: heuristic + OCR cues only. No HTTP, no screenshots.</summary>
    Heuristic,

    /// <summary>Ollama vision model — sees the screen, slower (seconds), needs a local Ollama install.</summary>
    Ollama,

    /// <summary>Jev decision model — no eyes, decides from the structured state, ~0.1–0.5 s per decision.</summary>
    Jev,
}

/// <summary>
/// Represents an AutoPlay profile containing actions and LLM configuration.
/// Similar to ActionTrigger but for AI-driven gameplay.
/// </summary>
public class AutoPlayProfile : EditableNotificationObject
{
    private string _matchProcess = "";

    private AutoPlayAction[] _originalActions;

    public AutoPlayProfile()
    {
        Id = Guid.NewGuid().ToFormattedId();
        AppConfig.ConfigLoaded += (sender, args) => DetectChanges();
    }

    private void DetectChanges()
    {
        if (AppConfig.Current?.ToggleState != null)
        {
            AppConfig.Current.ToggleState.PropertyChanged -= OnToggleStateChange;
            AppConfig.Current.ToggleState.PropertyChanged += OnToggleStateChange;
        }
    }

    private void OnToggleStateChange(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppConfig.Current.ToggleState.AutoPlay) or nameof(AppConfig.Current.ToggleState.GlobalActive))
            RaisePropertyChanged(nameof(IsActive));
    }

    public override void BeginEdit()
    {
        _originalActions = Actions?.ToArray() ?? [];
        base.BeginEdit();
    }

    public override void CancelEdit()
    {
        base.CancelEdit();
        Actions = new ObservableCollection<AutoPlayAction>(_originalActions);
    }

    protected override void RaisePropertyChanged(string propertyName)
    {
        if (propertyName is not nameof(Description))
            RaisePropertyChanged(nameof(Description));
        base.RaisePropertyChanged(propertyName);
    }

    /// <summary>
    /// Unique identifier for this profile
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Name of this profile
    /// </summary>
    public string Name
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RaisePropertyChanged(nameof(IsValid));
                RaisePropertyChanged(nameof(IsActive));
            }
        }
    }

    /// <summary>
    /// Whether this profile is enabled
    /// </summary>
    public bool Enabled
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                RaisePropertyChanged(nameof(IsActive));
        }
    }

    /// <summary>
    /// Description of the game for the LLM to understand context.
    /// Example: "This is a 2D platformer. The player needs to avoid obstacles and collect coins."
    /// </summary>
    public string GameContext
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    ///     Legacy flag, kept so configs written before <see cref="DecisionBackend"/> existed still load
    ///     with the behaviour their author picked. Read it through <see cref="Backend"/>, never directly.
    /// </summary>
    public bool UseOllama
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                RaisePropertyChanged(nameof(Description));
        }
    } = true;

    /// <summary>Legacy flag — see <see cref="UseOllama"/>. Read through <see cref="Backend"/>.</summary>
    public bool UseJev
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                RaisePropertyChanged(nameof(Description));
        }
    }

    /// <summary>
    ///     The stored strategic-layer choice. <c>null</c> in configs written before this setting existed —
    ///     <see cref="Backend"/> then derives it from the legacy bools, so upgrading changes nothing.
    /// </summary>
    public AutoPlayDecisionBackend? DecisionBackend
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RaisePropertyChanged(nameof(Backend));
                RaisePropertyChanged(nameof(Description));
            }
        }
    }

    /// <summary>
    ///     Which strategic layer this profile runs. Setting it also writes the legacy bools, so a config
    ///     saved here still opens correctly in an older build.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public AutoPlayDecisionBackend Backend
    {
        get => DecisionBackend ?? (UseJev ? AutoPlayDecisionBackend.Jev
                                  : UseOllama ? AutoPlayDecisionBackend.Ollama
                                  : AutoPlayDecisionBackend.Heuristic);
        set
        {
            UseOllama = value == AutoPlayDecisionBackend.Ollama;
            UseJev = value == AutoPlayDecisionBackend.Jev;
            DecisionBackend = value;
        }
    }

    /// <summary>
    /// Ollama model to use for vision analysis.
    /// Recommended: "moondream" (fast), "llava:7b" (balanced), "qwen2.5-vl:7b" (quality)
    /// </summary>
    public string OllamaModel
    {
        get;
        set => SetProperty(ref field, value);
    } = "moondream";

    /// <summary>
    /// Minimum time in seconds between decisions.
    /// Lower = more responsive but higher load.
    /// Recommended: 0.5 - 2.0 seconds
    /// </summary>
    public double DecisionInterval
    {
        get;
        set => SetProperty(ref field, value);
    } = 0.5;

    /// <summary>
    ///     Multiplier applied to every AutoPlay mouse movement (ApplyMouseMove). 1.0 = recorded
    ///     magnitudes, 0.5 = half-strength (slower aim), 2.0 = double (faster). Useful when one
    ///     profile is for a high-sens game and another for low-sens. Doesn't affect general aim
    ///     — that has its own X/Y sens sliders.
    /// </summary>
    public double MouseSensScale
    {
        get;
        set => SetProperty(ref field, value);
    } = 1.0;

    /// <summary>
    ///     Anti-detection: random ±N pixel offset added to every AutoPlay mouse move so the
    ///     emitted path doesn't look perfectly deterministic. 0 (default) disables jitter and
    ///     preserves the original recorded behaviour. Typical values: 1–4 px for a subtle
    ///     human-ish wobble, higher for noticeably noisy aim.
    /// </summary>
    public int MouseJitterPx
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    ///     Anti-detection: random 0..N ms delay inserted before each key down/up burst so key
    ///     events don't all land on perfectly even tick boundaries. 0 (default) disables the
    ///     delay. The delay is fire-and-forget — it doesn't block the AutoPlay tick.
    /// </summary>
    public int KeyDelayJitterMs
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Available actions for this profile
    /// </summary>
    public ObservableCollection<AutoPlayAction> Actions
    {
        get;
        set
        {
            if (field != null)
                field.CollectionChanged -= RaiseValidChange;
            if (SetProperty(ref field, value))
            {
                value.CollectionChanged += RaiseValidChange;
                RaiseValidChange(null, null);
            }
        }
    } = new();

    private void RaiseValidChange(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(IsValid));
        RaisePropertyChanged(nameof(IsActive));
    }

    /// <summary>
    /// Whether this profile has valid configuration
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Actions.Any(a => a is { IsValid: true });

    /// <summary>
    /// Whether this profile is currently active (valid, enabled, and AutoPlay is on)
    /// </summary>
    /// <summary>
    ///     Optional process-name pattern (wildcard / pipe-separated) that scopes this profile
    ///     to a specific game. Empty (default) = active in every process. Matched via
    ///     <see cref="PowerAim.Class.ProcessMatcher"/> against the currently focused process.
    ///     Only consulted when <see cref="ActiveProcessSettings.AutoSwitchProfile"/> is on.
    /// </summary>
    public string MatchProcess
    {
        get => _matchProcess;
        set => SetProperty(ref _matchProcess, value ?? "");
    }

    /// <summary>True iff the profile should drive AutoPlay right now.</summary>
    public bool IsActive =>
        IsValid &&
        Enabled &&
        AppConfig.Current.ToggleState.AutoPlay &&
        ProcessMatchesIfFilterEnabled();

    private bool ProcessMatchesIfFilterEnabled()
    {
        var settings = AppConfig.Current.ActiveProcessSettings;
        if (settings == null || !settings.AutoSwitchProfile) return true;
        if (string.IsNullOrWhiteSpace(_matchProcess)) return true;
        return PowerAim.Class.ProcessMatcher.Matches(_matchProcess, PowerAim.Class.WindowFocusWatcher.Instance.CurrentProcessName);
    }

    /// <summary>
    /// Display description for UI
    /// </summary>
    public string Description => $"{Name ?? "New Profile"} ({Actions.Count(a => a.IsValid)} actions, {Backend switch
    {
        AutoPlayDecisionBackend.Jev => "Jev",
        AutoPlayDecisionBackend.Ollama => OllamaModel,
        _ => "heuristic only",
    }})";
}
