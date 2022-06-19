using System.Runtime.InteropServices;
using ImageMounter.Interop;
using ImageMounter.Interop.Struct;

namespace ImageMounter.IO.Native.Struct
{

    [StructLayout(LayoutKind.Sequential)]
    public struct ObjectTypeInformation
    {
        public UNICODE_STRING Name { get; }
        public uint ObjectCount { get; }
        public uint HandleCount { get; }
        private readonly uint Reserved11;
        private readonly uint Reserved12;
        private readonly uint Reserved13;
        private readonly uint Reserved14;
        public uint PeakObjectCount { get; }
        public uint PeakHandleCount { get; }
        private readonly uint Reserved21;
        private readonly uint Reserved22;
        private readonly uint Reserved23;
        private readonly uint Reserved24;
        public uint InvalidAttributes { get; }
        public uint GenericRead { get; }
        public uint GenericWrite { get; }
        public uint GenericExecute { get; }
        public uint GenericAll { get; }
        public uint ValidAccess { get; }
        private readonly byte Unknown;
        [MarshalAs(UnmanagedType.I1)]
        private readonly bool MaintainHandleDatabase;
        private readonly ushort Reserved3;
        public int PoolType { get; }
        public uint PagedPoolUsage { get; }
        public uint NonPagedPoolUsage { get; }
    }

}
