using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD Size { get; }
        public COORD CursorPosition { get; }
        public short Attributes { get; }
        public SMALL_RECT Window { get; }
        public COORD MaximumWindowSize { get; }
    }

}
