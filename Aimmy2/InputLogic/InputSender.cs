using Aimmy2.InputLogic.Contracts;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsInput;
using InputLogic;

namespace Aimmy2.InputLogic
{
    public class InputSender
    {
        private static InputSimulator _inputSimulator = new InputSimulator();
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_MOUSE = 0;
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint XBUTTON1 = 0x0001;
        private const uint XBUTTON2 = 0x0002;
        private const uint MOUSEEVENTF_XDOWN = 0x0080;
        private const uint MOUSEEVENTF_XUP = 0x0100;

        public static async Task SendKeyAsync(StoredInputBinding evt)
        {
            if (evt.Is<MouseEventArgs>())
            {
                MouseEventArgs mouseArgs = evt.MouseEventArgs;
                SendMouseEvent(mouseArgs);
            }
            else if (evt.Is<GamepadEventArgs>() && GamepadManager.CanSend)
            {
                GamepadManager.GamepadSender?.Send(evt.GamepadEventArgs);
            }
            else if (evt.Is<KeyEventArgs>())
            {
                KeyEventArgs keyArgs = evt.KeyEventArgs;
                await SendKeyboardKeyAsync(keyArgs, KeyboardSendMode.UseInputSimulator);
            }
        }

        public static void SendMouseEvent(MouseEventArgs args)
        {
            SendMouseEvent(args.Button, args.X, args.Y, args.Delta);
        }

        public static void SendMouseEvent(MouseButtons button, int x = 0, int y = 0, int wheelDelta = 0)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = x;
            inputs[0].u.mi.dy = y;
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.dwFlags = 0;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = IntPtr.Zero;

            switch (button)
            {
                case MouseButtons.Left:
                    inputs[0].u.mi.dwFlags = MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP;
                    break;
                case MouseButtons.Right:
                    inputs[0].u.mi.dwFlags = MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP;
                    break;
                case MouseButtons.Middle:
                    inputs[0].u.mi.dwFlags = MOUSEEVENTF_MIDDLEDOWN | MOUSEEVENTF_MIDDLEUP;
                    break;
                case MouseButtons.XButton1:
                    inputs[0].u.mi.dwFlags = MOUSEEVENTF_XDOWN | MOUSEEVENTF_XUP;
                    inputs[0].u.mi.mouseData = XBUTTON1;
                    break;
                case MouseButtons.XButton2:
                    inputs[0].u.mi.dwFlags = MOUSEEVENTF_XDOWN | MOUSEEVENTF_XUP;
                    inputs[0].u.mi.mouseData = XBUTTON2;
                    break;
                default:
                    inputs[0].u.mi.dwFlags = MOUSEEVENTF_MOVE;
                    break;
            }

            if (wheelDelta != 0)
            {
                inputs[0].u.mi.mouseData = (uint)wheelDelta;
                inputs[0].u.mi.dwFlags |= MOUSEEVENTF_WHEEL;
            }

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public static async Task SendKeyboardKeyAsync(KeyEventArgs keyArgs, KeyboardSendMode sendMode)
        {
            if(sendMode == KeyboardSendMode.UseInputSimulator)
            {
                _inputSimulator.Keyboard.KeyDown((VirtualKeyCode)keyArgs.KeyCode);
                await Task.Delay(MouseManager.GetRandomDelay());
                _inputSimulator.Keyboard.KeyUp((VirtualKeyCode)keyArgs.KeyCode);
                return;
            }

            INPUT[] inputs = new INPUT[2];

            if (sendMode == KeyboardSendMode.WindowsSendInputByKeyCode)
            {
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].u.ki.wVk = (ushort)keyArgs.KeyCode;
                inputs[0].u.ki.dwFlags = 0; // Key down

                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].u.ki.wVk = (ushort)keyArgs.KeyCode;
                inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP; // Key up

                SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
            else if (sendMode == KeyboardSendMode.WindowsSendInputByScanCode)
            {

                ushort scanCode = (ushort)MapVirtualKey((uint)keyArgs.KeyCode, 0);

                // Key down event
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].u.ki.wScan = scanCode;
                inputs[0].u.ki.dwFlags = KEYEVENTF_SCANCODE;

                // Key up event
                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].u.ki.wScan = scanCode;
                inputs[1].u.ki.dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP;

                SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
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