using System.Runtime.InteropServices;
using ImageMounter.IO.Native.Enum;

namespace ImageMounter.IO.Native.Struct
{

    [StructLayout(LayoutKind.Sequential)]
    public struct ObjectAttributes
    {
        private readonly Int32 uLength;

        public IntPtr RootDirectory { get; }

        public IntPtr ObjectName { get; }

        public NtObjectAttributes Attributes { get; }

        public IntPtr SecurityDescriptor { get; }

        public IntPtr SecurityQualityOfService { get; }

        public ObjectAttributes(IntPtr rootDirectory, IntPtr objectName, NtObjectAttributes objectAttributes, IntPtr securityDescriptor, IntPtr securityQualityOfService) : this()
        {
            uLength = Marshal.SizeOf(this);
            RootDirectory = rootDirectory;
            ObjectName = objectName;
            Attributes = objectAttributes;
            SecurityDescriptor = securityDescriptor;
            SecurityQualityOfService = securityQualityOfService;
        }
    }

}
