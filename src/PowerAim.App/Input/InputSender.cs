using PowerAim.Input.Contracts;
using PowerAim.Config;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PowerAim.Class.Native;
using WindowsInput;
using PowerAim.Input;
using Nextended.UI.Input;
using Nextended.UI.Input.Gamepad;
using Nextended.UI.Native;

namespace PowerAim.Input
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

        // Latest aim vector for the right stick; the mixer thread keeps sending it (blended with the
        // physical stick when configured) and releases it to neutral once it is older than 80 ms.
        private static readonly PowerAim.Input.Gamepad.StickMixer _stickMixer = new();
        private static readonly System.Diagnostics.Stopwatch _stickClock = System.Diagnostics.Stopwatch.StartNew();
        private static readonly object _stickSendGate = new();
        private static short _sentStickX, _sentStickY;
        private static bool _sentValid;
        // The sender whose aim-stick axes we took over (and on which side): a re-created sender (send mode
        // changed) or a southpaw switch claims again instead of writing into axes its sync loop still owns.
        private static IGamepadSender? _claimedBy;
        private static bool _claimedLeft;
        private static System.Threading.Thread? _stickThread;
        private static volatile bool _stickThreadRun = true;

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

        private static (GamepadAxis X, GamepadAxis Y) AimAxes(bool left)
            => left ? (GamepadAxis.LeftThumbX, GamepadAxis.LeftThumbY) : (GamepadAxis.RightThumbX, GamepadAxis.RightThumbY);

        /// <summary>
        ///     Take over the aim stick's axes on <paramref name="sender"/> (right stick, or left for southpaw):
        ///     otherwise the sender's sync loop mirrors the physical pad over our values every millisecond.
        ///     Switching sides hands the previous stick back to the pad.
        /// </summary>
        private static void ClaimAimStick(IGamepadSender sender)
        {
            bool left = AppConfig.Current?.ControllerSettings?.AimStickLeft == true;
            if (ReferenceEquals(sender, _claimedBy) && left == _claimedLeft) return;
            lock (_stickSendGate)
            {
                if (ReferenceEquals(sender, _claimedBy))
                {
                    var (oldX, oldY) = AimAxes(_claimedLeft);
                    sender.SetAxisValue(oldX, 0);
                    sender.SetAxisValue(oldY, 0);
                    sender.ResumeSync(oldX);
                    sender.ResumeSync(oldY);
                }
                var (x, y) = AimAxes(left);
                sender.PauseSync(x);
                sender.PauseSync(y);
                _claimedBy = sender;
                _claimedLeft = left;
                _sentValid = false;
            }
        }

        private static void DriveRightStick(int dx, int dy)
        {
            var sender = GamepadManager.GamepadSender;
            if (sender == null || !sender.CanWork) return;
            // CRITICAL: take ownership of the right-stick axes ONCE — otherwise the sender's
            // SyncLoop (mirror physical→virtual every 1 ms) overwrites our values back to the
            // physical pad's state (0 if nothing is plugged in) immediately after we write them.
            // Same root cause that broke the mapping engine.
            ClaimAimStick(sender);
            // ±150 px clipping → roughly full deflection.
            const double scale = 200.0;
            double ax = Math.Clamp(dx * scale, -short.MaxValue, short.MaxValue) / short.MaxValue;
            double ay = Math.Clamp(-dy * scale, -short.MaxValue, short.MaxValue) / short.MaxValue;
            var options = StickOptions();
            var (sx, sy) = PowerAim.Input.Gamepad.StickOutputShaper.ShapeAi(ax, ay, options);
            _stickMixer.SetAi(sx, sy, _stickClock.Elapsed.TotalMilliseconds);
            PushStick(sender, options); // send now; the mixer thread only keeps it up to date
            EnsureStickThread();
        }

        /// <summary>
        ///     Play a calibration pattern on the right stick (and right bumper) for <paramref name="seconds"/>,
        ///     then return the stick to neutral and the bumper to the physical pad. The values go through the
        ///     stick mixer unshaped, so the deadzone check sends exactly the configured deadzone.
        /// </summary>
        public static async Task<bool> PlayStickPatternAsync(Func<double, (double X, double Y, bool Bumper)> pattern,
            double seconds, CancellationToken cancellationToken = default)
        {
            var sender = GamepadManager.GamepadSender;
            if (sender == null || !sender.CanWork) return false;
            ClaimAimStick(sender);
            sender.PauseSync(GamepadButton.RightShoulder);
            var started = System.Diagnostics.Stopwatch.StartNew();
            bool bumper = false;
            try
            {
                EnsureStickThread();
                while (started.Elapsed.TotalSeconds < seconds && !cancellationToken.IsCancellationRequested)
                {
                    var (x, y, b) = pattern(started.Elapsed.TotalSeconds);
                    _stickMixer.SetAi(x, y, _stickClock.Elapsed.TotalMilliseconds);
                    if (b != bumper) sender.SetButtonState(GamepadButton.RightShoulder, bumper = b);
                    try { await Task.Delay(10, cancellationToken); } catch (OperationCanceledException) { break; }
                }
            }
            finally
            {
                _stickMixer.SetAi(0, 0, _stickClock.Elapsed.TotalMilliseconds);
                if (bumper) sender.SetButtonState(GamepadButton.RightShoulder, false);
                sender.ResumeSync(GamepadButton.RightShoulder);
            }
            return true;
        }

        /// <summary>
        ///     Drive the aim stick with a share of the game's full turn speed per axis (screen coordinates,
        ///     −1..1) — the V2 engine's gamepad output. Deadzone remap, response curve and ceiling are applied here.
        /// </summary>
        /// <returns>False when no virtual pad is ready, so nothing was sent.</returns>
        public static bool SetAimStick(double speedX, double speedY)
        {
            var sender = GamepadManager.GamepadSender;
            if (sender == null || !sender.CanWork) return false;
            ClaimAimStick(sender);
            var options = StickOptions();
            var (sx, sy) = PowerAim.Input.Gamepad.StickOutputShaper.ShapeAi(speedX, -speedY, options); // stick Y is up
            _stickMixer.SetAi(sx, sy, _stickClock.Elapsed.TotalMilliseconds);
            PushStick(sender, options);
            EnsureStickThread();
            return true;
        }

        public static PowerAim.Input.Gamepad.StickShapeOptions CurrentStickOptions => StickOptions();

        private static PowerAim.Input.Gamepad.StickShapeOptions StickOptions()
        {
            var c = AppConfig.Current?.ControllerSettings;
            return c == null
                ? PowerAim.Input.Gamepad.StickShapeOptions.Default
                : new(c.GameDeadzonePct, c.MaxOutputPct, c.ResponseCurve, c.ManualOverridePct, c.AntiStrafePct, c.InvertAiY);
        }

        /// <summary>Send the mixer's current right-stick value when it changed.</summary>
        private static void PushStick(IGamepadSender sender, PowerAim.Input.Gamepad.StickShapeOptions options)
        {
            double userX = 0, userY = 0, leftX = 0;
            if ((options.ManualOverridePct > 0 || options.AntiStrafePct > 0)
                && GamepadManager.GamepadReader is IGamepadStateSource { IsConnected: true } source)
            {
                var pad = source.GetState().Gamepad;
                // Southpaw swaps the roles: the left stick aims, the right one moves.
                bool southpaw = _claimedLeft;
                userX = (southpaw ? pad.LeftThumbX : pad.RightThumbX) / (double)short.MaxValue;
                userY = (southpaw ? pad.LeftThumbY : pad.RightThumbY) / (double)short.MaxValue;
                leftX = (southpaw ? pad.RightThumbX : pad.LeftThumbX) / (double)short.MaxValue;
            }
            var (x, y) = _stickMixer.Next(_stickClock.Elapsed.TotalMilliseconds, userX, userY, leftX, options);
            short sx = (short)Math.Round(Math.Clamp(x, -1, 1) * short.MaxValue);
            short sy = (short)Math.Round(Math.Clamp(y, -1, 1) * short.MaxValue);
            lock (_stickSendGate)
            {
                if (!ReferenceEquals(sender, _claimedBy)) return; // not ours (yet): its sync loop owns the sticks
                if (_sentValid && sx == _sentStickX && sy == _sentStickY) return;
                var (axisX, axisY) = AimAxes(_claimedLeft);
                sender.SetAxisValue(axisX, sx);
                sender.SetAxisValue(axisY, sy);
                _sentStickX = sx;
                _sentStickY = sy;
                _sentValid = true;
            }
        }

        /// <summary>
        ///     Keeps the right stick current between aim frames (blend with the physical stick, anti-strafe)
        ///     and releases it to neutral after 80 ms without a new aim vector, so the in-game view doesn't
        ///     keep panning once we lose the target.
        /// </summary>
        private static void EnsureStickThread()
        {
            if (_stickThread != null) return;
            _stickThread = new System.Threading.Thread(() =>
            {
                while (_stickThreadRun)
                {
                    try
                    {
                        // Also after the movement method left "Gamepad": the stick still has to go back to neutral.
                        var sender = GamepadManager.GamepadSender;
                        if (sender is { CanWork: true }) PushStick(sender, StickOptions());
                    }
                    catch { /* ignored */ }
                    // Fast while an aim vector is live or the physical stick is passed through, slow when idle.
                    bool busy = _stickMixer.IsFresh(_stickClock.Elapsed.TotalMilliseconds)
                                || AppConfig.Current?.ControllerSettings?.ManualOverridePct > 0;
                    System.Threading.Thread.Sleep(busy ? 2 : 15);
                }
            })
            {
                IsBackground = true,
                Name = "PowerAim stick mixer",
                Priority = System.Threading.ThreadPriority.AboveNormal,
            };
            _stickThread.Start();
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
            if (evt.MouseButton is { } button)
            {
                ReportMouse(button, pressState);
                if (button == MouseButtons.Left)
                {
                    if (pressState == KeyPressState.Down)
                        MouseManager.LeftDown();
                    else if (pressState == KeyPressState.Up)
                        MouseManager.LeftUp();
                    else
                        await MouseManager.DoTriggerClick();
                }
                else
                    MouseManager.SendMouseEvent(button, state: pressState);
            }
            else if (evt.ToGamepadEventArgs() is { } gamepadArgs && GamepadManager.CanSend)
            {
                GamepadManager.GamepadSender?.Send(gamepadArgs, pressState);
            }
            else if (evt.KeyboardKey is { } key)
            {
                await SendKeyboardKeyAsync(new KeyEventArgs(key), KeyboardSendMode.UseInputSimulator, pressState);
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
                    NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
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
                    NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
                }
            }
        }
        public static void Shutdown()
        {
            try
            {
                _stickThreadRun = false;
            }
            catch { }
        }
    }
}

public enum KeyboardSendMode
{
    UseInputSimulator,
    WindowsSendInputByScanCode,
    WindowsSendInputByKeyCode,
}
