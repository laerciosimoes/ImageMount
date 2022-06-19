using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{

    [StructLayout(LayoutKind.Sequential)]
    public struct COMMTIMEOUTS
    {
        public UInt32 ReadIntervalTimeout { get; set; }
        public UInt32 ReadTotalTimeoutMultiplier { get; set; }
        public UInt32 ReadTotalTimeoutConstant { get; set; }
        public UInt32 WriteTotalTimeoutMultiplier { get; set; }
        public UInt32 WriteTotalTimeoutConstant { get; set; }
    }

}
