using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ByHandleFileInformation
    {
        public FileAttributes FileAttributes { get; }
        private readonly long ftCreationTime;
        private readonly long ftLastAccessTime;
        private readonly long ftLastWriteTime;
        public uint VolumeSerialNumber { get; }
        private readonly int nFileSizeHigh;
        private readonly uint nFileSizeLow;
        public int NumberOfLinks { get; }
        private readonly uint nFileIndexHigh;
        private readonly uint nFileIndexLow;

        public DateTime CreationTime => DateTime.FromFileTime(ftCreationTime);

        public DateTime LastAccessTime => DateTime.FromFileTime(ftLastAccessTime);

        public DateTime LastWriteTime => DateTime.FromFileTime(ftLastWriteTime);

        public long FileSize => (System.Convert.ToInt64(nFileSizeHigh) << 32) | nFileSizeLow;

        public ulong FileIndexAndSequence => (System.Convert.ToUInt64(nFileIndexHigh) << 32) | nFileIndexLow;

        public long FileIndex => ((nFileIndexHigh & 0xFFFFL) << 32) | nFileIndexLow;

        public ushort Sequence => System.Convert.ToUInt16(nFileIndexHigh >> 16);

        public static ByHandleFileInformation FromHandle(SafeFileHandle handle)
        {
            ByHandleFileInformation obj = new ByHandleFileInformation();

            Win32Try(UnsafeNativeMethods.GetFileInformationByHandle(handle, obj));

            return obj;
        }
    }

}
