using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct IoStatusBlock
    {
        public IntPtr Status { get; }

        public IntPtr Information { get; }
    }
}
