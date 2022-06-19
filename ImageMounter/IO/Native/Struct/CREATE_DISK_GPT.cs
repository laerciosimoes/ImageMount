using System.Runtime.InteropServices;
using ImageMounter.IO.Native.Enum;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CREATE_DISK_GPT
    {
        [MarshalAs(UnmanagedType.I1)]
        private PARTITION_STYLE _partitionStyle;

        public Guid DiskId { get; set; }

        public int MaxPartitionCount { get; set; }

        public PARTITION_STYLE PartitionStyle
        {
            get
            {
                return _partitionStyle;
            }
            set
            {
                _partitionStyle = value;
            }
        }
    }

}
