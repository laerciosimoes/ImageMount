using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    /// <summary>
    /// SRB_IO_CONTROL header, as defined in NTDDDISK.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    [ComVisible(false)]
    public struct SRB_IO_CONTROL
    {
        public UInt32 HeaderLength { get; set; }
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        private byte[] _signature;
        public UInt32 Timeout { get; set; }
        public UInt32 ControlCode { get; set; }
        public UInt32 ReturnCode { get; set; }
        public UInt32 Length { get; set; }

        internal byte[] Signature
        {
            get
            {
                return _signature;
            }
            set
            {
                _signature = value;
            }
        }
    }

}
