using System.Runtime.InteropServices;
using ImageMounter.IO.Native.Enum;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct STORAGE_DEVICE_NUMBER
    {
        public DeviceType DeviceType { get; }

        public UInt32 DeviceNumber { get; }

        public Int32 PartitionNumber { get; }
    }
}
