using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public readonly struct SP_DEVINFO_DATA
    {
        public UInt32 Size => System.Convert.ToUInt32(Marshal.SizeOf(this));
        public Guid ClassGuid { get; }
        public UInt32 DevInst { get; }
        public UIntPtr Reserved { get; }
    }
}