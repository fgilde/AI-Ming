using System.Windows.Forms;
using Nextended.UI.Input.Gamepad;
using PowerAim.Config;
using PowerAim.Input;
using PowerAim.Input.Mapping;

namespace PowerAim.App.Tests;

public class MappingRemapTests
{
    private static InputMapping Map(MappingInputKind src, int srcCode, MappingInputKind dst, int dstCode)
        => new() { SourceKind = src, SourceCode = srcCode, TargetKind = dst, TargetCode = dstCode, Enabled = true };

    private static int Code(GamepadButton b) => (int)b.ToXboxButtonId();

    [Fact]
    public void A_pad_to_pad_remap_takes_over_the_source_button_too()
    {
        var profile = new ControllerMappingProfile();
        profile.Mappings.Add(Map(MappingInputKind.GamepadButton, Code(GamepadButton.A), MappingInputKind.GamepadButton, Code(GamepadButton.B)));

        var owned = MappingEngine.OwnedChannels(profile);

        Assert.Contains(GamepadButton.A, owned.OfType<GamepadButton>());
        Assert.Contains(GamepadButton.B, owned.OfType<GamepadButton>());
    }

    [Fact]
    public void Pad_to_keyboard_leaves_the_pad_alone()
        => Assert.Empty(MappingEngine.OwnedChannels(new ControllerMappingProfile
        {
            Mappings = { Map(MappingInputKind.GamepadButton, Code(GamepadButton.A), MappingInputKind.KeyboardKey, (int)Keys.E) },
        }));

    [Fact]
    public void Keyboard_to_pad_owns_only_the_target()
    {
        var profile = new ControllerMappingProfile();
        profile.Mappings.Add(Map(MappingInputKind.KeyboardKey, (int)Keys.Space, MappingInputKind.GamepadButton, Code(GamepadButton.A)));

        Assert.Equal([GamepadButton.A], MappingEngine.OwnedChannels(profile).OfType<GamepadButton>());
    }
}
