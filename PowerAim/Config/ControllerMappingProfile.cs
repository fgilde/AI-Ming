using System.Collections.ObjectModel;
using Nextended.Core;
using Nextended.Core.Extensions;

namespace PowerAim.Config;

/// <summary>
///     Runtime direction filter applied by the mapping engine. Each mapping has a fixed source /
///     target — but at activation time the user picks which "side" should drive, so a profile can
///     hold both directions and the user flips between them without re-editing.
/// </summary>
public enum MappingDirection
{
    /// <summary>Both sides fire — keyboard/mouse drives gamepad AND gamepad drives keyboard/mouse.</summary>
    Both = 0,
    /// <summary>Only KB/M → gamepad mappings fire (play gamepad-only titles with KB+M).</summary>
    KeyboardToController = 1,
    /// <summary>Only gamepad → KB/M mappings fire (play KB+M-only titles with a controller).</summary>
    ControllerToKeyboard = 2,
}

/// <summary>
///     A named set of <see cref="InputMapping"/>s, plus a per-profile enabled-flag and an optional
///     process match. The engine runs at most one profile at a time — the first one whose
///     <see cref="Enabled"/> is true and whose <see cref="MatchProcess"/> matches the focused
///     process (empty = match any). Profiles are persisted under
///     <see cref="AppConfig.ControllerMappingProfiles"/>.
/// </summary>
public class ControllerMappingProfile : EditableNotificationObject
{
    private string _name = "Profile";
    private bool _enabled = false;
    private string _matchProcess = "";
    private ObservableCollection<InputMapping> _mappings = new();
    private double _stickToMouseSensitivity = 12.0;
    private double _mouseToStickSensitivity = 1.0;
    private double _stickDeadzone = 0.15;
    private double _stickAntiDeadzone = 0.0;
    private double _stickResponseCurve = 1.0;
    private double _stickMouseExponent = 1.4;
    private bool _invertMouseY = false;

    public ControllerMappingProfile()
    {
        Id = Guid.NewGuid().ToFormattedId();
    }

    public string Id { get; set; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    ///     Master switch — when on, this profile is a candidate for the engine. The engine still
    ///     respects the optional process-name filter and "only one active profile at a time" rule.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    /// <summary>
    ///     Optional process-name pattern (wildcards / pipes) — when set, this profile only
    ///     activates while a matching process is in the foreground. Empty = always-on.
    /// </summary>
    public string MatchProcess
    {
        get => _matchProcess;
        set => SetProperty(ref _matchProcess, value ?? "");
    }

    public ObservableCollection<InputMapping> Mappings
    {
        get => _mappings;
        set => SetProperty(ref _mappings, value);
    }

    /// <summary>
    ///     How many screen-pixels per tick the engine moves the mouse when a stick-direction
    ///     mapping fires at full deflection. Scales linearly with stick magnitude.
    /// </summary>
    public double StickToMouseSensitivity
    {
        get => _stickToMouseSensitivity;
        set => SetProperty(ref _stickToMouseSensitivity, value);
    }

    /// <summary>
    ///     Scale applied to mouse delta when synthesizing right-stick movement on the virtual
    ///     controller. 1.0 means a 100px mouse swipe maps to ~full stick deflection.
    /// </summary>
    public double MouseToStickSensitivity
    {
        get => _mouseToStickSensitivity;
        set => SetProperty(ref _mouseToStickSensitivity, value);
    }

    /// <summary>
    ///     Dead-zone for stick→mouse and stick-direction mappings. Values 0.0 – 0.5; below this
    ///     fraction of full deflection the stick is treated as centred. reWASD-style.
    /// </summary>
    public double StickDeadzone
    {
        get => _stickDeadzone;
        set => SetProperty(ref _stickDeadzone, value);
    }

    /// <summary>
    ///     Anti-dead-zone — minimum output applied as soon as the stick leaves the dead-zone.
    ///     Useful for games with their own internal dead-zones that swallow small inputs.
    ///     0.0 – 0.5. Default 0.
    /// </summary>
    public double StickAntiDeadzone
    {
        get => _stickAntiDeadzone;
        set => SetProperty(ref _stickAntiDeadzone, value);
    }

    /// <summary>
    ///     Exponent applied to the normalised magnitude on stick→mouse mappings (≥ 1.0 = slow at
    ///     centre, fast at edge — micro-aim feel). reWASD calls this "response curve".
    /// </summary>
    public double StickResponseCurve
    {
        get => _stickResponseCurve;
        set => SetProperty(ref _stickResponseCurve, value);
    }

    /// <summary>
    ///     Separate exponent used specifically for stick→mouse motion. Lets you tune mouse feel
    ///     without affecting other mappings. 1.0 = linear.
    /// </summary>
    public double StickMouseExponent
    {
        get => _stickMouseExponent;
        set => SetProperty(ref _stickMouseExponent, value);
    }

    /// <summary>Invert Y axis when feeding the right stick into mouse motion (or vice versa).</summary>
    public bool InvertMouseY
    {
        get => _invertMouseY;
        set => SetProperty(ref _invertMouseY, value);
    }
}
