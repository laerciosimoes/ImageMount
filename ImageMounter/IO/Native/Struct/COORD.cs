using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct COORD
    {
        public short X { get; }
        public short Y { get; }
    }
}
