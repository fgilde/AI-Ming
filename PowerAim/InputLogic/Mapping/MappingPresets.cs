using System.Windows.Forms;
using PowerAim.Config;
using PowerAim;

namespace PowerAim.InputLogic.Mapping;

/// <summary>
///     Factory for built-in starter profiles. The "FPS" preset wires the canonical FPS layout
///     (WASD strafe, mouse look, LMB shoot, RMB ADS, R reload, Space jump, Ctrl crouch, Shift
///     sprint) into the right gamepad equivalents — so a KB+M user can play gamepad-only titles
///     out of the box.
/// </summary>
public static class MappingPresets
{
    /// <summary>
    ///     Generic FPS preset: keyboard+mouse → virtual Xbox controller. Mirrors what most
    ///     modern FPS gamepad bindings expect (Call of Duty, Battlefield, Apex Legends, etc.).
    /// </summary>
    public static ControllerMappingProfile NewFpsKbToPad()
    {
        var p = new ControllerMappingProfile
        {
            Name = Locale.PresetNameFpsKbToPad,
            Enabled = false,
            MatchProcess = "",
            MouseToStickSensitivity = 1.0,
        };
        var m = p.Mappings;

        // Movement: WASD → Left stick.
        m.Add(MakeKbToStick(Keys.W, GamepadStickDirection.LeftStickUp));
        m.Add(MakeKbToStick(Keys.S, GamepadStickDirection.LeftStickDown));
        m.Add(MakeKbToStick(Keys.A, GamepadStickDirection.LeftStickLeft));
        m.Add(MakeKbToStick(Keys.D, GamepadStickDirection.LeftStickRight));

        // Mouse → Right stick (look). Single sentinel mapping enables the engine's mouse-to-stick
        // pump; X/Y wiring is implicit inside the engine.
        m.Add(new InputMapping
        {
            SourceKind = MappingInputKind.MouseButton, SourceCode = 0xFFFF,
            TargetKind = MappingInputKind.GamepadStickDirection,
            TargetCode = (int)GamepadStickDirection.RightStickRight,
        });

        // Combat.
        m.Add(MakeMouseToTrigger(MouseButtons.Left,  /* right trigger */ 1)); // shoot
        m.Add(MakeMouseToTrigger(MouseButtons.Right, /* left trigger  */ 0)); // ADS

        // Standard FPS buttons.
        m.Add(MakeKbToButton(Keys.Space,    XboxButtonId.A));
        m.Add(MakeKbToButton(Keys.ControlKey, XboxButtonId.B));     // crouch
        m.Add(MakeKbToButton(Keys.R,        XboxButtonId.X));        // reload
        m.Add(MakeKbToButton(Keys.E,        XboxButtonId.Y));        // use / interact
        m.Add(MakeKbToButton(Keys.ShiftKey, XboxButtonId.LeftThumb));// sprint
        m.Add(MakeKbToButton(Keys.G,        XboxButtonId.LeftShoulder)); // grenade
        m.Add(MakeKbToButton(Keys.F,        XboxButtonId.RightShoulder));// melee
        m.Add(MakeKbToButton(Keys.Escape,   XboxButtonId.Start));
        m.Add(MakeKbToButton(Keys.Tab,      XboxButtonId.Back));

        // Weapon select.
        m.Add(MakeKbToButton(Keys.D1, XboxButtonId.Up));
        m.Add(MakeKbToButton(Keys.D2, XboxButtonId.Right));
        m.Add(MakeKbToButton(Keys.D3, XboxButtonId.Down));
        m.Add(MakeKbToButton(Keys.D4, XboxButtonId.Left));

        return p;
    }

    /// <summary>
    ///     FPS preset combining both directions in one profile — the runtime direction picker
    ///     decides which side actually fires. Handy when you want a single profile per game.
    /// </summary>
    public static ControllerMappingProfile NewFpsBoth()
    {
        var kbToPad = NewFpsKbToPad();
        var padToKb = NewFpsPadToKb();
        var combined = new ControllerMappingProfile
        {
            Name = Locale.PresetNameFpsBoth,
            Enabled = true,
            MouseToStickSensitivity = kbToPad.MouseToStickSensitivity,
            StickToMouseSensitivity = padToKb.StickToMouseSensitivity,
        };
        foreach (var m in kbToPad.Mappings) combined.Mappings.Add(m);
        foreach (var m in padToKb.Mappings) combined.Mappings.Add(m);
        return combined;
    }

    /// <summary>
    ///     Driving preset: KB+M → virtual pad with steering on the left stick and triggers on the
    ///     mouse buttons. Suited for arcade racers that gate KB+M players (Forza Horizon, NFS).
    /// </summary>
    public static ControllerMappingProfile NewDrivingKbToPad()
    {
        var p = new ControllerMappingProfile
        {
            Name = Locale.PresetNameDrivingKbToPad,
            Enabled = false,
            MouseToStickSensitivity = 1.0,
        };
        var m = p.Mappings;
        // Steering — A/D drive left/right on the left stick.
        m.Add(MakeKbToStick(Keys.A, GamepadStickDirection.LeftStickLeft));
        m.Add(MakeKbToStick(Keys.D, GamepadStickDirection.LeftStickRight));
        // Accelerator / Brake on the triggers, also wired to mouse for thumb-style users.
        m.Add(MakeKbToTrigger(Keys.W, /* RT */ 1));
        m.Add(MakeKbToTrigger(Keys.S, /* LT */ 0));
        m.Add(MakeMouseToTrigger(MouseButtons.Left,  1));
        m.Add(MakeMouseToTrigger(MouseButtons.Right, 0));
        // Handbrake / gear / camera.
        m.Add(MakeKbToButton(Keys.Space,    XboxButtonId.A));        // handbrake
        m.Add(MakeKbToButton(Keys.LShiftKey, XboxButtonId.RightShoulder)); // shift up
        m.Add(MakeKbToButton(Keys.ControlKey, XboxButtonId.LeftShoulder));  // shift down
        m.Add(MakeKbToButton(Keys.Q,        XboxButtonId.LeftThumb));   // look back / camera
        m.Add(MakeKbToButton(Keys.E,        XboxButtonId.RightThumb));
        m.Add(MakeKbToButton(Keys.R,        XboxButtonId.Y));          // reset car
        m.Add(MakeKbToButton(Keys.Escape,   XboxButtonId.Start));
        m.Add(MakeKbToButton(Keys.Tab,      XboxButtonId.Back));
        return p;
    }

    /// <summary>
    ///     Controller-as-mouse navigation preset. Use the gamepad's left stick to drive the OS
    ///     cursor — handy for couch use, accessibility, or grinding through menus without a
    ///     keyboard. Right stick scrolls. Triggers / face buttons are LMB/RMB/etc.
    /// </summary>
    public static ControllerMappingProfile NewControllerAsMouse()
    {
        var p = new ControllerMappingProfile
        {
            Name = Locale.PresetNameControllerAsMouse,
            Enabled = false,
            StickToMouseSensitivity = 14.0,
        };
        var m = p.Mappings;
        // Right stick → mouse motion (sentinel).
        m.Add(new InputMapping
        {
            SourceKind = MappingInputKind.GamepadStickDirection,
            SourceCode = (int)GamepadStickDirection.RightStickRight,
            TargetKind = MappingInputKind.MouseButton, TargetCode = 0xFFFF,
        });
        // Triggers → mouse buttons.
        m.Add(MakeTriggerToMouse(unchecked((int)0x80000002), MouseButtons.Left));   // RT = LMB
        m.Add(MakeTriggerToMouse(unchecked((int)0x80000001), MouseButtons.Right));  // LT = RMB
        // Face buttons → common keys for menus / explorer.
        m.Add(MakeButtonToKb(XboxButtonId.A,          Keys.Enter));
        m.Add(MakeButtonToKb(XboxButtonId.B,          Keys.Escape));
        m.Add(MakeButtonToKb(XboxButtonId.X,          Keys.Back));
        m.Add(MakeButtonToKb(XboxButtonId.Y,          Keys.Tab));
        m.Add(MakeButtonToKb(XboxButtonId.LeftThumb,  Keys.LWin));
        m.Add(MakeButtonToKb(XboxButtonId.Start,      Keys.Apps));
        // D-Pad → arrow keys for menus.
        m.Add(MakeButtonToKb(XboxButtonId.Up,    Keys.Up));
        m.Add(MakeButtonToKb(XboxButtonId.Down,  Keys.Down));
        m.Add(MakeButtonToKb(XboxButtonId.Left,  Keys.Left));
        m.Add(MakeButtonToKb(XboxButtonId.Right, Keys.Right));
        return p;
    }

    private static InputMapping MakeKbToTrigger(Keys key, int triggerCode) => new()
    {
        SourceKind = MappingInputKind.KeyboardKey, SourceCode = (int)key,
        TargetKind = MappingInputKind.GamepadTrigger, TargetCode = triggerCode,
    };

    /// <summary>
    ///     Reverse: gamepad → keyboard+mouse. Useful when a title insists on KB only.
    /// </summary>
    public static ControllerMappingProfile NewFpsPadToKb()
    {
        var p = new ControllerMappingProfile
        {
            Name = Locale.PresetNameFpsPadToKb,
            Enabled = false,
            MatchProcess = "",
            StickToMouseSensitivity = 12.0,
        };
        var m = p.Mappings;

        // Left stick → WASD.
        m.Add(MakeStickToKb(GamepadStickDirection.LeftStickUp,    Keys.W));
        m.Add(MakeStickToKb(GamepadStickDirection.LeftStickDown,  Keys.S));
        m.Add(MakeStickToKb(GamepadStickDirection.LeftStickLeft,  Keys.A));
        m.Add(MakeStickToKb(GamepadStickDirection.LeftStickRight, Keys.D));

        // Right stick → mouse motion (sentinel, like in the other direction).
        m.Add(new InputMapping
        {
            SourceKind = MappingInputKind.GamepadStickDirection,
            SourceCode = (int)GamepadStickDirection.RightStickRight,
            TargetKind = MappingInputKind.MouseButton, TargetCode = 0xFFFF,
        });

        // Triggers → mouse buttons.
        m.Add(MakeTriggerToMouse(/* RT */ unchecked((int)0x80000002), MouseButtons.Left));
        m.Add(MakeTriggerToMouse(/* LT */ unchecked((int)0x80000001), MouseButtons.Right));

        // Face buttons.
        m.Add(MakeButtonToKb(XboxButtonId.A,             Keys.Space));
        m.Add(MakeButtonToKb(XboxButtonId.B,             Keys.ControlKey));
        m.Add(MakeButtonToKb(XboxButtonId.X,             Keys.R));
        m.Add(MakeButtonToKb(XboxButtonId.Y,             Keys.E));
        m.Add(MakeButtonToKb(XboxButtonId.LeftThumb,     Keys.ShiftKey));
        m.Add(MakeButtonToKb(XboxButtonId.LeftShoulder,  Keys.G));
        m.Add(MakeButtonToKb(XboxButtonId.RightShoulder, Keys.F));
        m.Add(MakeButtonToKb(XboxButtonId.Start,         Keys.Escape));
        m.Add(MakeButtonToKb(XboxButtonId.Back,          Keys.Tab));
        m.Add(MakeButtonToKb(XboxButtonId.Up,            Keys.D1));
        m.Add(MakeButtonToKb(XboxButtonId.Right,         Keys.D2));
        m.Add(MakeButtonToKb(XboxButtonId.Down,          Keys.D3));
        m.Add(MakeButtonToKb(XboxButtonId.Left,          Keys.D4));

        return p;
    }

    // ---------- helpers ----------

    private static InputMapping MakeKbToStick(Keys key, GamepadStickDirection dir) => new()
    {
        SourceKind = MappingInputKind.KeyboardKey, SourceCode = (int)key,
        TargetKind = MappingInputKind.GamepadStickDirection, TargetCode = (int)dir,
    };

    private static InputMapping MakeMouseToTrigger(MouseButtons btn, int triggerCode) => new()
    {
        SourceKind = MappingInputKind.MouseButton, SourceCode = (int)btn,
        TargetKind = MappingInputKind.GamepadTrigger, TargetCode = triggerCode,
    };

    private static InputMapping MakeKbToButton(Keys key, XboxButtonId button) => new()
    {
        SourceKind = MappingInputKind.KeyboardKey, SourceCode = (int)key,
        TargetKind = MappingInputKind.GamepadButton, TargetCode = (int)button,
    };

    private static InputMapping MakeStickToKb(GamepadStickDirection dir, Keys key) => new()
    {
        SourceKind = MappingInputKind.GamepadStickDirection, SourceCode = (int)dir,
        TargetKind = MappingInputKind.KeyboardKey, TargetCode = (int)key,
    };

    private static InputMapping MakeButtonToKb(XboxButtonId button, Keys key) => new()
    {
        SourceKind = MappingInputKind.GamepadButton, SourceCode = (int)button,
        TargetKind = MappingInputKind.KeyboardKey, TargetCode = (int)key,
    };

    private static InputMapping MakeTriggerToMouse(int triggerCode, MouseButtons btn) => new()
    {
        SourceKind = MappingInputKind.GamepadTrigger, SourceCode = triggerCode,
        TargetKind = MappingInputKind.MouseButton, TargetCode = (int)btn,
    };
}

/// <summary>
///     Stable persistence-friendly indices into the canonical Xbox360Button list used by the
///     engine's <c>Xbox360ButtonIdToFlag</c> mapping. Order must stay aligned with that method.
/// </summary>
public enum XboxButtonId
{
    Up = 0, Down = 1, Left = 2, Right = 3,
    Start = 4, Back = 5,
    LeftThumb = 6, RightThumb = 7,
    LeftShoulder = 8, RightShoulder = 9,
    A = 10, B = 11, X = 12, Y = 13,
}

/// <summary>
///     Bridge between the persistence-stable <see cref="XboxButtonId"/> index (used by
///     <see cref="PowerAim.Config.InputMapping"/>) and PowerAim's runtime
///     <see cref="PowerAim.InputLogic.Contracts.GamepadButton"/> enum. Lives in the same file as
///     <see cref="XboxButtonId"/> so adding a button only ever touches one place, and
///     <c>MappingEngine</c> / <c>MappingBindingConverter</c> no longer need their own copies.
/// </summary>
public static class XboxButtonIdExtensions
{
    public static PowerAim.InputLogic.Contracts.GamepadButton ToGamepadButton(this XboxButtonId id) => id switch
    {
        XboxButtonId.Up            => PowerAim.InputLogic.Contracts.GamepadButton.Up,
        XboxButtonId.Down          => PowerAim.InputLogic.Contracts.GamepadButton.Down,
        XboxButtonId.Left          => PowerAim.InputLogic.Contracts.GamepadButton.Left,
        XboxButtonId.Right         => PowerAim.InputLogic.Contracts.GamepadButton.Right,
        XboxButtonId.Start         => PowerAim.InputLogic.Contracts.GamepadButton.Start,
        XboxButtonId.Back          => PowerAim.InputLogic.Contracts.GamepadButton.Back,
        XboxButtonId.LeftThumb     => PowerAim.InputLogic.Contracts.GamepadButton.LeftThumb,
        XboxButtonId.RightThumb    => PowerAim.InputLogic.Contracts.GamepadButton.RightThumb,
        XboxButtonId.LeftShoulder  => PowerAim.InputLogic.Contracts.GamepadButton.LeftShoulder,
        XboxButtonId.RightShoulder => PowerAim.InputLogic.Contracts.GamepadButton.RightShoulder,
        XboxButtonId.A             => PowerAim.InputLogic.Contracts.GamepadButton.A,
        XboxButtonId.B             => PowerAim.InputLogic.Contracts.GamepadButton.B,
        XboxButtonId.X             => PowerAim.InputLogic.Contracts.GamepadButton.X,
        XboxButtonId.Y             => PowerAim.InputLogic.Contracts.GamepadButton.Y,
        _ => PowerAim.InputLogic.Contracts.GamepadButton.A,
    };

    public static XboxButtonId ToXboxButtonId(this PowerAim.InputLogic.Contracts.GamepadButton button) => button switch
    {
        PowerAim.InputLogic.Contracts.GamepadButton.Up            => XboxButtonId.Up,
        PowerAim.InputLogic.Contracts.GamepadButton.Down          => XboxButtonId.Down,
        PowerAim.InputLogic.Contracts.GamepadButton.Left          => XboxButtonId.Left,
        PowerAim.InputLogic.Contracts.GamepadButton.Right         => XboxButtonId.Right,
        PowerAim.InputLogic.Contracts.GamepadButton.Start         => XboxButtonId.Start,
        PowerAim.InputLogic.Contracts.GamepadButton.Back          => XboxButtonId.Back,
        PowerAim.InputLogic.Contracts.GamepadButton.LeftThumb     => XboxButtonId.LeftThumb,
        PowerAim.InputLogic.Contracts.GamepadButton.RightThumb    => XboxButtonId.RightThumb,
        PowerAim.InputLogic.Contracts.GamepadButton.LeftShoulder  => XboxButtonId.LeftShoulder,
        PowerAim.InputLogic.Contracts.GamepadButton.RightShoulder => XboxButtonId.RightShoulder,
        PowerAim.InputLogic.Contracts.GamepadButton.A             => XboxButtonId.A,
        PowerAim.InputLogic.Contracts.GamepadButton.B             => XboxButtonId.B,
        PowerAim.InputLogic.Contracts.GamepadButton.X             => XboxButtonId.X,
        PowerAim.InputLogic.Contracts.GamepadButton.Y             => XboxButtonId.Y,
        _ => XboxButtonId.A,
    };
}
