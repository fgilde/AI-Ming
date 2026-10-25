using System.Windows;
using Nextended.UI.Native;
using Nextended.UI.WPF.Extensions;

namespace PowerAim.Class.Native
{
    /// <summary>
    ///     Applies the "hide UI from capture" setting (<c>ToggleState.HideUIFromCapture</c>) to app windows.
    ///     The raw capture-exclusion calls live in <see cref="NativeMethods"/>.
    /// </summary>
    internal static class CaptureExclusion
    {
        private static bool Enabled => PowerAim.Config.AppConfig.Current?.ToggleState?.HideUIFromCapture ?? true;

        public static T HideForCaptureIfEnabled<T>(this T window) where T : Window
        {
            if (Enabled)
                window.HideForCapture();
            return window;
        }

        public static void ApplyCaptureExclusionToAllWindows()
        {
            if (Application.Current == null) return;
            var exclude = Enabled;
            foreach (Window w in Application.Current.Windows)
            {
                var hwnd = w.GetHandle();
                if (hwnd != IntPtr.Zero)
                    NativeMethods.SetCaptureExclusion(hwnd, exclude);
            }
        }
    }
}
