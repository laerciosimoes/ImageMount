using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SP_DEVINFO_LIST_DETAIL_DATA
    {
        public const int SP_MAX_MACHINENAME_LENGTH = 263;

        public UInt32 Size { get; }

        public Guid ClassGUID { get; }

        public IntPtr RemoteMachineHandle { get; }

        public string RemoteMachineName
        {
            get
            {
                return _remoteMachineName;
            }
            set
            {
                _remoteMachineName = value;
            }
        }

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = SP_MAX_MACHINENAME_LENGTH)]
        private string _remoteMachineName;

        public void Initialize()
        {
            Size = System.Convert.ToUInt32(Marshal.SizeOf(this));
        }
    }

}
