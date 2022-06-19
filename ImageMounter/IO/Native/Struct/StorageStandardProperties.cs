using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using ImageMounter.Interop;

namespace ImageMounter.IO.Native.Struct
{

    public struct StorageStandardProperties
    {
        public STORAGE_DEVICE_DESCRIPTOR DeviceDescriptor { get; }

        public string VendorId { get; }
        public string ProductId { get; }
        public string ProductRevision { get; }
        public string SerialNumber { get; }

        public ReadOnlyCollection<byte> RawProperties { get; }

        public StorageStandardProperties(SafeBuffer buffer)
        {
            DeviceDescriptor = buffer.Read<STORAGE_DEVICE_DESCRIPTOR>(0);

            if (DeviceDescriptor.ProductIdOffset != 0)
                ProductId = Marshal.PtrToStringAnsi(buffer.DangerousGetHandle() + DeviceDescriptor.ProductIdOffset);

            if (DeviceDescriptor.VendorIdOffset != 0)
                VendorId = Marshal.PtrToStringAnsi(buffer.DangerousGetHandle() + DeviceDescriptor.VendorIdOffset);

            if (DeviceDescriptor.SerialNumberOffset != 0)
                SerialNumber = Marshal.PtrToStringAnsi(buffer.DangerousGetHandle() + DeviceDescriptor.SerialNumberOffset);

            if (DeviceDescriptor.ProductRevisionOffset != 0)
                ProductRevision = Marshal.PtrToStringAnsi(buffer.DangerousGetHandle() + DeviceDescriptor.ProductRevisionOffset);

            if (DeviceDescriptor.RawPropertiesLength != 0)
            {

                var RawProperties = new byte[DeviceDescriptor.RawPropertiesLength];

                Marshal.Copy(buffer.DangerousGetHandle() + PinnedBuffer<STORAGE_DEVICE_DESCRIPTOR>.TypeSize, RawProperties, 0, DeviceDescriptor.RawPropertiesLength);
                RawProperties = Array.AsReadOnly(RawProperties);
            }
        }
    }

}
