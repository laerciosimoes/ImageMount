using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUNTMGR_MOUNT_POINT
    {
        public int SymbolicLinkNameOffset { get; }
        public ushort SymbolicLinkNameLength { get; }
        public ushort Reserved1 { get; }
        public int UniqueIdOffset { get; }
        public ushort UniqueIdLength { get; }
        public ushort Reserved2 { get; }
        public int DeviceNameOffset { get; }
        public ushort DeviceNameLength { get; }
        public ushort Reserved3 { get; }

        public MOUNTMGR_MOUNT_POINT(string device_name)
        {
            DeviceNameOffset = Marshal.SizeOf(this);
            DeviceNameLength = System.Convert.ToUInt16(device_name.Length << 1);
        }
    }

}
