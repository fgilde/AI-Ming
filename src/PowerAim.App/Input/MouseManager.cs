using System.Drawing;
using PowerAim.Class;
using PowerAim.Config;
using PowerAim.MouseMovement.GHubSupport;
using PowerAim.MouseMovement.ddxoftSupport;
using PowerAim.MouseMovement.RazerSupport;
using PowerAim.MouseMovement.SendInputSupport;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Input;
using PowerAim.Class.Native;
using Application = System.Windows.Application;
using Point = System.Drawing.Point;
using PowerAim.Input;
using Nextended.UI.Native;

namespace PowerAim.Input
{
    internal class MouseManager
    {

        private static DateTime LastClickTime = DateTime.MinValue;
        private static readonly Random _random = new();
        private const int WHEEL_DELTA = 120;
        private static double previousX = 0;
        private static double previousY = 0;
        public static double smoothingFactor => AppConfig.Current.SliderSettings.EMASmoothening;
        public static bool IsEMASmoothingEnabled => AppConfig.Current.ToggleState.EMASmoothening;


        private static readonly Random MouseRandom = new();

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
            NativeMethods.MouseEvent((uint)InputEventFlags.MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);
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
                case MouseButtons.Left:
                    ;
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

            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
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
                    NativeMethods.MouseEvent((uint)InputEventFlags.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
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

                if (delay.HasValue)
                    await Task.Delay(delay.Value, cancellationToken);
            }
            catch
            { }

            LeftUp();
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
                    NativeMethods.MouseEvent((uint)InputEventFlags.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
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
                    NativeMethods.MouseEvent((uint)InputEventFlags.MOUSEEVENTF_MOVE, (uint)x, (uint)y, 0, 0);
                    break;
            }
        }

        /// <summary>
        /// Move relative to FOV centre toward the detection.
        /// Direct / robotic mode: high MouseSensitivity + Lerp path + optional zero jitter
        /// produces a single straight delta with minimal lag.
        /// </summary>
        public static void MoveCrosshair(int detectedX, int detectedY, Rectangle area)
        {
            int halfScreenWidth = area.Width / 2;
            int halfScreenHeight = area.Height / 2;

            double targetX = detectedX - halfScreenWidth;
            double targetY = detectedY - halfScreenHeight;

            double aspectRatioCorrection = area.Height > 0 ? (double)area.Width / area.Height : 1.0;
            targetY *= aspectRatioCorrection;

            // Wider clamp so long flicks aren't truncated mid-snap (was ±150).
            const int MaxDelta = 400;
            targetX = Math.Clamp(targetX, -MaxDelta, MaxDelta);
            targetY = Math.Clamp(targetY, -MaxDelta, MaxDelta);

            int mouseJitter = (int)AppConfig.Current.SliderSettings.MouseJitter;
            if (mouseJitter > 0)
            {
                targetX += MouseRandom.Next(-mouseJitter, mouseJitter + 1);
                targetY += MouseRandom.Next(-mouseJitter, mouseJitter + 1);
            }

            // MouseSensitivity: 0 = no move, 1 = full delta this frame (snappy / robotic).
            // UI stores 0..1; invert-style path factor removed for direct control.
            double sens = Math.Clamp(AppConfig.Current.SliderSettings.MouseSensitivity, 0.0, 1.0);

            // Path type still applied, but default Lerp + high sens ≈ pure linear snap.
            Point start = new(0, 0);
            Point end = new((int)Math.Round(targetX), (int)Math.Round(targetY));

            // t = sens: 1.0 lands on target in one step; lower values under-shoot (humanized).
            double t = sens;
            Point newPosition = AppConfig.Current.DropdownState.MovementPathType switch
            {
                MovementPathType.Lerp => MovementPaths.Robotic(start, end, t),
                MovementPathType.Exponential => MovementPaths.Exponential(start, end, t),
                MovementPathType.Adaptive => MovementPaths.Adaptive(start, end, t),
                MovementPathType.PerlinNoise => MovementPaths.PerlinNoise(start, end, t),
                _ /* Bezier */               => CubicBezier(
                    start, end,
                    new Point(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3),
                    new Point(start.X + 2 * (end.X - start.X) / 3, start.Y + 2 * (end.Y - start.Y) / 3),
                    t)
            };

            int dx = newPosition.X;
            int dy = newPosition.Y;

            // Optional EMA on the *output delta* only when toggle is on (off = pure robotic).
            if (IsEMASmoothingEnabled)
            {
                dx = (int)Math.Round(EmaSmoothing(previousX, dx, smoothingFactor));
                dy = (int)Math.Round(EmaSmoothing(previousY, dy, smoothingFactor));
                previousX = dx;
                previousY = dy;
            }
            else
            {
                previousX = dx;
                previousY = dy;
            }

            if (dx == 0 && dy == 0) return;
            Move(dx, dy);
        }
    }
}