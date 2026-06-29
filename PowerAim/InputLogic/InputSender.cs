using PowerAim.InputLogic.Contracts;
using PowerAim.Config;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PowerAim.Class.Native;
using WindowsInput;
using PowerAim.InputLogic;

namespace PowerAim.InputLogic
{
    public enum KeyPressState
    {
        DownAndUp,
        Down,
        Up,
    }

    public class InputSender
    {
        private static readonly InputSimulator _inputSimulator = new();

        // ===========================================================================
        //  Crosshair / aim movement — routes either through MouseManager (synth mouse)
        //  or through the active gamepad sender's right-stick. This used to live in a
        //  separate MoveInputManager class; folded in here so the codebase only has
        //  one "send input" entry-point.
        // ===========================================================================

        private const int StickReleaseAfterMs = 80;
        private static DateTime _lastStickPush = DateTime.MinValue;
        private static System.Threading.Timer? _releaseTimer;

        /// <summary>
        ///     True if a working gamepad sender exists. When false, all aim-via-gamepad calls
        ///     silently fall back to the mouse path.
        /// </summary>
        public static bool GamepadAimAvailable =>
            GamepadManager.CanSend && GamepadManager.GamepadSender != null;

        /// <summary>
        ///     True when the aim pipeline should drive the right-stick instead of the mouse.
        ///     Source of truth is now <see cref="DropdownState.MouseMovementMethod"/> ==
        ///     <see cref="MouseMovementMethod.Gamepad"/> — the old <c>ToggleState.UseControllerForAim</c>
        ///     toggle was folded into the same dropdown the other movement methods live in.
        /// </summary>
        public static bool GamepadAimActive =>
            GamepadAimAvailable && AppConfig.Current?.DropdownState?.MouseMovementMethod == MouseMovementMethod.Gamepad;

        /// <summary>
        ///     Apply an incremental aim delta. Routes through the gamepad's right-stick when
        ///     <see cref="GamepadAimActive"/>, otherwise through <see cref="MouseManager"/>.
        /// </summary>
        public static void Move(int dx, int dy)
        {
            if (GamepadAimActive) DriveRightStick(dx, dy);
            else MouseManager.Move(dx, dy);
        }

        /// <summary>
        ///     Same semantics as <see cref="MouseManager.MoveCrosshair"/> — compute the delta
        ///     from the detected point to the crosshair centre, then dispatch through whichever
        ///     path is active. Keeping the path-switch here means gamepad-aim still benefits
        ///     from the same Sensitivity slider and EMA smoothing as mouse-aim.
        /// </summary>
        public static void MoveCrosshair(int detectedX, int detectedY, System.Drawing.Rectangle area)
        {
            if (!GamepadAimActive)
            {
                // Coarse aim direction for the debug visualizer (mouse path); the gamepad path is
                // captured at the right-stick instead.
                InputEventBus.MouseMove(detectedX - area.Width / 2.0, detectedY - area.Height / 2.0);
                MouseManager.MoveCrosshair(detectedX, detectedY, area);
                return;
            }

            // Match MouseManager's delta computation so the Sensitivity slider feels identical
            // on both engines.
            int halfW = area.Width / 2;
            int halfH = area.Height / 2;
            int targetX = detectedX - halfW;
            int targetY = detectedY - halfH;
            double aspect = area.Height > 0 ? (double)area.Width / area.Height : 1.0;
            targetY = (int)(targetY * aspect);
            targetX = Math.Clamp(targetX, -150, 150);
            targetY = Math.Clamp(targetY, -150, 150);
            double t = 1 - AppConfig.Current.SliderSettings.MouseSensitivity;
            int stepX = (int)(targetX * t);
            int stepY = (int)(targetY * t);
            DriveRightStick(stepX, stepY);
        }

        private static bool _stickAxesPaused;

        private static void DriveRightStick(int dx, int dy)
        {
            var sender = GamepadManager.GamepadSender;
            if (sender == null || !sender.CanWork) return;
            // CRITICAL: take ownership of the right-stick axes ONCE — otherwise the sender's
            // SyncLoop (mirror physical→virtual every 1 ms) overwrites our values back to the
            // physical pad's state (0 if nothing is plugged in) immediately after we write them.
            // Same root cause that broke the mapping engine.
            if (!_stickAxesPaused)
            {
                sender.PauseSync(GamepadAxis.RightThumbX);
                sender.PauseSync(GamepadAxis.RightThumbY);
                _stickAxesPaused = true;
            }
            // ±150 px clipping → roughly full deflection.
            const double scale = 200.0;
            short sx = (short)Math.Clamp(dx * scale, short.MinValue, short.MaxValue);
            short sy = (short)Math.Clamp(-dy * scale, short.MinValue, short.MaxValue);
            sender.SetAxisValue(GamepadAxis.RightThumbX, sx);
            sender.SetAxisValue(GamepadAxis.RightThumbY, sy);
            _lastStickPush = DateTime.UtcNow;
            EnsureReleaseTimer();
        }

        /// <summary>
        ///     Auto-release the right-stick to neutral after <see cref="StickReleaseAfterMs"/> ms
        ///     of inactivity so the in-game view doesn't keep panning once we lose the target.
        /// </summary>
        private static void EnsureReleaseTimer()
        {
            if (_releaseTimer != null) return;
            _releaseTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if ((DateTime.UtcNow - _lastStickPush).TotalMilliseconds < StickReleaseAfterMs) return;
                    var sender = GamepadManager.GamepadSender;
                    if (sender == null || !sender.CanWork) return;
                    sender.SetAxisValue(GamepadAxis.RightThumbX, 0);
                    sender.SetAxisValue(GamepadAxis.RightThumbY, 0);
                }
                catch { /* ignored */ }
            }, null, 30, 30);
        }

        // ===========================================================================
        //  Keypress / button dispatch
        // ===========================================================================

        public static async Task SendKeyAsync(StoredInputBinding evt, CancellationTokenSource token = default)
        {
            await SendKeyAsync(evt, KeyPressState.DownAndUp, token);
        }

        public static async Task SendKeyAsync(StoredInputBinding evt, KeyPressState pressState, CancellationTokenSource token = default)
        {
            if (!evt.IsCombo)
            {
                await SendSingleAsync(evt, pressState, token);
                return;
            }

            // Chord: press components in order, release in reverse — e.g. Ctrl+A → Ctrl↓ A↓ A↑ Ctrl↑.
            // Never forward DownAndUp to a child (that would release a modifier before the main key).
            switch (pressState)
            {
                case KeyPressState.Down:
                    foreach (var c in evt.Components!)
                        await SendSingleAsync(c, KeyPressState.Down, token);
                    break;
                case KeyPressState.Up:
                    foreach (var c in Enumerable.Reverse(evt.Components!))
                        await SendSingleAsync(c, KeyPressState.Up, token);
                    break;
                default: // DownAndUp
                    foreach (var c in evt.Components!)
                        await SendSingleAsync(c, KeyPressState.Down, token);
                    foreach (var c in Enumerable.Reverse(evt.Components!))
                        await SendSingleAsync(c, KeyPressState.Up, token);
                    break;
            }
        }

        /// <summary>Sends ONE input (mouse / gamepad / keyboard). Chords are decomposed into these by <see cref="SendKeyAsync"/>.</summary>
        private static async Task SendSingleAsync(StoredInputBinding evt, KeyPressState pressState, CancellationTokenSource token = default)
        {
            if (evt.Is<MouseEventArgs>())
            {
                MouseEventArgs mouseArgs = evt.MouseEventArgs;
                ReportMouse(mouseArgs.Button, pressState);
                if (mouseArgs.Button == MouseButtons.Left)
                {
                    if (pressState == KeyPressState.Down)
                        MouseManager.LeftDown();
                    else if (pressState == KeyPressState.Up)
                        MouseManager.LeftUp();
                    else
                        await MouseManager.DoTriggerClick();
                }
                else
                    MouseManager.SendMouseEvent(mouseArgs, pressState);
            }
            else if (evt.Is<GamepadEventArgs>() && GamepadManager.CanSend)
            {
                GamepadManager.GamepadSender?.Send(evt.GamepadEventArgs, pressState);
            }
            else if (evt.Is<KeyEventArgs>())
            {
                KeyEventArgs keyArgs = evt.KeyEventArgs;
                await SendKeyboardKeyAsync(keyArgs, KeyboardSendMode.UseInputSimulator, pressState);
            }
        }


        /// <summary>Mirror a synthetic mouse-button send to the debug input visualizer.</summary>
        private static void ReportMouse(MouseButtons button, KeyPressState pressState)
        {
            int code = button switch
            {
                MouseButtons.Left => 0,
                MouseButtons.Right => 1,
                MouseButtons.Middle => 2,
                MouseButtons.XButton1 => 3,
                MouseButtons.XButton2 => 4,
                _ => -1
            };
            if (code < 0) return;
            if (pressState is KeyPressState.Down or KeyPressState.DownAndUp) InputEventBus.MouseButton(code, true);
            if (pressState is KeyPressState.Up or KeyPressState.DownAndUp) InputEventBus.MouseButton(code, false);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public static async Task SendKeyboardKeyAsync(KeyEventArgs keyArgs, KeyboardSendMode sendMode, KeyPressState pressState = KeyPressState.DownAndUp)
        {
            if(sendMode == KeyboardSendMode.UseInputSimulator)
            {
                if (pressState is KeyPressState.DownAndUp or KeyPressState.Down)
                {
                    _inputSimulator.Keyboard.KeyDown((VirtualKeyCode)keyArgs.KeyCode);
                    InputEventBus.Key((int)keyArgs.KeyCode, true);
                }
                if (pressState == KeyPressState.DownAndUp)
                    await Task.Delay(MouseManager.GetRandomDelay());
                if (pressState is KeyPressState.DownAndUp or KeyPressState.Up)
                {
                    _inputSimulator.Keyboard.KeyUp((VirtualKeyCode)keyArgs.KeyCode);
                    InputEventBus.Key((int)keyArgs.KeyCode, false);
                }
                return;
            }

            if (sendMode == KeyboardSendMode.WindowsSendInputByKeyCode)
            {
                List<INPUT> inputs = [];

                if (pressState is KeyPressState.DownAndUp or KeyPressState.Down)
                {
                    // Key down event
                    INPUT downInput = new()
                    {
                        type = (uint)InputEventFlags.INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = (ushort)keyArgs.KeyCode,
                                dwFlags = 0 // Key down
                            }
                        }
                    };
                    inputs.Add(downInput);
                }

                if (pressState is KeyPressState.DownAndUp or KeyPressState.Up)
                {
                    // Key up event
                    INPUT upInput = new()
                    {
                        type = (uint)InputEventFlags.INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = (ushort)keyArgs.KeyCode,
                                dwFlags = (uint)InputEventFlags.KEYEVENTF_KEYUP // Key up
                            }
                        }
                    };
                    inputs.Add(upInput);
                }

                if (inputs.Count > 0)
                {
                    NativeAPIMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
                }
            }
            else if (sendMode == KeyboardSendMode.WindowsSendInputByScanCode)
            {
                ushort scanCode = (ushort)MapVirtualKey((uint)keyArgs.KeyCode, 0);
                List<INPUT> inputs = [];

                if (pressState is KeyPressState.DownAndUp or KeyPressState.Down)
                {
                    // Key down event
                    INPUT downInput = new()
                    {
                        type = (uint)InputEventFlags.INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wScan = scanCode,
                                dwFlags = (uint)InputEventFlags.KEYEVENTF_SCANCODE // Key down
                            }
                        }
                    };
                    inputs.Add(downInput);
                }

                if (pressState is KeyPressState.DownAndUp or KeyPressState.Up)
                {
                    // Key up event
                    INPUT upInput = new()
                    {
                        type = (uint)InputEventFlags.INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wScan = scanCode,
                                dwFlags = (uint)(InputEventFlags.KEYEVENTF_SCANCODE | InputEventFlags.KEYEVENTF_KEYUP) // Key up
                            }
                        }
                    };
                    inputs.Add(upInput);
                }

                if (inputs.Count > 0)
                {
                    NativeAPIMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
                }
            }
        }
    }
}

public enum KeyboardSendMode
{
    UseInputSimulator,
    WindowsSendInputByScanCode,
    WindowsSendInputByKeyCode,
}