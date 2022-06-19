using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMounter.Interop;
using ImageMounter.IO.Native.Struct;
using Microsoft.Win32.SafeHandles;

namespace ImageMounter.IO.Native
{
    /// <summary>
    /// Control methods for direct communication with SCSI miniport.
    /// </summary>
    public sealed class PhDiskMntCtl
    {
        public const UInt32 SMP_IMSCSI = 0x83730000U;
        public const UInt32 SMP_IMSCSI_QUERY_VERSION = SMP_IMSCSI | 0x800U;
        public const UInt32 SMP_IMSCSI_CREATE_DEVICE = SMP_IMSCSI | 0x801U;
        public const UInt32 SMP_IMSCSI_QUERY_DEVICE = SMP_IMSCSI | 0x802U;
        public const UInt32 SMP_IMSCSI_QUERY_ADAPTER = SMP_IMSCSI | 0x803U;
        public const UInt32 SMP_IMSCSI_CHECK = SMP_IMSCSI | 0x804U;
        public const UInt32 SMP_IMSCSI_SET_DEVICE_FLAGS = SMP_IMSCSI | 0x805U;
        public const UInt32 SMP_IMSCSI_REMOVE_DEVICE = SMP_IMSCSI | 0x806U;
        public const UInt32 SMP_IMSCSI_EXTEND_DEVICE = SMP_IMSCSI | 0x807U;

        /// <summary>
        /// Signature to set in SRB_IO_CONTROL header. This identifies that sender and receiver of IOCTL_SCSI_MINIPORT requests talk to intended components only.
        /// </summary>
        private static readonly byte[] SrbIoCtlSignature = Encoding.ASCII.GetBytes("PhDskMnt".PadRight(8, new char()));

        /// <summary>
        /// Sends an IOCTL_SCSI_MINIPORT control request to a SCSI miniport.
        /// </summary>
        /// <param name="adapter">Open handle to SCSI adapter.</param>
        /// <param name="ctrlcode">Control code to set in SRB_IO_CONTROL header.</param>
        /// <param name="timeout">Timeout to set in SRB_IO_CONTROL header.</param>
        /// <param name="databytes">Optional request data after SRB_IO_CONTROL header. The Length field in SRB_IO_CONTROL header will be automatically adjusted to reflect the amount of data passed by this function.</param>
        /// <param name="returncode">ReturnCode value from SRB_IO_CONTROL header upon return.</param>
        /// <returns>This method returns a BinaryReader object that can be used to read and parse data returned after the SRB_IO_CONTROL header.</returns>
        public static byte[] SendSrbIoControl(SafeFileHandle adapter, UInt32 ctrlcode, UInt32 timeout, byte[] databytes,
            out Int32 returncode)
        {

            var indata = new byte[28 + databytes.Length];

            using (var Request = PinnedBuffer.Create(indata))
            {
                Request.Write(0, PinnedBuffer<SRB_IO_CONTROL>.TypeSize);
                Request.WriteArray(4, SrbIoCtlSignature, 0, 8);
                Request.Write(12, timeout);
                Request.Write(16, ctrlcode);

                if (databytes == null)
                    Request.Write(24, 0U);
                else
                {
                    Request.Write(24, databytes.Length);
                    Buffer.BlockCopy(databytes, 0, indata, 28, databytes.Length);
                }
            }

            var Response = DeviceIoControl(adapter, NativeConstants.IOCTL_SCSI_MINIPORT, indata, 0);

            returncode = BitConverter.ToInt32(Response, 20);

            if (databytes != null)
            {
                var ResponseLength = Math.Min(Math.Min(BitConverter.ToInt32(Response, 24), Response.Length - 28),
                    databytes.Length);
                Buffer.BlockCopy(Response, 28, databytes, 0, ResponseLength);
                Array.Resize(ref databytes, ResponseLength);
            }

            return databytes;
        }
    }
}
