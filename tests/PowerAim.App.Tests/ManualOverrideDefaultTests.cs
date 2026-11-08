using System.Text.Json;
using System.Text.Json.Nodes;
using PowerAim.Config;

namespace PowerAim.App.Tests;

/// <summary>
///     The physical right stick has to be able to steer against the aim: manual override is on by default,
///     and configs saved with the old default (0 = aim only) are moved to it once.
/// </summary>
[Collection(nameof(TestConfig))]
public class ManualOverrideDefaultTests
{
    public ManualOverrideDefaultTests() => TestConfig.Ensure();

    private static ControllerSettings Saved(double overridePct, int? schemaVersion)
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(new ControllerSettings()))!.AsObject();
        json["ManualOverridePct"] = overridePct;
        json.Remove("SchemaVersion");
        if (schemaVersion is { } v) json["SchemaVersion"] = v;
        var settings = JsonSerializer.Deserialize<ControllerSettings>(json.ToJsonString())!;
        settings.MigrateManualOverrideDefault();
        return settings;
    }

    [Fact]
    public void New_settings_let_the_physical_stick_take_over() => Assert.Equal(100, new ControllerSettings().ManualOverridePct);

    [Fact]
    public void The_old_default_of_zero_is_raised_once()
    {
        var s = Saved(0, schemaVersion: null);
        Assert.Equal(100, s.ManualOverridePct);

        s.ManualOverridePct = 0; // chosen again by the user afterwards
        s.MigrateManualOverrideDefault();
        Assert.Equal(0, s.ManualOverridePct);
    }

    [Theory]
    [InlineData(40, null)] // set by the user before
    [InlineData(0, 1)]     // set to 0 after the migration
    public void A_value_the_user_chose_stays(double pct, int? schemaVersion)
        => Assert.Equal(pct, Saved(pct, schemaVersion).ManualOverridePct);
}
