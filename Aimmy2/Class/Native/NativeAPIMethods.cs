using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Aimmy2.Extensions;

namespace Aimmy2.Class.Native
{
    internal static class NativeAPIMethods
    {
        static readonly Guid GraphicsCaptureItemGuid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] MINPUT[] pInputs, int cbSize);


        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        public static void MouseEvent(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo) => mouse_event(dwFlags, dx, dy, dwData, dwExtraInfo);

        #region P/Invoke signatures
        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr SetTimer(IntPtr hWnd, int nIDEvent, int uElapse, IntPtr lpTimerFunc);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool KillTimer(IntPtr hwnd, int idEvent);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, [In, Out] ref RECT rect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int flags);

        [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public extern static IntPtr CreateWindow(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, int crKey, byte bAlpha, LayeredWindowAttributeFlags dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPWStr)] string modName);


        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool InvalidateRect(IntPtr hWnd, IntPtr rect, [MarshalAs(UnmanagedType.Bool)] bool erase);

        [DllImport("Magnification.dll", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagInitialize();

        [DllImport("Magnification.dll", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagUninitialize();

        [DllImport("Magnification.dll", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagSetWindowSource(IntPtr hwnd, RECT rect);

        [DllImport("Magnification.dll", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagGetWindowSource(IntPtr hwnd, ref RECT pRect);

        [DllImport("Magnification.dll", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagSetWindowTransform(IntPtr hwnd, ref Transformation pTransform);

        [DllImport("Magnification.dll", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagGetWindowTransform(IntPtr hwnd, ref Transformation pTransform);

        [DllImport("Magnification.dll", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagSetWindowFilterList(IntPtr hwnd, int dwFilterMode, int count, IntPtr pHWND);

        [DllImport("Magnification.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int MagGetWindowFilterList(IntPtr hwnd, IntPtr pdwFilterMode, int count, IntPtr pHWND);

        [DllImport("Magnification.dll", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagSetColorEffect(IntPtr hwnd, ref ColorEffect pEffect);

        [DllImport("Magnification.dll", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagGetColorEffect(IntPtr hwnd, ref ColorEffect pEffect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, NativeMonitorInfo lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);


        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);


        [DllImport("user32.dll")]
        static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags flags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE dwAttribute, out bool pvAttribute, int cbAttribute);

        #endregion P/Invoke signatures


        #region Functions


        public static T HideForCapture<T>(this T window) where T : Window
        {
            HideForCapture(window.GetHandleSafe());
            return window;
        }

        public static void HideForCapture(IntPtr hwnd)
        {
            SetWindowDisplayAffinity(hwnd, (uint)WindowAffinity.WDA_EXCLUDEFROMCAPTURE);
        }

        public static T MakeClickThrough<T>(this T window) where T : Window
        {
            MakeClickThrough(window.GetHandleSafe());
            return window;
        }

        public static void MakeClickThrough(IntPtr hwnd)
        {
            SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x00000020);
        }

        public static System.Drawing.Point GetCursorPosition()
        {
            if (GetCursorPos(out POINT lpPoint))
            {
                return new System.Drawing.Point(lpPoint.X, lpPoint.Y);
            }
            else
            {
                // Handle the case when GetCursorPos fails
                throw new Exception("Failed to get cursor position.");
            }
        }

        public static RECT GetWindowRectangle(IntPtr hWnd)
        {
            RECT rect = new RECT();
            if (!GetWindowRect(hWnd, ref rect))
            {
                throw new Exception("Failed to get window rectangle.");
            }
            return rect;
        }

        #endregion Functions



        public static IEnumerable<Process> RecordableProcesses()
        {
            return from p in Process.GetProcesses()
                   where !string.IsNullOrWhiteSpace(p.MainWindowTitle) && IsWindowValidForCapture(p.MainWindowHandle)
                   select p;
        }

        public static bool IsWindowValidForCapture(IntPtr hwnd)
        {
            try
            {
                if (hwnd.ToInt32() == 0)
                {
                    return false;
                }

                if (hwnd == GetShellWindow())
                {
                    return false;
                }

                if (!IsWindowVisible(hwnd))
                {
                    return false;
                }

                if (GetAncestor(hwnd, GetAncestorFlags.GetRoot) != hwnd)
                {
                    return false;
                }

                var style = (WindowStyles)(uint)GetWindowLongPtr(hwnd, (int)GWL.STYLE).ToInt32();
                if (style.HasFlag(WindowStyles.WS_DISABLED))
                {
                    return false;
                }

                var cloaked = false;
                var hrTemp = DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.Cloaked, out cloaked, Marshal.SizeOf<bool>());
                if (hrTemp == 0 && cloaked)
                {
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }


        public static bool IsWindowTopMost(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, NativeStruct.GWL_EXSTYLE);
            return (exStyle & NativeStruct.WS_EX_TOPMOST) == NativeStruct.WS_EX_TOPMOST;
        }

        public static Window SetTopMost(this Window window, bool isTopMost = true)
        {
            if(!window.Dispatcher.CheckAccess())
            {
                return window.Dispatcher.Invoke(() => SetTopMost(window, isTopMost));
            }
            SetTopMost(new WindowInteropHelper(window).Handle, isTopMost);
            return window;
        }

        public static void SetTopMost(IntPtr handle, bool isTopMost = true)
        {
            SetWindowPos(handle, isTopMost ? NativeStruct.HWND_TOPMOST : NativeStruct.HWND_NOTOPMOST, 0, 0, 0, 0,
                (int)(SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_SHOWWINDOW));
        }

        public static void RemoveTopMost(IntPtr handle)
        {
            SetTopMost(handle, false);
        }

        public static IntPtr GetWindowInsertAfter(IntPtr hWnd)
        {
            return GetWindow(hWnd, NativeStruct.GW_HWNDPREV);
        }

    }
}