using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{

    [StructLayout(LayoutKind.Sequential)]
    public struct DEVICE_TRIM_DESCRIPTOR
    {
        public STORAGE_DESCRIPTOR_HEADER Header { get; }

        public byte TrimEnabled { get; }
    }
}
