using System;
using System.Runtime.InteropServices;
using Nextended.UI.Native;

namespace PowerAim.MouseMovement.SendInputSupport;

/// <summary>
/// Relative mouse injection via Win32 SendInput.
/// Fixed: correct x64 INPUT layout (type @0, union @8), SetLastError,
/// MOUSEEVENTF_MOVE_NOCOALESCE to stop coalescing under high fire-rate,
/// and GetMessageExtraInfo so injected packets stay distinguishable.
/// </summary>
internal static class SendInputMouse
{
    private const int INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_MOVE_NOCOALESCE = 0x2000;

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

    [DllImport("user32.dll")]
    private static extern UIntPtr GetMessageExtraInfo();

    /// <summary>
    /// Emit a relative mouse packet. Flags may include button / wheel bits;
    /// when only motion is requested, MOUSEEVENTF_MOVE_NOCOALESCE is OR'd in
    /// so high-rate aim frames do not get merged by the OS.
    /// </summary>
    public static void SendMouseCommand(uint mouseCommand, int x = 0, int y = 0)
    {
        uint flags = mouseCommand;
        // Relative motion: prevent coalescing of rapid small deltas (common freeze/jitter cause).
        if ((flags & MOUSEEVENTF_MOVE) != 0)
            flags |= MOUSEEVENTF_MOVE_NOCOALESCE;

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
                dwExtraInfo = GetMessageExtraInfo()
            }
        };

        uint sent = SendInput(1, new[] { input }, InputSize);
        // Silent on failure — high-frequency path; logging every miss would flood.
        // Caller can still observe via GetLastError if needed.
        _ = sent;
    }
}