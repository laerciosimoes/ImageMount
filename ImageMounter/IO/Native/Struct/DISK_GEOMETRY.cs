using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    struct DISK_GEOMETRY
    {
        public enum MEDIA_TYPE : Int32
        {
            Unknown = 0x0,
            F5_1Pt2_512 = 0x1,
            F3_1Pt44_512 = 0x2,
            F3_2Pt88_512 = 0x3,
            F3_20Pt8_512 = 0x4,
            F3_720_512 = 0x5,
            F5_360_512 = 0x6,
            F5_320_512 = 0x7,
            F5_320_1024 = 0x8,
            F5_180_512 = 0x9,
            F5_160_512 = 0xA,
            RemovableMedia = 0xB,
            FixedMedia = &H,
            F3_120M_512 = 0xD,
            F3_640_512 = 0xE,
            F5_640_512 = 0xF,
            F5_720_512 = 0x10,
            F3_1Pt2_512 = 0x11,
            F3_1Pt23_1024 = 0x12,
            F5_1Pt23_1024 = 0x13,
            F3_128Mb_512 = 0x14,
            F3_230Mb_512 = 0x15,
            F8_256_128 = 0x16,
            F3_200Mb_512 = 0x17,
            F3_240M_512 = 0x18,
            F3_32M_512 = 0x19
        }

        public Int64 Cylinders { get; }
        public MEDIA_TYPE MediaType { get; }
        public Int32 TracksPerCylinder { get; }
        public Int32 SectorsPerTrack { get; }
        public Int32 BytesPerSector { get; }
    }

}
