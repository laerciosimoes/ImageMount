using System.Runtime.InteropServices;
using ImageMounter.Interop;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct OSVERSIONINFO
    {
        public Int32 OSVersionInfoSize { get; }
        public Int32 MajorVersion { get; }
        public Int32 MinorVersion { get; }
        public Int32 BuildNumber { get; }
        public PlatformID PlatformId { get; }

        public string CSDVersion
        {
            get
            {
                return _cSDVersion;
            }
        }

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        private readonly string _cSDVersion;

        public static OSVERSIONINFO Initalize()
        {
            return new OSVERSIONINFO()
            {
                OSVersionInfoSize = PinnedBuffer<OSVERSIONINFO>.TypeSize
            };
        }
    }

}
