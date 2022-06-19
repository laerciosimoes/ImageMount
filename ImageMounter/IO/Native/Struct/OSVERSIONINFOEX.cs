using System.Runtime.InteropServices;
using ImageMounter.Interop;

namespace ImageMounter.IO.Native.Struct
{

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct OSVERSIONINFOEX
    {
        public Int32 OSVersionInfoSize { get; }
        public Int32 MajorVersion { get; }
        public Int32 MinorVersion { get; }
        public Int32 BuildNumber { get; }
        public PlatformID PlatformId { get; }

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        private readonly string _cSDVersion;

        public ushort ServicePackMajor { get; }

        public ushort ServicePackMinor { get; }

        public short SuiteMask { get; }

        public byte ProductType { get; }

        public byte Reserved { get; }

        public string CSDVersion
        {
            get
            {
                return _cSDVersion;
            }
        }

        public static OSVERSIONINFOEX Initalize()
        {
            return new OSVERSIONINFOEX()
            {
                OSVersionInfoSize = PinnedBuffer<OSVERSIONINFOEX>.TypeSize
            };
        }
    }

}
