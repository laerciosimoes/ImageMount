using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DRIVE_LAYOUT_INFORMATION_GPT
    {
        public Guid DiskId { get; }
        public long StartingUsableOffset { get; }
        public long UsableLength { get; }
        public int MaxPartitionCount { get; }

        public override int GetHashCode()
        {
            return DiskId.GetHashCode();
        }

        public override string ToString()
        {
            return DiskId.ToString("b");
        }
    }

}
