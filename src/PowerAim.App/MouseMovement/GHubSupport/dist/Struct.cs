using System.Runtime.InteropServices;

namespace PowerAim.MouseMovement.GHubSupport.dist;

internal class Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSE_IO
    {
        public byte Button;
        public byte X;
        public byte Y;
        public byte Wheel;
        public byte Unk1;
    }
}