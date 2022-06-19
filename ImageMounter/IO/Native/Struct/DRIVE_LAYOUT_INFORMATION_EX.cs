using System.Runtime.InteropServices;
using ImageMounter.IO.Native.Enum;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DRIVE_LAYOUT_INFORMATION_EX
    {
        public DRIVE_LAYOUT_INFORMATION_EX(PARTITION_STYLE PartitionStyle, int PartitionCount)
        {
            PartitionStyle = PartitionStyle;
            PartitionCount = PartitionCount;
        }

        public PARTITION_STYLE PartitionStyle { get; }
        public int PartitionCount { get; }
    }
}
