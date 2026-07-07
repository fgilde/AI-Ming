using System.ComponentModel;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using SharpDX.XInput;
using static CoreDX.vJoy.Wrapper.VJoyControllerManager;

namespace PowerAim.InputLogic.Contracts;


public enum GamepadButton
{
    [Description("A")]
    A,

    [Description("B")]
    B,

    [Description("X")]
    X,

    [Description("Y")]
    Y,

    [Description("LB")]
    LeftShoulder,

    [Description("RB")]
    RightShoulder,

    [Description("BACK")]
    Back,

    [Description("START")]
    Start,

    [Description("LS")]
    LeftThumb,

    [Description("RS")]
    RightThumb,

    [Description("UP")]
    Up,

    [Description("DOWN")]
    Down,

    [Description("LEFT")]
    Left,

    [Description("RIGHT")]
    Right
}

public enum GamepadSlider
{
    [Description("LT")]
    LeftTrigger,
    [Description("RT")]
    RightTrigger
}

public enum GamepadAxis
{
    [Description("LSX")]
    LeftThumbX, 
    [Description("LSY")]
    LeftThumbY,
    [Description("RSX")]
    RightThumbX, 
    [Description("RSY")]
    RightThumbY
}

public static class GamepadEnumExtensions
{
    public static Xbox360Button ToXbox360Button(this GamepadButton button)
    {
        return button switch
        {
            GamepadButton.A => Xbox360Button.A,
            GamepadButton.B => Xbox360Button.B,
            GamepadButton.X => Xbox360Button.X,
            GamepadButton.Y => Xbox360Button.Y,
            GamepadButton.LeftShoulder => Xbox360Button.LeftShoulder,
            GamepadButton.RightShoulder => Xbox360Button.RightShoulder,
            GamepadButton.Back => Xbox360Button.Back,
            GamepadButton.Start => Xbox360Button.Start,
            GamepadButton.LeftThumb => Xbox360Button.LeftThumb,
            GamepadButton.RightThumb => Xbox360Button.RightThumb,
            GamepadButton.Up => Xbox360Button.Up,
            GamepadButton.Down => Xbox360Button.Down,
            GamepadButton.Left => Xbox360Button.Left,
            GamepadButton.Right => Xbox360Button.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, null)
        };
    }

    public static Xbox360Slider ToXbox360Slider(this GamepadSlider slider)
    {
        return slider switch
        {
            GamepadSlider.LeftTrigger => Xbox360Slider.LeftTrigger,
            GamepadSlider.RightTrigger => Xbox360Slider.RightTrigger,
            _ => throw new ArgumentOutOfRangeException(nameof(slider), slider, null)
        };
    }

    public static Xbox360Axis ToXbox360Axis(this GamepadAxis axis)
    {
        return axis switch
        {
            GamepadAxis.LeftThumbX => Xbox360Axis.LeftThumbX,
            GamepadAxis.LeftThumbY => Xbox360Axis.LeftThumbY,
            GamepadAxis.RightThumbX => Xbox360Axis.RightThumbX,
            GamepadAxis.RightThumbY => Xbox360Axis.RightThumbY,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };
    }

    // ---- DualShock 4 (ViGEm) mappings ----
    // The face/shoulder/thumb/menu buttons map 1:1. The D-pad is NOT individual buttons on a DS4 —
    // it's a single 8-way direction (DualShock4DPadDirection) — so the D-pad is handled in the sender,
    // and this helper only covers the real buttons (throws for D-pad, which the sender never routes here).

    /// <summary>True for the four D-pad members, which the DS4 sender maps to a combined direction.</summary>
    public static bool IsDPad(this GamepadButton button) =>
        button is GamepadButton.Up or GamepadButton.Down or GamepadButton.Left or GamepadButton.Right;

    public static DualShock4Button ToDualShock4Button(this GamepadButton button) => button switch
    {
        GamepadButton.A => DualShock4Button.Cross,
        GamepadButton.B => DualShock4Button.Circle,
        GamepadButton.X => DualShock4Button.Square,
        GamepadButton.Y => DualShock4Button.Triangle,
        GamepadButton.LeftShoulder => DualShock4Button.ShoulderLeft,
        GamepadButton.RightShoulder => DualShock4Button.ShoulderRight,
        GamepadButton.Back => DualShock4Button.Share,
        GamepadButton.Start => DualShock4Button.Options,
        GamepadButton.LeftThumb => DualShock4Button.ThumbLeft,
        GamepadButton.RightThumb => DualShock4Button.ThumbRight,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "D-pad is handled via DualShock4DPadDirection"),
    };

    public static DualShock4Slider ToDualShock4Slider(this GamepadSlider slider) => slider switch
    {
        GamepadSlider.LeftTrigger => DualShock4Slider.LeftTrigger,
        GamepadSlider.RightTrigger => DualShock4Slider.RightTrigger,
        _ => throw new ArgumentOutOfRangeException(nameof(slider), slider, null),
    };

    public static DualShock4Axis ToDualShock4Axis(this GamepadAxis axis) => axis switch
    {
        GamepadAxis.LeftThumbX => DualShock4Axis.LeftThumbX,
        GamepadAxis.LeftThumbY => DualShock4Axis.LeftThumbY,
        GamepadAxis.RightThumbX => DualShock4Axis.RightThumbX,
        GamepadAxis.RightThumbY => DualShock4Axis.RightThumbY,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
    };

    /// <summary>Combine the four D-pad booleans into a DualShock 4 8-way direction.</summary>
    public static DualShock4DPadDirection ToDualShock4DPad(bool up, bool down, bool left, bool right)
    {
        if (up && right) return DualShock4DPadDirection.Northeast;
        if (up && left) return DualShock4DPadDirection.Northwest;
        if (down && right) return DualShock4DPadDirection.Southeast;
        if (down && left) return DualShock4DPadDirection.Southwest;
        if (up) return DualShock4DPadDirection.North;
        if (down) return DualShock4DPadDirection.South;
        if (left) return DualShock4DPadDirection.West;
        if (right) return DualShock4DPadDirection.East;
        return DualShock4DPadDirection.None;
    }

    /// <summary>Xbox/XInput axis (short, centre 0) → DualShock 4 axis (byte, centre 128).</summary>
    public static byte ToDualShock4AxisByte(short value) => (byte)Math.Clamp((value + 32768) >> 8, 0, 255);

    public static GamepadButtonFlags ToGamepadButtonFlags(this GamepadButton button)
    {
        return button switch
        {
            GamepadButton.A => GamepadButtonFlags.A,
            GamepadButton.B => GamepadButtonFlags.B,
            GamepadButton.X => GamepadButtonFlags.X,
            GamepadButton.Y => GamepadButtonFlags.Y,
            GamepadButton.LeftShoulder => GamepadButtonFlags.LeftShoulder,
            GamepadButton.RightShoulder => GamepadButtonFlags.RightShoulder,
            GamepadButton.LeftThumb => GamepadButtonFlags.LeftThumb,
            GamepadButton.RightThumb => GamepadButtonFlags.RightThumb,
            GamepadButton.Up => GamepadButtonFlags.DPadUp,
            GamepadButton.Down => GamepadButtonFlags.DPadDown,
            GamepadButton.Left => GamepadButtonFlags.DPadLeft,
            GamepadButton.Right => GamepadButtonFlags.DPadRight,
            GamepadButton.Start => GamepadButtonFlags.Start,
            GamepadButton.Back => GamepadButtonFlags.Back,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, null),
        };
    }

    /// <summary>
    ///     Reverse of <see cref="ToGamepadButtonFlags"/> — translate a single XInput bit flag back
    ///     into PowerAim's <see cref="GamepadButton"/> enum. Returns <c>null</c> for unknown /
    ///     composite flags so callers can skip cleanly (e.g. <see cref="GamepadButtonFlags.None"/>).
    /// </summary>
    public static GamepadButton? ToGamepadButton(this GamepadButtonFlags flag) => flag switch
    {
        GamepadButtonFlags.A             => GamepadButton.A,
        GamepadButtonFlags.B             => GamepadButton.B,
        GamepadButtonFlags.X             => GamepadButton.X,
        GamepadButtonFlags.Y             => GamepadButton.Y,
        GamepadButtonFlags.LeftShoulder  => GamepadButton.LeftShoulder,
        GamepadButtonFlags.RightShoulder => GamepadButton.RightShoulder,
        GamepadButtonFlags.LeftThumb     => GamepadButton.LeftThumb,
        GamepadButtonFlags.RightThumb    => GamepadButton.RightThumb,
        GamepadButtonFlags.DPadUp        => GamepadButton.Up,
        GamepadButtonFlags.DPadDown      => GamepadButton.Down,
        GamepadButtonFlags.DPadLeft      => GamepadButton.Left,
        GamepadButtonFlags.DPadRight     => GamepadButton.Right,
        GamepadButtonFlags.Start         => GamepadButton.Start,
        GamepadButtonFlags.Back          => GamepadButton.Back,
        _ => null,
    };

    public static string ToTriggerString(this GamepadSlider slider)
    {
        return slider switch
        {
            GamepadSlider.LeftTrigger => "LT",
            GamepadSlider.RightTrigger => "RT",
            _ => throw new ArgumentOutOfRangeException(nameof(slider), slider, null),
        };
    }

    public static string ToStickString(this GamepadAxis axis)
    {
        return axis switch
        {
            GamepadAxis.LeftThumbX => "LS",
            GamepadAxis.LeftThumbY => "LS",
            GamepadAxis.RightThumbX => "RS",
            GamepadAxis.RightThumbY => "RS",
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
        };
    }

    public static uint ToVJoyButton(this GamepadButton button)
    {
        return button switch
        {
            GamepadButton.A => 1,
            GamepadButton.B => 2,
            GamepadButton.X => 3,
            GamepadButton.Y => 4,
            GamepadButton.LeftShoulder => 5,
            GamepadButton.RightShoulder => 6,
            GamepadButton.Back => 7,
            GamepadButton.Start => 8,
            GamepadButton.LeftThumb => 9,
            GamepadButton.RightThumb => 10,
            GamepadButton.Up => 11,
            GamepadButton.Down => 12,
            GamepadButton.Left => 13,
            GamepadButton.Right => 14,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, null),
        };
    }

    public static USAGES ToVJoyUsage(this GamepadAxis axis)
    {
        return axis switch
        {
            GamepadAxis.LeftThumbX => USAGES.X,
            GamepadAxis.LeftThumbY => USAGES.Y,
            GamepadAxis.RightThumbX => USAGES.Rx,
            GamepadAxis.RightThumbY => USAGES.Ry,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
        };
    }

    public static USAGES ToVJoyUsage(this GamepadSlider slider)
    {
        return slider switch
        {
            GamepadSlider.LeftTrigger => USAGES.Slider0,
            GamepadSlider.RightTrigger => USAGES.Slider1,
            // Add other mappings as needed
            _ => throw new ArgumentOutOfRangeException(nameof(slider), slider, null),
        };
    }
}