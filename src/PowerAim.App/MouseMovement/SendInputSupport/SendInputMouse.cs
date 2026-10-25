using System;
using System.Runtime.InteropServices;

namespace PowerAim.MouseMovement.SendInputSupport;

/// <summary>
/// Relative mouse injection via Win32 SendInput.
/// Explicit x64 INPUT layout (type @0, union @8) and SetLastError.
/// </summary>
internal static class SendInputMouse
{
    private const int INPUT_MOUSE = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    // Explicit layout matches official x64 INPUT: type at 0, union at 8 → sizeof = 40.
    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(8)] public MOUSEINPUT mi;
    }

    private static readonly int InputSize = Marshal.SizeOf<INPUT>();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>Emit a relative mouse packet. Flags may include button / wheel bits.</summary>
    public static void SendMouseCommand(uint mouseCommand, int x = 0, int y = 0)
    {
        uint flags = mouseCommand;

        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                dx = x,
                dy = y,
                mouseData = 0,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = UIntPtr.Zero
            }
        };

        uint sent = SendInput(1, new[] { input }, InputSize);
        // Silent on failure — high-frequency path; logging every miss would flood.
        // Caller can still observe via GetLastError if needed.
        _ = sent;
    }
}