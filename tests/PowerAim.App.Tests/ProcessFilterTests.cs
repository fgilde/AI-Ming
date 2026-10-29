using PowerAim.Class;

namespace PowerAim.App.Tests;

public class ProcessFilterTests
{
    [Fact]
    public void Disabled_filter_lets_everything_through()
        => Assert.True(ProcessMatcher.MatchesIfEnabled(false, "cod", "notepad"));

    [Fact]
    public void Empty_pattern_lets_everything_through()
        => Assert.True(ProcessMatcher.MatchesIfEnabled(true, "", "notepad"));

    [Theory]
    [InlineData("cod", "cod", true)]
    [InlineData("cod.exe", "cod", true)]
    [InlineData("cod|valorant", "valorant", true)]
    [InlineData("cod", "notepad", false)]
    [InlineData("cod", "", false)]
    public void Enabled_filter_matches_the_focused_process(string pattern, string focused, bool expected)
        => Assert.Equal(expected, ProcessMatcher.MatchesIfEnabled(true, pattern, focused));
}
