using System.IO;
using System.Text.Json;

namespace PowerAim.App.Tests;

public class LocalizationTests
{
    private static string LocalesDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "PowerAim.App", "Localization", "Locales");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("Localization/Locales not found above the test output.");
    }

    private static HashSet<string> Keys(string file) =>
        JsonDocument.Parse(File.ReadAllText(file)).RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

    // Other languages fall back to English for untranslated keys (the same gaps as on main).
    [Fact]
    public void German_has_every_key()
    {
        var english = Keys(Path.Combine(LocalesDir(), "Locale_en-US.json"));

        Assert.Empty(english.Except(Keys(Path.Combine(LocalesDir(), "Locale_de-DE.json"))));
    }
}
