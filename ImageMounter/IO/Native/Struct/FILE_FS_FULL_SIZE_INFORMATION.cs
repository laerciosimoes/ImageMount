using System.Globalization;
using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FILE_FS_FULL_SIZE_INFORMATION
    {
        public Int64 TotalAllocationUnits { get; }
        public Int64 CallerAvailableAllocationUnits { get; }
        public Int64 ActualAvailableAllocationUnits { get; }
        public UInt32 SectorsPerAllocationUnit { get; }
        public UInt32 BytesPerSector { get; }

        public Int64 TotalBytes
        {
            get
            {
                return TotalAllocationUnits * SectorsPerAllocationUnit * BytesPerSector;
            }
        }

        public UInt32 BytesPerAllocationUnit
        {
            get
            {
                return SectorsPerAllocationUnit * BytesPerSector;
            }
        }

        public override string ToString()
        {
            return TotalBytes.ToString(NumberFormatInfo.InvariantInfo);
        }
    }

}
