using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public UInt32 Size { get; }

        public string DevicePath
        {
            get
            {
                return _devicePath;
            }
        }

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32768)]
        private readonly string _devicePath;

        public void Initialize()
        {
            Size = System.Convert.ToUInt32(Marshal.SizeOf(this));
        }
    }

}
