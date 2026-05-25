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
}
