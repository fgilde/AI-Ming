using System.Text.Json;
using System.Text.Json.Nodes;
using Nextended.UI.Input;

namespace PowerAim.Config;

/// <summary>
///     Upgrades input bindings saved before the switch to Nextended.UI's <see cref="StoredInputBinding"/>.
///     Those stored the whole device event (<c>MouseEventArgs</c> / <c>KeyEventArgs</c> /
///     <c>GamepadEventArgs</c>, plus computed <c>IsValid</c> / <c>DeviceName</c>); the new shape is
///     <c>{ Key, Device, MinTime, Components }</c>. <c>Key</c> already held the button/key name, so only
///     the device has to be derived from whichever event object was set.
/// </summary>
internal static class LegacyInputBindingMigration
{
    private static readonly string[] LegacyEventProperties = ["MouseEventArgs", "KeyEventArgs", "GamepadEventArgs"];

    /// <summary>Returns <paramref name="json"/> with every legacy binding rewritten; unchanged when there is none.</summary>
    public static string Migrate(string json)
    {
        if (!LegacyEventProperties.Any(p => json.Contains($"\"{p}\"", StringComparison.Ordinal)))
            return json;

        var root = JsonNode.Parse(json);
        if (root == null || !MigrateTree(root))
            return json;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool MigrateTree(JsonNode node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                changed = TryMigrateBinding(obj);
                foreach (var (_, child) in obj.ToList())
                    if (child != null) changed |= MigrateTree(child);
                break;
            case JsonArray array:
                foreach (var item in array)
                    if (item != null) changed |= MigrateTree(item);
                break;
        }
        return changed;
    }

    private static bool TryMigrateBinding(JsonObject obj)
    {
        if (obj.ContainsKey(nameof(StoredInputBinding.Device)) || !LegacyEventProperties.Any(obj.ContainsKey))
            return false;

        // A combo parent has none of the events set and keeps Device None; its Components migrate on their own.
        var device = obj["MouseEventArgs"] is JsonObject ? InputDeviceType.Mouse
            : obj["KeyEventArgs"] is JsonObject ? InputDeviceType.Keyboard
            : obj["GamepadEventArgs"] is JsonObject ? InputDeviceType.Gamepad
            : InputDeviceType.None;

        foreach (var legacy in LegacyEventProperties.Append("IsValid").Append("DeviceName"))
            obj.Remove(legacy);
        obj[nameof(StoredInputBinding.Device)] = (int)device;
        return true;
    }
}
