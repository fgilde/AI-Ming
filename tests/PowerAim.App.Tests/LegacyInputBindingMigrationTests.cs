using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Nextended.UI.Input;
using Nextended.UI.Input.Gamepad;
using PowerAim.Config;

namespace PowerAim.App.Tests;

public class LegacyInputBindingMigrationTests
{
    private const string LegacyBindings = """
        {
          "Mouse":    { "Key": "Right", "MouseEventArgs": { "Button": 2097152, "X": 0 }, "KeyEventArgs": null, "GamepadEventArgs": null, "MinTime": 0.5, "IsValid": true, "DeviceName": "Mouse" },
          "Keyboard": { "Key": "LMenu", "MouseEventArgs": null, "KeyEventArgs": { "KeyCode": 164 }, "GamepadEventArgs": null, "MinTime": 0, "IsValid": true, "DeviceName": "Keyboard" },
          "Gamepad":  { "Key": "RT", "MouseEventArgs": null, "KeyEventArgs": null, "GamepadEventArgs": { "Button": "RT", "Value": 1 }, "MinTime": 0 },
          "Combo":    { "Key": "LControlKey+X", "MouseEventArgs": null, "KeyEventArgs": null, "GamepadEventArgs": null, "MinTime": 0,
                        "Components": [
                          { "Key": "LControlKey", "MouseEventArgs": null, "KeyEventArgs": { "KeyCode": 162 }, "GamepadEventArgs": null },
                          { "Key": "X", "MouseEventArgs": null, "KeyEventArgs": { "KeyCode": 88 }, "GamepadEventArgs": null }
                        ] }
        }
        """;

    [Fact]
    public void Device_is_derived_from_the_stored_event()
    {
        var bindings = JsonSerializer.Deserialize<Dictionary<string, StoredInputBinding>>(LegacyInputBindingMigration.Migrate(LegacyBindings))!;

        Assert.Equal(MouseButtons.Right, bindings["Mouse"].MouseButton);
        Assert.Equal(0.5, bindings["Mouse"].MinTime);
        Assert.Equal(Keys.LMenu, bindings["Keyboard"].KeyboardKey);
        Assert.True(bindings["Gamepad"].Is(GamepadSlider.RightTrigger));
        Assert.Equal(StoredInputBinding.Combo([Keys.LControlKey, Keys.X]), bindings["Combo"]);
        Assert.Equal(InputDeviceType.None, bindings["Combo"].Device);
    }

    [Fact]
    public void Legacy_fields_are_dropped()
    {
        var json = LegacyInputBindingMigration.Migrate(LegacyBindings);

        Assert.DoesNotContain("EventArgs", json);
        Assert.DoesNotContain("DeviceName", json);
    }

    [Fact]
    public void Current_format_is_returned_untouched()
    {
        var json = JsonSerializer.Serialize(new StoredInputBinding(Keys.F1));

        Assert.Same(json, LegacyInputBindingMigration.Migrate(json));
    }

    [Fact]
    public void Shipped_default_config_keeps_its_bindings()
    {
        var legacy = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "legacy_default.cfg"));

        var config = JsonSerializer.Deserialize<AppConfig>(LegacyInputBindingMigration.Migrate(legacy))!;

        Assert.Equal(new StoredInputBinding[] { MouseButtons.Right, Keys.LMenu }, config.BindingSettings.AimKeyBindings);
        Assert.Equal(MouseButtons.Left, config.BindingSettings.DynamicFOVKeybind.MouseButton);
    }
}
