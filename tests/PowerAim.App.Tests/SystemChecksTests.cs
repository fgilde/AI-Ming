using System.IO;
using PowerAim.Class;

namespace PowerAim.App.Tests;

public class SystemChecksTests
{
    [Theory]
    [InlineData("381b4222-f694-41f0-9685-ff5bb260df2e", true)]  // Balanced
    [InlineData("a1841308-3541-4fab-bc81-f71556f20b4a", true)]  // Power saver
    [InlineData("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", false)] // High performance
    [InlineData("e9a42b02-d5df-448d-aa00-03f14749eb61", false)] // Ultimate performance
    [InlineData("12345678-0000-0000-0000-000000000000", false)] // OEM/custom plan: unknown → don't nag
    public void Throttling_power_plans_are_flagged(string guid, bool throttles)
        => Assert.Equal(throttles, SystemChecks.IsThrottlingPowerPlan(Guid.Parse(guid)));

    [Fact]
    public void The_active_power_plan_can_be_read()
        => Assert.NotNull(SystemChecks.ActivePowerPlan());

    [Fact]
    public void A_minidump_is_written()
    {
        // Dump a child process: dumping the test host itself can stall its other test threads.
        var dir = Path.Combine(Path.GetTempPath(), "poweraim-dump-test-" + Guid.NewGuid().ToString("N"));
        using var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c ping -n 6 127.0.0.1 >nul")
            { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            var path = SystemChecks.WriteMiniDump(dir, child);

            Assert.NotNull(path);
            Assert.True(new FileInfo(path!).Length > 1000);
        }
        finally
        {
            if (!child.HasExited) child.Kill();
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
