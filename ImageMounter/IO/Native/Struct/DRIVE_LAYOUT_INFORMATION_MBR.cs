using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DRIVE_LAYOUT_INFORMATION_MBR
    {
        public DRIVE_LAYOUT_INFORMATION_MBR(uint DiskSignature)
        {
            DiskSignature = DiskSignature;
        }

        public uint DiskSignature { get; }
        public uint Checksum { get; }

        public override int GetHashCode()
        {
            return DiskSignature.GetHashCode();
        }

        public override string ToString()
        {
            return DiskSignature.ToString("X8");
        }
    }

}
