using System.Drawing;
using Aimmy2.Class;
using Aimmy2.Config;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using MouseMovementLibraries.SendInputSupport;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Input;
using Aimmy2.Class.Native;
using Application = System.Windows.Application;
using Point = System.Drawing.Point;
using Aimmy2.InputLogic;

namespace InputLogic
{
    internal class MouseManager
    {

        private static DateTime LastClickTime = DateTime.MinValue;
        private static int LastAntiRecoilClickTime = 0;
        private static Random _random = new Random();
        private const int WHEEL_DELTA = 120; 
        private static double previousX = 0;
        private static double previousY = 0;
        public static double smoothingFactor => AppConfig.Current.SliderSettings.EMASmoothening;
        public static bool IsEMASmoothingEnabled => AppConfig.Current.ToggleState.EMASmoothening;


        private static Random MouseRandom = new();

        private static Point CubicBezier(Point start, Point end, Point control1, Point control2, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;

            double x = uu * u * start.X + 3 * uu * t * control1.X + 3 * u * tt * control2.X + tt * t * end.X;
            double y = uu * u * start.Y + 3 * uu * t * control1.Y + 3 * u * tt * control2.Y + tt * t * end.Y;

            if (IsEMASmoothingEnabled)
            {
                x = EmaSmoothing(previousX, x, smoothingFactor);
                y = EmaSmoothing(previousY, y, smoothingFactor);
            }

            return new Point((int)x, (int)y);
        }

        private static double EmaSmoothing(double previousValue, double currentValue, double smoothingFactor) => (currentValue * smoothingFactor) + (previousValue * (1 - smoothingFactor));
        public static bool IsLeftDown
        {
            get
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    return _leftDown || Mouse.LeftButton == MouseButtonState.Pressed;
                }

                return _leftDown || Application.Current.Dispatcher.Invoke(() => Mouse.LeftButton == MouseButtonState.Pressed);
            }
        }
        private static bool _leftDown;
        public static void ScrollMouseWheel(int delta)
        {
            NativeAPIMethods.MouseEvent((uint)InputEventFlags.MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);
        }

        //public static void SendMouseEvent(MouseEventArgs args)
        //{
        //    SendMouseEvent(args.LeftButton, args.X, args.Y, args.Delta);
        //}

        public static void SendMouseEvent(System.Windows.Forms.MouseEventArgs args, KeyPressState state = KeyPressState.DownAndUp)
        {
            SendMouseEvent(args.Button, args.X, args.Y, args.Delta, state);
        }

        public static void SendMouseEvent(MouseButtons button, int x = 0, int y = 0, int wheelDelta = 0, KeyPressState state = KeyPressState.DownAndUp)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = (uint)InputEventFlags.INPUT_MOUSE;
            inputs[0].u.mi.dx = x;
            inputs[0].u.mi.dy = y;
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.dwFlags = 0;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = IntPtr.Zero;

            switch (button)
            {
                case MouseButtons.Left: ;
                    inputs[0].u.mi.dwFlags = state switch
                    {
                        KeyPressState.Down => (uint)InputEventFlags.MOUSEEVENTF_LEFTDOWN,
                        KeyPressState.Up => (uint)InputEventFlags.MOUSEEVENTF_LEFTUP,
                        _ => (uint)InputEventFlags.MOUSEEVENTF_LEFTDOWN | (uint)InputEventFlags.MOUSEEVENTF_LEFTUP
                    };
                    break;
                case MouseButtons.Right:
                    inputs[0].u.mi.dwFlags = state switch
                    {
                        KeyPressState.Down => (uint)InputEventFlags.MOUSEEVENTF_RIGHTDOWN,
                        KeyPressState.Up => (uint)InputEventFlags.MOUSEEVENTF_RIGHTUP,
                        _ => (uint)InputEventFlags.MOUSEEVENTF_RIGHTDOWN | (uint)InputEventFlags.MOUSEEVENTF_RIGHTUP
                    };
                    break;
                case MouseButtons.Middle:
                    inputs[0].u.mi.dwFlags = state switch
                    {
                        KeyPressState.Down => (uint)InputEventFlags.MOUSEEVENTF_MIDDLEDOWN,
                        KeyPressState.Up => (uint)InputEventFlags.MOUSEEVENTF_MIDDLEUP,
                        _ => (uint)InputEventFlags.MOUSEEVENTF_MIDDLEDOWN | (uint)InputEventFlags.MOUSEEVENTF_MIDDLEUP
                    };
                    break;
                case MouseButtons.XButton1:
                    inputs[0].u.mi.dwFlags = state switch
                    {
                        KeyPressState.Down => (uint)InputEventFlags.MOUSEEVENTF_XDOWN,
                        KeyPressState.Up => (uint)InputEventFlags.MOUSEEVENTF_XUP,
                        _ => (uint)InputEventFlags.MOUSEEVENTF_XDOWN | (uint)InputEventFlags.MOUSEEVENTF_XUP
                    };
                    inputs[0].u.mi.mouseData = (uint)InputEventFlags.XBUTTON1;
                    break;
                case MouseButtons.XButton2:
                    inputs[0].u.mi.dwFlags = state switch
                    {
                        KeyPressState.Down => (uint)InputEventFlags.MOUSEEVENTF_XDOWN,
                        KeyPressState.Up => (uint)InputEventFlags.MOUSEEVENTF_XUP,
                        _ => (uint)InputEventFlags.MOUSEEVENTF_XDOWN | (uint)InputEventFlags.MOUSEEVENTF_XUP
                    };
                    inputs[0].u.mi.mouseData = (uint)InputEventFlags.XBUTTON2;
                    break;
                default:
                    inputs[0].u.mi.dwFlags = (uint)InputEventFlags.MOUSEEVENTF_MOVE;
                    break;
            }

            if (wheelDelta != 0)
            {
                inputs[0].u.mi.mouseData = (uint)wheelDelta;
                inputs[0].u.mi.dwFlags |= (uint)InputEventFlags.MOUSEEVENTF_WHEEL;
            }

            NativeAPIMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static async Task ScrollMouseWheelUpAndDown()
        {
            ScrollMouseWheel(WHEEL_DELTA); // Scroll up
            await Task.Delay(20); // Optional: Ein kleiner Delay zwischen den Scrolls
            ScrollMouseWheel(-WHEEL_DELTA); // Scroll down
        }

        public static void LeftDown()
        {
            if (IsLeftDown)
                return;
            
            switch (AppConfig.Current.DropdownState.MouseMovementMethod)
            {
                case MouseMovementMethod.SendInput:
                    SendInputMouse.SendMouseCommand((uint)InputEventFlags.MOUSEEVENTF_LEFTDOWN);
                    return;

                case MouseMovementMethod.LGHUB:
                    LGMouse.Move(1, 0, 0, 0);
                    return;

                case MouseMovementMethod.RazerSynapse:
                    RZMouse.mouse_click(1);
                    return;

                case MouseMovementMethod.ddxoft:
                    DdxoftMain.ddxoftInstance.btn!(1);
                    return;

                default:
                    NativeAPIMethods.MouseEvent((uint)InputEventFlags.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    break;
            }

        }

        public static async Task LeftDownUntil(Func<Task<bool>> condition, TimeSpan? delay = null, CancellationToken cancellationToken = default)
        {
            LeftDown();

            try
            {
                while (!await condition() && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(5, cancellationToken);
                }

                if(delay.HasValue)
                    await Task.Delay(delay.Value, cancellationToken);
            }
            catch 
            {}

            LeftUp();
            _leftDown = true;
            Task.Delay(1000).ContinueWith(_ =>
            {
                NativeAPIMethods.MouseEvent((uint)InputEventFlags.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                _leftDown = false;
            });
            LastClickTime = DateTime.UtcNow;
        }

        public static void LeftUp()
        {
            switch (AppConfig.Current.DropdownState.MouseMovementMethod)
            {
                case MouseMovementMethod.SendInput:
                    SendInputMouse.SendMouseCommand((uint)InputEventFlags.MOUSEEVENTF_LEFTUP);
                    return;

                case MouseMovementMethod.LGHUB:
                    LGMouse.Move(0, 0, 0, 0);
                    return;

                case MouseMovementMethod.RazerSynapse:
                    RZMouse.mouse_click(0);
                    return;

                case MouseMovementMethod.ddxoft:
                    DdxoftMain.ddxoftInstance.btn(2);
                    return;

                default:
                    NativeAPIMethods.MouseEvent((uint)InputEventFlags.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    break;
            }

            _leftDown = false;
        }

        public static int GetRandomDelay()
        {
            var ts = TimeSpan.FromSeconds(AppConfig.Current.SliderSettings.FirePressDelay);
            var ms = ts.TotalMilliseconds;
            int randomMs = _random.Next(0, (int)ms);
            return randomMs;
        }

        public static async Task DoTriggerClick()
        {
            var randomMs = Math.Max(_random.Next(45, 55), GetRandomDelay());
            LeftDown();
            await Task.Delay(randomMs);
            //Console.WriteLine("Fire waited: "+randomMs);
            LeftUp();

            LastClickTime = DateTime.UtcNow;
        }

        public static void DoAntiRecoil()
        {
            int timeSinceLastClick = Math.Abs(DateTime.UtcNow.Millisecond - LastAntiRecoilClickTime);

            if (timeSinceLastClick < AppConfig.Current.AntiRecoilSettings.FireRate)
            {
                return;
            }

            int xRecoil = (int)AppConfig.Current.AntiRecoilSettings.XRecoil;
            int yRecoil = (int)AppConfig.Current.AntiRecoilSettings.YRecoil;

            Move(xRecoil, yRecoil);

            LastAntiRecoilClickTime = DateTime.UtcNow.Millisecond;
        }

        public static void Move(int x, int y)
        {
            switch (AppConfig.Current.DropdownState.MouseMovementMethod)
            {
                case MouseMovementMethod.SendInput:
                    SendInputMouse.SendMouseCommand((uint)InputEventFlags.MOUSEEVENTF_MOVE, x, y);
                    break;

                case MouseMovementMethod.LGHUB:
                    LGMouse.Move(0, x, y, 0);
                    break;

                case MouseMovementMethod.RazerSynapse:
                    RZMouse.mouse_move(x, y, true);
                    break;

                case MouseMovementMethod.ddxoft:
                    DdxoftMain.ddxoftInstance.movR!(x, y);
                    break;

                default:
                    NativeAPIMethods.MouseEvent((uint)InputEventFlags.MOUSEEVENTF_MOVE, (uint)x, (uint)y, 0, 0);
                    break;
            }
        }

        public static void MoveCrosshair(int detectedX, int detectedY, Rectangle area)
        {
            int halfScreenWidth = area.Width / 2;
            int halfScreenHeight = area.Height / 2;

            int targetX = detectedX - halfScreenWidth;
            int targetY = detectedY - halfScreenHeight;

            double aspectRatioCorrection = area.Width / area.Height;

            int MouseJitter = (int)AppConfig.Current.SliderSettings.MouseJitter;
            int jitterX = MouseRandom.Next(-MouseJitter, MouseJitter);
            int jitterY = MouseRandom.Next(-MouseJitter, MouseJitter);

            Point start = new(0, 0);
            Point end = new(targetX, targetY);
            Point control1 = new(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3);
            Point control2 = new(start.X + 2 * (end.X - start.X) / 3, start.Y + 2 * (end.Y - start.Y) / 3);
            Point newPosition = CubicBezier(start, end, control1, control2, 1 - AppConfig.Current.SliderSettings.MouseSensitivity);

            targetX = Math.Clamp(targetX, -150, 150);
            targetY = Math.Clamp(targetY, -150, 150);

            targetY = (int)(targetY * aspectRatioCorrection);

            targetX += jitterX;
            targetY += jitterY;

            switch (AppConfig.Current.DropdownState.MouseMovementMethod)
            {
                case MouseMovementMethod.SendInput:
                    SendInputMouse.SendMouseCommand((uint)InputEventFlags.MOUSEEVENTF_MOVE, newPosition.X, newPosition.Y);
                    break;

                case MouseMovementMethod.LGHUB:
                    LGMouse.Move(0, newPosition.X, newPosition.Y, 0);
                    break;

                case MouseMovementMethod.RazerSynapse:
                    RZMouse.mouse_move(newPosition.X, newPosition.Y, true);
                    break;

                case MouseMovementMethod.ddxoft:
                    DdxoftMain.ddxoftInstance.movR!(newPosition.X, newPosition.Y);
                    break;

                default:
                    NativeAPIMethods.MouseEvent((uint)InputEventFlags.MOUSEEVENTF_MOVE, (uint)newPosition.X, (uint)newPosition.Y, 0, 0);
                    break;
            }

        }
    }
}
