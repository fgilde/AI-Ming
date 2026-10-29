using Nextended.UI.Input.Gamepad;
using PowerAim.Config;
using PowerAim.Input;
using PowerAim.Input.Contracts;
using PowerAim.Input.Gamepad;

namespace PowerAim.App.Tests;

/// <summary>Right-stick output through <see cref="InputSender"/> with a recording virtual pad.</summary>
[Collection(nameof(TestConfig))]
public class StickOutputIntegrationTests
{
    private sealed class RecordingSender : IGamepadSender
    {
        private readonly object _gate = new();
        public readonly List<(GamepadAxis Axis, short Value)> Axes = [];
        public readonly List<(GamepadButton Button, bool Pressed)> Buttons = [];
        public readonly HashSet<string> Paused = [];
        public bool CanWork => true;
        public IGamepadSender SyncWith(IGamepadStateSource? source) => this;
        public IGamepadSender StopSync() => this;
        public IGamepadSender PauseSync(GamepadButton button) { lock (_gate) Paused.Add(button.ToString()); return this; }
        public IGamepadSender PauseSync(GamepadSlider slider) => this;
        public IGamepadSender PauseSync(GamepadAxis axis) { lock (_gate) Paused.Add(axis.ToString()); return this; }
        public IGamepadSender ResumeSync() { lock (_gate) Paused.Clear(); return this; }
        public IGamepadSender ResumeSync(GamepadButton button) { lock (_gate) Paused.Remove(button.ToString()); return this; }
        public IGamepadSender ResumeSync(GamepadSlider slider) => this;
        public IGamepadSender ResumeSync(GamepadAxis axis) { lock (_gate) Paused.Remove(axis.ToString()); return this; }
        public IGamepadSender SetButtonState(GamepadButton button, bool pressed, GamepadSyncState s = GamepadSyncState.None)
        {
            // Like the real senders: Paused takes the button over, Resume hands it back.
            if (s == GamepadSyncState.Paused) PauseSync(button);
            lock (_gate) Buttons.Add((button, pressed));
            if (s == GamepadSyncState.Resume) ResumeSync(button);
            return this;
        }
        public IGamepadSender SetSliderValue(GamepadSlider slider, byte value, GamepadSyncState s = GamepadSyncState.None) => this;
        public IGamepadSender SetAxisValue(GamepadAxis axis, short value, GamepadSyncState s = GamepadSyncState.None) { lock (_gate) Axes.Add((axis, value)); return this; }
        public short Last(GamepadAxis axis) { lock (_gate) return Axes.LastOrDefault(a => a.Axis == axis).Value; }
        public List<short> All(GamepadAxis axis) { lock (_gate) return Axes.Where(a => a.Axis == axis).Select(a => a.Value).ToList(); }
        public void Dispose() { }
    }

    private static void UseSender(IGamepadSender? sender)
        => typeof(GamepadManager).GetProperty(nameof(GamepadManager.GamepadSender))!.SetValue(null, sender);

    [Fact]
    public async Task Deadzone_check_sends_exactly_the_deadzone_then_returns_to_neutral()
    {
        TestConfig.Ensure();
        var pad = new RecordingSender();
        UseSender(pad);
        try
        {
            Assert.True(await InputSender.PlayStickPatternAsync(t => StickTestPatterns.DeadzoneCheck(t, 5), 0.3));
            await Task.Delay(60);

            Assert.Contains((short)Math.Round(0.05 * short.MaxValue), pad.All(GamepadAxis.RightThumbX));
            Assert.Equal(0, pad.Last(GamepadAxis.RightThumbX));
            Assert.Equal(0, pad.Last(GamepadAxis.RightThumbY));
            Assert.DoesNotContain(nameof(GamepadButton.RightShoulder), pad.Paused); // the bumper goes back to the pad
        }
        finally { UseSender(null); }
    }

    [Fact]
    public async Task Output_test_presses_and_releases_the_bumper()
    {
        TestConfig.Ensure();
        var pad = new RecordingSender();
        UseSender(pad);
        try
        {
            await InputSender.PlayStickPatternAsync(StickTestPatterns.OutputTest, 0.3);

            Assert.Equal([(GamepadButton.RightShoulder, true), (GamepadButton.RightShoulder, false)], pad.Buttons);
        }
        finally { UseSender(null); }
    }

    [Fact]
    public async Task The_stick_returns_to_neutral_when_the_movement_method_leaves_gamepad()
    {
        TestConfig.Ensure();
        var dropdowns = AppConfig.Current.DropdownState;
        var method = dropdowns.MouseMovementMethod;
        var pad = new RecordingSender();
        UseSender(pad);
        try
        {
            dropdowns.MouseMovementMethod = MouseMovementMethod.Gamepad;
            InputSender.Move(10, 0);
            Assert.Equal(2000, pad.Last(GamepadAxis.RightThumbX));

            dropdowns.MouseMovementMethod = MouseMovementMethod.MouseEvent; // switched away mid-correction
            for (int waited = 0; pad.Last(GamepadAxis.RightThumbX) != 0 && waited < 1000; waited += 10) await Task.Delay(10);

            Assert.Equal(0, pad.Last(GamepadAxis.RightThumbX));
        }
        finally
        {
            dropdowns.MouseMovementMethod = method;
            UseSender(null);
        }
    }

    [Fact]
    public async Task Southpaw_aims_with_the_left_stick()
    {
        TestConfig.Ensure();
        var cfg = AppConfig.Current;
        var method = cfg.DropdownState.MouseMovementMethod;
        var pad = new RecordingSender();
        UseSender(pad);
        try
        {
            cfg.DropdownState.MouseMovementMethod = MouseMovementMethod.Gamepad;
            cfg.ControllerSettings.AimStickLeft = true;
            InputSender.Move(0, -10); // up

            Assert.Equal(2000, pad.Last(GamepadAxis.LeftThumbY));
            Assert.Contains(nameof(GamepadAxis.LeftThumbY), pad.Paused);
            Assert.DoesNotContain(pad.All(GamepadAxis.RightThumbY), v => v != 0);

            cfg.ControllerSettings.AimStickLeft = false; // switching back hands the left stick to the pad
            InputSender.Move(0, -10);
            await Task.Delay(30);
            Assert.Equal(0, pad.Last(GamepadAxis.LeftThumbY));
            Assert.DoesNotContain(nameof(GamepadAxis.LeftThumbY), pad.Paused);
            Assert.Equal(2000, pad.Last(GamepadAxis.RightThumbY));
        }
        finally
        {
            cfg.ControllerSettings.AimStickLeft = false;
            cfg.DropdownState.MouseMovementMethod = method;
            UseSender(null);
        }
    }

    [Fact]
    public async Task A_pad_button_tap_hands_back_only_its_button()
    {
        TestConfig.Ensure();
        var pad = new RecordingSender();
        pad.PauseSync(GamepadAxis.RightThumbX); // e.g. the aim stick, taken over before
        var tap = new GamepadEventArgs { Button = nameof(GamepadButton.A) };
        Assert.Equal(GamepadButton.A, tap.GamepadButton);

        pad.Send(tap, KeyPressState.DownAndUp);
        Assert.Contains(nameof(GamepadButton.A), pad.Paused);
        for (int waited = 0; pad.Paused.Contains(nameof(GamepadButton.A)) && waited < 3000; waited += 10) await Task.Delay(10);

        Assert.DoesNotContain(nameof(GamepadButton.A), pad.Paused);
        Assert.Contains(nameof(GamepadAxis.RightThumbX), pad.Paused);
    }

    [Fact]
    public void Turn_speed_share_becomes_stick_deflection_with_screen_y_flipped()
    {
        TestConfig.Ensure();
        var pad = new RecordingSender();
        UseSender(pad);
        try
        {
            InputSender.SetAimStick(0.5, 0.25); // right and down on screen

            Assert.Equal((short)Math.Round(0.5 * short.MaxValue), pad.Last(GamepadAxis.RightThumbX));
            Assert.Equal((short)Math.Round(-0.25 * short.MaxValue), pad.Last(GamepadAxis.RightThumbY));
        }
        finally { UseSender(null); }
    }
}
