using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct STORAGE_DESCRIPTOR_HEADER
    {
        public uint Version { get; }

        public uint Size { get; }
    }
}
