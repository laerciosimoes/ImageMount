using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DISK_GROW_PARTITION
    {
        public Int32 PartitionNumber { get; set; }
        public Int64 BytesToGrow { get; set; }
    }
}
