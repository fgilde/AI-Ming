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
