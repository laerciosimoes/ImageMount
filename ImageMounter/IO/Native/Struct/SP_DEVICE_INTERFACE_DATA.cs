using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA
    {
        public UInt32 Size { get; }
        public Guid InterfaceClassGuid { get; }
        public UInt32 Flags { get; }
        public IntPtr Reserved { get; }

        public void Initialize()
        {
            Size = System.Convert.ToUInt32(Marshal.SizeOf(this));
        }
    }
}
