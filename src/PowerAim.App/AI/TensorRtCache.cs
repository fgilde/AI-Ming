using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace PowerAim.AI;

/// <summary>
///     One TensorRT engine-cache folder per model content: <c>&lt;root&gt;/&lt;sha256 prefix&gt;/</c> with a manifest
///     naming the source model. A model updated in place gets a new folder (no stale engine is ever loaded) and
///     the folder built for its previous content is deleted.
/// </summary>
public static class TensorRtCache
{
    private const string ManifestName = "manifest.json";

    private sealed record Manifest(string ModelPath, string Sha256);

    public static string ResolveDir(string root, string modelPath)
    {
        string fullPath = Path.GetFullPath(modelPath);
        string sha;
        using (var stream = File.OpenRead(fullPath))
            sha = Convert.ToHexString(SHA256.HashData(stream));

        string dir = Path.Combine(root, sha[..16]);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ManifestName), JsonSerializer.Serialize(new Manifest(fullPath, sha)));

        foreach (var other in Directory.GetDirectories(root))
        {
            if (string.Equals(other, dir, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(other, ManifestName)));
                if (manifest != null && string.Equals(manifest.ModelPath, fullPath, StringComparison.OrdinalIgnoreCase))
                    Directory.Delete(other, recursive: true);
            }
            catch
            {
                // No / unreadable manifest: not ours to judge — leave it.
            }
        }
        return dir;
    }
}
