using Aimmy2.InputLogic.Contracts;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Accord.Diagnostics;
using Aimmy2.Class.Native;
using WindowsInput;
using InputLogic;

namespace Aimmy2.InputLogic
{
    public enum KeyPressState
    {
        DownAndUp,
        Down,
        Up,
    }

    public class InputSender
    {
        private static InputSimulator _inputSimulator = new InputSimulator();


        public static async Task SendKeyAsync(StoredInputBinding evt, CancellationTokenSource token = default)
        {
            await SendKeyAsync(evt, KeyPressState.DownAndUp, token);
        }

        public static async Task SendKeyAsync(StoredInputBinding evt, KeyPressState pressState, CancellationTokenSource token = default)
        {
            if (evt.Is<MouseEventArgs>())
            {
                MouseEventArgs mouseArgs = evt.MouseEventArgs;
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


        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public static async Task SendKeyboardKeyAsync(KeyEventArgs keyArgs, KeyboardSendMode sendMode, KeyPressState pressState = KeyPressState.DownAndUp)
        {
            if(sendMode == KeyboardSendMode.UseInputSimulator)
            {
                if(pressState == KeyPressState.DownAndUp || pressState == KeyPressState.Down)
                    _inputSimulator.Keyboard.KeyDown((VirtualKeyCode)keyArgs.KeyCode);
                if (pressState == KeyPressState.DownAndUp)
                    await Task.Delay(MouseManager.GetRandomDelay());
                if (pressState == KeyPressState.DownAndUp || pressState == KeyPressState.Up)
                    _inputSimulator.Keyboard.KeyUp((VirtualKeyCode)keyArgs.KeyCode);
                return;
            }

            if (sendMode == KeyboardSendMode.WindowsSendInputByKeyCode)
            {
                List<INPUT> inputs = new List<INPUT>();

                if (pressState == KeyPressState.DownAndUp || pressState == KeyPressState.Down)
                {
                    // Key down event
                    INPUT downInput = new INPUT
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

                if (pressState == KeyPressState.DownAndUp || pressState == KeyPressState.Up)
                {
                    // Key up event
                    INPUT upInput = new INPUT
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
                List<INPUT> inputs = new List<INPUT>();

                if (pressState == KeyPressState.DownAndUp || pressState == KeyPressState.Down)
                {
                    // Key down event
                    INPUT downInput = new INPUT
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

                if (pressState == KeyPressState.DownAndUp || pressState == KeyPressState.Up)
                {
                    // Key up event
                    INPUT upInput = new INPUT
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