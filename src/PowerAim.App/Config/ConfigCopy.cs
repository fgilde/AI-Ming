using System.Text.Json;

namespace PowerAim.Config;

public static class ConfigCopy
{
    /// <summary>Deep copy through the same JSON the config is saved as, so the copy holds exactly what is saved.</summary>
    public static T Clone<T>(T source) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source))!;
}
