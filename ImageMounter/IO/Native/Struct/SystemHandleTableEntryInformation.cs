using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SystemHandleTableEntryInformation
    {
        public int ProcessId { get; }
        public byte ObjectType { get; }     // ' OB_TYPE_* (OB_TYPE_TYPE, etc.) 
        public byte Flags { get; }      // ' HANDLE_FLAG_* (HANDLE_FLAG_INHERIT, etc.) 
        public ushort Handle { get; }
        public IntPtr ObjectPtr { get; }
        public uint GrantedAccess { get; }
    }

}
