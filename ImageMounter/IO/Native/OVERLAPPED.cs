using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ImageMounter.IO.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public class OVERLAPPED
    {
        public UIntPtr Status { get; }
        public UIntPtr BytesTransferred { get; }
        public long StartOffset { get; set; }
        public SafeWaitHandle EventHandle { get; set; }
    }
}
