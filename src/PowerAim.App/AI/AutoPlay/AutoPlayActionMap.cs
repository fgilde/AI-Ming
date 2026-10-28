using PowerAim.Config;

namespace PowerAim.AI.AutoPlay;

/// <summary>The controls the reflex drives. Profile actions are matched to these by name.</summary>
public enum ControlRole { MoveForward, MoveBackward, MoveLeft, MoveRight, Sprint, Jump, Shoot, Aim, Crouch, Reload }

/// <summary>
///     Which profile action plays which <see cref="ControlRole"/>, and which actions are left for the
///     decision models. Exact name matches win over substring matches, in alias order.
/// </summary>
public sealed class AutoPlayActionMap
{
    private static readonly (ControlRole Role, string Key, string[] Aliases)[] Roles =
    [
        (ControlRole.MoveForward, "move_forward", ["move_forward", "forward", "fwd", "walk", "move_fwd"]),
        (ControlRole.MoveBackward, "move_backward", ["move_backward", "move_back", "backward", "back", "retreat"]),
        (ControlRole.MoveLeft, "move_left", ["move_left", "strafe_left", "left"]),
        (ControlRole.MoveRight, "move_right", ["move_right", "strafe_right", "right"]),
        (ControlRole.Sprint, "sprint", ["sprint", "run", "dash"]),
        (ControlRole.Jump, "jump", ["jump", "leap"]),
        (ControlRole.Shoot, "shoot", ["shoot", "fire", "attack", "primary"]),
        (ControlRole.Aim, "aim", ["aim", "ads", "scope", "secondary"]),
        (ControlRole.Crouch, "crouch", ["crouch", "duck", "prone"]),
        (ControlRole.Reload, "reload", ["reload"]),
    ];

    /// <summary>Roles the reflex owns every frame; decision models never trigger these directly.</summary>
    public static readonly ControlRole[] ReflexOwned =
        [ControlRole.MoveForward, ControlRole.MoveBackward, ControlRole.MoveLeft, ControlRole.MoveRight,
         ControlRole.Sprint, ControlRole.Aim, ControlRole.Shoot];

    private readonly Dictionary<ControlRole, AutoPlayAction> _byRole = new();
    private readonly Dictionary<AutoPlayAction, string> _roleKeys = new();

    /// <summary>Valid actions that fill no role: the free-form abilities.</summary>
    public IReadOnlyList<AutoPlayAction> Tactical { get; }

    /// <summary>Valid actions a decision model may trigger: everything except the reflex-owned roles.</summary>
    public IReadOnlyList<AutoPlayAction> Strategic { get; }

    private AutoPlayActionMap(AutoPlayProfile profile)
    {
        var valid = profile.Actions.Where(a => a.IsValid && !string.IsNullOrWhiteSpace(a.Name)).ToList();
        foreach (var (role, key, aliases) in Roles)
        {
            var hit = Find(valid, aliases);
            if (hit == null || _roleKeys.ContainsKey(hit)) continue;
            _byRole[role] = hit;
            _roleKeys[hit] = key;
        }
        Tactical = valid.Where(a => !_roleKeys.ContainsKey(a)).ToList();
        var reflex = ReflexOwned.Select(Get).Where(a => a != null).ToHashSet();
        Strategic = valid.Where(a => !reflex.Contains(a)).ToList();
    }

    public static AutoPlayActionMap Build(AutoPlayProfile profile) => new(profile);

    public AutoPlayAction? Get(ControlRole role) => _byRole.GetValueOrDefault(role);

    /// <summary>The role key ("shoot", "reload", …) an action fills, or null for a free-form ability.</summary>
    public string? RoleKey(AutoPlayAction action) => _roleKeys.GetValueOrDefault(action);

    /// <summary>Role keys of the reflex-owned controls the profile actually has.</summary>
    public IEnumerable<string> ReflexControls =>
        ReflexOwned.Select(Get).Where(a => a != null).Select(a => _roleKeys[a!]);

    private static AutoPlayAction? Find(List<AutoPlayAction> actions, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var hit = actions.FirstOrDefault(a => string.Equals(a.Name, alias, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }
        foreach (var alias in aliases)
        {
            var hit = actions.FirstOrDefault(a => a.Name!.Contains(alias, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }
        return null;
    }
}
