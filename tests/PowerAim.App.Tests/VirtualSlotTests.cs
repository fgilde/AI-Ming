using PowerAim.Input.Gamepad.Interaction;
using SharpDX.XInput;

namespace PowerAim.App.Tests;

public class VirtualSlotTests
{
    private static Func<UserIndex, bool> Connected(params UserIndex[] slots) => s => slots.Contains(s);

    [Fact]
    public void The_one_new_slot_is_the_virtual_pad()
        => Assert.Equal(UserIndex.Two, GamepadReader.NewSlot([UserIndex.One], [UserIndex.One, UserIndex.Two]));

    [Fact]
    public void No_or_several_new_slots_are_not_guessed()
    {
        Assert.Null(GamepadReader.NewSlot([UserIndex.One], [UserIndex.One]));
        Assert.Null(GamepadReader.NewSlot([], [UserIndex.One, UserIndex.Two]));
    }

    [Fact]
    public void Preferred_slot_is_kept_when_connected_and_not_excluded()
        => Assert.Equal(UserIndex.Three, GamepadReader.ChooseSlot(UserIndex.Three, Connected(UserIndex.One, UserIndex.Three), null));

    [Fact]
    public void The_virtual_pad_is_never_chosen_as_input()
    {
        // Physical pad on slot two, our own virtual pad took slot one.
        Assert.Equal(UserIndex.Two, GamepadReader.ChooseSlot(UserIndex.One, Connected(UserIndex.One, UserIndex.Two), UserIndex.One));
    }

    [Fact]
    public void Without_a_physical_pad_the_reader_waits_on_a_non_virtual_slot()
        => Assert.NotEqual(UserIndex.One, GamepadReader.ChooseSlot(UserIndex.One, Connected(UserIndex.One), UserIndex.One));
}

public class StaleInputTests
{
    [Theory]
    [InlineData(1000, 990, false)]
    [InlineData(1000, 900, false)]
    [InlineData(1000, 899, true)]
    public void A_sample_older_than_100_ms_counts_as_stale(long now, long sampledAt, bool stale)
        => Assert.Equal(stale, PowerAim.Input.Gamepad.Interaction.DirectInputGamepadReader.IsStale(now, sampledAt));
}

public class DirectInputMappingTests
{
    private static SharpDX.DirectInput.JoystickState State(int x, int y, int z, int rz)
    {
        var s = new SharpDX.DirectInput.JoystickState();
        s.X = x; s.Y = y; s.Z = z; s.RotationZ = rz;
        return s;
    }

    [Fact]
    public void Missing_axes_read_as_centred_not_as_full_deflection()
    {
        // A 2-axis pad: X/Y centred, no Z/Rz — the raw zeros used to become a full up-left right stick.
        var pad = PowerAim.Input.Gamepad.Interaction.DirectInputGamepadReader.MapToGamepad(
            State(32767, 32767, 0, 0), new(X: true, Y: true, Z: false, Rz: false, Rx: false, Ry: false));

        Assert.Equal(0, pad.RightThumbX);
        Assert.Equal(0, pad.RightThumbY);
        Assert.InRange(pad.LeftThumbX, -2, 2);
    }

    [Fact]
    public void A_full_pad_maps_both_sticks()
    {
        var pad = PowerAim.Input.Gamepad.Interaction.DirectInputGamepadReader.MapToGamepad(
            State(65535, 0, 0, 65535), PowerAim.Input.Gamepad.Interaction.DirectInputGamepadReader.AxisPresence.All);

        Assert.Equal(short.MaxValue, pad.LeftThumbX);
        Assert.Equal(short.MaxValue, pad.LeftThumbY);   // DirectInput up = 0 → XInput up = +
        Assert.Equal(short.MinValue, pad.RightThumbX);
        Assert.Equal(short.MinValue + 1, pad.RightThumbY);
    }

    [Fact]
    public void A_dual_stick_pad_is_preferred_over_the_first_device()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        Assert.Equal(b, PowerAim.Input.Gamepad.Interaction.DirectInputGamepadReader.PickDevice([(a, false), (b, true)]));
        Assert.Equal(a, PowerAim.Input.Gamepad.Interaction.DirectInputGamepadReader.PickDevice([(a, false), (b, false)]));
        Assert.Null(PowerAim.Input.Gamepad.Interaction.DirectInputGamepadReader.PickDevice([]));
    }
}
