using PowerAim.Config;

namespace PowerAim.AI.AutoPlay;

/// <summary>
///     Keeps AutoPlay from fighting the other features when they run together: if an active AutoTrigger
///     already presses the shoot keys on detections, AutoPlay leaves firing to it (otherwise one presses
///     while the other releases). Aim is handed over the same way when Aim Assist steers the mouse.
/// </summary>
public static class AutoPlayCoordination
{
    /// <summary>
    ///     True when some active, detection-driven trigger sends any of <paramref name="shoot"/>'s keys on its
    ///     own — a trigger that also needs a held trigger key never fires while the bot plays, so it doesn't count.
    ///     <paramref name="isActive"/> defaults to <see cref="ActionTrigger.IsActive"/> (tests pass their own).
    /// </summary>
    public static bool TriggerOwnsFire(IEnumerable<ActionTrigger>? triggers, AutoPlayAction? shoot,
        Func<ActionTrigger, bool>? isActive = null)
    {
        if (triggers == null || shoot == null) return false;
        var keys = shoot.Keys.Where(k => k.IsValid).ToList();
        if (keys.Count == 0) return false;
        isActive ??= t => t.IsActive;
        return triggers.Any(t => t != null && isActive(t) && t.NeedsDetection &&
                                 !t.TriggerKeys.Any(k => k.IsValid) &&
                                 t.Actions.Any(a => a.IsValid && keys.Contains(a)));
    }
}
