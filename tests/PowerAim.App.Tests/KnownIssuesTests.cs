using System.IO;
using PowerAim.Config;

namespace PowerAim.App.Tests;

public class KnownIssuesTests
{
    [Fact]
    public void Known_issues_are_embedded_under_the_name_the_dialog_reads()
    {
        // App.ReadEmbeddedResource("KnownIssues.md") resolves "PowerAim.KnownIssues.md".
        using var stream = typeof(AppConfig).Assembly.GetManifestResourceStream("PowerAim.KnownIssues.md");

        Assert.NotNull(stream);
        Assert.False(string.IsNullOrWhiteSpace(new StreamReader(stream).ReadToEnd()));
    }
}
