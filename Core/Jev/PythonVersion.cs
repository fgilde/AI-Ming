namespace Core.Jev;

/// <summary>
///     Parses the output of <c>python --version</c> / <c>py -3 --version</c> and answers whether it is new
///     enough for simple-jev's hf-server (which needs modern typing + recent transformers wheels).
/// </summary>
public static class PythonVersion
{
    /// <summary>Lowest interpreter the hf-server dependency set installs cleanly on.</summary>
    public static readonly Version Minimum = new(3, 10);

    /// <summary>"Python 3.11.9" → 3.11.9. Returns null when the line isn't a python version banner.</summary>
    public static Version? Parse(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;
        // Tolerate "Python 3.11.9", "Python 3.13.0rc1", leading/trailing noise from wrappers.
        var m = System.Text.RegularExpressions.Regex.Match(output, @"Python\s+(\d+)\.(\d+)(?:\.(\d+))?");
        if (!m.Success) return null;
        int major = int.Parse(m.Groups[1].Value);
        int minor = int.Parse(m.Groups[2].Value);
        int patch = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
        return new Version(major, minor, patch);
    }

    public static bool IsSupported(Version? v) => v != null && v >= Minimum;
}
