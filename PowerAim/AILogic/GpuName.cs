namespace PowerAim.AILogic;

/// <summary>Formatting helpers for GPU adapter descriptions.</summary>
public static class GpuName
{
    private static readonly string[] VendorPrefixes =
        ["NVIDIA GeForce ", "NVIDIA ", "AMD Radeon(TM) ", "AMD Radeon ", "AMD ", "Intel(R) ", "Intel "];

    /// <summary>
    ///     Drops the vendor prefix so a title-bar chip stays compact
    ///     ("NVIDIA GeForce RTX 4090" → "RTX 4090"). Returns the input unchanged when no known
    ///     vendor prefix matches.
    /// </summary>
    public static string Shorten(string full) =>
        VendorPrefixes.FirstOrDefault(p => full.StartsWith(p, StringComparison.OrdinalIgnoreCase)) is { } prefix
            ? full[prefix.Length..].Trim()
            : full;
}
