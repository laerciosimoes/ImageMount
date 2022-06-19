using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{



    [StructLayout(LayoutKind.Sequential)]
    public struct STORAGE_DEVICE_DESCRIPTOR
    {
        public STORAGE_DESCRIPTOR_HEADER Header { get; }

        public byte DeviceType { get; }

        public byte DeviceTypeModifier { get; }

        public byte RemovableMedia { get; }

        public byte CommandQueueing { get; }

        public int VendorIdOffset { get; }

        public int ProductIdOffset { get; }

        public int ProductRevisionOffset { get; }

        public int SerialNumberOffset { get; }

        public byte StorageBusType { get; }

        public int RawPropertiesLength { get; }
    }

}
