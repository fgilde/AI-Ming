using System.Windows;
using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using StackPanel = System.Windows.Controls.StackPanel;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.UI.UILibrary;

namespace PowerAim.App.Tests;

/// <summary>
///     Building a dropdown must not write its first entry into the config (the combo box selects index 0 on
///     its own), and the title-bar movement picker follows the config both ways.
/// </summary>
[Collection(nameof(TestConfig))]
public class MovementMethodPickerTests
{
    [Fact]
    public void An_enum_dropdown_shows_the_value_without_selecting_anything_while_it_is_built() => Sta.Run(() =>
    {
        TestConfig.Ensure();
        var picks = new List<GamepadSendMode>();
        var panel = new StackPanel();

        var dropdown = panel.AddDropdown(Locale.GamepadSendCommandMode, GamepadSendMode.DualShock4, picks.Add);

        Assert.Empty(picks);
        Assert.Equal((int)GamepadSendMode.DualShock4, dropdown.DropdownBox.SelectedIndex);

        dropdown.DropdownBox.SelectedIndex = (int)GamepadSendMode.VJoy; // a real pick still goes through
        Assert.Equal([GamepadSendMode.VJoy], picks);
    });

    [Fact]
    public void The_settings_dropdown_keeps_the_saved_movement_method() => Sta.Run(() =>
    {
        TestConfig.Ensure();
        var dropdowns = AppConfig.Current.DropdownState;
        var before = dropdowns.MouseMovementMethod;
        try
        {
            dropdowns.MouseMovementMethod = MouseMovementMethod.Gamepad;
            var picks = new List<MouseMovementMethod>();
            var box = new ComboBox();

            MovementMethodPicker.Fill(box, m => { picks.Add(m); return Task.CompletedTask; });

            Assert.Empty(picks);
            Assert.Equal(MouseMovementMethod.Gamepad, dropdowns.MouseMovementMethod);
            Assert.Equal(MouseMovementMethod.Gamepad, ((ComboBoxItem)box.SelectedItem).Tag);
        }
        finally { dropdowns.MouseMovementMethod = before; }
    });

    [Fact]
    public void The_title_bar_chip_follows_the_config_and_picks_through_the_callback() => Sta.Run(() =>
    {
        TestConfig.Ensure();
        var dropdowns = AppConfig.Current.DropdownState;
        var before = dropdowns.MouseMovementMethod;
        var picks = new List<MouseMovementMethod>();
        var chip = new MovementMethodPicker(m => { picks.Add(m); dropdowns.MouseMovementMethod = m; return Task.CompletedTask; });
        var window = new Window { Content = chip, Width = 300, Height = 80, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -2000 };
        try
        {
            dropdowns.MouseMovementMethod = MouseMovementMethod.SendInput;
            window.Show();
            Assert.Equal(MovementMethodPicker.ShortName(MouseMovementMethod.SendInput), chip.Label);

            dropdowns.MouseMovementMethod = MouseMovementMethod.Gamepad; // changed on the settings page
            Assert.Equal(MovementMethodPicker.ShortName(MouseMovementMethod.Gamepad), chip.Label);

            chip.Pick(MouseMovementMethod.ddxoft);
            Assert.Equal([MouseMovementMethod.ddxoft], picks);
            Assert.Equal(MovementMethodPicker.ShortName(MouseMovementMethod.ddxoft), chip.Label);
        }
        finally
        {
            window.Close();
            dropdowns.MouseMovementMethod = before;
        }
    });
}
