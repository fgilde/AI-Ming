using System.IO;
using System.Text.Json;
using PowerAim.Config;

namespace PowerAim.AI;

/// <summary>
///     Optional <c>&lt;model&gt;.onnx.json</c> next to a model: recommended per-class aim rules and notes such as the
///     in-game settings the model was tuned with.
/// </summary>
public sealed record ModelSidecar(List<ClassTargetSpec>? ClassSpecs, string? Notes)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>The sidecar of <paramref name="modelPath"/>, or null when there is none or it can't be read.</summary>
    public static ModelSidecar? Load(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath)) return null;
        var path = modelPath + ".json";
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<ModelSidecar>(File.ReadAllText(path), Options); }
        catch { return null; }
    }
}
