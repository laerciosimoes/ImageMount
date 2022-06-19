using System.Runtime.InteropServices;
using ImageMounter.Interop;
using ImageMounter.IO.Native.Enum;

namespace ImageMounter.IO.Native.Struct
{

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct PARTITION_INFORMATION_EX
    {
        [MarshalAs(UnmanagedType.I1)]
        private readonly PARTITION_STYLE _partitionStyle;
        public Int64 StartingOffset { get; }
        public Int64 PartitionLength { get; }
        public UInt32 PartitionNumber { get; }
        [MarshalAs(UnmanagedType.I1)]
        private readonly bool _rewritePartition;

        private readonly byte padding1;
        private readonly byte padding2;
        private readonly byte padding3;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 112)]
        private byte[] fields;

        public PARTITION_INFORMATION_MBR MBR
        {
            get
            {
                return PinnedBuffer.Deserialize<PARTITION_INFORMATION_MBR>(fields);
            }
            set
            {
                using (PinnedBuffer<byte> buffer = new PinnedBuffer<byte>(112))
                {
                    buffer.Write(0, Value);
                    fields = buffer.Target;
                }
            }
        }

        public PARTITION_INFORMATION_GPT GPT
        {
            get
            {
                return PinnedBuffer.Deserialize<PARTITION_INFORMATION_GPT>(fields);
            }
            set
            {
                using (PinnedBuffer<byte> buffer = new PinnedBuffer<byte>(112))
                {
                    buffer.Write(0, Value);
                    fields = buffer.Target;
                }
            }
        }

        public bool RewritePartition
        {
            get
            {
                return _rewritePartition;
            }
        }

        public PARTITION_STYLE PartitionStyle
        {
            get
            {
                return _partitionStyle;
            }
        }
    }



}
