using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SMALL_RECT
    {
        public short Left { get; }
        public short Top { get; }
        public short Right { get; }
        public short Bottom { get; }
        public short Width
        {
            get
            {
                return Right - Left + 1;
            }
        }
        public short Height
        {
            get
            {
                return Bottom - Top + 1;
            }
        }
    }



}
