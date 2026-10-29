using PowerAim.Config;

namespace PowerAim.App.Tests;

/// <summary>Loads the default config once for tests that need <see cref="AppConfig.Current"/> (thread-safe across test classes).</summary>
internal static class TestConfig
{
    private static readonly object Gate = new();

    public static void Ensure()
    {
        lock (Gate)
            // A missing path loads defaults and records nothing.
            if (AppConfig.Current == null) AppConfig.Load(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "poweraim-tests-missing.cfg"));
    }
}
