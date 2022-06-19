using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PARTITION_INFORMATION_MBR
    {
        public byte PartitionType { get; }

        [MarshalAs(UnmanagedType.I1)]
        private readonly bool _bootIndicator;

        [MarshalAs(UnmanagedType.I1)]
        private readonly bool _recognizedPartition;

        public int HiddenSectors { get; }

        public bool RecognizedPartition
        {
            get
            {
                return _recognizedPartition;
            }
        }

        public bool BootIndicator
        {
            get
            {
                return _bootIndicator;
            }
        }
    }

}
