using System.Runtime.InteropServices;
using ImageMounter.IO.Native.Enum;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct PARTITION_INFORMATION
    {
        

        public Int64 StartingOffset { get; }
        public Int64 PartitionLength { get; }
        public UInt32 HiddenSectors { get; }
        public UInt32 PartitionNumber { get; }
        public PARTITION_TYPE PartitionType { get; }
        public byte BootIndicator { get; }
        public byte RecognizedPartition { get; }
        public byte RewritePartition { get; }

        /// <summary>
        /// Indicates whether this partition entry represents a Windows NT fault tolerant partition,
        /// such as mirror or stripe set.
        /// </summary>
        /// <value>
        /// Indicates whether this partition entry represents a Windows NT fault tolerant partition,
        /// such as mirror or stripe set.
        /// </value>
        /// <returns>True if this partition entry represents a Windows NT fault tolerant partition,
        /// such as mirror or stripe set. False otherwise.</returns>
        public bool IsFTPartition
        {
            get
            {
                return PartitionType.HasFlag(PARTITION_TYPE.PARTITION_NTFT);
            }
        }

        /// <summary>
        /// If this partition entry represents a Windows NT fault tolerant partition, such as mirror or stripe,
        /// set, then this property returns partition subtype, such as PARTITION_IFS for NTFS or HPFS
        /// partitions.
        /// </summary>
        /// <value>
        /// If this partition entry represents a Windows NT fault tolerant partition, such as mirror or stripe,
        /// set, then this property returns partition subtype, such as PARTITION_IFS for NTFS or HPFS
        /// partitions.
        /// </value>
        /// <returns>If this partition entry represents a Windows NT fault tolerant partition, such as mirror or
        /// stripe, set, then this property returns partition subtype, such as PARTITION_IFS for NTFS or HPFS
        /// partitions.</returns>
        public PARTITION_TYPE FTPartitionSubType
        {
            get
            {
                return PartitionType & !PARTITION_TYPE.PARTITION_NTFT;
            }
        }

        /// <summary>
        /// Indicates whether this partition entry represents a container partition, also known as extended
        /// partition, where an extended partition table can be found in first sector.
        /// </summary>
        /// <value>
        /// Indicates whether this partition entry represents a container partition.
        /// </value>
        /// <returns>True if this partition entry represents a container partition. False otherwise.</returns>
        public bool IsContainerPartition
        {
            get
            {
                return (PartitionType == PARTITION_TYPE.PARTITION_EXTENDED) || (PartitionType == PARTITION_TYPE.PARTITION_XINT13_EXTENDED);
            }
        }
    }

}
